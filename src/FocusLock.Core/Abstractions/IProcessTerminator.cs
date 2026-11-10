namespace FocusLock.Core.Abstractions;

public interface IProcessTerminator
{
    TerminateResult Terminate(int processId);
}
