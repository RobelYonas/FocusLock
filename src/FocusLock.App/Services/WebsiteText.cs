namespace FocusLock.App.Services;

/// <summary>Plain wording for website blocking, kept free of UI so it can move to Core and be tested.</summary>
public static class WebsiteText
{
    /// <summary>"Chrome", "Chrome and Edge", "Chrome, Edge and Firefox".</summary>
    public static string JoinNames(IReadOnlyList<string> names) => names.Count switch
    {
        0 => "your browser",
        1 => names[0],
        _ => $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}",
    };

    /// <summary>"12 games and 3 websites will be blocked"; empty when both counts are zero.</summary>
    public static string BlockedSummary(int games, int websites)
    {
        var parts = new List<string>();
        if (games > 0)
        {
            parts.Add(games == 1 ? "1 game" : $"{games} games");
        }

        if (websites > 0)
        {
            parts.Add(websites == 1 ? "1 website" : $"{websites} websites");
        }

        return parts.Count == 0 ? string.Empty : $"{string.Join(" and ", parts)} will be blocked";
    }
}
