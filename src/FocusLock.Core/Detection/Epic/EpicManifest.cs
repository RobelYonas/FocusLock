using System.Text.Json.Serialization;

namespace FocusLock.Core.Detection.Epic;

/// <summary>The fields FocusLock reads from an Epic Games Launcher .item manifest.</summary>
public sealed class EpicManifest
{
    public string? DisplayName { get; set; }

    public string? AppName { get; set; }

    public string? MainGameAppName { get; set; }

    public string? InstallLocation { get; set; }

    public string? LaunchExecutable { get; set; }

    public List<string>? AppCategories { get; set; }

    [JsonPropertyName("bIsIncompleteInstall")]
    public bool IsIncompleteInstall { get; set; }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(EpicManifest))]
internal sealed partial class EpicJsonContext : JsonSerializerContext
{
}
