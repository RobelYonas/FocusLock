using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusLock.Core.Detection;
using FocusLock.Core.Settings;

namespace FocusLock.App.ViewModels;

/// <summary>One detected game on the Blocklist page.</summary>
public sealed partial class GameItemViewModel : ObservableObject
{
    private readonly Action<GameItemViewModel, bool> _setEnabled;

    [ObservableProperty]
    private bool _isEnabled;

    public GameItemViewModel(CatalogEntry entry, Action<GameItemViewModel, bool> setEnabled, Func<GameItemViewModel, Task>? remove)
    {
        _setEnabled = setEnabled;
        Id = entry.Game.Id;
        Name = entry.Game.Name;
        Target = entry.Game.Target;
        Detail = entry.Game.Target;
        _isEnabled = entry.IsEnabled;
        CanRemove = remove is not null;
        RemoveCommand = new AsyncRelayCommand(() => remove?.Invoke(this) ?? Task.CompletedTask);
    }

    public string Id { get; }

    public string Name { get; }

    public string Target { get; }

    public string Detail { get; }

    public bool CanRemove { get; }

    public IAsyncRelayCommand RemoveCommand { get; }

    public string ToggleAccessibleName => $"Block {Name}";

    public string RemoveAccessibleName => $"Remove {Name}";

    partial void OnIsEnabledChanged(bool value) => _setEnabled(this, value);
}

/// <summary>A section of the Blocklist page: one game source.</summary>
public sealed partial class GameGroupViewModel(string title, string emptyText, GameSource source) : ObservableObject
{
    public string Title { get; } = title;

    public string EmptyText { get; } = emptyText;

    public GameSource Source { get; } = source;

    public ObservableCollection<GameItemViewModel> Items { get; } = [];

    public bool HasItems => Items.Count > 0;

    public string CountText => Items.Count switch
    {
        0 => string.Empty,
        1 => "1 game",
        var count => $"{count} games",
    };

    public bool CanAdd { get; init; }

    public IAsyncRelayCommand? AddAppCommand { get; init; }

    public IAsyncRelayCommand? AddFolderCommand { get; init; }

    public void Replace(IEnumerable<GameItemViewModel> items)
    {
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }

        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(CountText));
    }
}

/// <summary>One allowlist entry.</summary>
public sealed class AllowItemViewModel
{
    public AllowItemViewModel(CustomEntry entry, Action<AllowItemViewModel> remove)
    {
        Path = entry.Path;
        Name = entry.IsFolder ? System.IO.Path.GetFileName(entry.Path.TrimEnd('\\')) : System.IO.Path.GetFileNameWithoutExtension(entry.Path);
        if (string.IsNullOrEmpty(Name))
        {
            Name = entry.Path;
        }

        Detail = entry.IsFolder ? $"Folder · {entry.Path}" : entry.Path;
        RemoveCommand = new RelayCommand(() => remove(this));
    }

    public string Path { get; }

    public string Name { get; }

    public string Detail { get; }

    public IRelayCommand RemoveCommand { get; }

    public string RemoveAccessibleName => $"Remove {Name} from the allowed list";
}
