namespace FocusLock.Core.Settings;

/// <summary>A user-picked .exe or folder on the blocklist or allowlist.</summary>
public sealed class CustomEntry
{
    public string Path { get; set; } = string.Empty;

    public bool IsFolder { get; set; }
}
