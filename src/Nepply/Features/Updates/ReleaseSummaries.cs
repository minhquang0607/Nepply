using System.Text;
using System.Text.Json;

namespace Nepply.Modules.Updates;

/// <summary>
/// Where one newer release keeps its short "what's new" file. <see cref="Asset"/> is null
/// when that release published no such file, for example releases made before the format.
/// </summary>
internal sealed record SummarySource(string Tag, ReleaseVersion Version, Uri? Asset, long Bytes, string? Sha256);

/// <summary>A newer release and its summary by language, or null when it could not be read.</summary>
internal sealed record ReleaseSummary(string Tag, ReleaseVersion Version, IReadOnlyDictionary<string, string[]>? Notes);

/// <summary>
/// The release-notes.json asset: a few plain-text lines per language, nothing else.
/// <code>{ "version": "2.0.0-rc.3", "notes": { "vi": ["…"], "en": ["…"] } }</code>
/// Text is shown literally; Markdown, HTML and links are never interpreted.
/// </summary>
internal static class ReleaseSummaryFile
{
    internal const string AssetName = "release-notes.json";
    internal const long MaximumBytes = 64 * 1024;
    internal const int MaximumItems = 20;
    internal const int MaximumItemCharacters = 400;
    /// <summary>Newer releases whose summaries are fetched; older ones in a long gap are skipped.</summary>
    internal const int MaximumReleases = 10;

    /// <summary>Languages Nepply cannot show are ignored, so a newer file may add languages.</summary>
    internal static IReadOnlyDictionary<string, string[]> Parse(byte[] json, ReleaseVersion expected)
    {
        if (json.Length is 0 or > (int)MaximumBytes) throw new FormatException("Release summary size is out of range.");
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 4 });
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.String
            || ReleaseVersion.Parse(version.GetString()!) != expected)
            throw new FormatException("Release summary belongs to another version.");
        if (!root.TryGetProperty("notes", out var notes) || notes.ValueKind != JsonValueKind.Object)
            throw new FormatException("Release summary has no notes.");
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var language in notes.EnumerateObject())
        {
            if (!Services.LocalizationService.IsSupported(language.Name)) continue;
            if (language.Value.ValueKind != JsonValueKind.Array) throw new FormatException("Release summary language is not a list.");
            var items = language.Value.EnumerateArray().ToArray();
            if (items.Length is 0 or > MaximumItems) throw new FormatException("Release summary item count is out of range.");
            result[language.Name] = items.Select(Item).ToArray();
        }
        return result;
    }

    private static string Item(JsonElement item)
    {
        string text = item.ValueKind == JsonValueKind.String ? item.GetString()!.Trim() : throw new FormatException("Release summary item is not text.");
        if (text.Length is 0 or > MaximumItemCharacters || text.Any(char.IsControl)) throw new FormatException("Release summary item is invalid.");
        return text;
    }

    /// <summary>
    /// The text for the update window, newest release first, in <paramref name="language"/>
    /// with English as fallback. Null when no newer release has a readable summary, so the
    /// caller can fall back to the release page's own notes.
    /// </summary>
    internal static string? Build(IReadOnlyList<ReleaseSummary> releases, string language, string missing)
    {
        if (!releases.Any(release => Pick(release, language) != null)) return null;
        bool headings = releases.Count > 1;
        var text = new StringBuilder();
        foreach (var release in releases.OrderByDescending(release => release.Version))
        {
            if (text.Length > 0) text.Append('\n');
            if (headings) text.Append(release.Tag.TrimStart('v')).Append('\n');
            if (Pick(release, language) is { } items) foreach (string item in items) text.Append("• ").Append(item).Append('\n');
            else text.Append(missing).Append('\n');
        }
        return text.ToString().TrimEnd();
    }

    private static string[]? Pick(ReleaseSummary release, string language) =>
        release.Notes is not { } notes ? null
        : notes.TryGetValue(language, out var items) ? items
        : notes.TryGetValue("en", out var english) ? english : null;
}
