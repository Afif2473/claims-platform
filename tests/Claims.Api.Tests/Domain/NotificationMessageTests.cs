using Claims.Api.Enums;
using Claims.Api.Messaging;

namespace Claims.Api.Tests.Domain;

public class NotificationMessageTests
{
    private static ClaimEvent Event(ClaimActivityType type, ClaimStatus status, decimal? approved = null) =>
        new(Guid.NewGuid(), type, Guid.NewGuid(), "CLM-SG-2026-TEST0001", Guid.NewGuid(), status, "SGD", approved, null, DateTimeOffset.UtcNow);

    [Theory]
    [InlineData(ClaimActivityType.ClaimSubmitted, ClaimStatus.Submitted, "received")]
    [InlineData(ClaimActivityType.ClaimAssigned, ClaimStatus.UnderReview, "being reviewed")]
    [InlineData(ClaimActivityType.InformationRequested, ClaimStatus.AdditionalInformationRequired, "Action needed")]
    [InlineData(ClaimActivityType.DecisionRecorded, ClaimStatus.Rejected, "rejected")]
    public void Claimant_is_told_about_progress(ClaimActivityType type, ClaimStatus status, string expectedText)
    {
        var message = ClaimNotificationHandler.BuildMessage(Event(type, status));
        Assert.NotNull(message);
        Assert.Contains(expectedText, message);
        Assert.Contains("CLM-SG-2026-TEST0001", message);
    }

    [Fact]
    public void Approval_message_includes_amount_and_currency()
    {
        var message = ClaimNotificationHandler.BuildMessage(Event(ClaimActivityType.DecisionRecorded, ClaimStatus.Approved, 4200m));
        Assert.Contains("approved for SGD 4,200.00", message);
    }

    [Fact]
    public void Settlement_message_includes_amount_paid()
    {
        var message = ClaimNotificationHandler.BuildMessage(Event(ClaimActivityType.StatusChanged, ClaimStatus.Settled, 4200m));
        Assert.Contains("Payment of SGD 4,200.00", message);
    }

    [Theory]
    [InlineData(ClaimActivityType.ReserveSet, ClaimStatus.UnderReview)]          // internal, not for the claimant
    [InlineData(ClaimActivityType.InformationProvided, ClaimStatus.UnderReview)] // the claimant's own action
    public void Claimant_is_not_notified(ClaimActivityType type, ClaimStatus status)
    {
        Assert.Null(ClaimNotificationHandler.BuildMessage(Event(type, status)));
    }
}
