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
        var host = input?.Trim();
        if (string.IsNullOrEmpty(host))
        {
            return false;
        }

        if (host.Any(character => char.IsWhiteSpace(character) || character is '#' or '*' or '@' or '\\'))
        {
            return false;
        }

        var schemeIndex = host.IndexOf("://", StringComparison.Ordinal);
        if (schemeIndex >= 0)
        {
            host = host[(schemeIndex + 3)..];
        }

        var pathIndex = host.IndexOfAny(['/', '?']);
        if (pathIndex >= 0)
        {
            host = host[..pathIndex];
        }

        if (host.StartsWith('['))
        {
            return false; // bracketed IPv6
        }

        var colonIndex = host.IndexOf(':');
        if (colonIndex >= 0)
        {
            var port = host[(colonIndex + 1)..];
            if (port.Length == 0 || !port.All(char.IsAsciiDigit))
            {
                return false; // also rejects bare IPv6
            }

            host = host[..colonIndex];
        }

        if (host.EndsWith('.'))
        {
            host = host[..^1];
        }

        host = host.ToLowerInvariant();
        if (host.Any(character => character > 127))
        {
            try
            {
                host = new IdnMapping().GetAscii(host);
            }
            catch (ArgumentException)
            {
                return false;
            }

            host = host.ToLowerInvariant();
        }

        foreach (var prefix in new[] { "www.", "m." })
        {
            if (host.StartsWith(prefix, StringComparison.Ordinal) && host.IndexOf('.', prefix.Length) > 0)
            {
                host = host[prefix.Length..];
                break;
            }
        }

        if (!IsValid(host))
        {
            return false;
        }

        domain = host;
        return true;
    }

    /// <summary>
    /// Strict check for a value that is about to be written to the hosts file: true only when
    /// <paramref name="value"/> is already a valid lowercase host name (no scheme, path, port or whitespace).
    /// </summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 253)
        {
            return false;
        }

        var labels = value.Split('.');
        if (labels.Length < 2)
        {
            return false;
        }

        foreach (var label in labels)
        {
            if (label.Length is 0 or > 63 || label[0] == '-' || label[^1] == '-')
            {
                return false;
            }

            if (!label.All(character => character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'))
            {
                return false;
            }
        }

        if (labels[^1].All(char.IsAsciiDigit))
        {
            return false; // numeric TLD, covers IPv4
        }

        return labels[^1] != "localhost";
    }

    /// <summary>Each domain plus its <c>www.</c> and <c>m.</c> variants, without duplicates, in first-seen order.</summary>
    public static IReadOnlyList<string> WithVariants(IEnumerable<string> domains)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var domain in domains)
        {
            var hasPrefix = domain.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
                || domain.StartsWith("m.", StringComparison.OrdinalIgnoreCase);
            foreach (var variant in hasPrefix ? [domain] : new[] { domain, "www." + domain, "m." + domain })
            {
                if (seen.Add(variant))
                {
                    result.Add(variant);
                }
            }
        }

        return result;
    }
}