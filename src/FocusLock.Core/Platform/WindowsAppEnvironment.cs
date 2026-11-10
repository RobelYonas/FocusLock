using FocusLock.Core.Abstractions;

namespace FocusLock.Core.Platform;

public sealed class WindowsAppEnvironment : IAppEnvironment
{
    public string DataDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FocusLock");

    public string LogDirectory => Path.Combine(DataDirectory, "logs");

    public string ProgramDataDirectory { get; } = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

    public string ProgramFilesX86Directory { get; } = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

    public string WindowsDirectory { get; } = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    public string ExecutablePath { get; } = Environment.ProcessPath ?? string.Empty;

    public int ProcessId { get; } = Environment.ProcessId;
}
