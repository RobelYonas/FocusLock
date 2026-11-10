namespace FocusLock.Core.Abstractions;

/// <summary>Well-known folders and facts about the running process.</summary>
public interface IAppEnvironment
{
    /// <summary>Where settings, stats and logs live (%AppData%\FocusLock).</summary>
    string DataDirectory { get; }

    string LogDirectory { get; }

    string ProgramDataDirectory { get; }

    string ProgramFilesX86Directory { get; }

    string WindowsDirectory { get; }

    string ExecutablePath { get; }

    int ProcessId { get; }
}
