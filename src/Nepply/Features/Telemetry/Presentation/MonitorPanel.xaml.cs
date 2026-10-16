using System.Windows.Controls;
using System.Windows;
using Nepply.Models;
using Nepply.Services;
using Nepply.ViewModels;
using Nepply.Modules.Appearance;
namespace Nepply.Views;
public partial class MonitorPanel : UserControl
{
    private readonly HardwareMonitorViewModel _viewModel = new();
    private AppSettings? _settings;
    private readonly MaterialLightScope _light;
    public static readonly DependencyProperty CardHeightProperty = DependencyProperty.Register(nameof(CardHeight), typeof(double), typeof(MonitorPanel), new PropertyMetadata(118d));
    public double CardHeight { get => (double)GetValue(CardHeightProperty); private set => SetValue(CardHeightProperty, value); }
    public static readonly DependencyProperty GpuColumnsProperty = DependencyProperty.Register(nameof(GpuColumns), typeof(int), typeof(MonitorPanel), new PropertyMetadata(1));
    public int GpuColumns { get => (int)GetValue(GpuColumnsProperty); private set => SetValue(GpuColumnsProperty, value); }
    public MonitorPanel() { InitializeComponent(); _light = new(Widgets); Widgets.SetValue(MetallicSurface.LightScopeProperty, _light); DataContext = _viewModel; SizeChanged += (_, _) => FitCards(); _viewModel.Gpus.CollectionChanged += (_, _) => FitCards(); }
    public void Initialize(Services.Telemetry.IHardwareMonitor service) => _viewModel.Initialize(service);
    internal void PreviewConfiguration(AppSettings? settings) => _viewModel.PreviewConfiguration(settings);
    public void SetActive(bool active) => _viewModel.SetActive(active);
    public void Cleanup() { _light.Dispose(); _viewModel.Cleanup(); }
    public void ConfigureLayout(Nepply.Models.AppSettings settings, bool editing)
    {
        _settings = settings;
        PresentationDensity.SetCompact(this, settings.EdgePosition.IsHorizontal() && !editing);
        var items = Widgets.Children.OfType<System.Windows.FrameworkElement>().ToArray();
        var order = items.Where(e=>e.Tag is string).Select(e=>(string)e.Tag).ToArray();
        bool reorder=!order.SequenceEqual(settings.WidgetOrder);
        if(reorder) foreach (var element in items) if (element.Tag is string) Widgets.Children.Remove(element);
        foreach (string id in settings.WidgetOrder)
        {
            var element = items.FirstOrDefault(e => Equals(e.Tag,id)); if (element == null) continue;
            element.Visibility = (id == "network" && !settings.ShowNetwork) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
            WidgetDrag.SetEnabled(element, editing); if(reorder) Widgets.Children.Add(element);
            if(id is "summary" or "network") element.SetResourceReference(System.Windows.Automation.AutomationProperties.NameProperty,id=="summary" ? "Loc.WidgetSummary" : "Loc.WidgetNetwork");
            else System.Windows.Automation.AutomationProperties.SetName(element,id=="gpus" ? "GPU" : id);
            if(editing) element.SetResourceReference(System.Windows.Automation.AutomationProperties.HelpTextProperty,"Loc.WidgetMoveHint");
            else element.ClearValue(System.Windows.Automation.AutomationProperties.HelpTextProperty);
            element.SetResourceReference(FrameworkElement.FocusVisualStyleProperty,"WidgetFocusVisual");
            element.ContextMenu = editing && id is "summary" or "gpus" ? ShapeMenu(id) : null;
            element.ContextMenuOpening -= OpenNativeMenu;
            if (element.ContextMenu != null) element.ContextMenuOpening += OpenNativeMenu;
        }
        FitCards();
    }
    private void FitCards()
    {
        if (_settings == null) return;
        bool compact = PresentationDensity.GetCompact(this);
        SummaryGrid.Columns = _settings.SummaryColumns == 0 ? (ActualWidth >= (compact ? 220 : 300) ? 2 : 1) : _settings.SummaryColumns;
        // Reserve the next GPU cell even when only one device is present.
        GpuColumns = SummaryGrid.Columns;
        CardHeight = compact ? 110 : 118;
    }
    private void OpenNativeMenu(object sender, ContextMenuEventArgs e)
    {
        e.Handled = true;
        if (sender is not FrameworkElement element || element.ContextMenu is not { } commands || Window.GetWindow(this) is not MainWindow main) return;
        main.SuppressHide();
        try { Helpers.WindowsPopupMenu.Show(commands, main, e.CursorLeft < 0 ? element.PointToScreen(new Point(12,12)) : null); }
        catch(Exception ex) { AppLogger.Error(ex,"Widget native menu"); }
        finally { main.ResumeHide(); }
    }
    private ContextMenu ShapeMenu(string id)
    {
        var menu = new ContextMenu();
        foreach (var (key, columns) in new[] { ("WidgetAutoWidth", 0), ("WidgetOneColumn", 1), ("WidgetTwoColumns", 2) })
        {
            var item = new MenuItem { IsCheckable = true, IsChecked = _settings!.SummaryColumns == columns, Tag=key, Header=LocalizationService.Get(key) };
            item.Icon = item.IsChecked ? "✓" : null;
            item.Click += (_, _) => { _settings!.SummaryColumns = columns; _settings.GpuColumns = columns; FitCards();
                foreach (MenuItem sibling in menu.Items) { sibling.IsChecked = sibling == item; sibling.Icon = sibling.IsChecked ? "✓" : null; }
                RaiseEvent(new RoutedEventArgs(WidgetDrag.ReorderedEvent, this)); };
            menu.Items.Add(item);
        }
        return menu;
    }
    internal void RefreshLanguage()
    {
        // These command menus feed a native popup and are never attached to a WPF tree.
        foreach(var widget in Widgets.Children.OfType<FrameworkElement>())
            if(widget.ContextMenu is {} menu)
                foreach(var item in menu.Items.OfType<MenuItem>())
                    if(item.Tag is string key) item.Header=LocalizationService.Get(key);
    }
    public string[] GetWidgetOrder() => Widgets.Children.OfType<System.Windows.FrameworkElement>().Select(e => e.Tag).OfType<string>().ToArray();
}
