namespace Nepply.Modules.Desktop;

public sealed record DesktopScanState(bool IsScanning, bool HasSnapshot, string? Error = null);
