using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusLock.App.Services;
using FocusLock.Core.Common;
using FocusLock.Core.Detection;
using FocusLock.Core.Settings;
using Microsoft.Extensions.Logging;

namespace FocusLock.App.ViewModels;

public sealed partial class BlocklistViewModel : ObservableObject
{
    private readonly GameCatalog _catalog;
    private readonly SettingsService _settings;
    private readonly IFilePicker _picker;
    private readonly SessionController _controller;
    private readonly ILogger<BlocklistViewModel> _logger;
    private bool _applyingToggle;

    [ObservableProperty]
    private bool _isRefreshing;

    public BlocklistViewModel(
        GameCatalog catalog,
        SettingsService settings,
        IFilePicker picker,
        SessionController controller,
        IUiDispatcher dispatcher,
        ILogger<BlocklistViewModel> logger)
    {
        _catalog = catalog;
        _settings = settings;
        _picker = picker;
        _controller = controller;
        _logger = logger;

        Groups =
        [
            new GameGroupViewModel("Steam", "No Steam games found.", GameSource.Steam),
            new GameGroupViewModel("Epic Games", "No Epic Games Store games found.", GameSource.Epic),
            new GameGroupViewModel("GOG", "No GOG games found.", GameSource.Gog),
            new GameGroupViewModel("EA app", "No EA games found.", GameSource.Ea),
            new GameGroupViewModel("Ubisoft Connect", "No Ubisoft games found.", GameSource.Ubisoft),
            new GameGroupViewModel("Battle.net", "No Battle.net games found.", GameSource.BattleNet),
            new GameGroupViewModel("Added by you", "Nothing added yet. Pick a game's .exe file or its folder.", GameSource.Custom)
            {
                CanAdd = true,
                AddAppCommand = AddCustomAppCommand,
                AddFolderCommand = AddCustomFolderCommand,
            },
        ];

        catalog.Changed += (_, _) =>
        {
            if (!_applyingToggle)
            {
                dispatcher.Post(Rebuild);
            }
        };
        controller.PropertyChanged += OnControllerPropertyChanged;
        Rebuild();
    }

    public IReadOnlyList<GameGroupViewModel> Groups { get; }

    public ObservableCollection<AllowItemViewModel> Allowed { get; } = [];

    public bool HasAllowed => Allowed.Count > 0;

    public string LauncherNames => string.Join(", ", _catalog.Entries.Where(entry => entry.Game.Source == GameSource.Launcher).Select(entry => entry.Game.Name));

    public bool BlockLaunchers
    {
        get => _settings.Current.BlockLaunchers;
        set
        {
            if (value == BlockLaunchers)
            {
                return;
            }

            _applyingToggle = true;
            try
            {
                _catalog.SetLaunchersEnabled(value);
            }
            finally
            {
                _applyingToggle = false;
            }

            OnPropertyChanged();
        }
    }

    public bool IsSessionRunning => _controller.IsRunning;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        try
        {
            await Task.Run(() => _catalog.RefreshAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Refreshing the game list failed");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private Task AddCustomAppAsync() => AddCustomAsync(_picker.PickExecutable(), isFolder: false);

    [RelayCommand]
    private Task AddCustomFolderAsync() => AddCustomAsync(_picker.PickFolder(), isFolder: true);

    [RelayCommand]
    private void AddAllowedApp() => AddAllowed(_picker.PickExecutable(), isFolder: false);

    [RelayCommand]
    private void AddAllowedFolder() => AddAllowed(_picker.PickFolder(), isFolder: true);

    private async Task AddCustomAsync(string? path, bool isFolder)
    {
        var normalized = PathNormalizer.TryNormalize(path);
        if (normalized is null)
        {
            return;
        }

        _settings.Update(settings =>
        {
            if (!settings.CustomBlocklist.Any(entry => PathNormalizer.PathEquals(entry.Path, normalized)))
            {
                settings.CustomBlocklist.Add(new CustomEntry { Path = normalized, IsFolder = isFolder });
            }
        });

        await RefreshAsync();
    }

    private async Task RemoveCustomAsync(GameItemViewModel item)
    {
        _settings.Update(settings => settings.CustomBlocklist.RemoveAll(entry => PathNormalizer.PathEquals(PathNormalizer.TryNormalize(entry.Path) ?? entry.Path, item.Target)));
        await RefreshAsync();
    }

    private void AddAllowed(string? path, bool isFolder)
    {
        var normalized = PathNormalizer.TryNormalize(path);
        if (normalized is null)
        {
            return;
        }

        _settings.Update(settings =>
        {
            if (!settings.Allowlist.Any(entry => PathNormalizer.PathEquals(entry.Path, normalized)))
            {
                settings.Allowlist.Add(new CustomEntry { Path = normalized, IsFolder = isFolder });
            }
        });

        RebuildAllowed();
    }

    private void RemoveAllowed(AllowItemViewModel item)
    {
        _settings.Update(settings => settings.Allowlist.RemoveAll(entry => PathNormalizer.PathEquals(entry.Path, item.Path)));
        RebuildAllowed();
    }

    private void Rebuild()
    {
        foreach (var group in Groups)
        {
            var items = _catalog.Entries
                .Where(entry => entry.Game.Source == group.Source)
                .OrderBy(entry => entry.Game.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(entry => new GameItemViewModel(entry, SetEnabled, group.Source == GameSource.Custom ? RemoveCustomAsync : null));
            group.Replace(items);
        }

        OnPropertyChanged(nameof(LauncherNames));
        OnPropertyChanged(nameof(BlockLaunchers));
        RebuildAllowed();
    }

    private void RebuildAllowed()
    {
        Allowed.Clear();
        foreach (var entry in _settings.Current.Allowlist)
        {
            Allowed.Add(new AllowItemViewModel(entry, RemoveAllowed));
        }

        OnPropertyChanged(nameof(HasAllowed));
    }

    private void SetEnabled(GameItemViewModel item, bool enabled)
    {
        _applyingToggle = true;
        try
        {
            _catalog.SetEnabled(item.Id, enabled);
        }
        finally
        {
            _applyingToggle = false;
        }
    }

    private void OnControllerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SessionController.State))
        {
            OnPropertyChanged(nameof(IsSessionRunning));
        }
    }
}
