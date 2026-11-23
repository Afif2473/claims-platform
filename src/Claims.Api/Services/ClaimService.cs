using Claims.Api.Data;
using Claims.Api.DTOs;
using Claims.Api.Enums;
using Claims.Api.Errors;
using Claims.Api.Interfaces;
using Claims.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Claims.Api.Services;

public class ClaimService : IClaimService
{
    private readonly ClaimsDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ClaimService(ClaimsDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ClaimDetailResponse> SubmitAsync(SubmitClaimRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _currentUser.GetRequiredUserAsync(cancellationToken);
        if (user.Role != UserRole.Claimant)
            throw new ForbiddenException("Only claimants can submit claims.");

        var marketCode = request.MarketCode.ToUpperInvariant();
        var now = DateTimeOffset.UtcNow;

        var claim = new Claim
        {
            Id = Guid.NewGuid(),
            ClaimNumber = GenerateClaimNumber(marketCode, now),
            ClaimantId = user.Id,
            ClaimType = request.ClaimType!.Value,
            Description = request.Description.Trim(),
            IncidentLocation = request.IncidentLocation.Trim(),
            IncidentDate = request.IncidentDate!.Value,
            ClaimedAmount = request.ClaimedAmount,
            MarketCode = marketCode,
            CurrencyCode = Markets.CurrencyByMarket[marketCode],
            Status = ClaimStatus.Submitted,
            CreatedAt = now,
            UpdatedAt = now
        };

        // The claim and its first audit entry are saved in one SaveChanges, so in one transaction.
        claim.Activities.Add(new ClaimActivity(
            claim.Id, ClaimActivityType.ClaimSubmitted, $"Claim submitted by {user.FullName}.", user.Id));

        _db.Claims.Add(claim);
        await _db.SaveChangesAsync(cancellationToken);

        return await GetAsync(claim.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<ClaimSummaryResponse>> ListAsync(ClaimListQuery query, CancellationToken cancellationToken = default)
    {
        var user = await _currentUser.GetRequiredUserAsync(cancellationToken);

        IQueryable<Claim> claims = _db.Claims
            .AsNoTracking()
            .Include(c => c.Claimant)
            .Include(c => c.AssignedOfficer);

        if (user.Role == UserRole.Claimant)
        {
            // Claimants only ever see their own claims; staff filters are ignored.
            claims = claims.Where(c => c.ClaimantId == user.Id);
        }
        else
        {
            claims = query.Assignment switch
            {
                AssignmentFilter.Mine => claims.Where(c => c.AssignedOfficerId == user.Id),
                AssignmentFilter.Unassigned => claims.Where(c => c.AssignedOfficerId == null),
                _ => claims
            };

            if (!string.IsNullOrWhiteSpace(query.MarketCode))
            {
                var market = query.MarketCode.ToUpperInvariant();
                claims = claims.Where(c => c.MarketCode == market);
            }
        }

        if (query.Status is not null)
            claims = claims.Where(c => c.Status == query.Status);

        var results = await claims.ToListAsync(cancellationToken);

        // Sorted in memory: the EF Core SQLite provider cannot translate ORDER BY on DateTimeOffset.
        // Claimants see newest first; staff see oldest first (a FIFO work queue).
        var ordered = user.Role == UserRole.Claimant
            ? results.OrderByDescending(c => c.CreatedAt)
            : results.OrderBy(c => c.CreatedAt);

        return ordered.Select(c => c.ToSummary()).ToList();
    }

    public async Task<ClaimDetailResponse> GetAsync(Guid claimId, CancellationToken cancellationToken = default)
    {
        var user = await _currentUser.GetRequiredUserAsync(cancellationToken);

        var claim = await _db.Claims
            .AsNoTracking()
            .AsSplitQuery()
            .Include(c => c.Claimant)
            .Include(c => c.AssignedOfficer)
            .Include(c => c.Activities).ThenInclude(a => a.ActorUser)
            .Include(c => c.InformationRequests).ThenInclude(r => r.RequestedBy)
            .FirstOrDefaultAsync(c => c.Id == claimId, cancellationToken);

        // A claimant asking for someone else's claim gets 404, not 403,
        // so the API does not reveal which claim ids exist.
        if (claim is null || (user.Role == UserRole.Claimant && claim.ClaimantId != user.Id))
            throw new NotFoundException($"Claim {claimId} was not found.");

        return claim.ToDetail();
    }

    /// <summary>
    /// Human-readable business key, e.g. CLM-SG-2026-3F9A1C2B. The random suffix avoids a
    /// "SELECT MAX + 1" race between concurrent submissions; the unique index is the final guard.
    /// </summary>
    private static string GenerateClaimNumber(string marketCode, DateTimeOffset now) =>
        $"CLM-{marketCode}-{now.Year}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
}
