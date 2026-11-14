using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusLock.Core.Websites;

namespace FocusLock.App.ViewModels;

/// <summary>One built-in site on the Blocklist page.</summary>
public sealed partial class SitePresetItemViewModel : ObservableObject
{
    private readonly Action<SitePresetItemViewModel, bool> _setEnabled;

    [ObservableProperty]
    private bool _isEnabled;

    public SitePresetItemViewModel(SitePreset preset, bool isEnabled, Action<SitePresetItemViewModel, bool> setEnabled)
    {
        _setEnabled = setEnabled;
        Id = preset.Id;
        Name = preset.Name;
        Detail = preset.Domains.FirstOrDefault() ?? string.Empty;
        _isEnabled = isEnabled;
    }

    public string Id { get; }

    public string Name { get; }

    public string Detail { get; }

    public string ToggleAccessibleName => $"Block {Name}";

    partial void OnIsEnabledChanged(bool value) => _setEnabled(this, value);
}

/// <summary>One website the person added.</summary>
public sealed class CustomSiteItemViewModel
{
    public CustomSiteItemViewModel(string domain, Action<CustomSiteItemViewModel> remove)
    {
        Domain = domain;
        RemoveCommand = new RelayCommand(() => remove(this));
    }

    public string Domain { get; }

    public IRelayCommand RemoveCommand { get; }

    public string RemoveAccessibleName => $"Remove {Domain}";
}
