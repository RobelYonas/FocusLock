using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusLock.App.Services;
using FocusLock.Core.Common;
using FocusLock.Core.Detection;
using FocusLock.Core.Updates;
using Microsoft.Extensions.Logging;

namespace FocusLock.App.ViewModels;

/// <summary>Owns navigation. While a session runs, the Home entry shows the active-session view instead.</summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly SessionController _controller;
    private readonly ActiveSessionViewModel _activeSession;
    private readonly GameCatalog _catalog;
    private readonly ILogger<ShellViewModel> _logger;
    private readonly UpdateChecker _updates;
    private readonly ToastNotifier _toasts;
    private NavItem _selected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdate), nameof(UpdateText))]
    private UpdateInfo? _update;

    public bool HasUpdate => Update is not null;

    public string UpdateText => Update is null ? string.Empty : $"FocusLock {Update.Version} is available.";

    public ShellViewModel(
        SessionController controller,
        HomeViewModel home,
        ActiveSessionViewModel activeSession,
        BlocklistViewModel blocklist,
        SettingsViewModel settings,
        GameCatalog catalog,
        UpdateChecker updates,
        ToastNotifier toasts,
        ILogger<ShellViewModel> logger)
    {
        _controller = controller;
        _activeSession = activeSession;
        _catalog = catalog;
        _updates = updates;
        _toasts = toasts;
        _logger = logger;

        Pages =
        [
            new NavItem("Home", "", home),
            new NavItem("Blocklist", "", blocklist),
            new NavItem("Settings", "", settings),
        ];

        foreach (var page in Pages)
        {
            page.PropertyChanged += OnPagePropertyChanged;
        }

        _selected = Pages[0];
        _selected.IsSelected = true;

        controller.PropertyChanged += OnControllerPropertyChanged;
    }

    public IReadOnlyList<NavItem> Pages { get; }

    public object CurrentPage => _selected.ViewModel is HomeViewModel && _controller.IsRunning ? _activeSession : _selected.ViewModel;

    public string Title => _controller.IsRunning
        ? $"FocusLock – {TimeText.Countdown(_controller.State.RemainingInPhase)}"
        : "FocusLock";

    public async Task InitializeAsync()
    {
        _ = _controller.RecoverWebsitesAsync();
        _ = CheckForUpdateAsync();
        try
        {
            await Task.Run(() => _catalog.RefreshAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Looking for games failed");
        }
    }

    // Never throws: UpdateChecker turns every failure into "no update", so being offline stays silent.
    // Repeats because FocusLock often starts at sign-in, before the network is up, and then sits in the tray for days.
    private async Task CheckForUpdateAsync()
    {
        var current = typeof(ShellViewModel).Assembly.GetName().Version ?? new Version(0, 0);
        while ((Update = await _updates.CheckAsync(current)) is null)
        {
            await Task.Delay(UpdateCheckInterval);
        }

        _toasts.Show("A new version is available", $"FocusLock {Update.Version} is ready to download. Open FocusLock to get it.");
    }

    private static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromHours(6);

    [RelayCommand]
    private void DownloadUpdate()
    {
        try
        {
            Process.Start(new ProcessStartInfo(UpdateChecker.DownloadUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Opening the download page failed");
        }
    }

    private void OnPagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is NavItem { IsSelected: true } page && e.PropertyName == nameof(NavItem.IsSelected) && page != _selected)
        {
            _selected = page;
            OnPropertyChanged(nameof(CurrentPage));
        }
    }

    private void OnControllerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SessionController.State))
        {
            OnPropertyChanged(nameof(CurrentPage));
            OnPropertyChanged(nameof(Title));
        }
    }
}
