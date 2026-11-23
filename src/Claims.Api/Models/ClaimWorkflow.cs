using Claims.Api.Enums;

namespace Claims.Api.Models;

/// <summary>
/// The claim lifecycle as data: the single source of truth for which status changes are legal.
///
///   Submitted ──pick up──▶ UnderReview ──approve──▶ Approved ──settle──▶ Settled
///                            │  ▲    └──reject──▶ Rejected
///               request info │  │ claimant responds
///                            ▼  │
///                  AdditionalInformationRequired ──reject──▶ Rejected
/// </summary>
public static class ClaimWorkflow
{
    private static readonly IReadOnlyDictionary<ClaimStatus, ClaimStatus[]> AllowedTransitions =
        new Dictionary<ClaimStatus, ClaimStatus[]>
        {
            [ClaimStatus.Submitted] = [ClaimStatus.UnderReview],
            [ClaimStatus.UnderReview] = [ClaimStatus.AdditionalInformationRequired, ClaimStatus.Approved, ClaimStatus.Rejected],
            // Rejecting from here covers a claimant who never responds.
            [ClaimStatus.AdditionalInformationRequired] = [ClaimStatus.UnderReview, ClaimStatus.Rejected],
            [ClaimStatus.Approved] = [ClaimStatus.Settled],
            [ClaimStatus.Rejected] = [],
            [ClaimStatus.Settled] = []
        };

    /// <summary>
    /// Statuses that still carry liability: not yet rejected and not yet paid out.
    /// </summary>
    public static readonly IReadOnlySet<ClaimStatus> OpenStatuses = new HashSet<ClaimStatus>
    {
        ClaimStatus.Submitted,
        ClaimStatus.UnderReview,
        ClaimStatus.AdditionalInformationRequired,
        ClaimStatus.Approved
    };

    public static bool CanTransition(ClaimStatus from, ClaimStatus to) =>
        AllowedTransitions.TryGetValue(from, out var targets) && targets.Contains(to);
}
