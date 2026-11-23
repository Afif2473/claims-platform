using Claims.Api.Enums;
using Claims.Api.Models;

namespace Claims.Api.Tests.Domain;

public class LiabilityExposureTests
{
    private static Claim NewClaim(ClaimStatus status, decimal claimed, decimal? reserve = null, decimal? approved = null) =>
        new() { Status = status, ClaimedAmount = claimed, ReserveAmount = reserve, ApprovedAmount = approved };

    [Fact]
    public void Unassessed_claim_uses_the_claimed_amount()
    {
        Assert.Equal(5000m, LiabilityExposure.For(NewClaim(ClaimStatus.Submitted, 5000m)));
    }

    [Fact]
    public void Reserve_overrides_the_claimed_amount()
    {
        Assert.Equal(4000m, LiabilityExposure.For(NewClaim(ClaimStatus.UnderReview, 5000m, reserve: 4000m)));
    }

    [Fact]
    public void Approved_amount_overrides_the_reserve()
    {
        Assert.Equal(4200m, LiabilityExposure.For(NewClaim(ClaimStatus.Approved, 5000m, reserve: 4000m, approved: 4200m)));
    }

    [Fact]
    public void Claim_waiting_on_claimant_is_still_exposure()
    {
        Assert.Equal(3200m, LiabilityExposure.For(NewClaim(ClaimStatus.AdditionalInformationRequired, 3200m)));
    }

    [Theory]
    [InlineData(ClaimStatus.Rejected)]
    [InlineData(ClaimStatus.Settled)]
    public void Closed_claims_carry_no_exposure(ClaimStatus status)
    {
        Assert.Equal(0m, LiabilityExposure.For(NewClaim(status, 5000m, reserve: 4000m, approved: 4200m)));
    }
}
