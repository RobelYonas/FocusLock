using System.ComponentModel;
using System.Diagnostics;
using FocusLock.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace FocusLock.Core.Platform;

public sealed class WindowsProcessTerminator(ILogger<WindowsProcessTerminator> logger) : IProcessTerminator
{
    private const int ErrorAccessDenied = 5;

    public TerminateResult Terminate(int processId)
    {
        Process process;
        try
        {
            process = Process.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            return TerminateResult.AlreadyExited;
        }

        using (process)
        {
            try
            {
                process.Kill(entireProcessTree: true);
                return TerminateResult.Terminated;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorAccessDenied)
            {
                return TerminateResult.AccessDenied;
            }
            catch (AggregateException ex) when (ex.InnerExceptions.All(inner => inner is Win32Exception { NativeErrorCode: ErrorAccessDenied }))
            {
                return TerminateResult.AccessDenied;
            }
            catch (InvalidOperationException)
            {
                return TerminateResult.AlreadyExited;
            }
            catch (Exception ex) when (ex is Win32Exception or AggregateException or NotSupportedException)
            {
                logger.LogWarning(ex, "Closing process {Pid} failed", processId);
                return TerminateResult.Failed;
            }
        }
    }
}
