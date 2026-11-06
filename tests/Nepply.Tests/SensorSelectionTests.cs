using Nepply.Services.Telemetry;

internal static class SensorSelectionTests
{
    internal static void Run(Action<string, bool> check)
    {
        NamedSensorValue tdie = new("die", "Core (Tdie)", 50);
        NamedSensorValue tctl = new("control", "Core (Tctl)", 70);
        NamedSensorValue gpu = new("core", "GPU Core", 45);
        NamedSensorValue? Cpu(params NamedSensorValue[] sensors) => SensorSelection.Select(sensors, 1, 130, SensorSelection.CpuTemperatureNames);
        NamedSensorValue? Gpu(params NamedSensorValue[] sensors) => SensorSelection.Select(sensors, 1, 130, SensorSelection.GpuCoreNames);
        check("CPU temperature prefers die over offset-bearing control", Cpu(tctl, tdie) == tdie && Cpu(tdie, tctl) == tdie);
        check("Combined Tctl Tdie sensor supported", Cpu(new NamedSensorValue("combined", "Core (Tctl/Tdie)", 51))?.Value == 51);
        check("Intel package sensor supported", Cpu(new NamedSensorValue("package", "CPU Package", 52))?.Value == 52);
        check("Core average is a labelled fallback", Cpu(new NamedSensorValue("avg", "Core Average", 53))?.Value == 53);
        check("CPU does not take arbitrary CCD average", Cpu(new NamedSensorValue("ccd", "CCD Average", 60)) == null);
        check("GPU cannot mistake core hotspot for core temperature", Gpu(new NamedSensorValue("hot", "GPU Core Hotspot", 90)) == null);
        check("GPU exact match wins independent of ordering", Gpu(new("hot", "GPU Core Hotspot", 90), gpu) == gpu);
        check("GPU memory and VRM do not substitute for core", Gpu(new("mem", "GPU Memory", 80), new("vrm", "GPU VR Core", 70)) == null);
        check("Sensor labels tolerate case and outer whitespace", Gpu(gpu with { Name = " gpu core " })?.Value == 45);
        check("Invalid primary can use valid named fallback", Cpu(tdie with { Value = float.NaN }, tctl) == tctl);
        check("Missing and infinite temperature stay unavailable", Gpu(gpu with { Value = null }, gpu with { Id = "inf", Value = float.PositiveInfinity }) == null);
        check("Ambiguous matching sensors are not chosen by order", Gpu(gpu, gpu with { Id = "second", Value = 60 }) == null);
        check("Repeated sensor identity is deduplicated", Gpu(gpu, gpu) == gpu);
        check("Zero load is retained while zero temperature rejected", SensorSelection.Select([gpu with { Value = 0 }], 0, 100, "GPU Core")?.Value == 0 && Gpu(gpu with { Value = 0 }) == null);

        int reads = 0;
        var adapters = new[] { ("/gpu-amd/0", "first"), ("/gpu-amd/1", "other"), ("/gpu-amd/0", "duplicate") };
        var byId = LibreHardwareProvider.FirstById(adapters, a => a.Item1, a => { reads++; return a.Item2; });
        check("A duplicate hardware ID keeps the first entry instead of throwing",
            byId.Count == 2 && byId["/gpu-amd/0"] == "first" && byId["/gpu-amd/1"] == "other");
        check("A duplicate hardware ID is read only once", reads == 2);
    }
}
