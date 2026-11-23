using Claims.Api.Data;
using Claims.Api.DTOs;
using Claims.Api.Enums;
using Claims.Api.Errors;
using Claims.Api.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Claims.Api.Tests.Services;

/// <summary>
/// Workflow behaviour through the real services and a real (in-memory) SQLite database.
/// Every call uses a fresh DbContext, as a separate HTTP request would.
/// </summary>
public sealed class ClaimWorkflowServiceTests : IDisposable
{
    private static readonly Guid Jane = DbSeeder.JaneTanId;
    private static readonly Guid Arjun = DbSeeder.ArjunKumarId;
    private static readonly Guid David = DbSeeder.DavidWongId;
    private static readonly Guid Mei = DbSeeder.MeiLinId;

    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private async Task<ClaimDetailResponse> SubmitAsJane(decimal amount = 5000m) =>
        await _db.ServicesFor(Jane).Claims.SubmitAsync(new SubmitClaimRequest
        {
            ClaimType = ClaimType.Motor,
            Description = "Rear-ended at a traffic light.",
            IncidentLocation = "Orchard Road",
            IncidentDate = DateTimeOffset.UtcNow.AddDays(-1),
            ClaimedAmount = amount,
            MarketCode = "sg"
        });

    private async Task<ClaimDetailResponse> PickUp(Guid claimId, Guid officer, int version) =>
        await _db.ServicesFor(officer).Workflow.PickUpAsync(claimId, new PickUpClaimRequest { Version = version });

    [Fact]
    public async Task Full_lifecycle_ends_settled_with_complete_audit_trail_and_events()
    {
        var claim = await SubmitAsJane();
        Assert.Equal("SGD", claim.CurrencyCode);
        Assert.StartsWith("CLM-SG-", claim.ClaimNumber);

        claim = await PickUp(claim.Id, David, 0);
        claim = await _db.ServicesFor(David).Workflow.SetReserveAsync(claim.Id, new SetReserveRequest { Version = 1, Amount = 4000m });
        claim = await _db.ServicesFor(David).Workflow.RequestInformationAsync(claim.Id, new RequestInformationRequest { Version = 2, Question = "Police report number?" });
        Assert.Equal(ClaimStatus.AdditionalInformationRequired, claim.Status);

        var requestId = claim.InformationRequests.Single().Id;
        claim = await _db.ServicesFor(Jane).Workflow.RespondToInformationRequestAsync(claim.Id, requestId, new RespondToInformationRequest { Version = 3, Response = "SG-12345" });
        Assert.Equal(ClaimStatus.UnderReview, claim.Status);

        claim = await _db.ServicesFor(David).Workflow.ApproveAsync(claim.Id, new ApproveClaimRequest { Version = 4, ApprovedAmount = 4200m, Reason = "Covered; excess deducted." });
        claim = await _db.ServicesFor(David).Workflow.SettleAsync(claim.Id, new SettleClaimRequest { Version = 5 });

        Assert.Equal(ClaimStatus.Settled, claim.Status);
        Assert.Equal(6, claim.Version);
        Assert.Equal(4200m, claim.ApprovedAmount);
        Assert.Equal("Covered; excess deducted.", claim.DecisionReason);
        Assert.True(claim.InformationRequests.Single().IsAnswered);

        ClaimActivityType[] expected =
        [
            ClaimActivityType.ClaimSubmitted, ClaimActivityType.ClaimAssigned, ClaimActivityType.ReserveSet,
            ClaimActivityType.InformationRequested, ClaimActivityType.InformationProvided,
            ClaimActivityType.DecisionRecorded, ClaimActivityType.StatusChanged
        ];
        Assert.Equal(expected, claim.Activities.Select(a => a.ActivityType));

        // One Kafka event per audit entry, in order, each carrying its audit entry's id.
        Assert.Equal(expected, _db.Events.Published.Select(e => e.EventType));
        Assert.Equal(claim.Activities.Select(a => a.Id), _db.Events.Published.Select(e => e.EventId));
    }

    [Fact]
    public async Task Stale_version_is_rejected_and_nothing_is_saved_or_published()
    {
        var claim = await SubmitAsJane();
        await PickUp(claim.Id, David, 0);
        var eventsBefore = _db.Events.Published.Count;

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => PickUp(claim.Id, Mei, 0));

