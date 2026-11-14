using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FocusLock.Core.Websites;

/// <summary>Validates and normalizes domain names typed by the user or passed to the elevated helper.</summary>
public static class DomainName
{
    /// <summary>
    /// Turns user input such as <c>https://www.Instagram.com/reels</c> into a bare lowercase domain
    /// without a leading <c>www.</c> or <c>m.</c> (<c>instagram.com</c>). False for IPs, <c>localhost</c>,
    /// single-label names, wildcards, and anything containing spaces or <c>#</c>.
    /// Unicode names are converted to punycode (<c>b&#252;cher.de</c> becomes <c>xn--bcher-kva.de</c>); names IDN
    /// mapping rejects are refused. A leading <c>www.</c>/<c>m.</c> is kept when stripping would leave one label
    /// (<c>www.com</c> stays <c>www.com</c>).
    /// </summary>
    public static bool TryNormalize(string? input, [NotNullWhen(true)] out string? domain)
    {
        domain = null;
        var s = input?.Trim();
        if (string.IsNullOrEmpty(s) || s.Any(c => char.IsWhiteSpace(c) || c is '#' or '*' or '@' or '\\'))
            return false;

        var scheme = s.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0) s = s[(scheme + 3)..];
        var end = s.IndexOfAny(['/', '?']);
        if (end >= 0) s = s[..end];

        if (s.StartsWith('[')) return false; // bracketed IPv6
        var colon = s.IndexOf(':');
        if (colon >= 0)
        {
            var port = s[(colon + 1)..];
            if (port.Length == 0 || !port.All(char.IsAsciiDigit)) return false; // also rejects bare IPv6
            s = s[..colon];
        }

        if (s.EndsWith('.')) s = s[..^1];
        s = s.ToLowerInvariant();
        if (s.Any(c => c > 127))
        {
            try { s = new IdnMapping().GetAscii(s); }
            catch (ArgumentException) { return false; }
            s = s.ToLowerInvariant();
        }

        foreach (var prefix in new[] { "www.", "m." })
        {
            if (s.StartsWith(prefix, StringComparison.Ordinal) && s.IndexOf('.', prefix.Length) > 0)
            {
                s = s[prefix.Length..];
                break;
            }
        }

        if (!IsValid(s)) return false;
        domain = s;
        return true;
    }

    /// <summary>
    /// Strict check for a value that is about to be written to the hosts file: true only when
    /// <paramref name="value"/> is already a valid lowercase host name (no scheme, path, port or whitespace).
    /// </summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 253) return false;
        var labels = value.Split('.');
        if (labels.Length < 2) return false;
        foreach (var l in labels)
        {
            if (l.Length is 0 or > 63 || l[0] == '-' || l[^1] == '-') return false;
            if (!l.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-')) return false;
        }
        if (labels[^1].All(char.IsAsciiDigit)) return false; // numeric TLD, covers IPv4
        return labels[^1] != "localhost";
    }

    /// <summary>Each domain plus its <c>www.</c> and <c>m.</c> variants, without duplicates, in first-seen order.</summary>
    public static IReadOnlyList<string> WithVariants(IEnumerable<string> domains)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var d in domains)
        {
            var hasPrefix = d.StartsWith("www.", StringComparison.OrdinalIgnoreCase) || d.StartsWith("m.", StringComparison.OrdinalIgnoreCase);
            foreach (var v in hasPrefix ? [d] : new[] { d, "www." + d, "m." + d })
                if (seen.Add(v)) result.Add(v);
        }
        return result;
    }
}