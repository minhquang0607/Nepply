using Nepply.Models;

namespace Nepply.Modules.Desktop;

/// <summary>
/// Read-only presentation boundary. Callbacks may arrive on a worker thread;
/// subscribers marshal/coalesce them and detach when their own lifetime ends.
/// GetIcons returns a stable collection snapshot, not a mutable backing list.
/// Consumers do not own or dispose the catalog.
/// </summary>
public interface IDesktopCatalog
{
    IReadOnlyList<DesktopIcon> GetIcons();
    DesktopScanState ScanState { get; }
    event Action? IconsChanged;
    event Action? ScanStateChanged;
}
