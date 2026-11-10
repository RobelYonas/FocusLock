namespace FocusLock.Core.Detection.Steam;

/// <summary>One entry of Valve's KeyValues text format, used by Steam's .vdf and .acf files.</summary>
public sealed class KeyValuesNode
{
    public KeyValuesNode(string key)
    {
        Key = key;
    }

    public string Key { get; }

    /// <summary>The string value, or null when this entry is a block with children.</summary>
    public string? Value { get; init; }

    public List<KeyValuesNode> Children { get; } = [];

    public KeyValuesNode? Child(string key) =>
        Children.Find(child => string.Equals(child.Key, key, StringComparison.OrdinalIgnoreCase));

    public string? ValueOf(string key) => Child(key)?.Value;
}
