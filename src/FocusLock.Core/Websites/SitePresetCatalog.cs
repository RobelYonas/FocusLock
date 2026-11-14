namespace FocusLock.Core.Websites;

/// <summary>The built-in site presets. Contract stub: Task D fills in the list.</summary>
public sealed class SitePresetCatalog : ISitePresetCatalog
{
    public IReadOnlyList<SitePreset> All { get; } = [];
}
