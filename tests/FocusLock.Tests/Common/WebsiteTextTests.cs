using FocusLock.Core.Common;

namespace FocusLock.Tests.Common;

public class WebsiteTextTests
{
    [Fact]
    public void JoinNames_handles_zero_to_three_names()
    {
        Assert.Equal("your browser", WebsiteText.JoinNames([]));
        Assert.Equal("Chrome", WebsiteText.JoinNames(["Chrome"]));
        Assert.Equal("Chrome and Edge", WebsiteText.JoinNames(["Chrome", "Edge"]));
        Assert.Equal("Chrome, Edge and Firefox", WebsiteText.JoinNames(["Chrome", "Edge", "Firefox"]));
    }

    [Theory]
    [InlineData(0, 0, "")]
    [InlineData(1, 0, "1 game will be blocked")]
    [InlineData(0, 1, "1 website will be blocked")]
    [InlineData(2, 3, "2 games and 3 websites will be blocked")]
    [InlineData(1, 1, "1 game and 1 website will be blocked")]
    public void BlockedSummary_words_the_counts(int games, int websites, string expected) =>
        Assert.Equal(expected, WebsiteText.BlockedSummary(games, websites));
}
