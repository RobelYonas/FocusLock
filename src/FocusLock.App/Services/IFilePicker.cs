using System.Windows;
using FocusLock.App.Views;
using FocusLock.Core.Detection;
using Microsoft.Win32;

namespace FocusLock.App.Services;

public interface IFilePicker
{
    string? PickExecutable();

    string? PickFolder();

    /// <summary>Shows a searchable list of apps, with "browse" as a fallback. Null when the person cancels.</summary>
    AppChoice? PickApp(IReadOnlyList<AppChoice> choices);
}

public sealed class WpfFilePicker : IFilePicker
{
    public AppChoice? PickApp(IReadOnlyList<AppChoice> choices)
    {
        var window = new AppPickerWindow(choices, this) { Owner = Application.Current.MainWindow };
        return window.ShowDialog() == true ? window.Selected : null;
    }

    public string? PickExecutable()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose a game or app",
            Filter = "Programs (*.exe)|*.exe",
            CheckFileExists = true,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Choose a game folder" };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
