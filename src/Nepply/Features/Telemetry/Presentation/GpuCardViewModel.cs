using System.ComponentModel;
using Nepply.Models;
namespace Nepply.ViewModels;

public sealed class GpuCardViewModel(GpuReading reading) : INotifyPropertyChanged
{
    private GpuReading _reading = reading;
    public string Id => _reading.Id;
    public string Name => _reading.Name;
    public string Label => _reading.Label;
    public string Details => TelemetrySourceText.GpuDetails(_reading);
    internal void RefreshLanguage()=>PropertyChanged?.Invoke(this,new(nameof(Details)));
    public double LoadValue => _reading.LoadValue;
    public string LoadText => _reading.LoadText;
    public string TemperatureText => string.IsNullOrWhiteSpace(_reading.TemperatureText) ? "—" : _reading.TemperatureText;
    public string ClockText => _reading.ClockText;
    public void Update(GpuReading reading)
    {
        if (_reading == reading) return;
        _reading = reading; PropertyChanged?.Invoke(this, new(null));
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}
