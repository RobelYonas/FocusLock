using FocusLock.Core.Abstractions;
using Microsoft.Win32;

namespace FocusLock.Core.Platform;

public sealed class WindowsRegistry : IRegistry
{
    public string? GetString(RegistryRoot root, string subKey, string valueName)
    {
        using var key = Hive(root).OpenSubKey(subKey);
        return key?.GetValue(valueName) as string;
    }

    public void SetString(RegistryRoot root, string subKey, string valueName, string value)
    {
        using var key = Hive(root).CreateSubKey(subKey, writable: true);
        key.SetValue(valueName, value, RegistryValueKind.String);
    }

    public void DeleteValue(RegistryRoot root, string subKey, string valueName)
    {
        using var key = Hive(root).OpenSubKey(subKey, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }

    private static RegistryKey Hive(RegistryRoot root) => root switch
    {
        RegistryRoot.CurrentUser => Registry.CurrentUser,
        RegistryRoot.LocalMachine => Registry.LocalMachine,
        _ => throw new ArgumentOutOfRangeException(nameof(root), root, "Unknown registry root."),
    };
}
