namespace Claims.Api.Enums;

/// <summary>
/// Represents the lifecycle status of an insurance claim.
/// </summary>
public enum ClaimStatus
{
    /// <summary>
    /// Claim submitted by claimant and waiting for initial pickup.
    /// </summary>
    Submitted,

    /// <summary>
    /// Claim has been picked up by a claims officer and is under active assessment.
    /// </summary>
    UnderReview,

    /// <summary>
    /// Additional documentation or details have been requested from the claimant.
    /// </summary>
    AdditionalInformationRequired,

    /// <summary>
    /// Assessment complete and claim approved for settlement.
    /// </summary>
    Approved,

    /// <summary>
    /// Claim rejected based on policy terms, liability assessment, or exclusions.
    /// </summary>
    Rejected,

    /// <summary>
    /// Payment processed and claim closed.
    /// </summary>
    Settled
}
