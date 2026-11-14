using System.Diagnostics.CodeAnalysis;

namespace FocusLock.Core.Websites;

/// <summary>Validates and normalizes domain names typed by the user or passed to the elevated helper. Contract stub: Task B implements it.</summary>
public static class DomainName
{
    /// <summary>
    /// Turns user input such as <c>https://www.Instagram.com/reels</c> into a bare lowercase domain
    /// without a leading <c>www.</c> or <c>m.</c> (<c>instagram.com</c>). False for IPs, <c>localhost</c>,
    /// single-label names, wildcards, and anything containing spaces or <c>#</c>.
    /// </summary>
    public static bool TryNormalize(string? input, [NotNullWhen(true)] out string? domain) =>
        throw new NotImplementedException();

    /// <summary>
    /// Strict check for a value that is about to be written to the hosts file: true only when
    /// <paramref name="value"/> is already a valid lowercase host name (no scheme, path, port or whitespace).
    /// </summary>
    public static bool IsValid(string? value) => throw new NotImplementedException();

    /// <summary>Each domain plus its <c>www.</c> and <c>m.</c> variants, without duplicates, in first-seen order.</summary>
    public static IReadOnlyList<string> WithVariants(IEnumerable<string> domains) => throw new NotImplementedException();
}
