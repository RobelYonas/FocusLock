namespace FocusLock.Core.Settings;

/// <summary>User settings, stored as JSON in %AppData%\FocusLock\settings.json.</summary>
public sealed class AppSettings
{
    public List<int> PresetMinutes { get; set; } = [25, 50, 60];

    public int DefaultMinutes { get; set; } = 25;

    public PomodoroSettings Pomodoro { get; set; } = new();

    /// <summary>Also close the launchers themselves (Steam, Epic, ...).</summary>
    public bool BlockLaunchers { get; set; } = true;

    /// <summary>Close games that are already open when a session starts.</summary>
    public bool CloseAlreadyRunningGames { get; set; } = true;

    /// <summary>Sessions cannot be ended early at all; they run until the timer finishes.</summary>
    public bool CommitToTimer { get; set; }

    public bool LaunchAtStartup { get; set; }

    public bool ShowNotifications { get; set; } = true;

    public AppTheme Theme { get; set; } = AppTheme.System;

    /// <summary>Detected games the user switched off, by <c>DetectedGame.Id</c>.</summary>
    public List<string> DisabledGameIds { get; set; } = [];

    public List<CustomEntry> CustomBlocklist { get; set; } = [];

    /// <summary>Programs that are never closed, even if a blocklist entry matches them.</summary>
    public List<CustomEntry> Allowlist { get; set; } = [];
}

/// <summary>Light or dark window colors. The numeric values are stored in settings.json, so do not reorder.</summary>
public enum AppTheme
{
    System = 0,
    Light = 1,
    Dark = 2,
}
