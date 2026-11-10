using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using FocusLock.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace FocusLock.Core.Settings;

/// <summary>Loads and saves one JSON file. An unreadable file is moved aside as .bak and defaults are used.</summary>
internal sealed class JsonFile(IFileSystem fileSystem, ILogger logger)
{
    public T Load<T>(string path, JsonTypeInfo<T> typeInfo, Func<T> createDefault)
        where T : class
    {
        if (!fileSystem.FileExists(path))
        {
            return createDefault();
        }

        try
        {
            var loaded = JsonSerializer.Deserialize(fileSystem.ReadAllText(path), typeInfo);
            if (loaded is not null)
            {
                return loaded;
            }

            logger.LogWarning("{Path} is empty; starting with defaults", path);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "{Path} could not be read; starting with defaults", path);
        }

        MoveAside(path);
        return createDefault();
    }

    public void Save<T>(string path, T value, JsonTypeInfo<T> typeInfo)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            fileSystem.CreateDirectory(directory);
        }

        var temporary = path + ".tmp";
        fileSystem.WriteAllText(temporary, JsonSerializer.Serialize(value, typeInfo));
        fileSystem.MoveFile(temporary, path);
    }

    private void MoveAside(string path)
    {
        var backup = path + ".bak";
        try
        {
            fileSystem.MoveFile(path, backup);
            logger.LogInformation("Moved the unreadable file to {Backup}", backup);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not move {Path} aside", path);
        }
    }
}
