using Claims.Api.Data;
using Claims.Api.Enums;
using Claims.Api.Messaging;
using Claims.Api.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Claims.Api.Tests.Services;

public sealed class ClaimNotificationHandlerTests : IDisposable
{
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private async Task<ClaimEvent> SeededClaimEvent(ClaimActivityType type)
    {
        await using var db = _db.CreateContext();
        var claim = await db.Claims.FirstAsync(c => c.ClaimantId == DbSeeder.JaneTanId);
        return new ClaimEvent(Guid.NewGuid(), type, claim.Id, claim.ClaimNumber, claim.ClaimantId,
            claim.Status, claim.CurrencyCode, claim.ApprovedAmount, null, DateTimeOffset.UtcNow);
    }

    private async Task Handle(ClaimEvent claimEvent)
    {
        await using var db = _db.CreateContext();
        await new ClaimNotificationHandler(db).HandleAsync(claimEvent, CancellationToken.None);
    }

    private async Task<int> NotificationCount()
    {
        await using var db = _db.CreateContext();
        return await db.Notifications.CountAsync();
    }

    [Fact]
    public async Task Redelivered_event_creates_only_one_notification()
    {
        var claimEvent = await SeededClaimEvent(ClaimActivityType.ClaimAssigned);

        await Handle(claimEvent);
        await Handle(claimEvent); // Kafka at-least-once delivery: same event again

        Assert.Equal(1, await NotificationCount());
    }

    [Fact]
    public async Task Notification_goes_to_the_claimant()
    {
        var claimEvent = await SeededClaimEvent(ClaimActivityType.ClaimAssigned);
        await Handle(claimEvent);

        await using var db = _db.CreateContext();
        var notification = await db.Notifications.SingleAsync();
        Assert.Equal(DbSeeder.JaneTanId, notification.UserId);
        Assert.Equal(claimEvent.EventId, notification.EventId);
    }

    [Fact]
    public async Task Internal_events_create_no_notification()
    {
        await Handle(await SeededClaimEvent(ClaimActivityType.ReserveSet));
        Assert.Equal(0, await NotificationCount());
    }
}
