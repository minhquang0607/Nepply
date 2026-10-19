using Nepply;
using Nepply.Models;
using Nepply.Services;
using Nepply.Services.Telemetry;

// Status keys travel as data (models, wire, scan state), out of reach of the static
// key gate: every key a producer can emit must resolve to a translated resource.
internal static class LocalizationKeyTests
{
    internal static void Run(Action<string,bool> check)
    {
        string[] produced = [
            "SensorDisabled","SensorReconnecting","SensorExpired",HelperFailure.PolicyMessageKey,
            "NoNetwork","NetworkReadError","DesktopRefreshError","DesktopReadError",
            "PrivilegeUnknown","PrivilegeAdmin","PrivilegeUser","DriverUnknown","DriverAbsent","DriverRegistered",
            .. LayoutPresets.All.SelectMany(p => new[] { p.NameKey, p.DescriptionKey })];
        foreach (string key in produced)
            check($"Produced status key {key} has a resource", LocalizationService.Get(key) != key);
        check("Initial snapshot network status is a resource key",
            HardwareInfoSnapshot.Empty.NetworkStatus is { } status && LocalizationService.Get(status) != status && HardwareInfoSnapshot.Empty.NetworkName == "");
        var expired = TelemetryFreshness.ExpireIfNeeded(HardwareReading.Empty with { MonotonicTimestamp = 1 }, 1000);
        check("Expired sensor reading carries a resource key", expired.Error is { } error && LocalizationService.Get(error) != error);
        check("Keyed exception renders its resource, not its log message",
            LocalizationService.Render(UserMessage.From(new InvalidOperationException("log only").WithUserMessage("LayoutFormatError")))
                == LocalizationService.Get("LayoutFormatError"));
        check("Exception without key renders its own detail verbatim",
            LocalizationService.Render(UserMessage.From(new InvalidOperationException("OS {0} detail"))) == "OS {0} detail");
        // Language switching is process-wide WPF state; the per-language UI suite
        // (ErrorLocalizationTests) covers verbatim text under every other language.
    }
}
