using System.IO;
using Nepply.Models;

namespace Nepply.Modules.Desktop;

/// <summary>Physical membership, separate from grouped UI objects. No native calls.</summary>
internal static class DesktopRawCatalog
{
    internal static List<DesktopIcon> BoundImages(List<DesktopIcon> raw)
    {
        if (raw.Count > Services.DesktopImageBudget.MaximumEntries) throw new InvalidDataException("Desktop membership exceeds limit.");
        long remaining = Services.DesktopImageBudget.MaximumDecodedBytes;
        for (int i = 0; i < raw.Count; i++)
        {
            var item = raw[i];
            if (item.Icon is not System.Windows.Media.Imaging.BitmapSource bitmap) continue;
            long bytes = (long)bitmap.PixelWidth * bitmap.PixelHeight * 4;
            if (bytes <= remaining) { remaining -= bytes; continue; }
            raw[i] = new DesktopIcon
            {
                Path = item.Path,
                Name = item.Name,
                IsSystemIcon = item.IsSystemIcon,
                GroupIdentity = item.GroupIdentity,
                SourcePaths = item.SourcePaths
            };
        }
        return raw;
    }
    internal static List<DesktopIcon> Merge(IReadOnlyList<DesktopIcon> previous, IReadOnlyList<DesktopIcon> changed, IReadOnlyList<string> paths)
    {
        var dirty = paths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (changed.Any(item => !dirty.Contains(item.Path)))
            throw new InvalidDataException("Partial Desktop reply contains an unrequested path.");
        var replacements = changed.ToDictionary(i => i.Path, StringComparer.OrdinalIgnoreCase);
        var result = new List<DesktopIcon>(previous.Count + changed.Count);
        foreach (var old in previous)
        {
            if (!dirty.Contains(old.Path)) result.Add(old);
            else if (replacements.Remove(old.Path, out var replacement)) result.Add(replacement);
        }
        result.AddRange(replacements.Values);
        if (result.Count > Services.DesktopImageBudget.MaximumEntries) throw new InvalidDataException("Desktop membership exceeds limit.");
        return result;
    }
    internal static List<DesktopIcon> Group(IEnumerable<DesktopIcon> raw, bool merge)
    {
        return raw.GroupBy(item => merge ? item.GroupIdentity ?? Services.ShellDesktopReader.Identity(item)
            : Services.ShellDesktopReader.Identity(item), StringComparer.Ordinal).Select(group =>
        {
            var first = group.First();
            // Never mutate raw membership or models already bound to the presentation.
            return new DesktopIcon
            {
                Name = first.Name,
                Path = first.Path,
                Icon = first.Icon,
                IsSystemIcon = first.IsSystemIcon,
                SourcePaths = group.Select(i => i.Path).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            };
        }).ToList();
    }
}
