using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FocusLock.App.Services;
using FocusLock.Core.Detection;

namespace FocusLock.App.Views;

public partial class AppPickerWindow : Window
{
    private readonly IFilePicker _files;

    public AppPickerWindow(IReadOnlyList<AppChoice> choices, IFilePicker files)
    {
        InitializeComponent();
        _files = files;
        Apps.ItemsSource = choices;
        Apps.Items.Filter = item => item is AppChoice app
            && (app.Name.Contains(Search.Text.Trim(), StringComparison.CurrentCultureIgnoreCase)
                || app.Path.Contains(Search.Text.Trim(), StringComparison.OrdinalIgnoreCase));
        Loaded += (_, _) =>
        {
            ShowFirstMatch();
            Search.Focus();
        };
    }

    public AppChoice? Selected { get; private set; }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        Apps.Items.Refresh();
        ShowFirstMatch();
    }

    private void ShowFirstMatch()
    {
        if (Apps.SelectedItem is null && Apps.Items.Count > 0)
        {
            Apps.SelectedIndex = 0;
        }

        NoMatches.Visibility = Apps.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // Arrow keys move through the list while typing stays in the search box.
    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Down or Key.Up && Apps.Items.Count > 0)
        {
            Apps.SelectedIndex = Math.Clamp(Apps.SelectedIndex + (e.Key == Key.Down ? 1 : -1), 0, Apps.Items.Count - 1);
            Apps.ScrollIntoView(Apps.SelectedItem);
            e.Handled = true;
        }
    }

    // Only a double-click on a row adds it; double-clicks on the scrollbar must not.
    private void OnListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Apps.ContainerFromElement((DependencyObject)e.OriginalSource) is ListBoxItem { Content: AppChoice app })
        {
            Choose(app);
        }
    }

    private void OnAdd(object sender, RoutedEventArgs e)
    {
        if (Apps.SelectedItem is AppChoice app)
        {
            Choose(app);
        }
    }

    private void OnBrowseFile(object sender, RoutedEventArgs e)
    {
        if (_files.PickExecutable() is { } path)
        {
            Choose(new AppChoice(System.IO.Path.GetFileNameWithoutExtension(path), path, IsFolder: false, IsRunning: false));
        }
    }

    private void OnBrowseFolder(object sender, RoutedEventArgs e)
    {
        if (_files.PickFolder() is { } path)
        {
            Choose(new AppChoice(System.IO.Path.GetFileName(path), path, IsFolder: true, IsRunning: false));
        }
    }

    private void Choose(AppChoice app)
    {
        Selected = app;
        DialogResult = true;
    }
}
