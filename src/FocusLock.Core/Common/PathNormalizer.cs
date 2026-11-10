namespace FocusLock.Core.Common;

/// <summary>Path helpers shared by detection and blocking. Comparisons are case-insensitive, like NTFS.</summary>
public static class PathNormalizer
{
    private const char Separator = '\\';

    /// <summary>Returns an absolute path with backslashes and no trailing separator, or null when the input is not a usable path.</summary>
    public static string? TryNormalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var full = Path.GetFullPath(path.Trim().Replace('/', Separator));
            var trimmed = full.TrimEnd(Separator);
            return trimmed.Length == 0 ? null : trimmed;
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>True when <paramref name="path"/> is <paramref name="directory"/> itself or lies underneath it.</summary>
    public static bool IsUnder(string path, string directory)
    {
        var trimmedDirectory = directory.TrimEnd(Separator);
        if (trimmedDirectory.Length == 0 || !path.StartsWith(trimmedDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return path.Length == trimmedDirectory.Length || path[trimmedDirectory.Length] == Separator;
    }

    public static bool PathEquals(string first, string second) =>
        string.Equals(first.TrimEnd(Separator), second.TrimEnd(Separator), StringComparison.OrdinalIgnoreCase);

    public static bool NameEquals(string first, string second) =>
        string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
}
