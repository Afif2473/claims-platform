using Claims.Api.Enums;

namespace Claims.Api.Models;

/// <summary>
/// Represents an immutable event or milestone in a claim's timeline for transparency and auditing.
/// Properties use private setters to enforce append-only behavior.
/// </summary>
public class ClaimActivity
{
    public Guid Id { get; private set; }
    public Guid ClaimId { get; private set; }

    /// <summary>
    /// User who performed the action. Null indicates a system-generated event.
    /// </summary>
    public Guid? ActorUserId { get; private set; }

    public ClaimActivityType ActivityType { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    // Navigation properties
    public Claim? Claim { get; private set; }
    public AppUser? ActorUser { get; private set; }

    /// <summary>
    /// Protected parameterless constructor required by Entity Framework Core.
    /// </summary>
    protected ClaimActivity() { }

    /// <summary>
    /// Enforces proper instantiation and immutability for audit records.
    /// </summary>
    public ClaimActivity(Guid claimId, ClaimActivityType activityType, string description, Guid? actorUserId = null)
    {
        Id = Guid.NewGuid();
        ClaimId = claimId;
        ActivityType = activityType;
        Description = string.IsNullOrWhiteSpace(description) ? throw new ArgumentException("Description cannot be empty.", nameof(description)) : description;
        ActorUserId = actorUserId;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
