using Claims.Api.Enums;

namespace Claims.Api.DTOs;

public enum AssignmentFilter
{
    All,
    Mine,
    Unassigned
}

/// <summary>
/// Query-string filters for GET /api/claims. Claimants always see only their own claims,
/// so Assignment and MarketCode only apply to staff.
/// </summary>
public class ClaimListQuery
{
    public ClaimStatus? Status { get; init; }
    public AssignmentFilter Assignment { get; init; } = AssignmentFilter.All;
    public string? MarketCode { get; init; }
}
