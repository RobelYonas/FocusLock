using FocusLock.Core.Abstractions;

namespace FocusLock.Core.Platform;

public sealed class PhysicalFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public string ReadAllText(string path) => File.ReadAllText(path);

    public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);

    public void AppendAllText(string path, string contents) => File.AppendAllText(path, contents);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public void DeleteFile(string path) => File.Delete(path);

    public void MoveFile(string sourcePath, string destinationPath) => File.Move(sourcePath, destinationPath, overwrite: true);

    public IEnumerable<string> EnumerateFiles(string directory, string searchPattern) => Directory.EnumerateFiles(directory, searchPattern);

    public IEnumerable<string> EnumerateDirectories(string directory) => Directory.EnumerateDirectories(directory);
}
