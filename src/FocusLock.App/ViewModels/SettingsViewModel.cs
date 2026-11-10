using CommunityToolkit.Mvvm.ComponentModel;
using FocusLock.Core.Detection;
using FocusLock.Core.Settings;

namespace FocusLock.App.ViewModels;

/// <summary>Every change is saved immediately; there is no Save button.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly GameCatalog _catalog;
    private readonly StartupRegistration _startup;
    private bool _loading;
    private bool _saving;

    [ObservableProperty]
    private bool _pomodoroEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BreakMinutesText))]
    private int _breakMinutes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CyclesText))]
    private int _cycles;

    [ObservableProperty]
    private bool _closeAlreadyRunning;

    [ObservableProperty]
    private bool _blockLaunchers;

    [ObservableProperty]
    private bool _strictMode;

    [ObservableProperty]
    private bool _launchAtStartup;

    [ObservableProperty]
    private bool _showNotifications;

    public SettingsViewModel(SettingsService settings, GameCatalog catalog, StartupRegistration startup)
    {
        _settings = settings;
        _catalog = catalog;
        _startup = startup;
        Load();
        settings.Saved += (_, _) =>
        {
            if (!_saving)
            {
                Load();
            }
        };
    }

    public string BreakMinutesText => BreakMinutes == 1 ? "1 min" : $"{BreakMinutes} min";

    public string CyclesText => $"{Cycles} blocks";

    partial void OnPomodoroEnabledChanged(bool value) => Save(settings => settings.Pomodoro.Enabled = value);

    partial void OnBreakMinutesChanged(int value) => Save(settings => settings.Pomodoro.BreakMinutes = value);

    partial void OnCyclesChanged(int value) => Save(settings => settings.Pomodoro.Cycles = value);

    partial void OnCloseAlreadyRunningChanged(bool value) => Save(settings => settings.CloseAlreadyRunningGames = value);

    partial void OnStrictModeChanged(bool value) => Save(settings => settings.StrictMode = value);

    partial void OnShowNotificationsChanged(bool value) => Save(settings => settings.ShowNotifications = value);

    partial void OnLaunchAtStartupChanged(bool value)
    {
        Save(settings => settings.LaunchAtStartup = value);
        if (!_loading)
        {
            _startup.Apply(value);
        }
    }

    partial void OnBlockLaunchersChanged(bool value)
    {
        if (_loading)
        {
            return;
        }

        _saving = true;
        try
        {
            _catalog.SetLaunchersEnabled(value);
        }
        finally
        {
            _saving = false;
        }
    }

    private void Save(Action<AppSettings> change)
    {
        if (_loading)
        {
            return;
        }

        _saving = true;
        try
        {
            _settings.Update(change);
        }
        finally
        {
            _saving = false;
        }
    }

    private void Load()
    {
        _loading = true;
        try
        {
            var settings = _settings.Current;
            PomodoroEnabled = settings.Pomodoro.Enabled;
            BreakMinutes = settings.Pomodoro.BreakMinutes;
            Cycles = settings.Pomodoro.Cycles;
            CloseAlreadyRunning = settings.CloseAlreadyRunningGames;
            BlockLaunchers = settings.BlockLaunchers;
            StrictMode = settings.StrictMode;
            LaunchAtStartup = settings.LaunchAtStartup;
            ShowNotifications = settings.ShowNotifications;
        }
        finally
        {
            _loading = false;
        }
    }
}
