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
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public AppUser? Claimant { get; set; }
    public AppUser? AssignedOfficer { get; set; }
    public ICollection<ClaimActivity> Activities { get; set; } = new List<ClaimActivity>();
}
