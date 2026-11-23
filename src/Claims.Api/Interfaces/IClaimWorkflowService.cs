using Claims.Api.DTOs;

namespace Claims.Api.Interfaces;

/// <summary>
/// State-changing actions on an existing claim. Each returns the updated claim.
/// </summary>
public interface IClaimWorkflowService
{
    Task<ClaimDetailResponse> PickUpAsync(Guid claimId, PickUpClaimRequest request, CancellationToken cancellationToken = default);
    Task<ClaimDetailResponse> SetReserveAsync(Guid claimId, SetReserveRequest request, CancellationToken cancellationToken = default);
    Task<ClaimDetailResponse> RequestInformationAsync(Guid claimId, RequestInformationRequest request, CancellationToken cancellationToken = default);
    Task<ClaimDetailResponse> RespondToInformationRequestAsync(Guid claimId, Guid requestId, RespondToInformationRequest request, CancellationToken cancellationToken = default);
    Task<ClaimDetailResponse> ApproveAsync(Guid claimId, ApproveClaimRequest request, CancellationToken cancellationToken = default);
    Task<ClaimDetailResponse> RejectAsync(Guid claimId, RejectClaimRequest request, CancellationToken cancellationToken = default);
    Task<ClaimDetailResponse> SettleAsync(Guid claimId, SettleClaimRequest request, CancellationToken cancellationToken = default);
}
