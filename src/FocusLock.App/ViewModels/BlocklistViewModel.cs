using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusLock.App.Services;
using FocusLock.Core.Common;
using FocusLock.Core.Detection;
using FocusLock.Core.Settings;
using FocusLock.Core.Websites;
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
    private readonly ISitePresetCatalog _sitePresets;

    [ObservableProperty]
    private string _newSite = string.Empty;

    [ObservableProperty]
    private string _siteMessage = string.Empty;

    [ObservableProperty]
    private bool _isRefreshing;

    public BlocklistViewModel(
        GameCatalog catalog,
        SettingsService settings,
        IFilePicker picker,
        SessionController controller,
        ISitePresetCatalog sitePresets,
        IUiDispatcher dispatcher,
        ILogger<BlocklistViewModel> logger)
    {
        _catalog = catalog;
        _settings = settings;
        _picker = picker;
        _controller = controller;
        _sitePresets = sitePresets;
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
        RebuildSites();
    }

    public IReadOnlyList<GameGroupViewModel> Groups { get; }

    public ObservableCollection<AllowItemViewModel> Allowed { get; } = [];

    public bool HasAllowed => Allowed.Count > 0;


    public bool IsSessionRunning => _controller.IsRunning;

    public ObservableCollection<SitePresetItemViewModel> SitePresets { get; } = [];

    public ObservableCollection<CustomSiteItemViewModel> CustomSites { get; } = [];

    public bool HasCustomSites => CustomSites.Count > 0;

    public bool HasSiteMessage => SiteMessage.Length > 0;

    public bool CanEditSites => !_controller.IsRunning;

    partial void OnSiteMessageChanged(string value) => OnPropertyChanged(nameof(HasSiteMessage));

    [RelayCommand]
    private void AddSite()
    {
        var typed = NewSite;
        if (string.IsNullOrWhiteSpace(typed))
        {
            return;
        }

        string? domain;
        try
        {
            if (!DomainName.TryNormalize(typed, out domain))
            {
                domain = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Checking a website address failed");
            domain = null;
        }

        if (domain is null)
        {
            SiteMessage = "That doesn't look like a website address. Try something like example.com.";
            return;
        }

        if (_settings.Current.CustomBlockedDomains.Contains(domain, StringComparer.OrdinalIgnoreCase))
        {
            SiteMessage = $"{domain} is already on the list.";
            return;
        }

        _settings.Update(settings => settings.CustomBlockedDomains.Add(domain));
        NewSite = string.Empty;
        SiteMessage = string.Empty;
        RebuildSites();
    }

    private void RemoveSite(CustomSiteItemViewModel item)
    {
        _settings.Update(settings => settings.CustomBlockedDomains.RemoveAll(domain => string.Equals(domain, item.Domain, StringComparison.OrdinalIgnoreCase)));
        SiteMessage = string.Empty;
        RebuildSites();
    }

    private void SetPresetEnabled(SitePresetItemViewModel item, bool enabled) =>
        _settings.Update(settings =>
        {
            settings.BlockedSitePresetIds.RemoveAll(id => id == item.Id);
            if (enabled)
            {
                settings.BlockedSitePresetIds.Add(item.Id);
            }
        });

    private void RebuildSites()
    {
        var current = _settings.Current;
        SitePresets.Clear();
        foreach (var preset in _sitePresets.All)
        {
            SitePresets.Add(new SitePresetItemViewModel(preset, current.BlockedSitePresetIds.Contains(preset.Id), SetPresetEnabled));
        }

        CustomSites.Clear();
        foreach (var domain in current.CustomBlockedDomains)
        {
            CustomSites.Add(new CustomSiteItemViewModel(domain, RemoveSite));
        }

        OnPropertyChanged(nameof(HasCustomSites));
    }

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
            OnPropertyChanged(nameof(CanEditSites));
        }
    }
}
