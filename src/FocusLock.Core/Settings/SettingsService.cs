using Microsoft.Extensions.Logging;

namespace FocusLock.Core.Settings;

/// <summary>Holds the loaded settings for the lifetime of the app and persists changes.</summary>
public sealed class SettingsService(ISettingsStore store, ILogger<SettingsService> logger)
{
    private AppSettings? _current;

    public AppSettings Current => _current ??= store.Load();

    public event EventHandler? Saved;

    public void Update(Action<AppSettings> change)
    {
        change(Current);
        Save();
    }

    public void Save()
    {
        try
        {
            store.Save(Current);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Saving settings failed");
        }

        Saved?.Invoke(this, EventArgs.Empty);
    }
}
