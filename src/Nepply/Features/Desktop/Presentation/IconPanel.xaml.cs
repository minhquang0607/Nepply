using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Nepply.Models;
using Nepply.Services;
using Nepply.ViewModels;
namespace Nepply.Views;

public partial class IconPanel : UserControl
{
    private Modules.Desktop.IDesktopActions? _service;
    private UserMessage? _actionNotice;
    private void RetryScan_Click(object sender, RoutedEventArgs e) => _service?.Refresh();
    private AppSettings? _settings;
    private readonly DesktopIconViewModel _viewModel = new();
    private DesktopIcon? _pressedIcon;
    private int _clickCount;
    private bool _editing;
    private Point _dragStart;
    private readonly DragScrollPace _dragScroll=new();
    private readonly System.Windows.Threading.DispatcherTimer _dragScrollTimer = new() { Interval=TimeSpan.FromMilliseconds(33) };
    private IconGridPanel? _dragGrid;
    private Point _dragPoint;
    private string? _activeDragPath;
    public event EventHandler? LayoutChanged;
    public void ConfigureLayout(AppSettings settings, bool editing) { _settings = settings; SetEditMode(editing); }
    public void SetEditMode(bool editing) { if (_editing && !editing) LayoutChanged?.Invoke(this,EventArgs.Empty); _editing = editing; if(!editing) StopDragFeedback(); IconList.AllowDrop = editing; IconList.Cursor = editing ? Cursors.SizeAll : Cursors.Arrow; ApplyTileShape(); }
    public static readonly DependencyProperty GridColumnsProperty=DependencyProperty.Register(nameof(GridColumns),typeof(int),typeof(IconPanel),new PropertyMetadata(4));
    public static readonly DependencyProperty GridRowsProperty=DependencyProperty.Register(nameof(GridRows),typeof(int),typeof(IconPanel),new PropertyMetadata(5));
    public int GridColumns {get=>(int)GetValue(GridColumnsProperty);private set=>SetValue(GridColumnsProperty,value);}
    public int GridRows {get=>(int)GetValue(GridRowsProperty);private set=>SetValue(GridRowsProperty,value);}
    private void ApplyTileShape()
    {
        GridColumns=Math.Clamp(_settings?.ActiveGridColumns ?? 4,1,_settings?.EdgePosition.IsHorizontal()==true ? Nepply.Modules.Desktop.GridCapacity.MaximumColumns : 5);
        GridRows=Math.Clamp(_settings?.ActiveGridRows ?? 5,1,_settings?.EdgePosition.IsHorizontal()==true ? 3 : Nepply.Modules.Desktop.GridCapacity.MaximumRows);
    }
    public static readonly DependencyProperty IconPixelsProperty = DependencyProperty.Register(nameof(IconPixels), typeof(double), typeof(IconPanel), new PropertyMetadata(36d));
    public double IconPixels { get => (double)GetValue(IconPixelsProperty); set => SetValue(IconPixelsProperty, value); }
    public IconPanel()
    {
        InitializeComponent(); DataContext = _viewModel;
        _dragScrollTimer.Tick += DragScrollTick;
        IconList.DragLeave += (_, _) => StopDragFeedback();
        Unloaded += (_, _) => { StopDragFeedback(); _pressedIcon=null; };
        IsVisibleChanged += (_, e) => { if (!(bool)e.NewValue) StopDragFeedback(); };
        IsEnabledChanged += (_, e) => { if (!(bool)e.NewValue) StopDragFeedback(); };
    }
    public void Initialize(Modules.Desktop.IDesktopCatalog catalog, Modules.Desktop.IDesktopActions actions, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(actions);
        LocalizationService.Changed-=RefreshActionNotice;
        LocalizationService.Changed+=RefreshActionNotice;
        _service = actions; _settings = settings; _viewModel.Initialize(catalog, settings); ApplyIconSize(IconSizeOption.Medium); ApplyTileShape();
    }
    public void ApplyIconSize(IconSizeOption size) => IconPixels = size switch { IconSizeOption.Small => 28, IconSizeOption.Large => 44, _ => 36 };
    public void Cleanup() { LocalizationService.Changed-=RefreshActionNotice; ShowActionFailure(null); StopDragFeedback(); _viewModel.Cleanup(); _service = null; }
    internal Size GridViewport => FindGrid(IconList) is {} grid ? new(grid.ViewportWidth,grid.ViewportHeight) : new();
    internal double GridOffset { get=>FindGrid(IconList)?.VerticalOffset ?? 0; set { FindGrid(IconList)?.SetVerticalOffset(value); } }
    public void RefreshLayout() => _viewModel.RefreshOrder();
    public void FocusSearch() => (Window.GetWindow(this) as MainWindow)?.FocusDesktopSearch();
    public bool ClearFocusedSearch()
    {
        if (Window.GetWindow(this) is not MainWindow main || !main.DesktopSearchHasFocus || _viewModel.SearchQuery.Length==0) return false;
        _viewModel.SearchQuery=""; return true;
    }
    internal void HandleSearchKey(KeyEventArgs e)
    {
        // Leave Enter and IME-processed keys with the text editor. Opening is
        // explicit, from the result list, never a side effect of composition.
        if (e.Key!=Key.Down || Keyboard.Modifiers!=ModifierKeys.None || IconList.Items.Count==0) return;
        if (IconList.SelectedItem==null) IconList.SelectedIndex=0;
        var selected=IconList.SelectedItem;
        IconList.ScrollIntoView(selected); IconList.Focus();
        Dispatcher.InvokeAsync(()=>
        {
            if (IconList.IsKeyboardFocusWithin && ReferenceEquals(IconList.SelectedItem,selected))
                (IconList.ItemContainerGenerator.ContainerFromItem(selected) as ListBoxItem)?.Focus();
        },System.Windows.Threading.DispatcherPriority.Loaded);
        e.Handled=true;
    }
    private void DismissLaunchError_Click(object sender,RoutedEventArgs e) => ShowActionFailure(null);
    private void Launch(DesktopIcon icon)
    {
        ShowActionFailure(_service?.LaunchIcon(icon));
    }
    internal void ShowActionNotice(string? key)
        => ShowActionFailure(key == null ? null : UserMessage.FromKey(key));
    internal void ShowActionFailure(UserMessage? message)
    {
        _actionNotice=message;
        RefreshActionNotice();
    }
    private void RefreshActionNotice()
    {
        LaunchNoticeText.Text=LocalizationService.Render(_actionNotice);
        LaunchNotice.Visibility=_actionNotice == null ? Visibility.Collapsed : Visibility.Visible;
    }
    internal void DismissActionNotice(string key)
    {
        if (_actionNotice?.Key == key && _actionNotice.Arguments.IsEmpty) ShowActionFailure(null);
    }
    internal void DismissActionFailure(UserMessage message)
    {
        if (ReferenceEquals(_actionNotice,message)) ShowActionFailure(null);
    }
    private DesktopIcon? ItemAt(MouseButtonEventArgs e) => (ItemsControl.ContainerFromElement(IconList, e.OriginalSource as DependencyObject) as ListBoxItem)?.DataContext as DesktopIcon;
    private void Icon_MouseUp(object sender, MouseButtonEventArgs e)
    {
        var pressed = _pressedIcon;
        _pressedIcon = null;
        if (_editing || ExceedsDragThreshold(_dragStart,e.GetPosition(IconList))) return;
        if (ItemAt(e) is not { } icon || !SameActionTarget(icon, pressed)) return;
        IconList.SelectedItem = icon;
        if ((_settings?.SingleClickToOpen == true && _clickCount == 1) || (_settings?.SingleClickToOpen != true && _clickCount == 2))
        { Launch(icon); e.Handled = true; }
    }
    private void Icon_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _pressedIcon = ItemAt(e);
        _dragStart = e.GetPosition(IconList);
        _clickCount = e.ClickCount;
    }
    internal static bool SameActionTarget(DesktopIcon current, DesktopIcon? pressed) =>
        pressed != null && current.IsSystemIcon == pressed.IsSystemIcon
        && string.Equals(current.Path, pressed.Path, StringComparison.OrdinalIgnoreCase);
    private void Icon_KeyDown(object sender, KeyEventArgs e)
    {
        if (IsContextMenuKey(e.Key,Keyboard.Modifiers))
        {
            e.Handled=true;
            if(e.IsRepeat) return;
            if(_editing) return;
            if(IconList.SelectedItem is DesktopIcon menuIcon && Window.GetWindow(this) is MainWindow main)
            {
                IconList.ScrollIntoView(menuIcon); IconList.UpdateLayout();
                var container=IconList.ItemContainerGenerator.ContainerFromItem(menuIcon) as FrameworkElement;
                var anchor=container?.PointToScreen(new Point(container.ActualWidth/2,container.ActualHeight/2));
                main.SuppressHide();
                try { ShowActionFailure(_service?.ShowShellContextMenu(menuIcon,new System.Windows.Interop.WindowInteropHelper(main).Handle,anchor)); }
                finally { main.ResumeHide(); }
            }
            return;
        }
        if (_editing && IconList.SelectedItem is DesktopIcon selected)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key is Key.Up or Key.Down)
            {
                if (_viewModel.MoveVisible(selected.Path,e.Key==Key.Up ? -1 : 1))
                {
                    LayoutChanged?.Invoke(this,EventArgs.Empty); IconList.SelectedItem = selected; IconList.ScrollIntoView(selected);
                }
                e.Handled = true;
            }
            if (e.Key == Key.Enter) e.Handled = true;
            return;
        }
        if (e.Key == Key.Enter && Keyboard.Modifiers==ModifierKeys.None && IconList.SelectedItem is DesktopIcon icon)
        { if (!e.IsRepeat) Launch(icon); e.Handled = true; }
    }
    private void Icon_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) { _pressedIcon=null; return; }
        if (_pressedIcon == null || !ExceedsDragThreshold(_dragStart,e.GetPosition(IconList))) return;
        var source = _pressedIcon; _pressedIcon = null;
        // Once the gesture becomes a drag, returning to the original tile must
        // not turn its mouse-up into a launch in single-click mode.
        if (!_editing) return;
        if (Window.GetWindow(this) is not MainWindow main) return;
        main.SuppressHide();
        _activeDragPath = source.Path;
        try { DragDrop.DoDragDrop(IconList, new DataObject("Nepply.Icon", source.Path), DragDropEffects.Move); }
        finally { _activeDragPath=null; StopDragFeedback(); main.ResumeHide(); }
    }
    internal static bool ExceedsDragThreshold(Point start, Point current) =>
        Math.Abs(current.X-start.X) >= SystemParameters.MinimumHorizontalDragDistance
        || Math.Abs(current.Y-start.Y) >= SystemParameters.MinimumVerticalDragDistance;
    private void Icon_DragOver(object sender, DragEventArgs e)
    {
        e.Effects=DragDropEffects.None;
        if (_editing && _activeDragPath is { } path && FindGrid(IconList) is { } grid)
        {
            _dragGrid=grid; _dragPoint=e.GetPosition(grid);
            if (DragScrollPace.CanScroll(_dragPoint.X,_dragPoint.Y,grid.ViewportWidth,grid.ViewportHeight,grid.VerticalOffset,grid.ExtentHeight))
            {
                if(!_dragScrollTimer.IsEnabled)
                { _dragScroll.Step(_dragPoint.Y,grid.ViewportHeight,Environment.TickCount64); _dragScrollTimer.Start(); }
            }
            else { _dragScrollTimer.Stop(); _dragScroll.Reset(); }
            if (ShowDropFeedback(grid,_dragPoint,path)) e.Effects=DragDropEffects.Move;
        }
        else StopDragFeedback();
        e.Handled = true;
    }
    private void StopDragFeedback()
    {
        _dragScrollTimer.Stop(); _dragScroll.Reset(); _dragGrid?.ClearDropPreview(); _dragGrid=null; DropHint.Clear();
    }
    private void DragScrollTick(object? sender,EventArgs e)
    {
        if (!_editing || !IsEnabled || !IsVisible || !IsLoaded || _activeDragPath is not { } path || _dragGrid is not {IsVisible:true} grid)
        { StopDragFeedback(); return; }
        if (!DragScrollPace.CanScroll(_dragPoint.X,_dragPoint.Y,grid.ViewportWidth,grid.ViewportHeight,grid.VerticalOffset,grid.ExtentHeight))
        {
            // Keep the insertion marker at an endpoint, but stop waking the UI
            // when there is no remaining scroll range (or geometry changed).
            _dragScrollTimer.Stop(); _dragScroll.Reset();
            ShowDropFeedback(grid,_dragPoint,path);
            return;
        }
        double delta=_dragScroll.Step(_dragPoint.Y,grid.ViewportHeight,Environment.TickCount64);
        double before=grid.VerticalOffset;
        grid.SetVerticalOffset(before+delta);
        if(before==grid.VerticalOffset) return;
        grid.UpdateLayout();
        ShowDropFeedback(grid,_dragPoint,path);
    }
    private bool ShowDropFeedback(IconGridPanel grid,Point point,string path)
    {
        var (target,after)=ResolveDropTarget(grid,point);
        if(target?.DataContext is DesktopIcon icon && _viewModel.CanReorder(path,icon.Path,after))
        {
            int source=-1;
            for(int i=0;i<IconList.Items.Count;i++)
                if(IconList.Items[i] is DesktopIcon item && string.Equals(item.Path,path,StringComparison.OrdinalIgnoreCase)) {source=i;break;}
            int insertion=grid.InsertionIndex(point,IconList.Items.Count);
            grid.SetDropPreview(source,insertion);
            // Anchor feedback to the destination slot, never to a tile moving out of it.
            int destination=IconGridPanel.PreviewIndex(source,source,insertion,IconList.Items.Count);
            DropHint.Show(grid,false,true,grid.SlotBounds(destination)); return true;
        }
        grid.ClearDropPreview(); DropHint.Clear(); return false;
    }
    private static IconGridPanel? FindGrid(DependencyObject parent)
    {
        if (parent is IconGridPanel grid) return grid;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            if (FindGrid(System.Windows.Media.VisualTreeHelper.GetChild(parent,i)) is { } found) return found;
        return null;
    }
    private void Icon_Drop(object sender, DragEventArgs e)
    {
        StopDragFeedback();
        var (container,after) = ResolveDropTarget(e);
        e.Effects=DragDropEffects.None;
        if (_editing && _activeDragPath is string path && container?.DataContext is DesktopIcon target && _viewModel.Reorder(path,target.Path,after))
        {
            IconList.SelectedItem=_viewModel.Icons.FirstOrDefault(i=>string.Equals(i.Path,path,StringComparison.OrdinalIgnoreCase));
            if(IconList.SelectedItem is {} moved) IconList.ScrollIntoView(moved);
            e.Effects=DragDropEffects.Move;
            LayoutChanged?.Invoke(this,EventArgs.Empty);
        }
        e.Handled = true;
    }
    private (ListBoxItem? Container,bool After) ResolveDropTarget(DragEventArgs e)
    {
        return FindGrid(IconList) is { } grid ? ResolveDropTarget(grid,e.GetPosition(grid)) : (null,false);
    }
    private (ListBoxItem? Container,bool After) ResolveDropTarget(IconGridPanel grid,Point point)
    {
        if(IconList.Items.Count==0) return (null,false);
        int insertion=grid.InsertionIndex(point,IconList.Items.Count);
        if (insertion<0) return (null,false);
        bool after=insertion==IconList.Items.Count;
        int index=after ? insertion-1 : insertion;
        return (IconList.ItemContainerGenerator.ContainerFromIndex(index) as ListBoxItem,after);
    }
    private void Icon_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (_editing && _settings != null)
        {
            e.Handled=true; return;
        }
        if (ItemAt(e) is not { } icon || Window.GetWindow(this) is not MainWindow main) return;
        IconList.SelectedItem = icon; main.SuppressHide();
        try { ShowActionFailure(_service?.ShowShellContextMenu(icon, new System.Windows.Interop.WindowInteropHelper(main).Handle)); }
        finally { main.ResumeHide(); }
        e.Handled = true;
    }
    internal static bool IsContextMenuKey(Key key,ModifierKeys modifiers) =>
        (key==Key.Apps && modifiers==ModifierKeys.None) || (key==Key.F10 && modifiers==ModifierKeys.Shift);
}
