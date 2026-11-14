using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace FocusLock.Core.Platform;

internal static class NativeMethods
{
    private const uint ProcessQueryLimitedInformation = 0x1000;

    /// <summary>Reads a process's image path with the least privilege, which also works for elevated processes of the same user.</summary>
    public static string? TryGetProcessImagePath(int processId)
    {
        using var handle = OpenProcess(ProcessQueryLimitedInformation, false, (uint)processId);
        if (handle.IsInvalid)
        {
            return null;
        }

        var buffer = new char[1024];
        var size = (uint)buffer.Length;
        return QueryFullProcessImageNameW(handle, 0, buffer, ref size) ? new string(buffer, 0, (int)size) : null;
    }

    /// <summary>Empties the DNS client cache, like <c>ipconfig /flushdns</c>. Needs elevation.</summary>
    public static bool FlushDnsCache() => DnsFlushResolverCache();

    [DllImport("dnsapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DnsFlushResolverCache();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageNameW(SafeProcessHandle process, uint flags, [Out] char[] exeName, ref uint size);
}
