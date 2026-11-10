using FocusLock.Core.Logging;
using FocusLock.Tests.Fakes;
using Microsoft.Extensions.Logging;

namespace FocusLock.Tests.Logging;

public class RollingFileLoggerTests
{
    private const string Directory = @"C:\logs";

    private readonly FakeFileSystem _fileSystem = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Writes_one_json_object_per_line_with_named_properties()
    {
        var logger = Provider().CreateLogger("Test.Category");

        logger.LogInformation("Closed {Game} (pid {Pid})", "Valheim", 42);
        logger.LogWarning(new IOException("disk"), "Saving failed");

        var contents = _fileSystem.GetContents($@"{Directory}\focuslock-2026-11-10.log");
        Assert.NotNull(contents);
        var lines = contents.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Contains("\"level\":\"Information\"", lines[0], StringComparison.Ordinal);
        Assert.Contains("\"category\":\"Test.Category\"", lines[0], StringComparison.Ordinal);
        Assert.Contains("\"message\":\"Closed Valheim (pid 42)\"", lines[0], StringComparison.Ordinal);
        Assert.Contains("\"props\":{\"Game\":\"Valheim\",\"Pid\":\"42\"}", lines[0], StringComparison.Ordinal);
        Assert.Contains("\"time\":\"2026-11-10T12:00:00.0000000+00:00\"", lines[0], StringComparison.Ordinal);
        Assert.Contains("\"exception\":\"System.IO.IOException: disk", lines[1], StringComparison.Ordinal);
        Assert.DoesNotContain("{OriginalFormat}", lines[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Respects_the_minimum_level()
    {
        var logger = Provider(options => options.MinimumLevel = LogLevel.Warning).CreateLogger("Test");

        logger.LogInformation("quiet");
        logger.LogWarning("loud");

        var contents = _fileSystem.GetContents($@"{Directory}\focuslock-2026-11-10.log");
        Assert.NotNull(contents);
        Assert.DoesNotContain("quiet", contents, StringComparison.Ordinal);
        Assert.Contains("loud", contents, StringComparison.Ordinal);
    }

    [Fact]
    public void Starts_a_new_file_each_day_and_keeps_only_the_retained_count()
    {
        for (var day = 1; day <= 9; day++)
        {
            _fileSystem.AddFile($@"{Directory}\focuslock-2026-11-0{day}.log", "old");
        }

        var logger = Provider(options => options.RetainedFiles = 7).CreateLogger("Test");

        logger.LogInformation("today");
        _clock.Advance(TimeSpan.FromDays(1));
        logger.LogInformation("tomorrow");

        var files = _fileSystem.EnumerateFiles(Directory, "focuslock-*.log").Select(Path.GetFileName).ToList();
        Assert.Equal(7, files.Count);
        Assert.Contains("focuslock-2026-11-11.log", files);
        Assert.Contains("focuslock-2026-11-10.log", files);
        Assert.Contains("focuslock-2026-11-05.log", files);
        Assert.DoesNotContain("focuslock-2026-11-04.log", files);
        Assert.Contains("tomorrow", _fileSystem.GetContents($@"{Directory}\focuslock-2026-11-11.log"), StringComparison.Ordinal);
    }

    [Fact]
    public void Uses_the_app_log_directory_by_default()
    {
        var provider = new RollingFileLoggerProvider(new RollingFileOptions(), _fileSystem, _clock, new FakeAppEnvironment { LogDirectory = @"D:\data\logs" });

        provider.CreateLogger("Test").LogInformation("hello");

        Assert.True(_fileSystem.FileExists(@"D:\data\logs\focuslock-2026-11-10.log"));
    }

    private RollingFileLoggerProvider Provider(Action<RollingFileOptions>? configure = null)
    {
        var options = new RollingFileOptions { Directory = Directory };
        configure?.Invoke(options);
        return new RollingFileLoggerProvider(options, _fileSystem, _clock, new FakeAppEnvironment());
    }
}
