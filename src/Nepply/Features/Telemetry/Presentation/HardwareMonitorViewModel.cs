using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using Nepply.Models;
using Nepply.Services;
namespace Nepply.ViewModels;

public sealed class HardwareMonitorViewModel : INotifyPropertyChanged
{
    private Services.Telemetry.IHardwareMonitor? _service;
    private HardwareInfoSnapshot _display = HardwareInfoSnapshot.Empty;
    private AppSettings? _presentationPreview;
    internal void PreviewConfiguration(AppSettings? settings)
    {
        if(_presentationPreview==null && settings==null) return;
        if(_presentationPreview is {} previous && settings is {} next &&
            (previous.GpuDisplay,previous.GpuIdentifier,previous.IntegratedGpuIdentifier)==(next.GpuDisplay,next.GpuIdentifier,next.IntegratedGpuIdentifier)) return;
        _presentationPreview=settings?.Clone();
        PresentGpus();
    }
    private void PresentGpus() => UpdateGpus(_presentationPreview is {} settings
        ? HardwareMonitorService.SelectGpus(_display.Hardware.Gpus,settings) : _display.Hardware.VisibleGpus);
    private sealed class Subscription
    {
        internal Action<HardwareInfoSnapshot>? Handler;
        internal HardwareInfoSnapshot? Pending;
        internal long Sequence = -1;
        internal bool Scheduled;
    }
    private readonly object _updates = new();
    private Subscription? _subscription;
    private volatile bool _active;
    private readonly DispatcherTimer _freshness = new() { Interval = TimeSpan.FromSeconds(2) };
    public HardwareMonitorViewModel() { _freshness.Tick += (_, _) => { Changed(nameof(StatusText)); Changed(nameof(IsStale)); }; }
    public void Initialize(Services.Telemetry.IHardwareMonitor service)
    {
        Cleanup();
        LocalizationService.Changed+=LanguageChanged;
        var subscription=new Subscription();
        subscription.Handler=snapshot=>OnUpdate(subscription,snapshot);
        lock(_updates) { _service=service; _subscription=subscription; }
        service.HardwareUpdated += subscription.Handler;
        OnUpdate(subscription,service.GetSnapshot());
    }
    public void SetActive(bool active)
    {
        _active = active;
        if (active)
        {
            _freshness.Start();
            if (_service != null && _subscription is {} subscription) OnUpdate(subscription,_service.GetSnapshot());
        }
        else _freshness.Stop();
    }
    public void Cleanup()
    {
        LocalizationService.Changed-=LanguageChanged;
        _freshness.Stop(); _active=false;
        Services.Telemetry.IHardwareMonitor? service;
        Subscription? subscription;
        lock(_updates)
        {
            service=_service; subscription=_subscription;
            _service=null; _subscription=null;
        }
        if(service != null && subscription?.Handler != null) service.HardwareUpdated-=subscription.Handler;
        _presentationPreview=null; _display=HardwareInfoSnapshot.Empty; Gpus.Clear(); Changed(null);
    }
    private void OnUpdate(Subscription subscription,HardwareInfoSnapshot snapshot)
    {
        lock(_updates)
        {
            if (!ReferenceEquals(_subscription,subscription)) return;
            if(snapshot.Sequence>subscription.Sequence)
            {
                subscription.Sequence=snapshot.Sequence;
                subscription.Pending=snapshot;
            }
            // Reactivation must flush an already received pending frame, not overwrite it
            // with an older GetSnapshot result. Each subscription owns its queued callback.
            if(!_active || subscription.Scheduled || subscription.Pending==null) return;
            subscription.Scheduled=true;
        }
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            HardwareInfoSnapshot? value;
            lock(_updates)
            {
                if(!ReferenceEquals(_subscription,subscription)) return;
                subscription.Scheduled=false;
                if(!_active) return;
                value=subscription.Pending; subscription.Pending=null;
            }
            if (value == null) return;
            _display = value;
            PresentGpus();
            Changed(null);
        }, DispatcherPriority.Background);
    }
    public bool IsStale => Services.Telemetry.TelemetryFreshness.IsSnapshotStale(_display,System.Diagnostics.Stopwatch.GetTimestamp());
    public string StatusText => _display.Sequence == 0 ? LocalizationService.Get("MonitorConnecting")
        : IsStale ? LocalizationService.Get("MonitorStale")
        : string.Format(LocalizationService.Get("MonitorUpdated"),_display.SampledAt.ToLocalTime().ToString("HH:mm:ss"));
    public string CpuName => _display.Hardware.CpuName;
    public string CpuDetails => CpuName + "\n" + TelemetrySourceText.Cpu(_display.Hardware);
    public string GpuName => _display.Hardware.GpuName;
    public double CpuLoad => _display.CpuLoad ?? 0;
    public double GpuLoad => _display.Hardware.GpuLoad ?? 0;
    public double RamLoad => _display.RamLoad ?? 0;
    public string CpuLoadText => Percent(_display.CpuLoad);
    public string GpuLoadText => Percent(_display.Hardware.GpuLoad);
    public string RamLoadText => Percent(_display.RamLoad);
    public string CpuTempText => Temperature(_display.Hardware.CpuTemp);
    public string GpuTempText => Temperature(_display.Hardware.GpuTemp);
    public string CpuClockText => Clock(_display.Hardware.CpuClockMHz);
    public string GpuClockText => Clock(_display.Hardware.GpuClockMHz);
    public string RamClockText => _display.Hardware.MemoryClockMHz is { } clock ? $"{clock:F0} MHz" : "";
    public string RamInfoText => _display.RamUsedGB is { } used && _display.RamTotalGB is { } total ? $"{used:F2} / {total:F2} GiB" : "— / — GiB";
    public string RamDetails => string.Format(LocalizationService.Get("RamAvailable"),RamInfoText)
        + (_display.RamInstalledGB is { } installed && _display.RamTotalGB is { } usable
            ? "\n"+string.Format(LocalizationService.Get("RamInstalled"),installed,Math.Max(0,installed-usable)) : "")
        + "\n"+LocalizationService.Get("RamPercentageHint");
    public string NetworkName => _display.NetworkStatus is {} status ? LocalizationService.Get(status) : _display.NetworkName;
    private void LanguageChanged()
    {
        Changed(nameof(NetworkName)); Changed(nameof(StatusText));
        Changed(nameof(RamDetails)); Changed(nameof(SensorNote));
        Changed(nameof(CpuDetails));
        foreach(var gpu in Gpus) gpu.RefreshLanguage();
    }
    public string NetUploadText => FormatSpeed(_display.NetUpload);
    public string NetDownloadText => FormatSpeed(_display.NetDownload);
    public string SensorNote => _display.Hardware.Error is {} error ? LocalizationService.Get(error)
        : LocalizationService.Get(_display.Hardware.CpuTemp == null && !_display.Hardware.Gpus.Any(g => g.Temperature != null)
            ? "TemperatureUnavailable" : "SensorLiveHint");
    public ObservableCollection<GpuCardViewModel> Gpus { get; } = [];
    public void UpdateGpus(IReadOnlyList<GpuReading> readings)
    {
        for (int i = 0; i < readings.Count; i++)
        {
            if (i >= Gpus.Count) Gpus.Add(new(readings[i]));
            else if (Gpus[i].Id == readings[i].Id) Gpus[i].Update(readings[i]);
            else Gpus[i] = new(readings[i]);
        }
        while (Gpus.Count > readings.Count) Gpus.RemoveAt(Gpus.Count - 1);
    }
    private static string Percent(float? value) => value is { } number ? $"{number:F0}%" : "—";
    private static string Temperature(float? value) => value is { } number ? $"{number:F0}°C" : "—";
    private static string Clock(float? value) => TelemetryDisplay.Clock(value);
    public static string FormatSpeed(double? value) => value is not { } number ? "—" : number >= 1048576 ? $"{number / 1048576:F1} MiB/s" : $"{number / 1024:F0} KiB/s";
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
