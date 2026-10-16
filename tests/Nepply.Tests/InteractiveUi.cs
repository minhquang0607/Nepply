using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Nepply;
using Nepply.Models;
using Nepply.Services;
using Nepply.Views;

internal static class InteractiveUi
{
    internal static int Run(string output, bool island = false)
    {
        var app=new App(testHost:true); app.InitializeComponent();
        var image=new DrawingImage(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(217,188,139)),null,new RectangleGeometry(new Rect(2,5,28,23),5,5))); image.Freeze();
        var samples=Enumerable.Range(1,24).Select(i=>new DesktopIcon {Name=$"Mẫu {i:00}",Path=Path.Combine(output,$"sample-{i:00}.not-a-real-file"),Icon=image}).ToList();
        var service=new DesktopIconService(_=>Task.FromResult(samples));
        var window=new MainWindow(preview:true,icons:service) {Title="Nepply · UI QA",ShowInTaskbar=true,AllowPreviewIsland=island};
        window.Settings.EdgePosition=EdgePosition.Top; window.Settings.ShowMonitor=false; window.Settings.PanelHeight=400;
        window.Loaded+=async (_,_)=>
        {
            await service.ScanDesktopIconsAsync();
            window.TogglePin(); window.ShowPanel();
            var icons=(IconPanel)window.FindName("IconPanelView");
            icons.LayoutChanged+=(_,_)=>File.WriteAllText(Path.Combine(output,"layout-events.json"),JsonSerializer.Serialize(new {window.Settings.IconOrder,window.Settings.GridCellWidth,window.Settings.GridCellHeight}));
        };
        window.Closed+=(_,_)=>app.Shutdown();
        return app.Run(window);
    }
}
