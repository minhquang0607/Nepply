using Nepply.Models;
namespace Nepply.Services.Telemetry;

public interface IHardwareProvider : IDisposable
{
    string Name { get; }
    HardwareReading Read(AppSettings settings);
    HardwareReading Read(AppSettings settings, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        return Read(settings);
    }
}

public interface IHardwareMonitor : IDisposable
{
    event Action<HardwareInfoSnapshot>? HardwareUpdated;
    void Configure(AppSettings settings, bool visible);
    /// <summary>The panel may open soon: refresh sensors without waiting for the reveal.</summary>
    void PrepareReveal() { }
    HardwareInfoSnapshot GetSnapshot();
    void Start();
}
