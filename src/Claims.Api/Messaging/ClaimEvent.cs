using Claims.Api.Enums;
using Claims.Api.Models;

namespace Claims.Api.Messaging;

/// <summary>
/// The message published to the claim-events topic: one event per audit entry.
/// EventId is the ClaimActivity id, so every event traces back to exactly one audit record
/// and consumers can use it as an idempotency key.
/// The event carries enough data for consumers to act without calling back into the API.
/// </summary>
public record ClaimEvent(
    Guid EventId,
    ClaimActivityType EventType,
    Guid ClaimId,
    string ClaimNumber,
    Guid ClaimantId,
    ClaimStatus Status,
    string CurrencyCode,
    decimal? ApprovedAmount,
    Guid? ActorUserId,
    DateTimeOffset OccurredAt)
{
    public static ClaimEvent From(Claim claim, ClaimActivity activity) => new(
        activity.Id,
        activity.ActivityType,
        claim.Id,
        claim.ClaimNumber,
        claim.ClaimantId,
        claim.Status,
        claim.CurrencyCode,
        claim.ApprovedAmount,
        activity.ActorUserId,
        activity.CreatedAt);
}
