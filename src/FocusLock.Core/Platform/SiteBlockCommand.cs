namespace FocusLock.Core.Platform;

/// <summary>A parsed <c>--site-block apply|clear</c> command line. <see cref="Domains"/> is empty for clear.</summary>
public sealed record SiteBlockCommand(bool IsApply, IReadOnlyList<string> Domains)
{
    public const string Switch = "--site-block";
    public const string ApplyVerb = "apply";
    public const string ClearVerb = "clear";
    public const int MaxDomains = 2000;

    /// <summary>True when the process was started as the elevated helper rather than the app.</summary>
    public static bool IsHelperInvocation(IReadOnlyList<string> args) =>
        args.Count > 0 && string.Equals(args[0], Switch, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Parses the full argument list (starting with <see cref="Switch"/>). Returns null when anything is wrong:
    /// unknown verb, extra arguments for clear, no or too many domains, or any domain failing <paramref name="isValidDomain"/>.
    /// </summary>
    public static SiteBlockCommand? TryParse(IReadOnlyList<string> args, Func<string, bool> isValidDomain)
    {
        if (!IsHelperInvocation(args) || args.Count < 2)
        {
            return null;
        }

        var rest = args.Skip(2).ToList();
        switch (args[1])
        {
            case ClearVerb:
                return rest.Count == 0 ? new SiteBlockCommand(false, []) : null;
            case ApplyVerb:
                if (rest.Count is 0 or > MaxDomains || !rest.All(isValidDomain))
                {
                    return null;
                }

                return new SiteBlockCommand(true, rest.Distinct(StringComparer.Ordinal).ToList());
            default:
                return null;
        }
    }

    /// <summary>The command line for this command, each argument quoted by the Windows rules so it survives as one argument.</summary>
    public string ToArguments()
    {
        IEnumerable<string> args = [Switch, IsApply ? ApplyVerb : ClearVerb, .. Domains];
        return string.Join(' ', args.Select(Quote));
    }

    internal static string Quote(string arg)
    {
        if (arg.Length > 0 && !arg.Any(c => char.IsWhiteSpace(c) || c == '"'))
        {
            return arg;
        }

        var builder = new System.Text.StringBuilder("\"");
        var backslashes = 0;
        foreach (var c in arg)
        {
            if (c == '\\')
            {
                backslashes++;
                continue;
            }

            // Backslashes are literal unless they precede a quote; then each one and the quote need escaping.
            builder.Append('\\', c == '"' ? (backslashes * 2) + 1 : backslashes).Append(c);
            backslashes = 0;
        }

        return builder.Append('\\', backslashes * 2).Append('"').ToString();
    }
}
