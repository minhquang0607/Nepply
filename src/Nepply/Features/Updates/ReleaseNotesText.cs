using System.Text;
using System.Text.RegularExpressions;

namespace Nepply.Modules.Updates;

/// <summary>
/// Turns GitHub release Markdown into readable plain text for the update window.
/// Only Markdown punctuation is removed; HTML and URLs are never interpreted or opened.
/// </summary>
internal static class ReleaseNotesText
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(1);
    private static readonly Regex Heading = new(@"^\s{0,3}#{1,6}\s+(.*?)\s*#*\s*$", RegexOptions.CultureInvariant, Limit);
    private static readonly Regex Bullet = new(@"^(\s*)[-*+]\s+", RegexOptions.CultureInvariant, Limit);
    private static readonly Regex Rule = new(@"^\s{0,3}([-*_])(\s*\1){2,}\s*$", RegexOptions.CultureInvariant, Limit);
    private static readonly Regex TableSeparator = new(@"^\s*\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)*\|?\s*$", RegexOptions.CultureInvariant, Limit);
    private static readonly Regex Image = new(@"!\[([^\]]*)\]\([^)]*\)", RegexOptions.CultureInvariant, Limit);
    private static readonly Regex Link = new(@"\[([^\]]+)\]\([^)]*\)", RegexOptions.CultureInvariant, Limit);
    private static readonly Regex Strong = new(@"(\*\*|__)(.+?)\1", RegexOptions.CultureInvariant, Limit);
    private static readonly Regex Emphasis = new(@"(?<![\w*])\*(?!\s)(.+?)(?<!\s)\*(?![\w*])", RegexOptions.CultureInvariant, Limit);
    private static readonly Regex Code = new(@"`([^`]+)`", RegexOptions.CultureInvariant, Limit);
    private static readonly Regex Comment = new(@"<!--.*?-->", RegexOptions.CultureInvariant | RegexOptions.Singleline, Limit);

    internal static string ToPlain(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return "";
        // Pathological input must not cost the user the notes: show them unformatted.
        try { return Convert(markdown); }
        catch (RegexMatchTimeoutException) { return markdown.Trim(); }
    }

    private static string Convert(string markdown)
    {
        string source = Comment.Replace(markdown.Replace("\r\n", "\n").Replace('\r', '\n'), "");
        var output = new StringBuilder(source.Length);
        int blank = 0;
        foreach (string raw in source.Split('\n'))
        {
            string? line = Line(raw.TrimEnd());
            if (line == null) continue; // Markup-only line: no text and no spacing.
            if (line.Length == 0)
            {
                // Collapse runs of empty lines; never start with one.
                if (output.Length > 0 && blank++ == 0) output.Append('\n');
                continue;
            }
            blank = 0;
            output.Append(line).Append('\n');
        }
        return output.ToString().TrimEnd();
    }

    private static string? Line(string line)
    {
        if (line.TrimStart().StartsWith("```", StringComparison.Ordinal) || Rule.IsMatch(line) || TableSeparator.IsMatch(line)) return null;
        while (line.TrimStart().StartsWith('>')) line = line.TrimStart()[1..].TrimStart();
        if (Heading.Match(line) is { Success: true } heading) line = heading.Groups[1].Value;
        else if (line.TrimStart().StartsWith('|'))
            line = string.Join("  ·  ", line.Trim().Trim('|').Split('|').Select(cell => cell.Trim()).Where(cell => cell.Length > 0));
        else line = Bullet.Replace(line, match => match.Groups[1].Value + "• ");
        line = Image.Replace(line, "$1");
        line = Link.Replace(line, "$1");
        line = Strong.Replace(line, "$2");
        line = Emphasis.Replace(line, "$1");
        return Code.Replace(line, "$1");
    }
}
