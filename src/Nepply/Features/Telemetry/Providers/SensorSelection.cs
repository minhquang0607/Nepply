namespace Nepply.Services.Telemetry;

internal sealed record NamedSensorValue(string Id, string Name, float? Value);

internal static class SensorSelection
{
    // These aliases identify the measurement, not just a substring of its label.
    // Tdie is preferred to an offset-bearing Tctl; hotspot/VRM are not GPU core.
    internal static readonly string[] CpuTemperatureNames =
        ["CPU Package", "Package", "Core (Tdie)", "Core (Tctl/Tdie)", "Core (Tctl)", "Core Average", "Cores (Average)"];
    internal static readonly string[] GpuCoreNames = ["GPU Core", "Core", "GPU Edge", "Edge"];

    internal static NamedSensorValue? Select(IEnumerable<NamedSensorValue> sensors,
        float minimum, float maximum, params string[] names)
    {
        var valid = sensors.Where(s => Nepply.Models.MetricValue.Valid(s.Value, minimum, maximum) != null).ToArray();
        foreach (string name in names)
        {
            var matches = valid.Where(s => string.Equals(s.Name.Trim(), name, StringComparison.OrdinalIgnoreCase))
                .DistinctBy(s => s.Id).Take(2).ToArray();
            if (matches.Length > 1) return null; // Ambiguity must not depend on enumeration order.
            if (matches.Length == 1) return matches[0];
        }
        return null;
    }
}
