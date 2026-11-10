namespace FocusLock.Core.Common;

/// <summary>Plain-language time formatting shared by the UI, the tray tooltip and notifications.</summary>
public static class TimeText
{
    /// <summary>"25:00", or "1:05:00" from one hour up. Rounds up, so a timer starts at the full length and ends at 00:00.</summary>
    public static string Countdown(TimeSpan time)
    {
        var seconds = (long)Math.Ceiling(Math.Max(0, time.TotalSeconds));
        var hours = seconds / 3600;
        var minutes = seconds % 3600 / 60;
        var rest = seconds % 60;
        return hours > 0 ? $"{hours}:{minutes:00}:{rest:00}" : $"{minutes:00}:{rest:00}";
    }

    /// <summary>Short: "50 min", "1 h 30 min", "2 h".</summary>
    public static string Duration(TimeSpan time)
    {
        var totalMinutes = (int)Math.Round(Math.Max(0, time.TotalMinutes));
        if (totalMinutes < 1)
        {
            return "less than a minute";
        }

        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;
        if (hours == 0)
        {
            return $"{minutes} min";
        }

        return minutes == 0 ? $"{hours} h" : $"{hours} h {minutes} min";
    }

    /// <summary>Spelled out: "50 minutes", "1 hour 30 minutes", "45 seconds".</summary>
    public static string Spoken(TimeSpan time)
    {
        var totalSeconds = (long)Math.Round(Math.Max(0, time.TotalSeconds));
        if (totalSeconds < 60)
        {
            return Plural(totalSeconds, "second");
        }

        var hours = totalSeconds / 3600;
        var minutes = totalSeconds % 3600 / 60;
        if (hours == 0)
        {
            return Plural(minutes, "minute");
        }

        return minutes == 0 ? Plural(hours, "hour") : $"{Plural(hours, "hour")} {Plural(minutes, "minute")}";
    }

    private static string Plural(long count, string unit) => count == 1 ? $"1 {unit}" : $"{count} {unit}s";
}
