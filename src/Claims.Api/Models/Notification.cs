namespace Claims.Api.Models;

/// <summary>
/// A message shown to a user about one of their claims. Created by the Kafka consumer
/// from claim events, never directly by the request path.
/// </summary>
public class Notification
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ClaimId { get; private set; }

    /// <summary>
    /// Id of the claim event this came from. Unique, so a redelivered Kafka message
    /// cannot create a duplicate notification (idempotent consumer).
    /// </summary>
    public Guid EventId { get; private set; }

    public string Message { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    // Navigation properties
    public AppUser? User { get; private set; }
    public Claim? Claim { get; private set; }

    /// <summary>
    /// Protected parameterless constructor required by Entity Framework Core.
    /// </summary>
    protected Notification() { }

    public Notification(Guid userId, Guid claimId, Guid eventId, string message)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        ClaimId = claimId;
        EventId = eventId;
        Message = string.IsNullOrWhiteSpace(message)
            ? throw new ArgumentException("Message cannot be empty.", nameof(message))
            : message;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
