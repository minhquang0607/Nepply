using Nepply.Models;
using Nepply.Services;
using Nepply.Services.Telemetry;

internal static class GpuIdentityTests
{
    internal static void Run(Action<string,bool> check)
    {
        GpuAdapterIdentity[] inventory = [new("AMD Radeon(TM) Graphics",GpuKind.Integrated),
            new("Intel Arc A770",GpuKind.Discrete),new("NVIDIA RTX 4060",GpuKind.Discrete)];
        check("GPU classification uses OS facts, including integrated AMD",GpuAdapterCatalog.Classify("AMD Radeon™ Graphics",inventory)==GpuKind.Integrated);
        check("Intel brand does not imply integrated GPU",GpuAdapterCatalog.Classify("Intel Arc A770",inventory)==GpuKind.Discrete);
        check("Unknown GPU model stays unknown",GpuAdapterCatalog.Classify("AMD unknown model",inventory)==GpuKind.Unknown);
        check("Ambiguous same-name adapters never guess type",GpuAdapterCatalog.Classify("same",
            [new("same",GpuKind.Integrated),new("same",GpuKind.Discrete)])==GpuKind.Unknown);
        check("Duplicate same-kind identities remain usable",GpuAdapterCatalog.Classify("same",
            [new("same",GpuKind.Integrated),new("same",GpuKind.Integrated)])==GpuKind.Integrated);
        var igpu = new GpuReading("integrated","AMD Radeon(TM) Graphics",16,40,1900,"fixture") {Kind=GpuKind.Integrated};
        var dgpu = new GpuReading("discrete","Intel Arc A770",80,65,2100,"fixture") {Kind=GpuKind.Discrete};
        var settings = new AppSettings();
        check("Single integrated GPU is iGPU, not GPU 1",HardwareMonitorService.SelectGpus([igpu],settings).Single().Label=="iGPU");
        check("One iGPU and one dedicated GPU have distinct labels",HardwareMonitorService.SelectGpus([igpu,dgpu],settings).Select(g=>g.Label).SequenceEqual(new[]{"iGPU","GPU"}));
        check("Multiple dedicated GPUs are numbered without numbering the iGPU",HardwareMonitorService.SelectGpus([dgpu,igpu,dgpu with {Id="second"}],settings)
            .Select(g=>g.Label).SequenceEqual(new[]{"GPU 1","iGPU","GPU 2"}));
        check("Unsupported detection uses generic GPU, not a fabricated iGPU",HardwareMonitorService.SelectGpus([dgpu with {Kind=GpuKind.Unknown}],settings).Single().Label=="GPU");
        settings.IntegratedGpuIdentifier="discrete";
        check("User GPU role override wins over OS classification",HardwareMonitorService.SelectGpus([dgpu],settings).Single().Label=="iGPU");
    }
}
