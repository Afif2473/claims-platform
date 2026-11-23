namespace Claims.Api.Enums;

/// <summary>
/// Categorises specific timeline events in a claim's audit history.
/// </summary>
public enum ClaimActivityType
{
    /// <summary>
    /// Initial submission of the claim by claimant.
    /// </summary>
    ClaimSubmitted,

    /// <summary>
    /// Claim assigned to a specific claims officer.
    /// </summary>
    ClaimAssigned,

    /// <summary>
    /// Claim status changed (e.g. Submitted -> UnderReview).
    /// </summary>
    StatusChanged,

    /// <summary>
    /// Claims officer set or revised the reserve (estimated cost) on the claim.
    /// </summary>
    ReserveSet,

    /// <summary>
    /// Claims officer requested additional information from claimant.
    /// </summary>
    InformationRequested,

    /// <summary>
    /// Claimant provided requested documents or answers.
    /// </summary>
    InformationProvided,

    /// <summary>
    /// Official decision recorded (Approved, Rejected, or Settled).
    /// </summary>
    DecisionRecorded
}
