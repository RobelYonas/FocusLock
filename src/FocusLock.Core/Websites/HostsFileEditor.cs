namespace FocusLock.Core.Websites;

/// <summary>Pure text edits of the hosts file. Only ever touches lines between FocusLock's markers.</summary>
public static class HostsFileEditor
{
    public const string BeginMarker = "# FocusLock begin";
    public const string EndMarker = "# FocusLock end";

    private sealed class Line(string text, string eol)
    {
        public string Text { get; } = text;
        public string Eol { get; set; } = eol;
    }

    /// <summary>
    /// Returns <paramref name="hostsText"/> with exactly one FocusLock section holding a <c>0.0.0.0 domain</c> line
    /// per domain. Throws <see cref="ArgumentException"/> when any domain fails <see cref="DomainName.IsValid"/>.
    /// </summary>
    public static string Apply(string hostsText, IReadOnlyList<string> domains)
    {
        foreach (var d in domains)
            if (!DomainName.IsValid(d))
                throw new ArgumentException($"'{d}' is not a valid domain name.", nameof(domains));
        return Rewrite(hostsText, domains.Distinct(StringComparer.Ordinal).ToList());
    }

    /// <summary>
    /// Returns <paramref name="hostsText"/> without any FocusLock section; every other line is kept exactly.
    /// Only exception: when Apply had to add a final newline (the file had none) and the section is the last
    /// thing in the file, that newline is taken off again, so Remove(Apply(x)) == x.
    /// </summary>
    public static string Remove(string hostsText) => Rewrite(hostsText, null);

    /// <summary>True when the text contains a FocusLock begin marker.</summary>
    public static bool ContainsBlock(string hostsText) => Split(hostsText).Any(l => IsMarker(l, BeginMarker));

    private static bool IsMarker(Line l, string marker) => l.Text.Trim().Equals(marker, StringComparison.OrdinalIgnoreCase);

    // Exactly what Apply writes; anything else is the user's.
    private static bool IsOwnLine(Line l) =>
        l.Text.StartsWith("0.0.0.0 ", StringComparison.Ordinal) && DomainName.IsValid(l.Text[8..]);

    private static List<Line> Split(string text)
    {
        var lines = new List<Line>();
        var start = 0;
        while (start < text.Length)
        {
            var nl = text.IndexOf('\n', start);
            if (nl < 0) { lines.Add(new Line(text[start..], "")); break; }
            var crlf = nl > start && text[nl - 1] == '\r';
            lines.Add(new Line(text[start..(crlf ? nl - 1 : nl)], crlf ? "\r\n" : "\n"));
            start = nl + 1;
        }
        return lines;
    }

    private static string Rewrite(string text, List<string>? domains)
    {
        var lines = Split(text);
        var crlf = lines.Count(l => l.Eol == "\r\n");
        var eol = crlf >= lines.Count(l => l.Eol == "\n") ? "\r\n" : "\n";
        var endsWithoutNewline = lines.Count > 0 && lines[^1].Eol == "";

        var output = new List<Line>();
        var insertAt = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (IsMarker(line, EndMarker)) continue; // stray end marker: drop only this line
            if (!IsMarker(line, BeginMarker)) { output.Add(line); continue; }

            // A complete section runs to the next end marker, unless another begin marker comes first.
            // A broken section (no end marker) drops only the begin line and the contiguous lines that are
            // exactly "0.0.0.0 <valid domain>"; cutting to end of file could delete the user's own entries.
            var j = i + 1;
            while (j < lines.Count && !IsMarker(lines[j], EndMarker) && !IsMarker(lines[j], BeginMarker)) j++;
            if (j < lines.Count && IsMarker(lines[j], EndMarker)) i = j;
            else
            {
                while (i + 1 < lines.Count && IsOwnLine(lines[i + 1])) i++;
            }
            if (insertAt < 0) insertAt = output.Count;
        }

        if (domains is not null)
        {
            var block = new List<Line> { new(BeginMarker, eol) };
            block.AddRange(domains!.Select(d => new Line("0.0.0.0 " + d, eol)));
            block.Add(new Line(EndMarker, eol));
            if (insertAt < 0)
            {
                insertAt = output.Count;
                if (insertAt > 0 && output[^1].Eol == "") output[^1].Eol = eol;
            }
            output.InsertRange(insertAt, block);
        }

        if (endsWithoutNewline && output.Count > 0) output[^1].Eol = "";
        return string.Concat(output.Select(l => l.Text + l.Eol));
    }
}