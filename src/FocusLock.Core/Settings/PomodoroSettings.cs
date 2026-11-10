namespace FocusLock.Core.Settings;

public sealed class PomodoroSettings
{
    public bool Enabled { get; set; }

    public int BreakMinutes { get; set; } = 5;

    public int Cycles { get; set; } = 4;
}
