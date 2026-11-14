namespace FocusLock.Core.Websites;

/// <summary>The built-in sites shown on the Blocklist page.</summary>
public interface ISitePresetCatalog
{
    IReadOnlyList<SitePreset> All { get; }
}
