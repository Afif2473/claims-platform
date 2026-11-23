using Claims.Api.Enums;
using Claims.Api.Models;

namespace Claims.Api.Tests.Domain;

public class ClaimWorkflowTests
{
    [Theory]
    [InlineData(ClaimStatus.Submitted, ClaimStatus.UnderReview)]
    [InlineData(ClaimStatus.UnderReview, ClaimStatus.AdditionalInformationRequired)]
    [InlineData(ClaimStatus.UnderReview, ClaimStatus.Approved)]
    [InlineData(ClaimStatus.UnderReview, ClaimStatus.Rejected)]
    [InlineData(ClaimStatus.AdditionalInformationRequired, ClaimStatus.UnderReview)]
    [InlineData(ClaimStatus.AdditionalInformationRequired, ClaimStatus.Rejected)]
    [InlineData(ClaimStatus.Approved, ClaimStatus.Settled)]
    public void Allows_every_documented_transition(ClaimStatus from, ClaimStatus to)
    {
        Assert.True(ClaimWorkflow.CanTransition(from, to));
    }

    [Theory]
    [InlineData(ClaimStatus.Submitted, ClaimStatus.Approved)]                      // must be reviewed first
    [InlineData(ClaimStatus.Submitted, ClaimStatus.Settled)]
    [InlineData(ClaimStatus.UnderReview, ClaimStatus.Settled)]                     // must be approved first
    [InlineData(ClaimStatus.UnderReview, ClaimStatus.UnderReview)]                 // double pick-up
    [InlineData(ClaimStatus.AdditionalInformationRequired, ClaimStatus.Approved)]  // can't approve while waiting on claimant
    [InlineData(ClaimStatus.AdditionalInformationRequired, ClaimStatus.AdditionalInformationRequired)]
    [InlineData(ClaimStatus.Approved, ClaimStatus.Rejected)]                       // decision can't be reversed
    [InlineData(ClaimStatus.Rejected, ClaimStatus.UnderReview)]
    [InlineData(ClaimStatus.Settled, ClaimStatus.UnderReview)]
    public void Rejects_illegal_transitions(ClaimStatus from, ClaimStatus to)
    {
        Assert.False(ClaimWorkflow.CanTransition(from, to));
    }

    [Theory]
    [InlineData(ClaimStatus.Rejected)]
    [InlineData(ClaimStatus.Settled)]
    public void Terminal_statuses_have_no_way_out(ClaimStatus terminal)
    {
        Assert.All(Enum.GetValues<ClaimStatus>(), to => Assert.False(ClaimWorkflow.CanTransition(terminal, to)));
    }

    [Fact]
    public void Open_statuses_are_everything_except_rejected_and_settled()
    {
        var expected = Enum.GetValues<ClaimStatus>().Except([ClaimStatus.Rejected, ClaimStatus.Settled]);
        Assert.Equal(expected.OrderBy(s => s), ClaimWorkflow.OpenStatuses.OrderBy(s => s));
    }
}
