namespace Nepply.Models;

/// <summary>Shared validity rule at native telemetry boundaries; missing is not zero.</summary>
internal static class MetricValue
{
    internal static float? Valid(float? value, float minimum, float maximum) =>
        value.HasValue && float.IsFinite(value.Value) && value >= minimum && value <= maximum ? value : null;
}
