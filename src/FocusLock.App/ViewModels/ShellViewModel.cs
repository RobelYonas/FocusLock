using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FocusLock.App.Services;
using FocusLock.Core.Common;
using FocusLock.Core.Detection;
using Microsoft.Extensions.Logging;

namespace FocusLock.App.ViewModels;

/// <summary>Owns navigation. While a session runs, the Home entry shows the active-session view instead.</summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly SessionController _controller;
    private readonly ActiveSessionViewModel _activeSession;
    private readonly GameCatalog _catalog;
    private readonly ILogger<ShellViewModel> _logger;
    private NavItem _selected;

    public ShellViewModel(
        SessionController controller,
        HomeViewModel home,
        ActiveSessionViewModel activeSession,
        BlocklistViewModel blocklist,
        SettingsViewModel settings,
        GameCatalog catalog,
        ILogger<ShellViewModel> logger)
    {
        _controller = controller;
        _activeSession = activeSession;
        _catalog = catalog;
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
        try
        {
            await Task.Run(() => _catalog.RefreshAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Looking for games failed");
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
