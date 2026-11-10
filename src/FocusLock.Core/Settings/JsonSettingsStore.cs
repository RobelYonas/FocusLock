using FocusLock.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace FocusLock.Core.Settings;

public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly JsonFile _file;

    public JsonSettingsStore(IFileSystem fileSystem, IAppEnvironment environment, ILogger<JsonSettingsStore> logger)
    {
        _file = new JsonFile(fileSystem, logger);
        FilePath = System.IO.Path.Combine(environment.DataDirectory, "settings.json");
    }

    public string FilePath { get; }

    public AppSettings Load() => _file.Load(FilePath, FocusLockJsonContext.Default.AppSettings, () => new AppSettings());

    public void Save(AppSettings settings) => _file.Save(FilePath, settings, FocusLockJsonContext.Default.AppSettings);
}
