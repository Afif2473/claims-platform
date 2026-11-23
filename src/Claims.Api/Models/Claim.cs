using Claims.Api.Enums;

namespace Claims.Api.Models;

/// <summary>
/// Represents an insurance claim filed in one of the supported APAC markets.
/// </summary>
public class Claim
{
    public Guid Id { get; set; }
    public string ClaimNumber { get; set; } = string.Empty;
    public Guid ClaimantId { get; set; }
    public Guid? AssignedOfficerId { get; set; }
    public ClaimType ClaimType { get; set; }
    public string Description { get; set; } = string.Empty;
    public string IncidentLocation { get; set; } = string.Empty;
    public DateTimeOffset IncidentDate { get; set; }
    public decimal ClaimedAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string MarketCode { get; set; } = string.Empty;
    public ClaimStatus Status { get; set; } = ClaimStatus.Submitted;

    /// <summary>
    /// Officer's estimate of what the claim will cost. Set during assessment.
    /// </summary>
    public decimal? ReserveAmount { get; set; }

    /// <summary>
    /// Amount the insurer has committed to pay. Set when the claim is approved.
    /// </summary>
    public decimal? ApprovedAmount { get; set; }

    public string? DecisionReason { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Optimistic concurrency token. Incremented by the service on every update;
    /// a stale write raises DbUpdateConcurrencyException (mapped to HTTP 409).
    /// </summary>
    public int Version { get; set; }

    // Navigation properties
    public AppUser? Claimant { get; set; }
    public AppUser? AssignedOfficer { get; set; }
    public ICollection<ClaimActivity> Activities { get; set; } = new List<ClaimActivity>();
    public ICollection<InformationRequest> InformationRequests { get; set; } = new List<InformationRequest>();
}
