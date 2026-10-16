using Nepply.Models;

namespace Nepply.Modules.Desktop;

/// <summary>Copy-on-write collection ownership. Item models remain presentation objects.</summary>
internal sealed class DesktopCatalogStore
{
    private readonly object _gate = new();
    private IReadOnlyList<DesktopIcon> _current = Array.AsReadOnly(Array.Empty<DesktopIcon>());
    internal IReadOnlyList<DesktopIcon> Read() => Volatile.Read(ref _current);
    internal bool Reconcile(IEnumerable<DesktopIcon> incoming,
        Func<IEnumerable<DesktopIcon>, IEnumerable<DesktopIcon>, DesktopIcon[]> reconcile)
    {
        lock (_gate)
        {
            var next = reconcile(_current, incoming);
            if (_current.SequenceEqual(next)) return false;
            Volatile.Write(ref _current, Array.AsReadOnly(next));
            return true;
        }
    }
    internal bool Add(DesktopIcon item)
    {
        lock (_gate)
        {
            if (_current.Any(i => i.Path.Equals(item.Path, StringComparison.OrdinalIgnoreCase))) return false;
            Volatile.Write(ref _current, Array.AsReadOnly(_current.Append(item).ToArray()));
            return true;
        }
    }
    internal bool Remove(string path)
    {
        lock (_gate)
        {
            int index = -1;
            for (int i = 0; i < _current.Count; i++)
                if (_current[i].Path.Equals(path, StringComparison.OrdinalIgnoreCase)) { index = i; break; }
            if (index < 0) return false;
            var next = _current.Where((_, i) => i != index).ToArray();
            Volatile.Write(ref _current, Array.AsReadOnly(next));
            return true;
        }
    }
}
