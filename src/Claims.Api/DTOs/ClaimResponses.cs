using Claims.Api.Enums;
using Claims.Api.Models;

namespace Claims.Api.DTOs;

/// <summary>
/// One row in a claim list. Version is returned so the client can send it back on updates
/// (optimistic concurrency, Step 07).
/// </summary>
public class ClaimSummaryResponse
{
    public Guid Id { get; set; }
    public string ClaimNumber { get; set; } = string.Empty;
    public ClaimType ClaimType { get; set; }
    public ClaimStatus Status { get; set; }
    public string MarketCode { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal ClaimedAmount { get; set; }
    public decimal? ReserveAmount { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public DateTimeOffset IncidentDate { get; set; }
    public Guid ClaimantId { get; set; }
    public string ClaimantName { get; set; } = string.Empty;
    public Guid? AssignedOfficerId { get; set; }
    public string? AssignedOfficerName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int Version { get; set; }
}

/// <summary>
/// Full claim view: everything in the summary plus the narrative, the decision,
/// the audit timeline and any information requests.
/// </summary>
public class ClaimDetailResponse : ClaimSummaryResponse
{
    public string Description { get; set; } = string.Empty;
    public string IncidentLocation { get; set; } = string.Empty;
    public string? DecisionReason { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public IReadOnlyList<ClaimActivityResponse> Activities { get; set; } = [];
    public IReadOnlyList<InformationRequestResponse> InformationRequests { get; set; } = [];
}

public record ClaimActivityResponse(
    Guid Id,
    ClaimActivityType ActivityType,
    string Description,
    string? ActorName,
    DateTimeOffset CreatedAt);

public record InformationRequestResponse(
    Guid Id,
    string Question,
    string? Response,
    string RequestedByName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RespondedAt,
    bool IsAnswered);

public static class ClaimMappings
{
    /// <summary>Requires Claimant and AssignedOfficer to be loaded.</summary>
    public static ClaimSummaryResponse ToSummary(this Claim claim) => Fill(new ClaimSummaryResponse(), claim);

    /// <summary>Requires Claimant, AssignedOfficer, Activities.ActorUser and InformationRequests.RequestedBy to be loaded.</summary>
    public static ClaimDetailResponse ToDetail(this Claim claim)
    {
        var detail = Fill(new ClaimDetailResponse(), claim);
        detail.Description = claim.Description;
        detail.IncidentLocation = claim.IncidentLocation;
        detail.DecisionReason = claim.DecisionReason;
        detail.DecidedAt = claim.DecidedAt;
        detail.Activities = claim.Activities
            .OrderBy(a => a.CreatedAt)
            .Select(a => new ClaimActivityResponse(a.Id, a.ActivityType, a.Description, a.ActorUser?.FullName, a.CreatedAt))
            .ToList();
        detail.InformationRequests = claim.InformationRequests
            .OrderBy(r => r.CreatedAt)
            .Select(r => new InformationRequestResponse(
                r.Id, r.Question, r.Response, r.RequestedBy?.FullName ?? string.Empty, r.CreatedAt, r.RespondedAt, r.IsAnswered))
            .ToList();
        return detail;
    }

    private static T Fill<T>(T target, Claim claim) where T : ClaimSummaryResponse
    {
        target.Id = claim.Id;
        target.ClaimNumber = claim.ClaimNumber;
        target.ClaimType = claim.ClaimType;
        target.Status = claim.Status;
        target.MarketCode = claim.MarketCode;
        target.CurrencyCode = claim.CurrencyCode;
        target.ClaimedAmount = claim.ClaimedAmount;
        target.ReserveAmount = claim.ReserveAmount;
        target.ApprovedAmount = claim.ApprovedAmount;
        target.IncidentDate = claim.IncidentDate;
        target.ClaimantId = claim.ClaimantId;
        target.ClaimantName = claim.Claimant?.FullName ?? string.Empty;
        target.AssignedOfficerId = claim.AssignedOfficerId;
        target.AssignedOfficerName = claim.AssignedOfficer?.FullName;
        target.CreatedAt = claim.CreatedAt;
        target.UpdatedAt = claim.UpdatedAt;
        target.Version = claim.Version;
        return target;
    }
}
