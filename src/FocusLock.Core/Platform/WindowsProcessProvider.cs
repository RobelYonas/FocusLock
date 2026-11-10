using System.Diagnostics;
using FocusLock.Core.Abstractions;

namespace FocusLock.Core.Platform;

public sealed class WindowsProcessProvider : IProcessProvider
{
    public IReadOnlyList<ProcessInfo> ListProcesses()
    {
        var processes = Process.GetProcesses();
        var list = new List<ProcessInfo>(processes.Length);
        foreach (var process in processes)
        {
            try
            {
                list.Add(new ProcessInfo(process.Id, WithExtension(process.ProcessName), null));
            }
            catch (InvalidOperationException)
            {
                // The process exited between listing and reading; nothing to report.
            }
            finally
            {
                process.Dispose();
            }
        }

        return list;
    }

    public string? TryGetExecutablePath(int processId) => NativeMethods.TryGetProcessImagePath(processId);

    private static string WithExtension(string processName) =>
        processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? processName : processName + ".exe";
}
