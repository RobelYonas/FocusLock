using FocusLock.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace FocusLock.Core.Logging;

[ProviderAlias("RollingFile")]
public sealed class RollingFileLoggerProvider : ILoggerProvider
{
    private readonly RollingFileSink _sink;
    private readonly LogLevel _minimumLevel;
    private readonly IClock _clock;

    public RollingFileLoggerProvider(RollingFileOptions options, IFileSystem fileSystem, IClock clock, IAppEnvironment environment)
    {
        _clock = clock;
        _minimumLevel = options.MinimumLevel;
        _sink = new RollingFileSink(fileSystem, clock, options.Directory ?? environment.LogDirectory, options.FilePrefix, options.RetainedFiles);
    }

    public ILogger CreateLogger(string categoryName) => new RollingFileLogger(categoryName, _sink, _minimumLevel, _clock);

    public void Dispose()
    {
    }
}
