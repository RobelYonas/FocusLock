namespace FocusLock.Core.Sessions;

/// <summary>
/// The strict-mode rule for ending a session early: wait out a short delay, or type the sentence.
/// Pure logic, so a future Windows Service can enforce exactly the same rule.
/// </summary>
public sealed class StrictModeGate
{
    public static readonly TimeSpan WaitTime = TimeSpan.FromSeconds(60);

    public const string Sentence = "I choose to end this session early.";

    private DateTimeOffset? _openedAt;

    public bool IsOpen => _openedAt is not null;

    /// <summary>Starts the waiting period. Opening an already open gate does not restart the clock.</summary>
    public void Open(DateTimeOffset now) => _openedAt ??= now;

    public void Close() => _openedAt = null;

    public TimeSpan RemainingWait(DateTimeOffset now)
    {
        if (_openedAt is not { } openedAt)
        {
            return WaitTime;
        }

        var remaining = WaitTime - (now - openedAt);
        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }

    /// <summary>Case does not matter and the trailing period is optional; anything else must match.</summary>
    public static bool SentenceMatches(string? typed)
    {
        if (string.IsNullOrWhiteSpace(typed))
        {
            return false;
        }

        return string.Equals(Normalize(typed), Normalize(Sentence), StringComparison.OrdinalIgnoreCase);

        static string Normalize(string text) => text.Trim().TrimEnd('.');
    }

    public bool IsSatisfied(DateTimeOffset now, string? typed) =>
        IsOpen && (RemainingWait(now) == TimeSpan.Zero || SentenceMatches(typed));
}
