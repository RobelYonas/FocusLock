using CommunityToolkit.Mvvm.ComponentModel;

namespace FocusLock.App.ViewModels;

/// <summary>One entry in the left navigation.</summary>
public sealed partial class NavItem(string label, string glyph, object viewModel) : ObservableObject
{
    public string Label { get; } = label;

    /// <summary>Segoe Fluent Icons glyph.</summary>
    public string Glyph { get; } = glyph;

    public object ViewModel { get; } = viewModel;

    [ObservableProperty]
    private bool _isSelected;
}
