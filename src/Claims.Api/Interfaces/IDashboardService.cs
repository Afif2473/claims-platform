using Claims.Api.DTOs;

namespace Claims.Api.Interfaces;

public interface IDashboardService
{
    Task<DashboardResponse> GetAsync(CancellationToken cancellationToken = default);
}
