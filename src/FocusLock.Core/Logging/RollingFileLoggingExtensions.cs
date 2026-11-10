using FocusLock.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FocusLock.Core.Logging;

public static class RollingFileLoggingExtensions
{
    /// <summary>Logs to a daily file in the app's log folder. Requires the Core services to be registered.</summary>
    public static ILoggingBuilder AddRollingFile(this ILoggingBuilder builder, Action<RollingFileOptions>? configure = null)
    {
        var options = new RollingFileOptions();
        configure?.Invoke(options);
        builder.Services.AddSingleton<ILoggerProvider>(provider => new RollingFileLoggerProvider(
            options,
            provider.GetRequiredService<IFileSystem>(),
            provider.GetRequiredService<IClock>(),
            provider.GetRequiredService<IAppEnvironment>()));
        return builder;
    }
}
