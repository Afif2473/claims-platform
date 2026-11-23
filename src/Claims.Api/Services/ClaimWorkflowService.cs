using System.Globalization;
using Claims.Api.Data;
using Claims.Api.DTOs;
using Claims.Api.Enums;
using Claims.Api.Errors;
using Claims.Api.Interfaces;
using Claims.Api.Messaging;
using Claims.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Claims.Api.Services;

/// <summary>
/// Every state-changing action on a claim. Each action runs the same checks in the same order:
///   1. role (403)  2. claim exists / is visible (404)  3. actor may act on this claim (403)
///   4. client's Version is current (409)  5. transition is legal (409)  6. action-specific rules (409)
/// then applies the change, appends an audit activity and saves everything in one SaveChanges.
/// </summary>
public class ClaimWorkflowService : IClaimWorkflowService
{
    private readonly ClaimsDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IClaimService _claims;
    private readonly IClaimEventPublisher _events;

    // Events for the activities recorded in this request; published after the commit.
    private readonly List<ClaimActivity> _recorded = [];

    public ClaimWorkflowService(ClaimsDbContext db, ICurrentUserService currentUser, IClaimService claims, IClaimEventPublisher events)
    {
        _db = db;
        _currentUser = currentUser;
        _claims = claims;
        _events = events;
    }

    public async Task<ClaimDetailResponse> PickUpAsync(Guid claimId, PickUpClaimRequest request, CancellationToken cancellationToken = default)
    {
        var officer = await RequireOfficerAsync(cancellationToken);
        var claim = await LoadAsync(claimId, cancellationToken);
        EnsureVersion(claim, request.Version!.Value);

        MoveTo(claim, ClaimStatus.UnderReview);
        claim.AssignedOfficerId = officer.Id;
        Record(claim, ClaimActivityType.ClaimAssigned, $"Picked up by {officer.FullName}; claim is now under review.", officer.Id);

        return await SaveAsync(claim, cancellationToken);
    }

    public async Task<ClaimDetailResponse> SetReserveAsync(Guid claimId, SetReserveRequest request, CancellationToken cancellationToken = default)
    {
        var officer = await RequireOfficerAsync(cancellationToken);
        var claim = await LoadAsync(claimId, cancellationToken);
        EnsureAssignedTo(claim, officer);
        EnsureVersion(claim, request.Version!.Value);

        if (claim.Status is not (ClaimStatus.UnderReview or ClaimStatus.AdditionalInformationRequired))
            throw new BusinessRuleException($"A reserve can only be set while the claim is being assessed (current status: {claim.Status}).");

        claim.ReserveAmount = request.Amount;
        Record(claim, ClaimActivityType.ReserveSet, $"Reserve set to {Money(claim, request.Amount)} by {officer.FullName}.", officer.Id);

        return await SaveAsync(claim, cancellationToken);
    }

    public async Task<ClaimDetailResponse> RequestInformationAsync(Guid claimId, RequestInformationRequest request, CancellationToken cancellationToken = default)
    {
        var officer = await RequireOfficerAsync(cancellationToken);
        var claim = await LoadAsync(claimId, cancellationToken);
        EnsureAssignedTo(claim, officer);
        EnsureVersion(claim, request.Version!.Value);

        // Only allowed from UnderReview, so a claim has at most one open request at a time.
        MoveTo(claim, ClaimStatus.AdditionalInformationRequired);
        _db.InformationRequests.Add(new InformationRequest(claim.Id, officer.Id, request.Question.Trim()));
        Record(claim, ClaimActivityType.InformationRequested, $"{officer.FullName} requested additional information from the claimant.", officer.Id);

        return await SaveAsync(claim, cancellationToken);
    }

    public async Task<ClaimDetailResponse> RespondToInformationRequestAsync(
        Guid claimId, Guid requestId, RespondToInformationRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _currentUser.GetRequiredUserAsync(cancellationToken);
        if (user.Role != UserRole.Claimant)
            throw new ForbiddenException("Only the claimant can respond to an information request.");

        var claim = await LoadAsync(claimId, cancellationToken);
        // Same rule as GetAsync: another claimant's claim is reported as not found.
        if (claim.ClaimantId != user.Id)
            throw new NotFoundException($"Claim {claimId} was not found.");
        EnsureVersion(claim, request.Version!.Value);

        var infoRequest = await _db.InformationRequests
            .FirstOrDefaultAsync(r => r.Id == requestId && r.ClaimId == claim.Id, cancellationToken)
            ?? throw new NotFoundException($"Information request {requestId} was not found on this claim.");

        if (infoRequest.IsAnswered)
            throw new BusinessRuleException("This information request has already been answered.");
        if (claim.Status != ClaimStatus.AdditionalInformationRequired)
            throw new BusinessRuleException($"This claim is not waiting for information (current status: {claim.Status}).");

        infoRequest.Respond(request.Response.Trim());
        MoveTo(claim, ClaimStatus.UnderReview);
        Record(claim, ClaimActivityType.InformationProvided, $"{user.FullName} provided the requested information; claim is back under review.", user.Id);

        return await SaveAsync(claim, cancellationToken);
    }

