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
    private readonly IClaimWorkflowService _workflow;

    public ClaimsController(IClaimService claims, IClaimWorkflowService workflow)
    {
        _claims = claims;
        _workflow = workflow;
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

    // ------------------------------------------------------------------
    // Workflow actions. Each returns the updated claim (200), or
    // 400 invalid body, 403 wrong role/not assigned, 404 not found, 409 stale version or illegal transition.
    // ------------------------------------------------------------------

    /// <summary>Officer takes an unassigned claim: Submitted → UnderReview.</summary>
    [HttpPost("{id:guid}/pick-up")]
    public async Task<ClaimDetailResponse> PickUp(Guid id, PickUpClaimRequest request, CancellationToken cancellationToken)
        => await _workflow.PickUpAsync(id, request, cancellationToken);

    /// <summary>Assigned officer sets or revises the reserve (estimated cost).</summary>
    [HttpPut("{id:guid}/reserve")]
    public async Task<ClaimDetailResponse> SetReserve(Guid id, SetReserveRequest request, CancellationToken cancellationToken)
        => await _workflow.SetReserveAsync(id, request, cancellationToken);

    /// <summary>Assigned officer asks the claimant a question: UnderReview → AdditionalInformationRequired.</summary>
    [HttpPost("{id:guid}/information-requests")]
    public async Task<ClaimDetailResponse> RequestInformation(Guid id, RequestInformationRequest request, CancellationToken cancellationToken)
        => await _workflow.RequestInformationAsync(id, request, cancellationToken);

    /// <summary>Claimant answers: AdditionalInformationRequired → UnderReview.</summary>
    [HttpPost("{id:guid}/information-requests/{requestId:guid}/response")]
    public async Task<ClaimDetailResponse> RespondToInformationRequest(
        Guid id, Guid requestId, RespondToInformationRequest request, CancellationToken cancellationToken)
        => await _workflow.RespondToInformationRequestAsync(id, requestId, request, cancellationToken);

    /// <summary>Assigned officer approves (amount ≤ claimed): UnderReview → Approved.</summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<ClaimDetailResponse> Approve(Guid id, ApproveClaimRequest request, CancellationToken cancellationToken)
        => await _workflow.ApproveAsync(id, request, cancellationToken);

    /// <summary>Assigned officer rejects: UnderReview / AdditionalInformationRequired → Rejected.</summary>
    [HttpPost("{id:guid}/reject")]
    public async Task<ClaimDetailResponse> Reject(Guid id, RejectClaimRequest request, CancellationToken cancellationToken)
        => await _workflow.RejectAsync(id, request, cancellationToken);

    /// <summary>Assigned officer records payment: Approved → Settled.</summary>
    [HttpPost("{id:guid}/settle")]
    public async Task<ClaimDetailResponse> Settle(Guid id, SettleClaimRequest request, CancellationToken cancellationToken)
        => await _workflow.SettleAsync(id, request, cancellationToken);
}
