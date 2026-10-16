namespace Nepply.Models;

internal static class TelemetryDisplay
{
    // Reported clock, not effective clock. Missing/invalid measurements are never zero.
    internal static string Clock(float? mhz) => mhz is { } value && float.IsFinite(value) && value > 0
        ? value < 1000 ? $"{value:F0} MHz" : $"{value / 1000:F2} GHz"
        : "—";
}
