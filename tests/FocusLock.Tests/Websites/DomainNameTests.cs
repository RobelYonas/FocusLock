using FocusLock.Core.Websites;
using Xunit;

namespace FocusLock.Tests.Websites;

public class DomainNameTests
{
    [Theory]
    [InlineData("instagram.com", "instagram.com")]
    [InlineData("https://www.instagram.com/reels", "instagram.com")]
    [InlineData("  Instagram.COM  ", "instagram.com")]
    [InlineData("http://m.youtube.com:443/watch?v=1", "youtube.com")]
    [InlineData("instagram.com/", "instagram.com")]
    [InlineData("instagram.com.", "instagram.com")]
    [InlineData("www.instagram.com", "instagram.com")]
    [InlineData("www.www.example.com", "www.example.com")]
    [InlineData("www.com", "www.com")]
    [InlineData("m.com", "m.com")]
    [InlineData("old.reddit.com", "old.reddit.com")]
    [InlineData("a-b.co.uk", "a-b.co.uk")]
    [InlineData("b\u00fccher.de", "xn--bcher-kva.de")]
    [InlineData("example.com:8080", "example.com")]
    public void Normalizes(string input, string expected)
    {
        Assert.True(DomainName.TryNormalize(input, out var d));
        Assert.Equal(expected, d);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("127.0.0.1")]
    [InlineData("http://192.168.1.1/x")]
    [InlineData("::1")]
    [InlineData("[::1]")]
    [InlineData("http://[2001:db8::1]:80/")]
    [InlineData("localhost")]
    [InlineData("foo.localhost")]
    [InlineData("intranet")]
    [InlineData("*.example.com")]
    [InlineData("exa mple.com")]
    [InlineData("example.com#frag")]
    [InlineData("user@example.com")]
    [InlineData("https://user:pw@example.com")]
    [InlineData("-a.example.com")]
    [InlineData("a-.example.com")]
    [InlineData("a..com")]
    [InlineData("exa_mple.com")]
    [InlineData("example.123")]
    [InlineData("example.com:abc")]
    [InlineData("www.")]
    [InlineData(".com")]
    public void Rejects(string? input) => Assert.False(DomainName.TryNormalize(input, out _));

    [Fact]
    public void RejectsLongLabelAndLongName()
    {
        Assert.True(DomainName.TryNormalize(new string('a', 63) + ".com", out _));
        Assert.False(DomainName.TryNormalize(new string('a', 64) + ".com", out _));
        var longName = string.Join(".", Enumerable.Repeat(new string('a', 60), 5)) + ".com"; // 304
        Assert.False(DomainName.TryNormalize(longName, out _));
    }

    [Theory]
    [InlineData("instagram.com", true)]
    [InlineData("www.instagram.com", true)]
    [InlineData("m.instagram.com", true)]
    [InlineData("xn--bcher-kva.de", true)]
    [InlineData("Instagram.com", false)]
    [InlineData("https://instagram.com", false)]
    [InlineData("instagram.com/", false)]
    [InlineData("instagram.com.", false)]
    [InlineData(" instagram.com", false)]
    [InlineData("instagram.com\n0.0.0.0 evil.com", false)]
    [InlineData("instagram.com:80", false)]
    [InlineData("localhost", false)]
    [InlineData("a.localhost", false)]
    [InlineData("1.2.3.4", false)]
    [InlineData("b\u00fccher.de", false)]
    [InlineData("a..com", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidIsStrict(string? value, bool expected) => Assert.Equal(expected, DomainName.IsValid(value));

    [Fact]
    public void WithVariantsDedupesInOrder()
    {
        var result = DomainName.WithVariants(["a.com", "www.b.com", "A.com", "m.c.com", "www.a.com"]);
        Assert.Equal(["a.com", "www.a.com", "m.a.com", "www.b.com", "m.c.com"], result);
    }
}