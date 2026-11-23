using Claims.Api.DTOs;
using Claims.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Claims.Api.Controllers;

/// <summary>
/// HTTP surface for claims. Controllers only translate HTTP to service calls;
/// role checks and business rules live in ClaimService, and errors are mapped by ApiExceptionHandler.
/// </summary>
[ApiController]
[Route("api/claims")]
[Produces("application/json")]
public class ClaimsController : ControllerBase
{
    private readonly IClaimService _claims;

    public ClaimsController(IClaimService claims)
    {
        _claims = claims;
    }

    /// <summary>Claimant reports a new incident.</summary>
    [HttpPost]
    [ProducesResponseType<ClaimDetailResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ClaimDetailResponse>> Submit(SubmitClaimRequest request, CancellationToken cancellationToken)
    {
        var claim = await _claims.SubmitAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = claim.Id }, claim);
    }

    /// <summary>Claimants get their own claims; staff get the filtered work queue.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ClaimSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IReadOnlyList<ClaimSummaryResponse>> List([FromQuery] ClaimListQuery query, CancellationToken cancellationToken)
        => await _claims.ListAsync(query, cancellationToken);

    /// <summary>Full claim with timeline and information requests.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ClaimDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ClaimDetailResponse> Get(Guid id, CancellationToken cancellationToken)
        => await _claims.GetAsync(id, cancellationToken);
}
