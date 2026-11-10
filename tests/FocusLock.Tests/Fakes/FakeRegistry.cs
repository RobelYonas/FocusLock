using FocusLock.Core.Abstractions;

namespace FocusLock.Tests.Fakes;

public sealed class FakeRegistry : IRegistry
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public FakeRegistry Set(RegistryRoot root, string subKey, string valueName, string value)
    {
        _values[Key(root, subKey, valueName)] = value;
        return this;
    }

    public string? GetString(RegistryRoot root, string subKey, string valueName) =>
        _values.TryGetValue(Key(root, subKey, valueName), out var value) ? value : null;

    public void SetString(RegistryRoot root, string subKey, string valueName, string value) => Set(root, subKey, valueName, value);

    public void DeleteValue(RegistryRoot root, string subKey, string valueName) => _values.Remove(Key(root, subKey, valueName));

    private static string Key(RegistryRoot root, string subKey, string valueName) => $"{root}|{subKey.TrimEnd('\\')}|{valueName}";
}
