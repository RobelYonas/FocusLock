using CommunityToolkit.Mvvm.ComponentModel;

namespace FocusLock.App.ViewModels;

/// <summary>A preset session length, or the custom entry when <see cref="Minutes"/> is null.</summary>
public sealed partial class LengthOption(int? minutes, string label, string accessibleName, Action<LengthOption> selected) : ObservableObject
{
    public int? Minutes { get; } = minutes;

    public string Label { get; } = label;

    public string AccessibleName { get; } = accessibleName;

    [ObservableProperty]
    private bool _isSelected;

    partial void OnIsSelectedChanged(bool value)
    {
        if (value)
        {
            selected(this);
        }
    }
}
