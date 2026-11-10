using FocusLock.Core.Abstractions;

namespace FocusLock.Core.Blocking;

public sealed class LaunchBlockedEventArgs(string gameName, ProcessInfo process, TerminateResult result) : EventArgs
{
    public string GameName { get; } = gameName;

    public ProcessInfo Process { get; } = process;

    public TerminateResult Result { get; } = result;
}
