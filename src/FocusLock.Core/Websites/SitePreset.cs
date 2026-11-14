namespace FocusLock.Core.Websites;

/// <summary>A built-in site the user can switch on, with every domain it needs blocked (site, mobile, CDN).</summary>
public sealed record SitePreset(string Id, string Name, IReadOnlyList<string> Domains);
