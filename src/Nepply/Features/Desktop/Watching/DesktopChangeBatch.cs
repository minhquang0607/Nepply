using System.IO;

namespace Nepply.Modules.Desktop;

/// <summary>Caller owns synchronization. Unknown/oversized batches promote to full repair.</summary>
internal sealed class DesktopChangeBatch
{
    internal const int MaximumPaths = 512;
    internal const int MaximumPathCharacters = 65536;
    private readonly Dictionary<string, string> _paths = new(StringComparer.OrdinalIgnoreCase);
    private bool _full;
    private int _characters;
    internal bool HasChanges => _full || _paths.Count != 0;
    internal void Full() { _full = true; _paths.Clear(); _characters = 0; }
    internal void Add(IEnumerable<string>? paths)
    {
        if (paths == null) { Full(); return; }
        if (_full) return;
        foreach (var path in paths)
        {
            if (!TryNormalize(path, out var normalized)) { Full(); return; }
            if (_paths.TryGetValue(normalized, out var previous)) _characters -= previous.Length;
            _paths[normalized] = normalized; // Retain the new spelling in a case-only rename.
            _characters += normalized.Length;
            if (_paths.Count > MaximumPaths || _characters > MaximumPathCharacters) { Full(); return; }
        }
    }
    internal string[]? Take()
    {
        var result = _full ? null : _paths.Values.ToArray();
        _full = false; _paths.Clear(); _characters = 0;
        return result;
    }
    internal static bool TryNormalize(string? path, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(path) || path.Length > 32768 || !Path.IsPathFullyQualified(path)) return false;
        try { normalized = Path.GetFullPath(path); return true; }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }
    internal static bool IsDirectChild(string path, IEnumerable<string> roots) =>
        TryNormalize(path, out var normalized) && roots.Any(root => TryNormalize(root, out var directory)
            && string.Equals(Path.GetDirectoryName(normalized), Path.TrimEndingDirectorySeparator(directory), StringComparison.OrdinalIgnoreCase));
}
