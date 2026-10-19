namespace Nepply.Modules.Desktop;

/// <param name="Error">Resource key of the user-facing scan failure, never display text.</param>
public sealed record DesktopScanState(bool IsScanning, bool HasSnapshot, string? Error = null);
