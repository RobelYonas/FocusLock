namespace FocusLock.Core.Websites;

/// <summary>Pure text edits of the hosts file. Only ever touches lines between FocusLock's markers. Contract stub: Task B implements it.</summary>
public static class HostsFileEditor
{
    public const string BeginMarker = "# FocusLock begin";
    public const string EndMarker = "# FocusLock end";

    /// <summary>
    /// Returns <paramref name="hostsText"/> with exactly one FocusLock section holding a <c>0.0.0.0 domain</c> line
    /// per domain. Throws <see cref="ArgumentException"/> when any domain fails <see cref="DomainName.IsValid"/>.
    /// </summary>
    public static string Apply(string hostsText, IReadOnlyList<string> domains) => throw new NotImplementedException();

    /// <summary>Returns <paramref name="hostsText"/> without any FocusLock section; every other line is kept exactly.</summary>
    public static string Remove(string hostsText) => throw new NotImplementedException();

    /// <summary>True when the text contains a FocusLock begin marker.</summary>
    public static bool ContainsBlock(string hostsText) => throw new NotImplementedException();
}
