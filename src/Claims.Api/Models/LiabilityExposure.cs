namespace Claims.Api.Models;

/// <summary>
/// How much the insurer may still have to pay on a claim.
/// Uses the best figure available at each stage of the lifecycle:
///   committed (ApprovedAmount) → officer estimate (ReserveAmount) → claimant's ask (ClaimedAmount).
/// Closed claims (Rejected, Settled) carry no outstanding liability.
/// </summary>
public static class LiabilityExposure
{
    public static decimal For(Claim claim) =>
        ClaimWorkflow.OpenStatuses.Contains(claim.Status)
            ? claim.ApprovedAmount ?? claim.ReserveAmount ?? claim.ClaimedAmount
            : 0m;
}
