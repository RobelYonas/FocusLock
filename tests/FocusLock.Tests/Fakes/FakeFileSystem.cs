using System.Text.RegularExpressions;
using FocusLock.Core.Abstractions;

namespace FocusLock.Tests.Fakes;

/// <summary>In-memory file system. Paths are compared case-insensitively, like NTFS.</summary>
public sealed class FakeFileSystem : IFileSystem
{
    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Reading any of these paths throws UnauthorizedAccessException.</summary>
    public HashSet<string> UnreadablePaths { get; } = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> Files => _files.Keys;

    public FakeFileSystem AddFile(string path, string contents = "")
    {
        var normalized = Normalize(path);
        _files[normalized] = contents;
        var parent = Parent(normalized);
        if (parent is not null)
        {
            AddDirectory(parent);
        }

        return this;
    }

    public FakeFileSystem AddDirectory(string path)
    {
        var current = Normalize(path);
        while (current is not null && _directories.Add(current))
        {
            current = Parent(current);
        }

        return this;
    }

    public string? GetContents(string path) => _files.TryGetValue(Normalize(path), out var contents) ? contents : null;

    public bool FileExists(string path) => _files.ContainsKey(Normalize(path));

    public bool DirectoryExists(string path) => _directories.Contains(Normalize(path));

    public string ReadAllText(string path)
    {
        var normalized = Normalize(path);
        if (UnreadablePaths.Contains(normalized))
        {
            throw new UnauthorizedAccessException($"Access to {path} is denied.");
        }

        return _files.TryGetValue(normalized, out var contents) ? contents : throw new FileNotFoundException("File not found.", path);
    }

    public void WriteAllText(string path, string contents) => AddFile(path, contents);

    public void AppendAllText(string path, string contents)
    {
        var normalized = Normalize(path);
        _files[normalized] = (_files.TryGetValue(normalized, out var existing) ? existing : string.Empty) + contents;
    }

    public void CreateDirectory(string path) => AddDirectory(path);

    public void DeleteFile(string path) => _files.Remove(Normalize(path));

    public void MoveFile(string sourcePath, string destinationPath)
    {
        var source = Normalize(sourcePath);
        if (!_files.Remove(source, out var contents))
        {
            throw new FileNotFoundException("File not found.", sourcePath);
        }

        AddFile(destinationPath, contents);
    }

    public IEnumerable<string> EnumerateFiles(string directory, string searchPattern)
    {
        var normalized = RequireDirectory(directory);
        var pattern = ToRegex(searchPattern);
        return _files.Keys
            .Where(file => string.Equals(Parent(file), normalized, StringComparison.OrdinalIgnoreCase) && pattern.IsMatch(Path.GetFileName(file)))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IEnumerable<string> EnumerateDirectories(string directory)
    {
        var normalized = RequireDirectory(directory);
        return _directories
            .Where(candidate => string.Equals(Parent(candidate), normalized, StringComparison.OrdinalIgnoreCase))
            .OrderBy(candidate => candidate, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string RequireDirectory(string directory)
    {
        var normalized = Normalize(directory);
        if (UnreadablePaths.Contains(normalized))
        {
            throw new UnauthorizedAccessException($"Access to {directory} is denied.");
        }

        return _directories.Contains(normalized) ? normalized : throw new DirectoryNotFoundException(directory);
    }

    private static string Normalize(string path) => Path.GetFullPath(path.Replace('/', '\\')).TrimEnd('\\');

    private static string? Parent(string normalizedPath)
    {
        var parent = Path.GetDirectoryName(normalizedPath);
        return string.IsNullOrEmpty(parent) ? null : parent.TrimEnd('\\');
    }

    private static Regex ToRegex(string searchPattern) =>
        new("^" + Regex.Escape(searchPattern).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase);
}
