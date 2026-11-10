using Microsoft.Extensions.Logging;

namespace FocusLock.Core.Logging;

public sealed class RollingFileOptions
{
    /// <summary>Log folder. Defaults to the app's log directory.</summary>
    public string? Directory { get; set; }

    public string FilePrefix { get; set; } = "focuslock";

    /// <summary>How many daily files to keep.</summary>
    public int RetainedFiles { get; set; } = 7;

    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;
}
