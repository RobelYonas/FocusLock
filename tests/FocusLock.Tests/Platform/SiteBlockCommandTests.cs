using System.Text.RegularExpressions;
using FocusLock.Core.Abstractions;
using FocusLock.Core.Platform;

namespace FocusLock.Tests.Platform;

public class SiteBlockCommandTests
{
    // Stand-in for DomainName.IsValid so parsing is tested on its own.
    internal static bool SimpleIsValid(string value) => Regex.IsMatch(value, "^[a-z0-9-]+(\\.[a-z0-9-]+)+$") && !Regex.IsMatch(value, "^[0-9.]+$");

    [Fact]
    public void Parses_apply_with_domains()
    {
        var command = SiteBlockCommand.TryParse(["--site-block", "apply", "instagram.com", "www.instagram.com", "instagram.com"], SimpleIsValid);

        Assert.NotNull(command);
        Assert.True(command.IsApply);
        Assert.Equal(["instagram.com", "www.instagram.com"], command.Domains);
    }

    [Fact]
    public void Parses_clear()
    {
        var command = SiteBlockCommand.TryParse(["--site-block", "clear"], SimpleIsValid);

        Assert.NotNull(command);
        Assert.False(command.IsApply);
        Assert.Empty(command.Domains);
    }

    [Theory]
    [InlineData("1.2.3.4")]
    [InlineData("*.example.com")]
    [InlineData(@"C:\Windows\System32\drivers\etc\hosts")]
    [InlineData("0.0.0.0 example.com")]
    [InlineData("example.com\r\n0.0.0.0 bank.com")]
    [InlineData("")]
    public void Rejects_whole_apply_when_any_domain_is_invalid(string bad)
    {
        Assert.Null(SiteBlockCommand.TryParse(["--site-block", "apply", "youtube.com", bad, "reddit.com"], SimpleIsValid));
    }

    [Theory]
    [InlineData("--site-block")]
    [InlineData("--site-block", "apply")]
    [InlineData("--site-block", "clear", "youtube.com")]
    [InlineData("--site-block", "remove", "youtube.com")]
    [InlineData("--site-block", "APPLY", "youtube.com")]
    [InlineData("--minimized", "apply", "youtube.com")]
    public void Rejects_bad_command_lines(params string[] args)
    {
        Assert.Null(SiteBlockCommand.TryParse(args, SimpleIsValid));
    }

    [Fact]
    public void Rejects_more_than_the_domain_limit()
    {
        var domains = Enumerable.Range(0, SiteBlockCommand.MaxDomains + 1).Select(i => $"site{i}.com");

        Assert.Null(SiteBlockCommand.TryParse(["--site-block", "apply", .. domains], SimpleIsValid));
        Assert.NotNull(SiteBlockCommand.TryParse(["--site-block", "apply", .. domains.Take(SiteBlockCommand.MaxDomains)], SimpleIsValid));
    }

    [Fact]
    public void Recognizes_helper_invocation_only_as_first_argument()
    {
        Assert.True(SiteBlockCommand.IsHelperInvocation(["--site-block", "clear"]));
        Assert.False(SiteBlockCommand.IsHelperInvocation(["--minimized", "--site-block"]));
        Assert.False(SiteBlockCommand.IsHelperInvocation([]));
    }

    [Fact]
    public void Arguments_round_trip_through_windows_quoting()
    {
        var command = new SiteBlockCommand(true, ["youtube.com", "reddit.com"]);

        Assert.Equal("--site-block apply youtube.com reddit.com", command.ToArguments());
        Assert.Equal("--site-block clear", new SiteBlockCommand(false, []).ToArguments());
    }

    [Theory]
    [InlineData("a b", "\"a b\"")]
    [InlineData("", "\"\"")]
    [InlineData("say \"hi\"", "\"say \\\"hi\\\"\"")]
    [InlineData("dir\\ x\\", "\"dir\\ x\\\\\"")]
    public void Quotes_arguments_that_need_it(string arg, string expected)
    {
        Assert.Equal(expected, SiteBlockCommand.Quote(arg));
    }

    [Theory]
    [InlineData(SiteBlockExitCodes.Success, true, SiteBlockResult.Applied)]
    [InlineData(SiteBlockExitCodes.Success, false, SiteBlockResult.Cleared)]
    [InlineData(SiteBlockExitCodes.BadArguments, true, SiteBlockResult.Failed)]
    [InlineData(SiteBlockExitCodes.HostsFailed, false, SiteBlockResult.Failed)]
    [InlineData(SiteBlockExitCodes.PolicyFailed, true, SiteBlockResult.Failed)]
    [InlineData(-1, true, SiteBlockResult.Failed)]
    public void Maps_exit_codes(int exitCode, bool apply, SiteBlockResult expected)
    {
        Assert.Equal(expected, SiteBlockExitCodes.ToResult(exitCode, apply));
    }
}
