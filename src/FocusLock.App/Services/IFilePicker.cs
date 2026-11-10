using Microsoft.Win32;

namespace FocusLock.App.Services;

public interface IFilePicker
{
    string? PickExecutable();

    string? PickFolder();
}

public sealed class WpfFilePicker : IFilePicker
{
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
