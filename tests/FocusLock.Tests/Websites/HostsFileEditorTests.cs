using FocusLock.Core.Websites;
using Xunit;

namespace FocusLock.Tests.Websites;

public class HostsFileEditorTests
{
    private const string Stock = "# Copyright (c) 1993-2009 Microsoft Corp.\r\n#\r\n# localhost name resolution is handled within DNS itself.\r\n#\t127.0.0.1       localhost\r\n#\t::1             localhost\r\n";
    private static readonly string[] D = ["a.com", "www.a.com"];

    [Theory]
    [InlineData("")]
    [InlineData(Stock)]
    [InlineData("# x\r\n127.0.0.1 localhost")]
    [InlineData("a\nb\n")]
    [InlineData("a\nb")]
    [InlineData("\n")]
    [InlineData("a\r\n\r\nb\r\n\r\n")]
    [InlineData("a\nb\r\nc\n")]
    public void RoundTrips(string x) => Assert.Equal(x, HostsFileEditor.Remove(HostsFileEditor.Apply(x, D)));

    [Fact]
    public void AppendsUsingDominantLineEnding()
    {
        Assert.Equal("a\nb\n# FocusLock begin\n0.0.0.0 a.com\n0.0.0.0 www.a.com\n# FocusLock end\n",
            HostsFileEditor.Apply("a\nb\n", D));
        Assert.Equal("a\r\n# FocusLock begin\r\n0.0.0.0 a.com\r\n0.0.0.0 www.a.com\r\n# FocusLock end",
            HostsFileEditor.Apply("a", D));
        Assert.Equal("# FocusLock begin\r\n0.0.0.0 a.com\r\n# FocusLock end\r\n", HostsFileEditor.Apply("", ["a.com"]));
    }

    [Fact]
    public void DedupesDomains() =>
        Assert.Equal("# FocusLock begin\r\n0.0.0.0 a.com\r\n# FocusLock end\r\n", HostsFileEditor.Apply("", ["a.com", "a.com"]));

    [Fact]
    public void ReplacesInPlace()
    {
        var text = "top\n# FocusLock begin\n0.0.0.0 old.com\n# FocusLock end\nbottom\n";
        Assert.Equal("top\n# FocusLock begin\n0.0.0.0 a.com\n# FocusLock end\nbottom\n", HostsFileEditor.Apply(text, ["a.com"]));
    }

    [Fact]
    public void MultipleSectionsKeepFirstPosition()
    {
        var text = "1\n# FocusLock begin\n0.0.0.0 x.com\n# FocusLock end\n2\n# focuslock BEGIN\n0.0.0.0 y.com\n  # FocusLock end  \n3\n";
        Assert.Equal("1\n# FocusLock begin\n0.0.0.0 a.com\n# FocusLock end\n2\n3\n", HostsFileEditor.Apply(text, ["a.com"]));
        Assert.Equal("1\n2\n3\n", HostsFileEditor.Remove(text));
    }

    [Fact]
    public void BrokenSectionDropsOnlyBeginAndOwnLines()
    {
        var text = "1\n# FocusLock begin\n0.0.0.0 x.com\n0.0.0.0 y.com\n127.0.0.1 mine.local\n0.0.0.0 not valid\n";
        Assert.Equal("1\n127.0.0.1 mine.local\n0.0.0.0 not valid\n", HostsFileEditor.Remove(text));
        Assert.Equal("1\n# FocusLock begin\n0.0.0.0 a.com\n# FocusLock end\n127.0.0.1 mine.local\n0.0.0.0 not valid\n",
            HostsFileEditor.Apply(text, ["a.com"]));
    }

    [Fact]
    public void BeginFollowedByAnotherBeginIsBroken()
    {
        var text = "# FocusLock begin\n0.0.0.0 x.com\n# FocusLock begin\n0.0.0.0 y.com\n# FocusLock end\nuser\n";
        Assert.Equal("user\n", HostsFileEditor.Remove(text));
    }

    [Fact]
    public void StrayEndMarkerIsRemoved() =>
        Assert.Equal("a\nb\n", HostsFileEditor.Remove("a\n# FocusLock end\nb\n"));

    [Fact]
    public void CommentsMentioningFocusLockAreUntouched()
    {
        var text = "# FocusLock is great\n# FocusLock begin here\n0.0.0.0 mine.com\n";
        Assert.Equal(text, HostsFileEditor.Remove(text));
        Assert.False(HostsFileEditor.ContainsBlock(text));
    }

    [Fact]
    public void ContainsBlockFindsMarker()
    {
        Assert.True(HostsFileEditor.ContainsBlock("a\n  # focuslock BEGIN \n"));
        Assert.False(HostsFileEditor.ContainsBlock(""));
    }

    [Fact]
    public void RemoveLeavesOtherLineEndingsAlone() =>
        Assert.Equal("a\nb\r\nc", HostsFileEditor.Remove("a\nb\r\nc"));

    [Theory]
    [InlineData("A.com")]
    [InlineData("a.com\n0.0.0.0 evil.com")]
    [InlineData("localhost")]
    [InlineData("https://a.com")]
    public void ApplyThrowsOnInvalidDomain(string bad)
    {
        var ex = Assert.Throws<ArgumentException>(() => HostsFileEditor.Apply("x\n", ["ok.com", bad]));
        Assert.Contains(bad, ex.Message);
    }
}