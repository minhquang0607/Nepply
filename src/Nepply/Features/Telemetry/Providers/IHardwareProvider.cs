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
    HardwareInfoSnapshot GetSnapshot();
    void Start();
}
