using Claims.Api.Enums;

namespace Claims.Api.DTOs;

/// <summary>
/// Team view for claims staff: what is waiting, who is working on what, how fast decisions
/// are made, and what liability is still outstanding.
/// </summary>
public record DashboardResponse(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<StatusCount> ClaimsByStatus,
    QueueSummary Queue,
    IReadOnlyList<OfficerWorkload> Officers,
    IReadOnlyList<MarketExposure> Exposure);

public record StatusCount(ClaimStatus Status, int Count);

/// <summary>Submitted claims nobody has picked up yet, and how long the oldest has waited.</summary>
public record QueueSummary(int Unassigned, double? OldestUnassignedDays);

/// <summary>
/// Workload: OpenClaims and AwaitingClaimant (ball is in the claimant's court).
/// Performance: Approved / Rejected decisions and average days from submission to decision.
/// </summary>
public record OfficerWorkload(
    Guid OfficerId,
    string OfficerName,
    int OpenClaims,
    int AwaitingClaimant,
    int Approved,
    int Rejected,
    double? AverageDaysToDecision);

/// <summary>
/// Outstanding liability for one market. Each market settles in one currency, so totals are
/// never mixed across currencies (no FX conversion).
/// UnreservedClaims counts open claims whose exposure still relies on the claimant's own figure.
/// </summary>
public record MarketExposure(
    string MarketCode,
    string CurrencyCode,
    int OpenClaims,
    decimal TotalClaimed,
    decimal OutstandingExposure,
    int UnreservedClaims);
