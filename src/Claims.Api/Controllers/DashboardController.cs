using Claims.Api.DTOs;
using Claims.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Claims.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Produces("application/json")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    /// <summary>Team workload, performance and outstanding liability exposure. Staff only.</summary>
    [HttpGet]
    [ProducesResponseType<DashboardResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<DashboardResponse> Get(CancellationToken cancellationToken)
        => await _dashboard.GetAsync(cancellationToken);
}
