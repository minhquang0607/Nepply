using System.Windows;
using System.Windows.Input;

namespace Nepply.Views;

public partial class SensorDiagnosticsWindow : Window
{
    public SensorDiagnosticsWindow(string report) { InitializeComponent(); ReportText.Text=report; }
    internal SensorDiagnosticsWindow(Func<string> report) : this(report())
    {
        void RefreshReport()=>ReportText.Text=report();
        Loaded+=(_,_)=> { Services.LocalizationService.Changed-=RefreshReport; Services.LocalizationService.Changed+=RefreshReport; RefreshReport(); };
        Unloaded+=(_,_)=>Services.LocalizationService.Changed-=RefreshReport;
        Closed+=(_,_)=>Services.LocalizationService.Changed-=RefreshReport;
    }
    private void DragTitle(object sender,MouseButtonEventArgs e) { if(e.ChangedButton==MouseButton.Left) DragMove(); }
    private void Close_Click(object sender,RoutedEventArgs e)=>Close();
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if(e.Key==Key.Escape) { Close(); e.Handled=true; }
        base.OnKeyDown(e);
    }
    private void Copy_Click(object sender,RoutedEventArgs e)
    {
        try { Clipboard.SetText(ReportText.Text); CopyStatus.SetResourceReference(System.Windows.Controls.TextBlock.TextProperty,"Loc.DiagnosticsCopied"); }
        catch(System.Runtime.InteropServices.ExternalException)
        { CopyStatus.SetResourceReference(System.Windows.Controls.TextBlock.TextProperty,"Loc.DiagnosticsClipboardBusy"); }
    }
}