    public async Task<ClaimDetailResponse> ApproveAsync(Guid claimId, ApproveClaimRequest request, CancellationToken cancellationToken = default)
    {
        var officer = await RequireOfficerAsync(cancellationToken);
        var claim = await LoadAsync(claimId, cancellationToken);
        EnsureAssignedTo(claim, officer);
        EnsureVersion(claim, request.Version!.Value);

        MoveTo(claim, ClaimStatus.Approved);
        if (request.ApprovedAmount > claim.ClaimedAmount)
            throw new BusinessRuleException($"Approved amount cannot exceed the claimed amount of {Money(claim, claim.ClaimedAmount)}.");

        claim.ApprovedAmount = request.ApprovedAmount;
        claim.DecisionReason = request.Reason.Trim();
        claim.DecidedAt = DateTimeOffset.UtcNow;
        Record(claim, ClaimActivityType.DecisionRecorded, $"Claim approved for {Money(claim, request.ApprovedAmount)} by {officer.FullName}.", officer.Id);

        return await SaveAsync(claim, cancellationToken);
    }

    public async Task<ClaimDetailResponse> RejectAsync(Guid claimId, RejectClaimRequest request, CancellationToken cancellationToken = default)
    {
        var officer = await RequireOfficerAsync(cancellationToken);
        var claim = await LoadAsync(claimId, cancellationToken);
        EnsureAssignedTo(claim, officer);
        EnsureVersion(claim, request.Version!.Value);

        MoveTo(claim, ClaimStatus.Rejected);
        claim.DecisionReason = request.Reason.Trim();
        claim.DecidedAt = DateTimeOffset.UtcNow;
        Record(claim, ClaimActivityType.DecisionRecorded, $"Claim rejected by {officer.FullName}.", officer.Id);

        return await SaveAsync(claim, cancellationToken);
    }

    public async Task<ClaimDetailResponse> SettleAsync(Guid claimId, SettleClaimRequest request, CancellationToken cancellationToken = default)
    {
        var officer = await RequireOfficerAsync(cancellationToken);
        var claim = await LoadAsync(claimId, cancellationToken);
        EnsureAssignedTo(claim, officer);
        EnsureVersion(claim, request.Version!.Value);

        MoveTo(claim, ClaimStatus.Settled);
        Record(claim, ClaimActivityType.StatusChanged, $"Claim settled; payment of {Money(claim, claim.ApprovedAmount ?? 0)} released by {officer.FullName}.", officer.Id);

        return await SaveAsync(claim, cancellationToken);
    }

    // ------------------------------------------------------------------
    // Shared steps
    // ------------------------------------------------------------------

    /// <summary>Managers are read-only in this version; only claims officers work claims.</summary>
    private async Task<AppUser> RequireOfficerAsync(CancellationToken cancellationToken)
    {
        var user = await _currentUser.GetRequiredUserAsync(cancellationToken);
        if (user.Role != UserRole.ClaimsOfficer)
            throw new ForbiddenException("Only claims officers can perform this action.");
        return user;
    }

    /// <summary>Loads the claim tracked, so EF can detect changes and apply the Version check on save.</summary>
    private async Task<Claim> LoadAsync(Guid claimId, CancellationToken cancellationToken) =>
        await _db.Claims.FirstOrDefaultAsync(c => c.Id == claimId, cancellationToken)
        ?? throw new NotFoundException($"Claim {claimId} was not found.");

    private static void EnsureAssignedTo(Claim claim, AppUser officer)
    {
        if (claim.AssignedOfficerId != officer.Id)
            throw new ForbiddenException("Only the officer assigned to this claim can act on it.");
    }

    /// <summary>
    /// Rejects a request based on an out-of-date view of the claim. This catches the case where the user
    /// had the page open while someone else acted. The race between our read and our write is caught by EF:
    /// the UPDATE runs with WHERE Version = (value we loaded) and throws DbUpdateConcurrencyException if it matches no row.
    /// </summary>
    private static void EnsureVersion(Claim claim, int expectedVersion)
    {
        if (claim.Version != expectedVersion)
            throw new ConcurrencyConflictException(
                $"The claim was changed by someone else (your version {expectedVersion}, current version {claim.Version}). Reload it and try again.");
    }

    private static void MoveTo(Claim claim, ClaimStatus target)
    {
        if (!ClaimWorkflow.CanTransition(claim.Status, target))
            throw new BusinessRuleException($"A claim cannot move from {claim.Status} to {target}.");
        claim.Status = target;
    }

    private void Record(Claim claim, ClaimActivityType type, string description, Guid actorUserId)
    {
        var activity = new ClaimActivity(claim.Id, type, description, actorUserId);
        _db.ClaimActivities.Add(activity);
        _recorded.Add(activity);
    }

    /// <summary>
    /// The change, its audit entry and any information request are committed in one transaction.
    /// Only after the commit succeeds is one event per audit entry published to Kafka.
    /// If the save throws (e.g. a 409 concurrency conflict), nothing is published.
    /// </summary>
    private async Task<ClaimDetailResponse> SaveAsync(Claim claim, CancellationToken cancellationToken)
    {
        claim.Version++;
        claim.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var activity in _recorded)
            _events.Publish(ClaimEvent.From(claim, activity));
        _recorded.Clear();

        return await _claims.GetAsync(claim.Id, cancellationToken);
    }

    private static string Money(Claim claim, decimal amount) =>
        $"{claim.CurrencyCode} {amount.ToString("N2", CultureInfo.InvariantCulture)}";
}