        var current = await _db.ServicesFor(David).Claims.GetAsync(claim.Id);
        Assert.Equal("David Wong", current.AssignedOfficerName);
        Assert.Equal(1, current.Version);
        Assert.Equal(eventsBefore, _db.Events.Published.Count);
    }

    [Fact]
    public async Task Concurrent_write_between_read_and_save_is_caught_by_the_version_token()
    {
        var claim = await SubmitAsJane();

        // Request B reads the claim...
        await using var requestB = _db.CreateContext();
        var staleCopy = await requestB.Claims.SingleAsync(c => c.Id == claim.Id);

        // ...request A picks it up and commits in the meantime...
        await PickUp(claim.Id, David, 0);

        // ...so B's write must fail instead of silently overwriting A's.
        staleCopy.AssignedOfficerId = Mei;
        staleCopy.Version++;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => requestB.SaveChangesAsync());
    }

    [Fact]
    public async Task Only_the_assigned_officer_can_act_on_a_claim()
    {
        var claim = await SubmitAsJane();
        await PickUp(claim.Id, David, 0);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _db.ServicesFor(Mei).Workflow.ApproveAsync(claim.Id, new ApproveClaimRequest { Version = 1, ApprovedAmount = 100m, Reason = "Looks fine" }));
    }

    [Fact]
    public async Task Claimants_and_managers_cannot_perform_officer_actions()
    {
        var claim = await SubmitAsJane();

        await Assert.ThrowsAsync<ForbiddenException>(() => PickUp(claim.Id, Jane, 0));
        await Assert.ThrowsAsync<ForbiddenException>(() => PickUp(claim.Id, DbSeeder.SarahLimId, 0));
    }

    [Fact]
    public async Task Illegal_transition_is_a_business_rule_violation()
    {
        var claim = await SubmitAsJane();
        await PickUp(claim.Id, David, 0);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _db.ServicesFor(David).Workflow.SettleAsync(claim.Id, new SettleClaimRequest { Version = 1 }));
        Assert.Contains("UnderReview to Settled", ex.Message);
    }

    [Fact]
    public async Task Cannot_approve_more_than_was_claimed()
    {
        var claim = await SubmitAsJane(amount: 5000m);
        await PickUp(claim.Id, David, 0);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _db.ServicesFor(David).Workflow.ApproveAsync(claim.Id, new ApproveClaimRequest { Version = 1, ApprovedAmount = 5000.01m, Reason = "Too generous" }));
    }

    [Fact]
    public async Task Another_claimant_cannot_see_or_answer_the_claim()
    {
        var claim = await SubmitAsJane();
        await PickUp(claim.Id, David, 0);
        claim = await _db.ServicesFor(David).Workflow.RequestInformationAsync(claim.Id, new RequestInformationRequest { Version = 1, Question = "Photos please?" });
        var requestId = claim.InformationRequests.Single().Id;

        await Assert.ThrowsAsync<NotFoundException>(() => _db.ServicesFor(Arjun).Claims.GetAsync(claim.Id));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _db.ServicesFor(Arjun).Workflow.RespondToInformationRequestAsync(claim.Id, requestId, new RespondToInformationRequest { Version = 2, Response = "Not mine" }));
    }

    [Fact]
    public async Task Late_answer_after_rejection_is_refused()
    {
        var claim = await SubmitAsJane();
        await PickUp(claim.Id, David, 0);
        claim = await _db.ServicesFor(David).Workflow.RequestInformationAsync(claim.Id, new RequestInformationRequest { Version = 1, Question = "Photos please?" });
        await _db.ServicesFor(David).Workflow.RejectAsync(claim.Id, new RejectClaimRequest { Version = 2, Reason = "No response within 30 days." });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _db.ServicesFor(Jane).Workflow.RespondToInformationRequestAsync(
                claim.Id, claim.InformationRequests.Single().Id, new RespondToInformationRequest { Version = 3, Response = "Sorry, late" }));
    }

    [Fact]
    public async Task Only_claimants_can_submit()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _db.ServicesFor(David).Claims.SubmitAsync(new SubmitClaimRequest
            {
                ClaimType = ClaimType.Property,
                Description = "Pipe burst in kitchen.",
                IncidentLocation = "Kuala Lumpur",
                IncidentDate = DateTimeOffset.UtcNow.AddDays(-1),
                ClaimedAmount = 100m,
                MarketCode = "MY"
            }));
    }
}
