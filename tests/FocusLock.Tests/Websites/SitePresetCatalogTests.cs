using System.Text.RegularExpressions;
using FocusLock.Core.Websites;

namespace FocusLock.Tests.Websites;

public class SitePresetCatalogTests
{
    private static readonly string[] ExpectedIds =
        ["instagram", "youtube", "tiktok", "reddit", "x", "facebook", "snapchat", "twitch", "netflix", "discord"];

    // A hostname: dot-separated labels of a-z, 0-9 and hyphens, with at least one dot. No wildcard, no trailing dot.
    private static readonly Regex HostName = new(@"^[a-z0-9-]+(\.[a-z0-9-]+)+$", RegexOptions.CultureInvariant);

    private readonly IReadOnlyList<SitePreset> _presets = new SitePresetCatalog().All;

    [Fact]
    public void Ids_are_unique_and_lowercase()
    {
        var ids = _presets.Select(p => p.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.All(ids, id => Assert.Equal(id.ToLowerInvariant(), id));
    }

    [Fact]
    public void All_ten_expected_presets_exist()
    {
        var ids = _presets.Select(p => p.Id).ToList();

        Assert.Equal(ExpectedIds.Order(), ids.Order());
    }

    [Fact]
    public void Every_preset_has_a_name()
    {
        Assert.All(_presets, p => Assert.False(string.IsNullOrWhiteSpace(p.Name), $"preset '{p.Id}' has no name"));
    }

    [Fact]
    public void Every_preset_has_at_least_two_domains()
    {
        Assert.All(_presets, p => Assert.True(p.Domains.Count >= 2, $"preset '{p.Id}' has {p.Domains.Count} domain(s)"));
    }

    [Fact]
    public void Every_domain_is_a_lowercase_hostname_with_a_dot()
    {
        var bad = _presets
            .SelectMany(p => p.Domains.Select(d => (p.Id, Domain: d)))
            .Where(x => !HostName.IsMatch(x.Domain))
            .Select(x => $"{x.Id}: {x.Domain}")
            .ToList();

        Assert.Empty(bad);
    }

    [Fact]
    public void No_domain_is_repeated_within_a_preset()
    {
        var duplicates = _presets
            .SelectMany(p => p.Domains.GroupBy(d => d).Where(g => g.Count() > 1).Select(g => $"{p.Id}: {g.Key}"))
            .ToList();

        Assert.Empty(duplicates);
    }
}
