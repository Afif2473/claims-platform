using Claims.Api.Data;
using Claims.Api.DTOs;
using Claims.Api.Enums;
using Claims.Api.Errors;
using Claims.Api.Interfaces;
using Claims.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Claims.Api.Services;

public class DashboardService : IDashboardService
{
    private readonly ClaimsDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DashboardService(ClaimsDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<DashboardResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        var user = await _currentUser.GetRequiredUserAsync(cancellationToken);
        if (user.Role == UserRole.Claimant)
            throw new ForbiddenException("The dashboard is only available to claims staff.");

        // Loaded once and aggregated in memory. That is fine at assessment scale, and it keeps
        // Build() a pure function that can be unit-tested without a database. At production volume
        // this would become database-side aggregation or a read model kept up to date from claim events.
        var claims = await _db.Claims.AsNoTracking().ToListAsync(cancellationToken);
        var officers = await _db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.ClaimsOfficer && u.IsActive)
            .ToListAsync(cancellationToken);

        return Build(claims, officers, DateTimeOffset.UtcNow);
    }

    public static DashboardResponse Build(IReadOnlyCollection<Claim> claims, IReadOnlyCollection<AppUser> officers, DateTimeOffset now)
    {
        var claimsByStatus = Enum.GetValues<ClaimStatus>()
            .Select(status => new StatusCount(status, claims.Count(c => c.Status == status)))
            .ToList();

        var unassigned = claims
            .Where(c => c.Status == ClaimStatus.Submitted && c.AssignedOfficerId is null)
            .ToList();
        var queue = new QueueSummary(
            unassigned.Count,
            unassigned.Count == 0 ? null : Days(now - unassigned.Min(c => c.CreatedAt)));

        var officerRows = officers
            .OrderBy(o => o.FullName)
            .Select(officer =>
            {
                var assigned = claims.Where(c => c.AssignedOfficerId == officer.Id).ToList();
                var decided = assigned.Where(c => c.DecidedAt.HasValue).ToList();
                return new OfficerWorkload(
                    officer.Id,
                    officer.FullName,
                    OpenClaims: assigned.Count(c => ClaimWorkflow.OpenStatuses.Contains(c.Status)),
                    AwaitingClaimant: assigned.Count(c => c.Status == ClaimStatus.AdditionalInformationRequired),
                    Approved: decided.Count(c => c.Status is ClaimStatus.Approved or ClaimStatus.Settled),
                    Rejected: decided.Count(c => c.Status == ClaimStatus.Rejected),
                    AverageDaysToDecision: decided.Count == 0
                        ? null
                        : Days(TimeSpan.FromTicks((long)decided.Average(c => (c.DecidedAt!.Value - c.CreatedAt).Ticks))));
            })
            .ToList();

        var exposure = claims
            .Where(c => ClaimWorkflow.OpenStatuses.Contains(c.Status))
            .GroupBy(c => (c.MarketCode, c.CurrencyCode))
            .OrderBy(g => g.Key.MarketCode)
            .Select(g => new MarketExposure(
                g.Key.MarketCode,
                g.Key.CurrencyCode,
                OpenClaims: g.Count(),
                TotalClaimed: g.Sum(c => c.ClaimedAmount),
                OutstandingExposure: g.Sum(LiabilityExposure.For),
                UnreservedClaims: g.Count(c => c.ReserveAmount is null && c.ApprovedAmount is null)))
            .ToList();

        return new DashboardResponse(now, claimsByStatus, queue, officerRows, exposure);
    }

    private static double Days(TimeSpan span) => Math.Round(span.TotalDays, 1);
}
