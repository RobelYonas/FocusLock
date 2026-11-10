namespace FocusLock.Core.Abstractions;

public enum RegistryRoot
{
    CurrentUser,
    LocalMachine,
}

/// <summary>The small slice of the Windows registry FocusLock needs: launcher locations and the run-at-sign-in entry.</summary>
public interface IRegistry
{
    string? GetString(RegistryRoot root, string subKey, string valueName);

    void SetString(RegistryRoot root, string subKey, string valueName, string value);

    void DeleteValue(RegistryRoot root, string subKey, string valueName);
}
