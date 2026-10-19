using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Nepply.Models;
using Nepply.Services;
using Nepply.Views;
using Nepply.Helpers;
using Nepply.Modules.Search;
using Nepply.Modules.Panel;
using Nepply.Modules.Settings;
using static Nepply.Helpers.NativeInterop;
namespace Nepply;

public partial class MainWindow : Window
{
    private readonly AppSettings _settings;
    private readonly Modules.WindowServices _services;
    private readonly DesktopIconService _icons;
    private DesktopUpdateCoordinator? _desktopUpdates;
    private readonly Services.Telemetry.IHardwareMonitor _monitor;
    private readonly DispatcherTimer _edgeTimer = new() { Interval = TimeSpan.FromMilliseconds(80) };
    private readonly DispatcherTimer _hideTimer = new();
    private readonly DispatcherTimer _displayTimer = new() {Interval=TimeSpan.FromMilliseconds(250)};
    private Modules.Search.SearchSession? _searchSession;
    private int _displayAttempts;
    private readonly AnimationService _animation;
    private readonly SearchReveal _searchReveal=new();
    internal bool AllowPreviewIsland { get; set; }
    private GlobalHotkeyService? _hotkey;
    private StartupCoordinator? _startup;
    private UserMessage? _hotkeyFailureNotice;
    private bool? _hotkeyRegistered;
    private DisplayScreen _screen = null!;
    private bool _started, _cleaned, _pinned;
    private readonly Modules.Panel.LayoutEditSession _layoutEdit = new();
    private bool _editingLayout => _layoutEdit.IsActive;
    private bool _finishingLayout;
    private bool _openingSettings;
    private bool _keyboardInteraction;
    private bool KeyboardInteractionActive => _keyboardInteraction && IsActive;
    private SettingsWindow? _settingsWindow;
    private AppSettings? _settingsPreview;
    private bool _skipPreviewTheme;
    private double? _previewGridOffset;
    private bool _previewCatalogChanged;
    private IReadOnlyList<DesktopIcon>? _previewDesktopSnapshot;
    private bool _previewHadDesktop;
    private int _previewRestoreGeneration;
    private static Modules.Desktop.DesktopScanOptions ScanOptions(AppSettings s) => new(s.ShowHiddenFiles,s.ShowSystemIcons,s.MergeEquivalentShortcuts);
    internal void PreviewSettings(AppSettings candidate)
    {
        if(_cleaned) return;
        if(_settingsPreview==null)
        {
            _previewRestoreGeneration++;
            _previewDesktopSnapshot=_icons.GetIcons();
            _previewHadDesktop=_icons.HasScanned;
        }
        _previewGridOffset ??= IconPanelView.GridOffset;
        var previous=_settingsPreview ?? _settings;
        var changes=SettingsChangeSet.Between(previous,candidate);
        if(changes.CanPreviewWithoutRebuild)
        {
            _settingsPreview=candidate.Clone();
            if(changes.Has(SettingsChange.Appearance))
            {
                ThemeService.Apply(candidate);
                _animation.RefreshMotionPreference();
                UpdateActionLabels();
            }
            _hideTimer.Interval=TimeSpan.FromMilliseconds(candidate.HideDelayMs);
            if(changes.Has(SettingsChange.Grid))
                IconPanelView.ConfigureLayout(candidate,_editingLayout);
            if(changes.Has(SettingsChange.Dimensions)
                && !TryPositionWindow(refreshScreen:false)) ScheduleDisplayRefresh();
            return;
        }
        if(changes.Has(SettingsChange.Desktop))
        {
            _previewCatalogChanged=true;
            _icons.Configure(ScanOptions(candidate));
            _icons.ScanDesktopIcons();
        }
        if(previous.ShowNetwork!=candidate.ShowNetwork || previous.NetworkInterfaceId!=candidate.NetworkInterfaceId || previous.MonitorUpdateIntervalMs!=candidate.MonitorUpdateIntervalMs || previous.EcoMode!=candidate.EcoMode || previous.SuspendMonitorWhenHidden!=candidate.SuspendMonitorWhenHidden)
        {
            var monitoring=_settings.Clone();
            monitoring.NetworkInterfaceId=candidate.NetworkInterfaceId;
            monitoring.ShowNetwork=candidate.ShowNetwork;
            monitoring.MonitorUpdateIntervalMs=candidate.MonitorUpdateIntervalMs;
            monitoring.EcoMode=candidate.EcoMode;
            monitoring.SuspendMonitorWhenHidden=candidate.SuspendMonitorWhenHidden;
            _monitor.Configure(monitoring,_animation.IsVisible);
        }
        _settingsPreview=candidate.Clone();
        _skipPreviewTheme=!changes.Has(SettingsChange.Appearance);
        try { ApplySettings(); PositionWindow(); UpdateLayout(); }
        finally { _skipPreviewTheme=false; }
    }
    internal void EndSettingsPreview()
    {
        _settingsPreview=null;
        if(_cleaned) return;
        ApplySettings(); PositionWindow(); UpdateLayout();
        if(_previewCatalogChanged && _previewDesktopSnapshot is {} snapshot)
            _icons.RestorePreviewCatalog(ScanOptions(_settings),snapshot,_previewHadDesktop);
        if(_previewGridOffset is {} offset)
        {
            IconPanelView.GridOffset=offset;
            int generation=_previewRestoreGeneration;
            Dispatcher.InvokeAsync(()=>
            {
                if(_cleaned || _settingsPreview!=null || generation!=_previewRestoreGeneration) return;
                UpdateLayout();IconPanelView.GridOffset=offset;
            },DispatcherPriority.ContextIdle);
        }
        _previewGridOffset=null;_previewDesktopSnapshot=null;
        if(_previewCatalogChanged) { _previewCatalogChanged=false; _icons.ScanDesktopIcons(); }
    }
    private LayoutDecisionWindow? _layoutDecision;
    private int _suppress;
    private readonly PointerActivationGate _edgeGate = new();
    internal Func<PointerContext?> ReadPointerContext { get; set; } = PointerContextReader.Read;
    private readonly bool _preview;
    private readonly bool _smokeTest;
    public MainWindow(bool preview = false, bool smokeTest = false, Services.Telemetry.IHardwareMonitor? monitor = null, DesktopIconService? icons = null)
        : this(Modules.WindowServices.Create(preview, smokeTest, monitor, icons), preview, smokeTest) { }
    internal MainWindow(Modules.WindowServices services, bool preview, bool smokeTest)
    {
        _services = services;
        _preview = preview; _smokeTest = smokeTest;
        _monitor = services.Monitor;
        _icons = services.Desktop;
        try
        {
        _settings = preview ? new() { HideDesktopIcons = false, HardwareSensorsEnabled = false } : AppSettings.Load();
        ThemeService.Apply(_settings);
        InitializeComponent();
        _animation = new(PanelTranslate, this, _settings, MainPanel);
        AddHandler(WidgetDrag.ReorderedEvent, new RoutedEventHandler((_, _) => { _settings.WidgetOrder = MonitorPanelView.GetWidgetOrder(); SaveLayout(); }));
        IconPanelView.LayoutChanged += (_, _) => SaveLayout();
        _animation.VisibilityChanged += visible => { _monitor.Configure(_settings, visible); MonitorPanelView.SetActive(visible && _settings.ShowMonitor); };
        _edgeTimer.Tick += EdgeTick; _hideTimer.Tick += HideTick;
        _displayTimer.Tick += DisplayRefreshTick;
        _searchSession = new(
            () => DesktopSearchButton.IsMouseOver || DesktopSearchEditor.IsMouseOver || DesktopSearchHasFocus || DesktopSearchBox.Text.Length > 0,
            PresentDesktopSearch);
        IsVisibleChanged += (_,e) => { if (!(bool)e.NewValue) { _searchSession.Suspend(DesktopSearchBox.Text.Length > 0); } };
        MouseEnter += (_, _) => _hideTimer.Stop();
        MouseLeave += (_, _) => ScheduleHide();
        Deactivated += (_,_) => { _keyboardInteraction=false; ScheduleHide(); };
        PreviewMouseDown += (_,e) =>
        {
            _keyboardInteraction=false;
            if(DesktopSearchEditor.Visibility==Visibility.Visible && SearchHitTest.IsBlank(e.OriginalSource as DependencyObject,this))
            {
                Keyboard.ClearFocus();
                _searchSession.DismissAndClear(DesktopSearchBox.Clear);
            }
            ScheduleHide();
        };
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplayChanged;
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += PreferenceChanged;
        LocalizationService.Changed += RefreshLanguage;
        }
        catch
        {
            Cleanup(); // App cannot dispose a constructor that never returned its window.
            throw;
        }
    }
    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_started) return; _started = true;
        IconPanelView.Initialize(_icons, _icons, _settings);
        MonitorPanelView.Initialize(_monitor);
        ApplySettings(); PositionWindow();
        if (_preview)
        {
            FooterState.SetResourceReference(TextBlock.TextProperty,"Loc.PanelPreview");
            if(AllowPreviewIsland)
            {
                _edgeTimer.Start();
                // A first ShowPanel can raise Loaded synchronously inside Show().
                // Never cancel that explicit reveal from initialization.
                if(!_animation.IsVisible) Dispatcher.BeginInvoke(new Action(()=>
                { if(!_cleaned && !_animation.IsVisible) _animation.HideInstantly(); }));
            }
            if (_smokeTest) { _icons.ScanDesktopIcons(); _monitor.Start(); _animation.HideInstantly(); ShowPanel(); }
            return;
        }
        try { _desktopUpdates = new(this,_icons); }
        catch (Exception ex) { AppLogger.Error(ex,"Start Desktop updates"); _icons.ScanDesktopIcons(); }
        _monitor.Start();
        if (AppSettings.RecoveryNotice is { } notice)
            MessageBox.Show(LocalizationService.Render(notice),"Nepply",MessageBoxButton.OK,MessageBoxImage.Information);
        _animation.HideInstantly();
        _edgeTimer.Start();
        RegisterHotkey();
        ApplyShellSettings(true,true,userInitiated:false);
        if (!_settings.HasCompletedOnboarding && AppSettings.RecoveryNotice == null)
            Dispatcher.BeginInvoke(ShowWelcome);
    }
    private void ShowWelcome()
    {
        if (_cleaned) return;
        SuppressHide();
        try
        {
            var welcome = new WelcomeWindow(_settings);
            if (welcome.ShowDialog() == true)
            {
                ApplySettings(); PositionWindow();
                ApplyShellSettings();
            }
            _animation.HideInstantly(); ShowPanel();
        }
        finally { ResumeHide(); }
    }
    private void RegisterHotkey()
    {
        _hotkey ??= new(new WindowInteropHelper(this).Handle, () =>
        {
            if (_openingSettings || _layoutDecision != null) OpenForInteraction();
            else if (_animation.IsVisible) HidePanel();
            else OpenForInteraction();
        });
        PresentHotkeyStatus(_hotkey.Register((uint)_settings.HotkeyModifiers, (uint)_settings.HotkeyKey));
    }
    internal void PresentHotkeyStatus(bool registered)
    {
        _hotkeyRegistered=registered;
        RefreshHotkeyText();
        if (!registered)
        {
            _hotkeyFailureNotice = UserMessage.FromKey("HotkeyUnavailableNotice",HotkeyLabel(_settings));
            IconPanelView.ShowActionFailure(_hotkeyFailureNotice);
        }
        else if (_hotkeyFailureNotice != null)
        {
            IconPanelView.DismissActionFailure(_hotkeyFailureNotice);
            _hotkeyFailureNotice = null;
        }
    }
    private void RefreshHotkeyText()
    {
        if(_hotkeyRegistered is not {} registered) return;
        string label=HotkeyLabel(_settings);
        HotkeyText.Text=registered ? label : string.Format(LocalizationService.Get("HotkeyUnavailableLabel"),label);
        HotkeyText.ToolTip=LocalizationService.Get(registered ? "HotkeyToggleHint" : "HotkeyChangeHint");
        System.Windows.Automation.AutomationProperties.SetName(HotkeyText,HotkeyText.Text);
    }
    private void RefreshLanguage()
    {
        if(_cleaned) return;
        UpdateActionLabels();
        // Refresh wording, not registration or notice visibility: a dismissed
        // warning must not reappear just because the language changed.
        RefreshHotkeyText();
        MonitorPanelView.RefreshLanguage();
    }
    public static string HotkeyLabel(AppSettings settings) =>
        HotkeyGesture.Format(settings.HotkeyModifiers,settings.HotkeyKey);
    public void ApplySettings()
    {
        var _settings = _settingsPreview ?? this._settings;
        _settings.Validate(); if(!_skipPreviewTheme) ThemeService.Apply(_settings); MainPanel.Opacity = 1;
        UpdateActionLabels();
        _animation.RefreshMotionPreference();
        _hideTimer.Interval = TimeSpan.FromMilliseconds(_settings.HideDelayMs);
        IconPanelView.ApplyIconSize(IconSizeOption.Medium);
        MonitorPanelView.ConfigureLayout(_settings, _editingLayout);
        MonitorPanelView.PreviewConfiguration(_settingsPreview);
        IconPanelView.ConfigureLayout(_settings, _editingLayout);
        if(_settingsPreview==null) _icons.Configure(new Modules.Desktop.DesktopScanOptions(_settings.ShowHiddenFiles,
            _settings.ShowSystemIcons, _settings.MergeEquivalentShortcuts));
        MonitorScroller.Visibility = ContentSeparator.Visibility = _settings.ShowMonitor ? Visibility.Visible : Visibility.Collapsed;
        ContentGrid.RowDefinitions.Clear(); ContentGrid.ColumnDefinitions.Clear();
        bool top = _settings.EdgePosition.IsHorizontal();
        PanelHeader.Margin = top ? new(24,12,20,10) : new(20,18,16,14);
        BrandTitle.Width = BrandTitle.Height = top ? 30 : 36;
        BrandUnderline.Visibility = Visibility.Collapsed;
        UpdateSearchHeader(_searchSession?.IsExpanded == true);
        PanelFooter.Padding = top ? new(20,3,20,5) : new(20,11,20,11);
        ContentGrid.ClipToBounds = true;
        if (_settings.EdgePosition.IsHorizontal())
        {
            double availableWidth = _screen == null ? _settings.TopPanelWidth : PanelDimensions.Resolve(_settings,_screen).Top;
            // Reconfiguration can rebuild columns without a SizeChanged event (e.g. swapping sections).
            if (ContentGrid.ActualWidth > 0) availableWidth = Math.Min(availableWidth, ContentGrid.ActualWidth);
            double monitorWidth = PanelGeometry.MonitorWidth(_settings.ShowMonitor, _settings.MonitorAreaWidth, availableWidth);
            ContentGrid.ColumnDefinitions.Add(new() { Width = _settings.IconsFirst ? new(1, GridUnitType.Star) : new(monitorWidth) });
            ContentGrid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            ContentGrid.ColumnDefinitions.Add(new() { Width = _settings.IconsFirst ? new(monitorWidth) : new(1, GridUnitType.Star) });
            Grid.SetRow(MonitorScroller, 0); Grid.SetColumn(MonitorScroller, _settings.IconsFirst ? 2 : 0);
            Grid.SetRow(ContentSeparator, 0); Grid.SetColumn(ContentSeparator, 1);
            Grid.SetRow(IconPanelView, 0); Grid.SetColumn(IconPanelView, _settings.IconsFirst ? 0 : 2);
            ContentSeparator.ResizeDirection = GridResizeDirection.Columns; ContentSeparator.Width = 8; ContentSeparator.Height = double.NaN; ContentSeparator.Margin = new(0, 6, 0, 16);
        }
        else
        {
            ContentGrid.RowDefinitions.Add(new() { Height = _settings.IconsFirst ? new(1, GridUnitType.Star) : new(_settings.ShowMonitor ? _settings.MonitorAreaHeight : 0), MinHeight = 0 });
            ContentGrid.RowDefinitions.Add(new() { Height = GridLength.Auto });
            ContentGrid.RowDefinitions.Add(new() { Height = _settings.IconsFirst ? new(_settings.ShowMonitor ? _settings.MonitorAreaHeight : 0) : new(1, GridUnitType.Star) });
            Grid.SetRow(MonitorScroller, _settings.IconsFirst ? 2 : 0); Grid.SetColumn(MonitorScroller, 0);
            Grid.SetRow(ContentSeparator, 1); Grid.SetColumn(ContentSeparator, 0);
            Grid.SetRow(IconPanelView, _settings.IconsFirst ? 0 : 2); Grid.SetColumn(IconPanelView, 0);
            ContentSeparator.ResizeDirection = GridResizeDirection.Rows; ContentSeparator.Width = double.NaN; ContentSeparator.Height = 8; ContentSeparator.Margin = new(20, 0, 20, 0);
        }
        ContentSeparator.IsEnabled = _editingLayout;
        if(_settingsPreview==null) _monitor.Configure(_settings, _animation.IsVisible);
        MonitorPanelView.SetActive(_animation.IsVisible && _settings.ShowMonitor);
    }
    public void PositionWindow()
    {
        if(!TryPositionWindow()) ScheduleDisplayRefresh();
    }
    private void ContentGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var _settings = _settingsPreview ?? this._settings;
        if (!_settings.EdgePosition.IsHorizontal() || ContentGrid.ColumnDefinitions.Count != 3) return;
        int column = _settings.IconsFirst ? 2 : 0;
        // Use actual layout width, including high-DPI constraints, not the saved preference.
        ContentGrid.ColumnDefinitions[column].Width = new(PanelGeometry.MonitorWidth(
            _settings.ShowMonitor, _settings.MonitorAreaWidth, e.NewSize.Width));
    }
    private bool TryPositionWindow(bool refreshScreen=true)
    {
        var _settings = _settingsPreview ?? this._settings;
        var availableScreen=!refreshScreen && _screen!=null ? _screen
            : ScreenManager.SelectAvailable(ScreenManager.GetScreens(),_settings.MonitorDevice);
        // Display topology may be temporarily empty during a reconnect. Keep
        // existing geometry until Windows reports usable bounds again.
        if(availableScreen==null) return false;
        _screen = availableScreen;
        double scale = _screen.Scale;
        var dimensions = PanelDimensions.Resolve(_settings,_screen);
        var placement = PanelGeometry.Calculate(_screen.Left, _screen.Top, _screen.Width, _screen.Height,
            scale, _settings.EdgePosition, dimensions.Side, dimensions.Top, dimensions.Height, _editingLayout);
        Width = placement.Width;
        Height = placement.Height;
        if (_settings.EdgePosition.IsHorizontal() && ContentGrid.ColumnDefinitions.Count == 3)
        {
            int column = _settings.IconsFirst ? 2 : 0;
            ContentGrid.ColumnDefinitions[column].Width = new(PanelGeometry.MonitorWidth(_settings.ShowMonitor, _settings.MonitorAreaWidth, Width));
        }
        int width = placement.PixelWidth, height = placement.PixelHeight;
        int x = placement.X, y = placement.Y;
        Left = x / scale; Top = y / scale;
        MainPanel.CornerRadius = new(26);
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero) ScreenManager.SetWindowPos(handle, IntPtr.Zero, x, y, width, height, 0x0014);
       
        return true;
    }
    public void ShowPanel()
    {
        if (_cleaned) return; _edgeGate.Disarm(); _hideTimer.Stop(); _animation.Show(); ScheduleHide();
    }
    public void OpenForInteraction()
    {
        if(_cleaned) return;
        if(_settingsWindow is { IsVisible:true }) { FocusDialog(_settingsWindow); return; }
        if(_layoutDecision is { IsVisible:true }) { FocusDialog(_layoutDecision); return; }
        ShowPanel();
        Activate();
        _keyboardInteraction=IsActive;
        if(_keyboardInteraction) _hideTimer.Stop();
        // Let the tray menu finish closing before moving focus into the dock.
        Dispatcher.BeginInvoke(DispatcherPriority.Input,new Action(() =>
        {
            if(_cleaned || !_animation.IsVisible || !KeyboardInteractionActive) return;
            IconPanelView.FocusSearch();
        }));
    }
    public void HidePanel() { if (_suppress == 0) { _keyboardInteraction=false; _edgeGate.Disarm(); _pinned = false; UpdateActionLabels(); _animation.Hide(); } }
    public void HidePanelInstantly() => _animation.HideInstantly();
    private void ScheduleHide() { if (_animation.IsVisible && !_pinned && _suppress == 0 && !KeyboardInteractionActive) { _hideTimer.Stop(); _hideTimer.Start(); } }
    private void HideTick(object? sender, EventArgs e)
    {
        if (!_animation.IsVisible) { _hideTimer.Stop(); return; }
        // A failed cursor read is not evidence that the user left the panel.
        if (!GetCursorPos(out var point)) { _hideTimer.Stop(); return; }
        // Transparent/rounded edges and moving visuals can report MouseLeave while
        // the pointer is still inside the dock. Poll stable screen bounds instead.
        // DispatcherTimer repeats: explicitly cancel or restart so an old false leave
        // cannot consume the delay and hide immediately on the next real leave.
        switch (AutoHidePolicy.Decide(_pinned, _suppress > 0, _animation.IsAnimating, PointerInsidePanel(point.X,point.Y),KeyboardInteractionActive))
        {
            case AutoHideAction.Cancel:
                _hideTimer.Stop();
                break;
            case AutoHideAction.RestartCountdown:
                _hideTimer.Stop();
                _hideTimer.Start();
                break;
            case AutoHideAction.Hide:
                _hideTimer.Stop();
                _animation.Hide();
                break;
        }
    }
    private void EdgeTick(object? sender, EventArgs e)
    {
        if (_cleaned || _screen == null || _displayTimer.IsEnabled) return;
        var context = ReadPointerContext();
        if (context is not {} point) { _edgeGate.Disarm(); return; }
        if (_animation.IsVisible && PointerActivationGate.ShouldYield(context, (uint)Environment.ProcessId,
            _suppress > 0 || _openingSettings || _editingLayout ||
            _settingsWindow is { IsVisible: true } || _layoutDecision != null || KeyboardInteractionActive))
        {
            _edgeGate.Disarm();
            _hideTimer.Stop();
            _keyboardInteraction = false;
            _animation.HideInstantly();
            return;
        }
        if (_animation.IsVisible)
        {
            bool inside=PointerInsidePanel(point.X,point.Y);
            if (inside || _pinned || _suppress > 0 || KeyboardInteractionActive) _hideTimer.Stop();
            else if (!_hideTimer.IsEnabled) ScheduleHide();
        }
        else _hideTimer.Stop();
        int zone = (int)Math.Ceiling(_settings.TriggerZoneSize * _screen.Scale);
        bool InZone(PointerContext sample, int size) => EdgeHitTest.Contains(_settings.EdgePosition,sample.X,sample.Y,
            _screen.Left,_screen.Top,_screen.Right,_screen.Bottom,size);
        bool insideMonitor = point.X >= _screen.Left && point.X < _screen.Right &&
            point.Y >= _screen.Top && point.Y < _screen.Bottom;
        if (_edgeGate.Update(context, insideMonitor, InZone(point, zone),
            InZone(point, zone + (int)Math.Ceiling(12 * _screen.Scale)),
            _animation.IsVisible || _animation.IsAnimating || _suppress > 0,
            Environment.TickCount64, _settings.TriggerDelayMs))
        {
            var current = ReadPointerContext();
            if (PointerActivationGate.CanCommit(point, current, current is {} latest && InZone(latest, zone))) ShowPanel();
            else _edgeGate.Disarm();
        }
    }
    private bool PointerInsidePanel(int screenX,int screenY)
    {
        if (!_animation.IsVisible) return false;
        var local = PointFromScreen(new Point(screenX, screenY));
        // The 12-DIP floating gap is a transit corridor, not a mouse leave.
        double left=_settings.EdgePosition==EdgePosition.Left ? -16 : -4;
        double right=_settings.EdgePosition==EdgePosition.Right ? ActualWidth+16 : ActualWidth+4;
        double top=_settings.EdgePosition==EdgePosition.Top ? -16 : -4;
        double bottom=_settings.EdgePosition==EdgePosition.Bottom ? ActualHeight+16 : ActualHeight+4;
        return local.X >= left && local.Y >= top && local.X <= right && local.Y <= bottom;
    }
    private void DisplayChanged(object? sender, EventArgs e) => Dispatcher.InvokeAsync(() =>
    {
        if(_cleaned) return;
        _edgeGate.Disarm(); _hideTimer.Stop();
        ScheduleDisplayRefresh();
    });
    private void ScheduleDisplayRefresh()
    {
        if(_cleaned) return;
        _displayAttempts=4;
        _displayTimer.Stop(); _displayTimer.Start();
    }
    private void DisplayRefreshTick(object? sender,EventArgs e)
    {
        _displayTimer.Stop();
        if(_cleaned) return;
        if(TryPositionWindow())
        {
            bool keepOpen=_animation.IsVisible && (_pinned || _editingLayout || KeyboardInteractionActive);
            if(keepOpen) _animation.RefreshGeometry();
            else _animation.HideInstantly();
        }
        else if(--_displayAttempts>0) _displayTimer.Start();
    }
    private void PreferenceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e) => Dispatcher.InvokeAsync(() => { if (!_cleaned) { ThemeService.Apply(_settingsPreview ?? _settings); MainPanel.Opacity=1; _animation.RefreshMotionPreference(); } });
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if(e.Key==Key.Escape && e.IsRepeat) { e.Handled=true; return; }
        if(IsActive && IsKeyboardFocusWithin) { _keyboardInteraction=true; _hideTimer.Stop(); }
        if (e.Key == Key.Escape)
        {
            if (_editingLayout) FinishLayout(false);
            else if (!IconPanelView.ClearFocusedSearch())
            {
                if (DesktopSearchEditor.Visibility==Visibility.Visible && DesktopSearchBox.Text.Length==0)
                { DesktopSearchButton.Focus(); SetDesktopSearchExpanded(false); }
                else HidePanel();
            }
            e.Handled=true;
        }
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { IconPanelView.FocusSearch(); e.Handled = true; }
    }
    private void SaveLayout()
    {
        if (_editingLayout || _finishingLayout) return;
        try { if (!_preview) _settings.Save(); }
        catch (Exception ex) { AppLogger.Error(ex,"Save layout"); MessageBox.Show(LocalizationService.Get("LayoutSaveError") + LocalizationService.Render(UserMessage.From(ex))); }
    }
    private void EditLayout_Click(object sender, RoutedEventArgs e)
    {
        if (_editingLayout) { FinishLayout(true); return; }
        _layoutEdit.Begin(_settings);
        SuppressHide();
        LayoutActions.Visibility = Visibility.Visible;
        EditLayoutButton.SetResourceReference(Control.ForegroundProperty,_editingLayout ? "AccentBrush" : "MutedBrush");
        FooterState.Visibility = _editingLayout ? Visibility.Visible : Visibility.Collapsed;
        FooterState.SetResourceReference(TextBlock.TextProperty,"Loc.LayoutEditingHint");
        ApplySettings(); PositionWindow();
    }
    private void DoneLayout_Click(object sender,RoutedEventArgs e) => FinishLayout(true);
    private void SwapSections_Click(object sender,RoutedEventArgs e)
    {
        if (!_editingLayout) return;
        _settings.IconsFirst = !_settings.IconsFirst;
        ApplySettings();
    }
    private void CancelLayout_Click(object sender,RoutedEventArgs e) => FinishLayout(false);
    public void FinishLayout(bool commit)
    {
        if (!_editingLayout) return;
        _finishingLayout = true;
        try
        {
            try { _layoutEdit.Complete(commit, settings => { if (!_preview) settings.Save(); }); }
            catch(Exception ex) { AppLogger.Error(ex,"Commit layout"); FooterState.SetResourceReference(TextBlock.TextProperty,"Loc.LayoutCommitError"); return; }
            LayoutActions.Visibility=Visibility.Collapsed;
            FooterState.Visibility=Visibility.Collapsed;
            EditLayoutButton.SetResourceReference(Control.ForegroundProperty,"MutedBrush");
            ApplySettings(); PositionWindow(); IconPanelView.RefreshLayout();
        }
        finally { _finishingLayout=false; if (!_editingLayout) ResumeHide(); }
    }
    private void ResizeLayout_End(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if (!_settings.EdgePosition.IsHorizontal()) _settings.MonitorAreaHeight = (int)MonitorScroller.ActualHeight;
        else _settings.MonitorAreaWidth = (int)MonitorScroller.ActualWidth;
        SaveLayout();
    }
    public bool IsPinned => _pinned;
    public void TogglePin() { _pinned = !_pinned; UpdateActionLabels(); if (_pinned) _hideTimer.Stop(); else ScheduleHide(); }
    private void Pin_StateChanged(object sender, RoutedEventArgs e)
    {
        // Toggle automation changes IsChecked without raising Click.
        // External commands synchronize the control below without toggling twice.
        if ((PinButton.IsChecked == true) != _pinned) TogglePin();
    }
    private void UpdateActionLabels()
    {
        string pinLabel=LocalizationService.Get(_pinned ? "Unpin" : "PinTip");
        string editLabel=LocalizationService.Get(_editingLayout ? "SaveLayout" : "EditLayout");
        PinButton.ToolTip=pinLabel;
        PinButton.IsChecked=_pinned;
        EditLayoutButton.ToolTip=editLabel;
        System.Windows.Automation.AutomationProperties.SetName(PinButton,pinLabel);
        System.Windows.Automation.AutomationProperties.SetItemStatus(PinButton,LocalizationService.Get(_pinned ? "Pinned" : "AutoHide"));
        System.Windows.Automation.AutomationProperties.SetName(EditLayoutButton,editLabel);
        System.Windows.Automation.AutomationProperties.SetItemStatus(EditLayoutButton,_editingLayout ? LocalizationService.Get("EditingStatus") : "");
    }
    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshIcons();
    public void RefreshIcons() => _icons.ScanDesktopIcons();
    private void Settings_Click(object sender, RoutedEventArgs e) => OpenSettings();
    public void OpenSettings()
    {
        Modules.Updates.PreparedUpdate? pendingUpdate = null;
        if(_cleaned) return;
        if(_openingSettings)
        {
            FocusDialog(_settingsWindow ?? (Window?)_layoutDecision);
            return;
        }
        _openingSettings=true;
        try
        {
            if (!ConfirmLeaveLayout()) return;
            SuppressHide();
            try
            {
            var snapshot = _monitor.GetSnapshot();
            var settings = new SettingsWindow(_settings, snapshot.Hardware.Gpus, snapshot) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterScreen };
            _settingsWindow=settings;
            settings.PreviewChanged+=PreviewSettings;
            settings.SettingsCommitted+=SettingsApplied;
            settings.PreviewViewport=()=>IconPanelView.GridViewport;
            ShowPanel();
            settings.ShowDialog();
            pendingUpdate = settings.PendingUpdate;
            }
            finally { EndSettingsPreview(); _settingsWindow=null; ResumeHide(); }
        }
        finally { _openingSettings=false; }
        if (pendingUpdate != null)
        {
            using (pendingUpdate)
            {
                try
                {
                    if (!ConfirmExit()) return;
                    pendingUpdate.Start();
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "Start update installer");
                    IconPanelView.ShowActionFailure(UserMessage.FromKey("UpdateFailed"));
                    return;
                }
                Cleanup();
                Application.Current.Shutdown();
            }
        }
    }
    private void SettingsApplied()
    {
        if(_cleaned) return;
        var changes=_settingsWindow?.LastCommittedChanges ?? new SettingsChangeSet(
            SettingsChange.Hotkey | SettingsChange.AutoStart | SettingsChange.DesktopVisibility | SettingsChange.Desktop);
        _settingsPreview=null;
        _previewRestoreGeneration++;
        _previewGridOffset=null; _previewDesktopSnapshot=null; _previewCatalogChanged=false;
        ApplySettings(); PositionWindow();
        if(!_preview)
        {
            if(changes.Has(SettingsChange.Hotkey)) RegisterHotkey();
            if(changes.Has(SettingsChange.AutoStart | SettingsChange.DesktopVisibility))
                ApplyShellSettings(changes.Has(SettingsChange.AutoStart),changes.Has(SettingsChange.DesktopVisibility));
            if(changes.Has(SettingsChange.Desktop)) _icons.ScanDesktopIcons();
        }
    }
    private static void FocusDialog(Window? dialog)
    {
        if(dialog==null || !dialog.IsVisible) return;
        if(dialog.WindowState==WindowState.Minimized) dialog.WindowState=WindowState.Normal;
        dialog.Activate();
    }
    public void ApplyShellSettings() => ApplyShellSettings(true,true);
    private void ApplyShellSettings(bool autoStart,bool desktopVisibility,bool userInitiated=true)
    {
        if (_preview) return;
        if (autoStart) _ = ApplyStartupAsync(_settings.AutoStart,userInitiated);
        if (desktopVisibility) _ = ApplyDesktopVisibilityAsync(_settings.HideDesktopIcons);
    }
    private async Task ApplyDesktopVisibilityAsync(bool hide)
    {
        try
        {
            var notice=await _icons.SetDesktopIconsHiddenAsync(hide);
            if(!_cleaned && notice is not null) IconPanelView.ShowActionFailure(notice);
        }
        catch(Exception ex)
        {
            AppLogger.Error(ex,"Apply Desktop visibility");
            if(!_cleaned) IconPanelView.ShowActionFailure(UserMessage.FromKey("DesktopVisibilityError"));
        }
    }
    public void SuppressHide() { _suppress++; _hideTimer.Stop(); }
    private async Task ApplyStartupAsync(bool requested,bool userInitiated)
    {
        var settingsView=_settingsWindow;
        int revision=settingsView?.AutoStartRevision ?? 0;
        try
        {
            _startup ??= new StartupCoordinator(ProcessPackageIdentity.IsPackaged
                ? new PackagedStartupBackend() : new RegistryStartupBackend());
            var outcome=await _startup.ApplyAsync(requested,userInitiated);
            if (_cleaned || outcome==null) return;
            if (outcome.Enabled is bool actual)
            {
                bool changed=_settings.AutoStart!=actual;
                _settings.AutoStart=actual;
                _settingsWindow?.SynchronizeAutoStart(requested,actual,ReferenceEquals(settingsView,_settingsWindow) ? revision : 0);
                if(changed) _settings.Save();
            }
            if(outcome.Notice!=null) IconPanelView.ShowActionFailure(outcome.Notice);
        }
        catch(Exception ex)
        {
            AppLogger.Error(ex,"Autostart");
            if(!_cleaned) IconPanelView.ShowActionFailure(UserMessage.FromKey("AutoStartUpdateError"));
        }
    }
    public bool ConfirmExit()
    {
        // Tray actions remain reachable while a modal settings dialog is open.
        // Do not shut down behind it and silently discard the user's draft.
        if (_settingsWindow != null)
        {
            _settingsWindow.ShowExitGuidance();
            FocusDialog(_settingsWindow);
            return false;
        }
        if (_openingSettings)
        {
            FocusDialog(_layoutDecision);
            return false;
        }
        return ConfirmLeaveLayout();
    }
    public bool ConfirmLeaveLayout()
    {
        if(_layoutDecision!=null) { FocusDialog(_layoutDecision); return false; }
        if(!_editingLayout) return true;
        if(!_layoutEdit.HasChanges)
        { FinishLayout(false); return true; }
        SuppressHide();
        try
        {
            ShowPanel();
            var decision=new LayoutDecisionWindow {Owner=this};
            _layoutDecision=decision;
            if(decision.ShowDialog()!=true) return false;
            FinishLayout(decision.SaveRequested);
            // Saving can fail: remain in the editor instead of losing the draft.
            return !_editingLayout;
        }
        finally { _layoutDecision=null; ResumeHide(); }
    }
    public void ResumeHide() { _suppress = Math.Max(0, _suppress - 1); ScheduleHide(); }
    public void Cleanup()
    {
        if (_cleaned) return; _cleaned = true;
        _startup?.Dispose();
        _edgeTimer.Stop(); _hideTimer.Stop(); _displayTimer.Stop(); _searchSession?.Dispose();
        if (DesktopSearchEditor != null) _searchReveal.Settle(DesktopSearchEditor, false);
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplayChanged;
        Microsoft.Win32.SystemEvents.UserPreferenceChanged -= PreferenceChanged;
        LocalizationService.Changed -= RefreshLanguage;
        CleanupSequence.Run(
            ("Layout draft",()=>_layoutEdit.Complete(false, _ => { })),
            ("Desktop updates",()=>_desktopUpdates?.Dispose()),
            ("Global hotkey",()=>_hotkey?.Dispose()),
            ("Icon view",()=>IconPanelView?.Cleanup()),
            ("Monitor view",()=>MonitorPanelView?.Cleanup()),
            ("Owned window services",_services.Dispose));
    }
    protected override void OnClosed(EventArgs e) { Cleanup(); base.OnClosed(e); }
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_cleaned && !_preview) { e.Cancel = true; HidePanel(); }
        base.OnClosing(e);
    }
    public HardwareInfoSnapshot CurrentSnapshot => _monitor.GetSnapshot();
    internal bool DesktopSearchHasFocus => DesktopSearchBox.IsKeyboardFocusWithin;
    public void FocusDesktopSearch()
    {
        SetDesktopSearchExpanded(true);
        DesktopSearchBox.Focus(); DesktopSearchBox.SelectAll();
    }
    private void SetDesktopSearchExpanded(bool expanded)
    {
        if (_cleaned) return;
        _searchSession?.SetExpanded(expanded);
    }
    private void PresentDesktopSearch(bool expanded)
    {
        UpdateSearchHeader(expanded);
        _searchReveal.Set(DesktopSearchEditor,DesktopSearchButton,expanded);
        DesktopSearchButton.SetResourceReference(Control.ForegroundProperty,expanded ? "AccentBrush" : "MutedBrush");
        if(expanded) DesktopSearchButton.SetResourceReference(System.Windows.Automation.AutomationProperties.ItemStatusProperty,"Loc.SearchOpenStatus");
        else DesktopSearchButton.ClearValue(System.Windows.Automation.AutomationProperties.ItemStatusProperty);
    }
    private void PanelHeader_SizeChanged(object sender, SizeChangedEventArgs e)
        => UpdateSearchHeader(_searchSession?.IsExpanded == true);
    private void UpdateSearchHeader(bool expanded)
    {
        if (BrandTitle == null || RefreshDesktopButton == null) return;
        BrandTitle.Visibility = expanded ? Visibility.Hidden : Visibility.Visible;
        // Keep settings and the search trigger available on narrow islands.
        // Secondary actions return when search closes; wide headers keep all actions.
        var secondary = expanded && PanelHeader.ActualWidth < 420 ? Visibility.Collapsed : Visibility.Visible;
        EditLayoutButton.Visibility = PinButton.Visibility = RefreshDesktopButton.Visibility = secondary;
    }
    private void DesktopSearch_Click(object sender,RoutedEventArgs e) => FocusDesktopSearch();
    private void DesktopSearch_Enter(object sender,MouseEventArgs e) => SetDesktopSearchExpanded(true);
    private void DesktopSearch_Leave(object sender,MouseEventArgs e) => _searchSession?.RequestDismiss();
    private void DesktopSearch_Focused(object sender,KeyboardFocusChangedEventArgs e) => SetDesktopSearchExpanded(true);
    private void DesktopSearch_Unfocused(object sender,KeyboardFocusChangedEventArgs e) => _searchSession?.RequestDismiss();
    private void DesktopSearch_Changed(object sender,TextChangedEventArgs e)
    {
        if (DesktopSearchEditor==null || DesktopSearchButton==null) return;
        if (DesktopSearchBox.Text.Length>0) SetDesktopSearchExpanded(true);
        else _searchSession?.RequestDismiss();
    }
    private void DesktopSearch_Clear(object sender,RoutedEventArgs e) { DesktopSearchBox.Clear(); FocusDesktopSearch(); }
    private void DesktopSearch_KeyDown(object sender,KeyEventArgs e) => IconPanelView.HandleSearchKey(e);
    public int DesktopItemCount => _icons.GetIcons().Count;
    internal Modules.Desktop.DesktopScanState CurrentDesktopScanState => _icons.ScanState;
    internal string? DesktopScanDiagnostic => _icons.ScanFailureDiagnostic;
    public AppSettings Settings => _settings;
}
