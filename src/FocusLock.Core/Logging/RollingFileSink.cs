using FocusLock.Core.Abstractions;

namespace FocusLock.Core.Logging;

/// <summary>Appends lines to one file per day and deletes the oldest files beyond the retention count.</summary>
internal sealed class RollingFileSink(IFileSystem fileSystem, IClock clock, string directory, string filePrefix, int retainedFiles)
{
    private readonly object _gate = new();
    private DateOnly? _currentDay;
    private string? _currentPath;

    public void Write(string line)
    {
        lock (_gate)
        {
            var today = DateOnly.FromDateTime(clock.UtcNow.LocalDateTime);
            var isNewDay = _currentDay != today || _currentPath is null;
            if (isNewDay)
            {
                _currentDay = today;
                _currentPath = Path.Combine(directory, $"{filePrefix}-{today:yyyy-MM-dd}.log");
            }

            var path = _currentPath!;
            try
            {
                if (isNewDay)
                {
                    fileSystem.CreateDirectory(directory);
                }

                fileSystem.AppendAllText(path, line + Environment.NewLine);

                if (isNewDay)
                {
                    Prune();
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Logging must never take the app down: a full disk or a locked file is not worth crashing for.
            }
        }
    }

    private void Prune()
    {
        var stale = fileSystem.EnumerateFiles(directory, $"{filePrefix}-*.log")
            .OrderByDescending(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .Skip(Math.Max(1, retainedFiles))
            .ToList();

        foreach (var path in stale)
        {
            try
            {
                fileSystem.DeleteFile(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Try again tomorrow.
            }
        }
    }
}
