using Claims.Api.DTOs;

namespace Claims.Api.Interfaces;

public interface IClaimService
{
    Task<ClaimDetailResponse> SubmitAsync(SubmitClaimRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClaimSummaryResponse>> ListAsync(ClaimListQuery query, CancellationToken cancellationToken = default);
    Task<ClaimDetailResponse> GetAsync(Guid claimId, CancellationToken cancellationToken = default);
}
