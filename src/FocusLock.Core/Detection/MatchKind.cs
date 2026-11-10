namespace FocusLock.Core.Detection;

/// <summary>How a rule target is compared against a running process.</summary>
public enum MatchKind
{
    /// <summary>Any executable inside the folder or its subfolders.</summary>
    Folder,

    /// <summary>One executable, matched by full path.</summary>
    Executable,

    /// <summary>Matched by executable file name only, e.g. "steam.exe".</summary>
    ProcessName,
}
