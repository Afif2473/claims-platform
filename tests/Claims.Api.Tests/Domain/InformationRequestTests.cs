using Claims.Api.Models;

namespace Claims.Api.Tests.Domain;

public class InformationRequestTests
{
    private static InformationRequest NewRequest() => new(Guid.NewGuid(), Guid.NewGuid(), "Police report number?");

    [Fact]
    public void New_request_is_open()
    {
        var request = NewRequest();
        Assert.False(request.IsAnswered);
        Assert.Null(request.Response);
    }

    [Fact]
    public void Responding_closes_the_request()
    {
        var request = NewRequest();
        request.Respond("SG-12345");
        Assert.True(request.IsAnswered);
        Assert.Equal("SG-12345", request.Response);
        Assert.NotNull(request.RespondedAt);
    }

    [Fact]
    public void Cannot_answer_twice()
    {
        var request = NewRequest();
        request.Respond("first");
        Assert.Throws<InvalidOperationException>(() => request.Respond("second"));
        Assert.Equal("first", request.Response);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Cannot_answer_with_blank_text(string blank)
    {
        Assert.Throws<ArgumentException>(() => NewRequest().Respond(blank));
    }
}
