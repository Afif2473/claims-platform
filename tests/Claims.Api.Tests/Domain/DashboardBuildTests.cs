using Claims.Api.Enums;
using Claims.Api.Models;
using Claims.Api.Services;

namespace Claims.Api.Tests.Domain;

public class DashboardBuildTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
    private static readonly AppUser David = new() { Id = Guid.NewGuid(), FullName = "David Wong", Role = UserRole.ClaimsOfficer };
    private static readonly AppUser Mei = new() { Id = Guid.NewGuid(), FullName = "Mei Lin", Role = UserRole.ClaimsOfficer };

    private static Claim NewClaim(ClaimStatus status, string market, decimal claimed, double createdDaysAgo,
        AppUser? officer = null, decimal? reserve = null, decimal? approved = null, double? decidedDaysAgo = null) => new()
    {
        Id = Guid.NewGuid(),
        Status = status,
        MarketCode = market,
        CurrencyCode = Markets.CurrencyByMarket[market],
        ClaimedAmount = claimed,
        ReserveAmount = reserve,
        ApprovedAmount = approved,
        AssignedOfficerId = officer?.Id,
        CreatedAt = Now.AddDays(-createdDaysAgo),
        DecidedAt = decidedDaysAgo is null ? null : Now.AddDays(-decidedDaysAgo.Value)
    };

    private static readonly Claim[] Claims =
    [
        NewClaim(ClaimStatus.Submitted, "SG", 5000m, createdDaysAgo: 3),
        NewClaim(ClaimStatus.Submitted, "SG", 1000m, createdDaysAgo: 1),
        NewClaim(ClaimStatus.UnderReview, "HK", 80000m, 6, David, reserve: 65000m),
        NewClaim(ClaimStatus.AdditionalInformationRequired, "SG", 3200m, 4, Mei),
        NewClaim(ClaimStatus.Approved, "AU", 9000m, 10, David, approved: 8500m, decidedDaysAgo: 2),
        NewClaim(ClaimStatus.Rejected, "TH", 150000m, 12, Mei, decidedDaysAgo: 5),
        NewClaim(ClaimStatus.Settled, "SG", 2500m, 20, Mei, approved: 2500m, decidedDaysAgo: 15)
    ];

    private static readonly Claims.Api.DTOs.DashboardResponse Result = DashboardService.Build(Claims, [Mei, David], Now);

    [Fact]
    public void Counts_every_status_including_zeros()
    {
        Assert.Equal(Enum.GetValues<ClaimStatus>().Length, Result.ClaimsByStatus.Count);
        Assert.Equal(2, Result.ClaimsByStatus.Single(s => s.Status == ClaimStatus.Submitted).Count);
        Assert.Equal(1, Result.ClaimsByStatus.Single(s => s.Status == ClaimStatus.Settled).Count);
    }

    [Fact]
    public void Queue_reports_unassigned_claims_and_age_of_the_oldest()
    {
        Assert.Equal(2, Result.Queue.Unassigned);
        Assert.Equal(3.0, Result.Queue.OldestUnassignedDays);
    }

    [Fact]
    public void Officer_rows_show_workload_and_performance()
    {
        var david = Result.Officers.Single(o => o.OfficerId == David.Id);
        Assert.Equal(2, david.OpenClaims);           // under review + approved-not-settled
        Assert.Equal(0, david.AwaitingClaimant);
        Assert.Equal(1, david.Approved);
        Assert.Equal(8.0, david.AverageDaysToDecision); // created 10 days ago, decided 2 days ago

        var mei = Result.Officers.Single(o => o.OfficerId == Mei.Id);
        Assert.Equal(1, mei.OpenClaims);
        Assert.Equal(1, mei.AwaitingClaimant);
        Assert.Equal(1, mei.Approved);              // settled counts as approved
        Assert.Equal(1, mei.Rejected);
        Assert.Equal(6.0, mei.AverageDaysToDecision); // (7 + 5) / 2
    }

    [Fact]
    public void Officers_are_sorted_by_name()
    {
        Assert.Equal(["David Wong", "Mei Lin"], Result.Officers.Select(o => o.OfficerName));
    }

    [Fact]
    public void Exposure_is_grouped_per_market_and_ignores_closed_claims()
    {
        Assert.Equal(["AU", "HK", "SG"], Result.Exposure.Select(e => e.MarketCode)); // TH was rejected

        var sg = Result.Exposure.Single(e => e.MarketCode == "SG");
        Assert.Equal("SGD", sg.CurrencyCode);
        Assert.Equal(3, sg.OpenClaims);              // settled SG claim excluded
        Assert.Equal(9200m, sg.TotalClaimed);
        Assert.Equal(9200m, sg.OutstandingExposure);
        Assert.Equal(3, sg.UnreservedClaims);

        var hk = Result.Exposure.Single(e => e.MarketCode == "HK");
        Assert.Equal(80000m, hk.TotalClaimed);
        Assert.Equal(65000m, hk.OutstandingExposure); // reserve, not the claimed figure
        Assert.Equal(0, hk.UnreservedClaims);

        Assert.Equal(8500m, Result.Exposure.Single(e => e.MarketCode == "AU").OutstandingExposure);
    }

    [Fact]
    public void Empty_data_gives_empty_but_valid_dashboard()
    {
        var empty = DashboardService.Build([], [David], Now);
        Assert.Equal(0, empty.Queue.Unassigned);
        Assert.Null(empty.Queue.OldestUnassignedDays);
        Assert.Null(empty.Officers.Single().AverageDaysToDecision);
        Assert.Empty(empty.Exposure);
    }
}
