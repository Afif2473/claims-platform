using System.Globalization;
using Claims.Api.Data;
using Claims.Api.Enums;
using Claims.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Claims.Api.Messaging;

/// <summary>
/// Turns a claim event into a claimant notification. Kept separate from the Kafka consumer loop
/// so the business logic (which events notify, and with what message) has no Kafka dependency.
/// </summary>
public class ClaimNotificationHandler
{
    private readonly ClaimsDbContext _db;

    public ClaimNotificationHandler(ClaimsDbContext db)
    {
        _db = db;
    }

    public async Task HandleAsync(ClaimEvent claimEvent, CancellationToken cancellationToken)
    {
        var message = BuildMessage(claimEvent);
        if (message is null)
            return; // internal event (e.g. reserve set) or the claimant's own action

        // Idempotency: a redelivered event has already produced its notification.
        if (await _db.Notifications.AnyAsync(n => n.EventId == claimEvent.EventId, cancellationToken))
            return;

        _db.Notifications.Add(new Notification(claimEvent.ClaimantId, claimEvent.ClaimId, claimEvent.EventId, message));
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>What the claimant is told for each event, or null if they are not notified.</summary>
    public static string? BuildMessage(ClaimEvent e) => e.EventType switch
    {
        ClaimActivityType.ClaimSubmitted =>
            $"We have received your claim {e.ClaimNumber}.",
        ClaimActivityType.ClaimAssigned =>
            $"Your claim {e.ClaimNumber} is now being reviewed by a claims officer.",
        ClaimActivityType.InformationRequested =>
            $"Action needed: we need more information to continue assessing claim {e.ClaimNumber}.",
        ClaimActivityType.DecisionRecorded when e.Status == ClaimStatus.Approved =>
            $"Good news: claim {e.ClaimNumber} has been approved for {e.CurrencyCode} {Format(e.ApprovedAmount)}.",
        ClaimActivityType.DecisionRecorded when e.Status == ClaimStatus.Rejected =>
            $"Claim {e.ClaimNumber} has been rejected. Open the claim to see the reason.",
        ClaimActivityType.StatusChanged when e.Status == ClaimStatus.Settled =>
            $"Payment of {e.CurrencyCode} {Format(e.ApprovedAmount)} for claim {e.ClaimNumber} has been released.",
        _ => null
    };

    private static string Format(decimal? amount) => (amount ?? 0m).ToString("N2", CultureInfo.InvariantCulture);
}
