namespace FocusLock.Core.Abstractions;

public interface IFileSystem
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    string ReadAllText(string path);

    void WriteAllText(string path, string contents);

    void AppendAllText(string path, string contents);

    void CreateDirectory(string path);

    void DeleteFile(string path);

    /// <summary>Moves a file, replacing the destination if it already exists.</summary>
    void MoveFile(string sourcePath, string destinationPath);

    IEnumerable<string> EnumerateFiles(string directory, string searchPattern);

    IEnumerable<string> EnumerateDirectories(string directory);
}
