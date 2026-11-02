using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Nepply.Helpers;
using Nepply.Models;
using Nepply.Modules.Search;
namespace Nepply.Views;

public partial class SettingsWindow : Window
{
    private readonly Modules.Settings.SettingsEditSession _settingsEdit;
    private AppSettings _draft { get => _settingsEdit.Draft; set => _settingsEdit.ReplaceDraft(value); }
    private bool _recording;
    private bool _loadingDraft;
    private UserMessage? _errorMessage;
    private void SetError(UserMessage? message) { _errorMessage = message; ErrorText.Text = Services.LocalizationService.Render(message); }
    private void SetOperationError(string key, Exception error)
    {
        Services.AppLogger.Error(error, "Settings operation: " + key);
        SetError(UserMessage.Join([UserMessage.FromKey(key), UserMessage.From(error)], ""));
    }
    private EdgePosition _gridEdge;
    private void StoreGrid()
    {
        if (_gridEdge.IsHorizontal()) { _draft.TopGridColumns = GridPicker.Columns; _draft.TopGridRows = GridPicker.Rows; }
        else { _draft.GridColumns = GridPicker.Columns; _draft.GridRows = GridPicker.Rows; }
    }
    private void LoadGrid(EdgePosition edge)
    {
        _gridEdge = edge;
        GridPicker.Columns = edge.IsHorizontal() ? _draft.TopGridColumns : _draft.GridColumns;
        GridPicker.Rows = edge.IsHorizontal() ? _draft.TopGridRows : _draft.GridRows;
        GridContext.Text = Services.LocalizationService.Get(edge.IsHorizontal() ? "TopGrid" : "SideGrid");
    }
    private bool _colorSync;
    private readonly Modules.Search.SearchSession _searchSession;
    private bool _searchExpanded => _searchSession?.IsExpanded == true;
    private readonly SearchReveal _searchReveal = new();
    private readonly MaterialLightScope _material;
    private int _sectionAnimationGeneration;
    private System.Windows.Threading.DispatcherOperation? _searchFocusOperation;
    private readonly HardwareInfoSnapshot? _diagnosticSnapshot;
    public sealed record Choice(string Id, string Name)
    {
        internal UserMessage? LabelMessage { get; init; }
        internal Choice Localized() => LabelMessage == null ? this : this with { Name = Services.LocalizationService.Render(LabelMessage) };
        internal static Choice FromMessage(string id, UserMessage message) => new Choice(id, Services.LocalizationService.Render(message)) { LabelMessage = message };
        public override string ToString() => Name;
    }
    public SettingsWindow(AppSettings settings, IEnumerable<GpuReading>? gpus = null, HardwareInfoSnapshot? snapshot = null)
        : this(settings, candidate => candidate.Save(), gpus, snapshot) { }
    internal SettingsWindow(AppSettings settings, Action<AppSettings> persist, IEnumerable<GpuReading>? gpus = null, HardwareInfoSnapshot? snapshot = null)
    {
        _diagnosticSnapshot = snapshot;
        _settingsEdit = new(settings, persist);
        InitializeComponent();
#if DEBUG
        DeveloperDiagnostics.Visibility = Visibility.Visible;
#endif
        _material = new(SettingsContent);
        SettingsContent.SetValue(MetallicSurface.LightScopeProperty, _material);
        _searchSession = new(
            () => SettingsSearchHost.IsMouseOver || SearchSettings.IsKeyboardFocusWithin || SearchResults.IsKeyboardFocusWithin || SearchSettings.Text.Length > 0,
            PresentSettingsSearch);
        PresetCards.ItemsSource = Services.LayoutPresets.All;
        EdgeChoice.ItemsSource = Enum.GetNames<EdgePosition>().Select(Services.LocalizationService.Get).ToArray();
        ThemeChoice.ItemsSource = new[] { "Dark", "Light", "System" }.Select(Services.LocalizationService.Get).ToArray();
        MotionChoice.ItemsSource = new[] { "Smooth", "Quick", "Off" }.Select(Services.LocalizationService.Get).ToArray();
        AccentChoice.ItemsSource = Services.ThemeService.Palettes.Keys.ToArray();
        GpuModeChoice.ItemsSource = new[] { "AllGpu", "SelectedGpu", "SelectedIGpu" }.Select(Services.LocalizationService.Get).ToArray();
        var gpuChoices = new List<Choice> { new("", Services.LocalizationService.Get("ChooseDevice")) };
        gpuChoices.AddRange((gpus ?? []).Select(g => new Choice(g.Id, g.Name)));
        foreach (var id in new[] { settings.GpuIdentifier, settings.IntegratedGpuIdentifier })
            if (id.Length > 0 && !gpuChoices.Any(g => g.Id == id)) gpuChoices.Add(Choice.FromMessage(id, UserMessage.FromKey("DisconnectedDevice", id)));
        GpuChoice.ItemsSource = gpuChoices; IntegratedGpuChoice.ItemsSource = gpuChoices;
        PaletteSwatches.ItemsSource = Services.ThemeService.Palettes.Where(p => p.Key != "Custom" && p.Key != "Blue" && p.Key != "Violet" && p.Key != "Jade").Select(p => new Palette(p.Key, p.Value)).ToArray();
        ScreenChoice.ItemsSource = BuildScreenChoices(ScreenManager.GetScreens(), settings.MonitorDevice);
        var adapters = new List<Choice> { new("", Services.LocalizationService.Get("AutoNetwork")) };
        try { adapters.AddRange(NetworkInterface.GetAllNetworkInterfaces().Select(n => new Choice(n.Id, n.Name))); }
        catch (NetworkInformationException ex) { Services.AppLogger.Error(ex, "Enumerate network choices"); }
        if (!string.IsNullOrEmpty(settings.NetworkInterfaceId) && !adapters.Any(a => a.Id == settings.NetworkInterfaceId))
            adapters.Add(new(settings.NetworkInterfaceId, settings.NetworkInterfaceId));
        NetworkChoice.ItemsSource = adapters;
        LanguageChoice.ItemsSource = Services.LocalizationService.Languages;
        Services.LocalizationService.Apply(settings.Language);
        Services.LocalizationService.Changed += RefreshLanguageChoices;
        RefreshLanguageChoices();
        Closed += (_, _) => { Services.LocalizationService.Changed -= RefreshLanguageChoices; Services.LocalizationService.Apply(settings.Language); };
        LoadDraft();
        AutoStart.Checked += (_, _) => { if (!_loadingDraft) AutoStartRevision++; RefreshAutoStartState(); };
        AutoStart.Unchecked += (_, _) => { if (!_loadingDraft) AutoStartRevision++; RefreshAutoStartState(); };
        // Re-read on return, because the switch that clears this lives in Task Manager.
        Activated += (_, _) => RefreshAutoStartState();
        EdgeChoice.SelectionChanged += (_, _) => { if (_loadingDraft) return; StoreGrid(); LoadGrid((EdgePosition)Math.Max(0, EdgeChoice.SelectedIndex)); };
        ShowSettingsSection(0);
        SetAccessibleNames();
        ConfigureSliders();
        InitializePreview();
        Closed += (_, _) => { _material.Dispose(); _searchSession.Dispose(); _searchReveal.Settle(SearchSettings, false); CancelSearchFocus(); ClearSectionAnimations(); };
        IsVisibleChanged += (_, e) => { if (!(bool)e.NewValue) _searchSession.Suspend(SearchSettings.Text.Length > 0); };
        PreviewMouseDown += (_, e) =>
        {
            if (_searchExpanded && SearchHitTest.IsBlank(e.OriginalSource as DependencyObject, this))
            { Keyboard.ClearFocus(); _searchSession.DismissAndClear(SearchSettings.Clear); }
        };
        Loaded += (_, _) => DialogPlacement.FitInitial(this);
        SizeChanged += (_, e) => ApplyCompactLayout(e.NewSize.Height);
    }
    internal void ApplyCompactLayout(double height)
    {
        bool compact = height < 480;
        TitleArea.Margin = compact ? new Thickness(16, 6, 16, 4) : new Thickness(26, 20, 18, 18);
        SettingsTitle.FontSize = compact ? 18 : 25;
        SearchArea.Margin = compact ? new Thickness(16, 0, 16, 4) : new Thickness(26, 0, 26, 12);
        SettingsNavigation.Margin = compact ? new Thickness(16, 0, 16, 4) : new Thickness(26, 0, 22, 14);
        ActionArea.Padding = compact ? new Thickness(16, 4, 16, 4) : new Thickness(26, 16, 26, 16);
        SearchResults.MaxHeight = compact ? 60 : 110;
        SettingsSearchHost.Padding = compact ? new Thickness(0) : new Thickness(3);
        ApplyNavigationLayout(SettingsBody.ActualWidth);
    }
    private void SettingsBody_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyNavigationLayout(e.NewSize.Width);
    private void ApplyNavigationLayout(double width)
    {
        bool rail = width >= 760;
        SettingsBody.ColumnDefinitions[0].Width = new(rail ? 170 : 0);
        System.Windows.Controls.Grid.SetColumn(SettingsNavigation, rail ? 0 : 1);
        System.Windows.Controls.Grid.SetRowSpan(SettingsNavigation, rail ? 2 : 1);
        System.Windows.Controls.Grid.SetRow(SettingsScroll, rail ? 1 : 2);
        System.Windows.Controls.Grid.SetRowSpan(SettingsScroll, rail ? 2 : 1);
        System.Windows.Controls.Grid.SetRow(SearchOverlayLayer, rail ? 1 : 2);
        System.Windows.Controls.Grid.SetRowSpan(SearchOverlayLayer, rail ? 2 : 1);
        SearchOverlayLayer.Margin = rail ? new Thickness(0, 0, 22, 0) : new Thickness(16, 0, 16, 0);
        SettingsNavigation.ItemsPanel = (System.Windows.Controls.ItemsPanelTemplate)FindResource(rail ? "SettingsRailPanel" : "SettingsTabsPanel");
        SettingsNavigation.Tag = rail ? "Rail" : "Tabs"; // Selects the bar or underline indicator.
        SettingsNavigation.Margin = rail ? new Thickness(20, 0, 8, 0) : new Thickness(16, 0, 16, 4);
        SettingsContent.Margin = rail ? new Thickness(0, 0, 22, 0) : new Thickness(16, 0, 16, 0);
    }
    internal static Choice[] BuildScreenChoices(IEnumerable<DisplayScreen> screens, string selectedDevice)
    {
        var choices = new List<Choice> { Choice.FromMessage("", UserMessage.FromKey("PrimaryScreen")) };
        choices.AddRange(screens.DistinctBy(s => s.Device, StringComparer.OrdinalIgnoreCase).Select(s => s.Primary
            ? Choice.FromMessage(s.Device, UserMessage.FromKey("PrimaryDisplayLabel", s.Device.Replace(@"\\.\", "")))
            : new Choice(s.Device, s.Device.Replace(@"\\.\", ""))));
        if (!string.IsNullOrEmpty(selectedDevice))
        {
            int index = choices.FindIndex(c => string.Equals(c.Id, selectedDevice, StringComparison.OrdinalIgnoreCase));
            if (index < 0) choices.Add(Choice.FromMessage(selectedDevice, UserMessage.FromKey("DisconnectedDevice", selectedDevice.Replace(@"\\.\", ""))));
            else choices[index] = choices[index] with { Id = selectedDevice };
        }
        return choices.ToArray();
    }
    private void ConfigureSliders()
    {
        // Keyboard steps are independent from pointer movement: retain continuous
        // dragging while making arrows and Page Up/Down useful at each scale.
        (System.Windows.Controls.Slider Slider, double Small, double Large)[] sliders = [
            (GlowSlider,1,5),(OpacitySlider,1,5),
            (WidthSlider,1,5),(TopWidthSlider,1,5),(HeightSlider,1,5),
            (ZoneSlider,1,4),(DwellSlider,10,100),(DelaySlider,50,250)];
        foreach (var (slider, small, large) in sliders)
        {
            slider.SmallChange = small; slider.LargeChange = large;
            slider.AutoToolTipPlacement = System.Windows.Controls.Primitives.AutoToolTipPlacement.TopLeft;
            slider.AutoToolTipPrecision = 0;
        }
    }
    private void SetAccessibleNames()
    {
        (UIElement Control, string Name)[] fields = [
            (ThemeChoice,"Theme"),(AccentChoice,"Materials"),(CustomAccent,"CustomColor"),(ColorButton,"ColorPicker"),
            (MotionChoice,"Motion"),(GlowSlider,"Glow"),(OpacitySlider,"Opacity"),(EdgeChoice,"Edge"),(ScreenChoice,"Screen"),
            (WidthSlider,"Width"),(TopWidthSlider,"TopWidth"),(HeightSlider,"TopHeight"),
            (ZoneSlider,"TriggerZone"),(DwellSlider,"TriggerDelay"),(DelaySlider,"HideDelay"),
            (HotkeyButton,"Hotkey"),(GridPicker,"IconGrid"),
            (GpuModeChoice,"Device"),(GpuChoice,"GPU"),
            (IntegratedGpuChoice,"iGPU"),(IntervalSlider,"Interval"),(NetworkChoice,"NetworkAdapter")];
        foreach (var field in fields) System.Windows.Automation.AutomationProperties.SetName(field.Control, Services.LocalizationService.Get(field.Name));
        System.Windows.Automation.AutomationProperties.SetName(LanguageChoice, Services.LocalizationService.Get("Language"));
        System.Windows.Automation.AutomationProperties.SetName(NeonEnabled, Services.LocalizationService.Get("Neon"));
    }
    private void SettingsNavigation_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (SettingsScroll == null || ApplicationSection == null) return;
        ShowSettingsSection(SettingsNavigation.SelectedIndex);
    }
    private void SearchSettings_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        CancelSearchFocus();
        if (SearchResults == null || SearchHint == null) return;
        if (SearchSettings.Text.Length > 0) ExpandSettingsSearch(true);
        RefreshSearchResults();
    }
    private void RefreshSearchResults()
    {
        string? selected = (SearchResults.SelectedItem as Services.SettingsSearch.Entry)?.Control;
        var results = Services.SettingsSearch.Find(SearchSettings.Text);
        SearchResults.ItemsSource = results;
        if (selected != null) SearchResults.SelectedItem = results.FirstOrDefault(result => result.Control == selected);
        SearchResults.Visibility = results.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        SearchHint.Text = string.IsNullOrWhiteSpace(SearchSettings.Text) ? Services.LocalizationService.Get("SearchSettingsTip") : results.Length == 0 ? Services.LocalizationService.Get("NoResults") + " · " + Services.LocalizationService.Get("SearchHint") : string.Format(Services.LocalizationService.Get("SearchResults"), results.Length);
        SearchHint.Visibility = string.IsNullOrWhiteSpace(SearchSettings.Text) ? Visibility.Collapsed : Visibility.Visible;
        if (SearchOverlayLayer != null) SearchOverlayLayer.Visibility = string.IsNullOrWhiteSpace(SearchSettings.Text) ? Visibility.Collapsed : Visibility.Visible;
    }
    private void SettingsSearchTrigger_Click(object sender, RoutedEventArgs e) => FocusSettingsSearch();
    private void FocusSettingsSearch() { ExpandSettingsSearch(true); SearchSettings.Focus(); SearchSettings.SelectAll(); }
    private void SearchHost_Enter(object sender, MouseEventArgs e) => ExpandSettingsSearch(true);
    private void SearchHost_Leave(object sender, MouseEventArgs e) => _searchSession.RequestDismiss();
    private void SearchSettings_Unfocused(object sender, KeyboardFocusChangedEventArgs e) => _searchSession.RequestDismiss();
    private void ExpandSettingsSearch(bool expanded)
    {
        _searchSession?.SetExpanded(expanded);
    }
    private void PresentSettingsSearch(bool expanded)
    {
        _searchReveal.Set(SearchSettings, SettingsSearchTrigger, expanded);
        SettingsSearchTrigger.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, expanded ? "AccentBrush" : "MutedBrush");
        if (expanded) SettingsSearchTrigger.SetResourceReference(System.Windows.Automation.AutomationProperties.ItemStatusProperty, "Loc.SearchOpenStatus");
        else SettingsSearchTrigger.ClearValue(System.Windows.Automation.AutomationProperties.ItemStatusProperty);
    }
    private void SearchSettings_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && SearchResults.Items.Count > 0) { SearchResults.SelectedIndex = 0; ActivateSearchSelection(); e.Handled = true; }
        else if (e.Key == Key.Down && SearchResults.Items.Count > 0)
        {
            SearchResults.SelectedIndex = 0; SearchResults.UpdateLayout();
            if (SearchResults.ItemContainerGenerator.ContainerFromIndex(0) is System.Windows.Controls.ListBoxItem item) item.Focus();
            e.Handled = true;
        }
    }
    private void SearchResults_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { ActivateSearchSelection(); e.Handled = true; }
    }
    private void SearchResult_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: Services.SettingsSearch.Entry entry }) OpenSearchResult(entry);
    }
    internal void ActivateSearchSelection()
    {
        if (SearchResults.SelectedItem is Services.SettingsSearch.Entry entry) OpenSearchResult(entry);
    }
    private void OpenSearchResult(Services.SettingsSearch.Entry entry)
    {
        SearchSettings.Clear();
        ShowSettingsSection(entry.Section);
        if (entry.Control is "IntervalSlider" or "SuspendMonitor" or "EcoMode" or "NetworkChoice")
            AdvancedMonitorOptions.IsExpanded = true;
        _searchFocusOperation = Dispatcher.InvokeAsync(() =>
        {
            _searchFocusOperation = null;
            if (IsVisible && SettingsNavigation.SelectedIndex == entry.Section &&
                FindName(entry.Control) is FrameworkElement { IsVisible: true, IsEnabled: true } control)
            { control.BringIntoView(); control.Focus(); }
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }
    private void CancelSearchFocus()
    {
        _searchFocusOperation?.Abort();
        _searchFocusOperation = null;
    }
    public void ShowSettingsSection(int index)
    {
        CancelSearchFocus();
        FrameworkElement[] sections = [AppearanceSection, PanelSection, DesktopSection, MonitorSection, ApplicationSection, ShortcutsSection];
        index = Math.Clamp(index, 0, sections.Length - 1);
        if (SettingsNavigation.SelectedIndex != index)
        {
            SettingsNavigation.SelectedIndex = index;
            return;
        }
        ClearSectionAnimations();
        for (int i = 0; i < sections.Length; i++) sections[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
        SettingsScroll.ScrollToTop();
        if (IsVisible && Nepply.Modules.Appearance.MotionPolicy.Enabled)
        {
            var section = sections[index];
            int generation = _sectionAnimationGeneration;
            var animation = new System.Windows.Media.Animation.DoubleAnimation(.6, 1, Nepply.Modules.Appearance.MotionPolicy.Duration(160))
            { FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop };
            animation.Completed += (_, _) =>
            {
                if (generation == _sectionAnimationGeneration) section.BeginAnimation(OpacityProperty, null);
            };
            section.BeginAnimation(OpacityProperty, animation);
        }
    }
    private void ClearSectionAnimations()
    {
        unchecked { _sectionAnimationGeneration++; }
        foreach (var section in new FrameworkElement[] { AppearanceSection, PanelSection, DesktopSection, MonitorSection, ApplicationSection, ShortcutsSection })
            section.BeginAnimation(OpacityProperty, null);
    }
    private void LoadDraft()
    {
        _loadingDraft = true;
        EdgeChoice.SelectedIndex = (int)_draft.EdgePosition; ThemeChoice.SelectedIndex = (int)_draft.Theme;
        AccentChoice.SelectedItem = _draft.Accent; LoadGrid(_draft.EdgePosition);
        ScreenChoice.SelectedValue = _draft.MonitorDevice;
        if (ScreenChoice.SelectedIndex < 0) ScreenChoice.SelectedValue = "";
        NetworkChoice.SelectedValue = _draft.NetworkInterfaceId;
        LoadRelativeDimensions();
        OpacitySlider.Value = _draft.PanelOpacity * 100; DelaySlider.Value = _draft.HideDelayMs;
        ZoneSlider.Value = _draft.TriggerZoneSize; DwellSlider.Value = _draft.TriggerDelayMs;
        IntervalSlider.Value = _draft.MonitorUpdateIntervalMs;
        SuspendMonitor.IsChecked = _draft.SuspendMonitorWhenHidden;
        HideIcons.IsChecked = _draft.HideDesktopIcons; AutoStart.IsChecked = _draft.AutoStart;
        AutoCheckUpdates.IsChecked = _draft.AutoCheckUpdates;
        AutoCheckUpdates.Visibility = Services.ProcessPackageIdentity.IsPackaged ? Visibility.Collapsed : Visibility.Visible;
        SingleClick.IsChecked = _draft.SingleClickToOpen;
        MergeShortcuts.IsChecked = _draft.MergeEquivalentShortcuts;
        CustomAccent.Text = _draft.CustomAccent; GpuModeChoice.SelectedIndex = (int)_draft.GpuDisplay;
        GpuChoice.SelectedValue = _draft.GpuIdentifier; IntegratedGpuChoice.SelectedValue = _draft.IntegratedGpuIdentifier;
        ShowMonitor.IsChecked = _draft.ShowMonitor; Sensors.IsChecked = _draft.HardwareSensorsEnabled;
        EcoMode.IsChecked = _draft.EcoMode; HiddenFiles.IsChecked = _draft.ShowHiddenFiles; SystemIcons.IsChecked = _draft.ShowSystemIcons;
        MetallicEnabled.IsChecked = _draft.MetallicEnabled;
        MotionChoice.SelectedIndex = (int)_draft.Motion;
        DynamicMaterial.IsChecked = _draft.DynamicMaterial; GlowSlider.Value = _draft.GlowStrength * 100;
        NeonEnabled.IsChecked = _draft.NeonEnabled;
        LanguageChoice.SelectedValue = _draft.Language;
        ShowNetwork.IsChecked = _draft.ShowNetwork;
        _recording = false;
        RefreshShortcutButtons();
        _loadingDraft = false;
        QueuePreview();
    }
    private void RefreshLanguageChoices()
    {
        bool loading = _loadingDraft; _loadingDraft = true;
        try
        {
            int edge = EdgeChoice.SelectedIndex, theme = ThemeChoice.SelectedIndex, motion = MotionChoice.SelectedIndex, gpu = GpuModeChoice.SelectedIndex;
            EdgeChoice.ItemsSource = Enum.GetNames<EdgePosition>().Select(Services.LocalizationService.Get).ToArray();
            ThemeChoice.ItemsSource = new[] { "Dark", "Light", "System" }.Select(Services.LocalizationService.Get).ToArray();
            MotionChoice.ItemsSource = new[] { "Smooth", "Quick", "Off" }.Select(Services.LocalizationService.Get).ToArray();
            GpuModeChoice.ItemsSource = new[] { "AllGpu", "SelectedGpu", "SelectedIGpu" }.Select(Services.LocalizationService.Get).ToArray();
            EdgeChoice.SelectedIndex = edge; ThemeChoice.SelectedIndex = theme; MotionChoice.SelectedIndex = motion; GpuModeChoice.SelectedIndex = gpu;
            Title = "Nepply · " + Services.LocalizationService.Get("Settings");
            void DefaultChoice(System.Windows.Controls.ComboBox combo, string key)
            {
                if (combo.ItemsSource is not IEnumerable<Choice> choices) return;
                var items = choices.Select(choice => choice.Localized()).ToArray(); if (items.Length == 0 || items[0].Id != "") return;
                var selected = combo.SelectedValue;
                items[0] = items[0] with { Name = Services.LocalizationService.Get(key) };
                combo.ItemsSource = items; combo.SelectedValue = selected;
            }
            DefaultChoice(ScreenChoice, "PrimaryScreen"); DefaultChoice(NetworkChoice, "AutoNetwork");
            DefaultChoice(GpuChoice, "ChooseDevice"); DefaultChoice(IntegratedGpuChoice, "ChooseDevice");
            GridContext.Text = Services.LocalizationService.Get(_gridEdge.IsHorizontal() ? "TopGrid" : "SideGrid");
            var version = typeof(App).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false).OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion ?? "";
            AboutVersion.Text = string.Format(Services.LocalizationService.Get("AboutVersion"), version);
            // A new collection regenerates the cards so their localized bindings are read again.
            PresetCards.ItemsSource = Services.LayoutPresets.All.ToArray();
            SetAccessibleNames();
            ErrorText.Text = Services.LocalizationService.Render(_errorMessage);
            RefreshSearchResults();
        }
        finally { _loadingDraft = loading; }
    }
    internal Modules.Updates.PreparedUpdate? PendingUpdate { get; private set; }
    private void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Modules.Updates.UpdateWindow { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Prepared is not { } prepared) return;
        // Explicit consent: updates never silently save or discard a settings draft.
        if (MessageBox.Show(this, Services.LocalizationService.Get("UpdateSaveRestart"), "Nepply", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK)
        { prepared.Dispose(); return; }
        if (!SaveSettings(close: false)) { prepared.Dispose(); return; }
        PendingUpdate = prepared;
        DialogResult = true;
    }
    private void Save_Click(object sender, RoutedEventArgs e) => SaveSettings(close: true);
    private void Apply_Click(object sender, RoutedEventArgs e) => SaveSettings(close: false);
    internal event Action? SettingsCommitted;
    internal int AutoStartRevision { get; private set; }
    private void RefreshAutoStartState() => ShowAutoStartState(!Services.ProcessPackageIdentity.IsPackaged && Services.AutoStartService.IsTurnedOffInWindows());
    internal void ShowAutoStartState(bool turnedOffInWindows) =>
        AutoStartWindowsNotice.Visibility = turnedOffInWindows && AutoStart.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    internal void SynchronizeAutoStart(bool requested, bool actual, int expectedRevision)
    {
        // Preserve a newer uncommitted checkbox edit while a request was pending.
        if (AutoStartRevision != expectedRevision || AutoStart.IsChecked != requested) return;
        bool loading = _loadingDraft;
        _loadingDraft = true;
        try { _draft.AutoStart = actual; AutoStart.IsChecked = actual; }
        finally { _loadingDraft = loading; }
    }
    internal Modules.Settings.SettingsChangeSet LastCommittedChanges => _settingsEdit.LastCommittedChanges;
    private bool SaveSettings(bool close)
    {
        if (!ReadDraft()) return false;
        FlushPreview();
        UpdateGridAdvice();
        try { _settingsEdit.Commit(); }
        catch (Exception ex) { SetOperationError("SaveError", ex); return false; }
        SetError(null);
        try { SettingsCommitted?.Invoke(); }
        catch (Exception ex) { SetOperationError("SavedPartial", ex); return false; }
        if (close) DialogResult = true;
        return true;
    }
    private bool ReadDraft()
    {
        _draft.EdgePosition = (EdgePosition)Math.Max(0, EdgeChoice.SelectedIndex); _draft.Theme = (ThemeOption)Math.Max(0, ThemeChoice.SelectedIndex);
        _draft.Accent = AccentChoice.SelectedItem as string ?? "Champagne"; StoreGrid();
        _draft.MonitorDevice = ScreenChoice.SelectedValue as string ?? ""; _draft.NetworkInterfaceId = NetworkChoice.SelectedValue as string ?? "";
        _draft.PanelWidthPercent = WidthSlider.Value;
        _draft.PanelHeightPercent = HeightSlider.Value;
        _draft.TopPanelWidthPercent = TopWidthSlider.Value;
        _draft.PanelOpacity = OpacitySlider.Value / 100; _draft.HideDelayMs = (int)DelaySlider.Value;
        _draft.TriggerZoneSize = (int)ZoneSlider.Value; _draft.TriggerDelayMs = (int)DwellSlider.Value;
        _draft.MonitorUpdateIntervalMs = (int)IntervalSlider.Value;
        _draft.SuspendMonitorWhenHidden = SuspendMonitor.IsChecked == true;
        _draft.HideDesktopIcons = HideIcons.IsChecked == true; _draft.AutoStart = AutoStart.IsChecked == true;
        _draft.AutoCheckUpdates = AutoCheckUpdates.IsChecked == true;
        _draft.SingleClickToOpen = SingleClick.IsChecked == true;
        _draft.GpuDisplay = (GpuDisplayMode)Math.Max(0, GpuModeChoice.SelectedIndex);
        _draft.GpuIdentifier = GpuChoice.SelectedValue as string ?? ""; _draft.IntegratedGpuIdentifier = IntegratedGpuChoice.SelectedValue as string ?? "";
        _draft.MergeEquivalentShortcuts = MergeShortcuts.IsChecked == true;
        string customColor = CustomAccent.Text.Trim();
        if (_draft.Accent == "Custom" && !AppSettings.IsHexColor(customColor)) { ShowValidationError(0, CustomAccent, "ValidateHex"); return false; }
        // An unfinished inactive HEX field must not block a named palette or
        // replace the last valid custom color in the draft.
        if (AppSettings.IsHexColor(customColor)) _draft.CustomAccent = customColor;
        if ((_draft.GpuDisplay == GpuDisplayMode.Primary && _draft.GpuIdentifier.Length == 0) || (_draft.GpuDisplay == GpuDisplayMode.Secondary && _draft.IntegratedGpuIdentifier.Length == 0)) { ShowValidationError(3, _draft.GpuDisplay == GpuDisplayMode.Primary ? GpuChoice : IntegratedGpuChoice, "ValidateGpu"); return false; }
        if (_draft.GpuIdentifier.Length > 0 && _draft.GpuIdentifier == _draft.IntegratedGpuIdentifier) { ShowValidationError(3, IntegratedGpuChoice, "ValidateDistinctGpu"); return false; }
        _draft.ShowMonitor = ShowMonitor.IsChecked == true; _draft.HardwareSensorsEnabled = Sensors.IsChecked == true;
        _draft.EcoMode = EcoMode.IsChecked == true; _draft.ShowHiddenFiles = HiddenFiles.IsChecked == true; _draft.ShowSystemIcons = SystemIcons.IsChecked == true;
        _draft.MetallicEnabled = MetallicEnabled.IsChecked == true;
        _draft.NeonEnabled = NeonEnabled.IsChecked == true;
        _draft.Language = LanguageChoice.SelectedValue as string ?? "vi";
        _draft.Motion = (MotionMode)Math.Max(0, MotionChoice.SelectedIndex);
        _draft.DynamicMaterial = DynamicMaterial.IsChecked == true; _draft.GlowStrength = GlowSlider.Value / 100;
        _draft.ShowNetwork = ShowNetwork.IsChecked == true;
        return true;
    }
    internal bool ImportLayout(string json)
    {
        return ImportLayout(Services.LayoutProfile.Parse(json));
    }
    private bool ImportLayout(Services.LayoutProfile profile)
    {
        if (!ReadDraft()) return false;
        _draft = profile.ApplyTo(_draft);
        LoadDraft(); SetError(null);
        ProfileStatus.SetResourceReference(System.Windows.Controls.TextBlock.TextProperty, "Loc.DraftReady");
        return true;
    }
    internal bool ApplyLayoutPreset(string id)
    {
        var preset = Services.LayoutPresets.All.FirstOrDefault(p => p.Id == id);
        if (preset == null || !ReadDraft()) return false;
        _draft = Services.LayoutPresets.Apply(id, _draft);
        LoadDraft(); SetError(null);
        ProfileStatus.SetResourceReference(System.Windows.Controls.TextBlock.TextProperty, "Loc.DraftReady");
        return true;
    }
    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: string id }) ApplyLayoutPreset(id);
    }
    private void ImportLayout_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = Services.LocalizationService.Get("LayoutFileType") + "|*.nepply-layout;*.hoverbox-layout", Title = Services.LocalizationService.Get("ImportLayoutTitle") };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            using var stream = new System.IO.FileStream(dialog.FileName, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read);
            ImportLayout(Services.LayoutProfile.Parse(stream));
        }
        catch (Exception ex) { SetOperationError("ImportLayoutError", ex); }
    }
    private void ExportLayout_Click(object sender, RoutedEventArgs e)
    {
        if (!ReadDraft()) return;
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = Services.LocalizationService.Get("LayoutFileType") + " (*.nepply-layout)|*.nepply-layout", FileName = "My-layout.nepply-layout", DefaultExt = ".nepply-layout", AddExtension = true, Title = Services.LocalizationService.Get("ExportLayoutTitle") };
        if (dialog.ShowDialog(this) != true) return;
        string temporary = dialog.FileName + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            System.IO.File.WriteAllText(temporary, Services.LayoutProfile.Capture(_draft).ToJson());
            System.IO.File.Move(temporary, dialog.FileName, true);
            SetError(null); ProfileStatus.SetResourceReference(System.Windows.Controls.TextBlock.TextProperty, "Loc.LayoutExported");
        }
        catch (Exception ex) { SetOperationError("ExportLayoutError", ex); }
        finally
        {
            try { if (System.IO.File.Exists(temporary)) System.IO.File.Delete(temporary); }
            catch (Exception ex) { Services.AppLogger.Error(ex, "Clean layout export temporary file"); }
        }
    }
    private void ShowValidationError(int section, FrameworkElement control, string key)
    {
        if (_readingPreview) return;
        ShowSettingsSection(section);
        SetError(UserMessage.FromKey(key));
        control.BringIntoView();
        control.Focus();
    }
    private void Hex_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_colorSync || ColorPreview == null || !AppSettings.IsHexColor(CustomAccent.Text)) return;
        ColorPreview.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(CustomAccent.Text));
        if (CustomAccent.IsKeyboardFocusWithin) AccentChoice.SelectedItem = "Custom";
    }
    private void OpenColor_Click(object sender, RoutedEventArgs e)
    {
        _colorSync = true;
        try { ColorPicker.SelectedColor = EditingColor(AccentChoice.SelectedItem as string, CustomAccent.Text); }
        finally { _colorSync = false; }
        RgbLabel.Text = $"RGB  {ColorPicker.SelectedColor.R} · {ColorPicker.SelectedColor.G} · {ColorPicker.SelectedColor.B}"; ColorPopup.IsOpen = true;
    }
    internal static System.Windows.Media.Color EditingColor(string? palette, string custom)
    {
        // This button edits the custom swatch beside it, not the active preset.
        // Opening/dismissing never switches palettes or overwrites a saved custom color.
        string hex = AppSettings.IsHexColor(custom) ? custom : Services.ThemeService.Palettes["Champagne"];
        return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
    }
    private void ColorPopup_Opened(object? sender, EventArgs e) => ColorPicker.Focus();
    private bool CloseColorPopup()
    {
        if (!ColorPopup.IsOpen) return false;
        ColorPopup.IsOpen = false; ColorButton.Focus(); return true;
    }
    private void ColorPopup_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && GridPicker.IsMouseCaptured) return;
        if (e.Key == Key.Escape && CloseColorPopup()) e.Handled = true;
    }
    private void ColorPicker_Changed(object? sender, EventArgs e)
    {
        if (_colorSync || CustomAccent == null || ColorPreview == null) return;
        var color = ColorPicker.SelectedColor;
        CustomAccent.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        RgbLabel.Text = $"RGB  {color.R} · {color.G} · {color.B}";
        AccentChoice.SelectedItem = "Custom";
    }
    public sealed record Palette(string Name, string Hex)
    {
        public System.Windows.Media.Brush Preview { get; } = CreatePreview(Hex);
        private static System.Windows.Media.Brush CreatePreview(string hex)
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            var brush = new System.Windows.Media.LinearGradientBrush { StartPoint = new(0, 0), EndPoint = new(1, 1) };
            brush.GradientStops.Add(new(color, 0)); brush.GradientStops.Add(new(System.Windows.Media.Color.FromRgb((byte)(color.R * .4), (byte)(color.G * .4), (byte)(color.B * .4)), .4));
            brush.GradientStops.Add(new(System.Windows.Media.Colors.White, .5)); brush.GradientStops.Add(new(color, .64)); brush.Freeze(); return brush;
        }
    }
    private void Palette_Selected(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // Custom and legacy palettes have no tile. Clearing that visual selection
        // must never clear the configured accent, hence the one-way host binding.
        if (e.AddedItems.OfType<Palette>().FirstOrDefault() is { } palette)
            AccentChoice.SelectedItem = palette.Name;
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    internal void ShowExitGuidance() => SetError(UserMessage.FromKey("SettingsExitGuidance"));
    /// <summary>The shareable sensor report (no paths, account names or raw logs) for the snapshot taken when Settings opened.</summary>
    internal string BuildDiagnosticReport()
    {
        string privilege = "PrivilegeUnknown", driver = "DriverUnknown";
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            privilege = new System.Security.Principal.WindowsPrincipal(identity).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator) ? "PrivilegeAdmin" : "PrivilegeUser";
            using var driverKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\PawnIO");
            driver = driverKey == null ? "DriverAbsent" : "DriverRegistered";
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException) { }
        return Services.Telemetry.SensorDiagnosticReport.Build(_diagnosticSnapshot, privilege, driver, System.Diagnostics.Stopwatch.GetTimestamp());
    }
    // Replaceable so tests never touch the shared system clipboard.
    internal Action<string> CopyText { get; set; } = Clipboard.SetText;
    private void CopyDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            CopyText(BuildDiagnosticReport());
            InfoStatus.SetResourceReference(TextBlock.TextProperty, "Loc.DiagnosticsCopied");
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or System.Runtime.InteropServices.ExternalException)
        {
            Services.AppLogger.Error(ex, "Copy diagnostics");
            InfoStatus.SetResourceReference(TextBlock.TextProperty, "Loc.DiagnosticsClipboardBusy");
        }
    }
    private void Diagnostics_Click(object sender, RoutedEventArgs e)
    {
#if DEBUG
        new SensorDiagnosticsWindow(BuildDiagnosticReport) { Owner = this }.ShowDialog();
#endif
    }
    internal static string? FindLocalDocument(string fileName, string? baseDirectory = null)
    {
        if (fileName is not ("PRIVACY.md" or "SECURITY.md" or "THIRD_PARTY_NOTICES.md")) return null;
        string root = baseDirectory ?? AppContext.BaseDirectory;
        string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, fileName));
        return System.IO.File.Exists(path) ? path : null;
    }
    private void OpenLocalDocument_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string fileName }) return;
        string? path = FindLocalDocument(fileName);
        if (path == null) { InfoStatus.SetResourceReference(TextBlock.TextProperty, "Loc.LocalDocumentMissing"); return; }
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); InfoStatus.Text = ""; }
        catch (Exception ex) { Services.AppLogger.Error(ex, "Open local documentation"); InfoStatus.SetResourceReference(TextBlock.TextProperty, "Loc.LocalDocumentError"); }
    }
    // Replaceable so tests never start a browser.
    internal Action<Uri> OpenLink { get; set; } = uri =>
    {
        using var _ = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    };
    /// <summary>
    /// Bug form prefilled with the Nepply and Windows versions only. Hardware names and
    /// other diagnostics go through the clipboard so nothing personal lands in a URL.
    /// </summary>
    internal static Uri BugReportUri(string version, Version windows)
    {
        string os = (windows.Major == 10 && windows.Build >= 22000 ? "Windows 11" : windows.Major == 10 ? "Windows 10" : "Windows " + windows.Major)
            + " (build " + windows.Build.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
        return new($"https://github.com/{Modules.Updates.UpdatePolicy.Repository}/issues/new?template=bug_report.yml"
            + "&version=" + Uri.EscapeDataString(version) + "&windows=" + Uri.EscapeDataString(os));
    }
    private void ReportProblem_Click(object sender, RoutedEventArgs e)
    {
        bool copied = true;
        try { CopyText(BuildDiagnosticReport()); }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or System.Runtime.InteropServices.ExternalException)
        {
            Services.AppLogger.Error(ex, "Copy diagnostics for bug report");
            copied = false;
        }
        try
        {
            OpenLink(BugReportUri(Modules.Updates.UpdateService.CurrentVersion.Split('+')[0], Environment.OSVersion.Version));
            InfoStatus.SetResourceReference(TextBlock.TextProperty, copied ? "Loc.ReportProblemOpened" : "Loc.DiagnosticsClipboardBusy");
        }
        catch (Exception ex) { Services.AppLogger.Error(ex, "Open bug report"); InfoStatus.SetResourceReference(TextBlock.TextProperty, "Loc.OpenBrowserError"); }
    }
    private void OpenSupport_Click(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/minhquang0607/Nepply/issues") { UseShellExecute = true }); InfoStatus.Text = ""; }
        catch (Exception ex) { Services.AppLogger.Error(ex, "Open support link"); InfoStatus.SetResourceReference(TextBlock.TextProperty, "Loc.OpenBrowserError"); }
    }
    private void Reset_Click(object sender, RoutedEventArgs e) { _draft.ResetToDefaults(); LoadDraft(); SetError(null); ProfileStatus.Text = ""; }
    private void DragTitle(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) DragMove(); }
    private ShortcutAction _recordingAction;
    private static ShortcutAction ShortcutFor(object sender) =>
        sender is FrameworkElement { Tag: string tag } && Enum.TryParse<ShortcutAction>(tag, out var action) ? action : ShortcutAction.TogglePanel;
    private Button ShortcutButton(ShortcutAction action) =>
        action == ShortcutAction.TogglePanel ? HotkeyButton : (Button)FindName("Shortcut" + action);
    private void RefreshShortcutButtons()
    {
        foreach (var action in ShortcutBindings.All)
        {
            var button = ShortcutButton(action);
            if (_recording && action == _recordingAction) continue;
            var gesture = ShortcutBindings.Get(_draft, action);
            if (gesture.IsEmpty) button.SetResourceReference(ContentControl.ContentProperty, "Loc.ShortcutNone");
            else button.Content = gesture.ToString();
            if (action != ShortcutAction.TogglePanel)
                button.SetResourceReference(System.Windows.Automation.AutomationProperties.NameProperty, "Loc." + ShortcutLabelKey(action));
        }
    }
    // Each action's label is the resource "Shortcut<Action>" (e.g. ShortcutTogglePin).
    internal static string ShortcutLabelKey(ShortcutAction action) => "Shortcut" + action;
    private void StopRecording()
    {
        if (!_recording) return;
        _recording = false;
        RefreshShortcutButtons();
    }
    private void Hotkey_Click(object sender, RoutedEventArgs e)
    {
        var action = ShortcutFor(sender);
        StopRecording();
        SetError(null);
        _recording = true; _recordingAction = action;
        var button = ShortcutButton(action);
        button.SetResourceReference(ContentControl.ContentProperty, ShortcutBindings.IsGlobal(action) ? "Loc.RecordHotkeyPrompt" : "Loc.RecordShortcutPrompt");
        button.Focus();
    }
    // Assigns only when no other action uses the gesture; the error names the owner instead.
    private bool TryAssignShortcut(ShortcutAction action, ShortcutGesture gesture)
    {
        if (ShortcutBindings.ConflictFor(_draft, action, gesture) is { } owner)
        {
            SetError(UserMessage.FromKey("ShortcutConflict", gesture.ToString(), UserMessage.FromKey(ShortcutLabelKey(owner))));
            return false;
        }
        ShortcutBindings.Set(_draft, action, gesture);
        SetError(null);
        RefreshShortcutButtons();
        return true;
    }
    private void ShortcutDefault_Click(object sender, RoutedEventArgs e)
    {
        var action = ShortcutFor(sender);
        StopRecording();
        TryAssignShortcut(action, ShortcutBindings.Default(action));
    }
    private void ShortcutClear_Click(object sender, RoutedEventArgs e)
    {
        var action = ShortcutFor(sender);
        StopRecording();
        if (ShortcutBindings.CanClear(action)) TryAssignShortcut(action, ShortcutGesture.None);
    }
    private void Guide_Click(object sender, RoutedEventArgs e)
    {
        StopRecording();
        // Shows the draft's shortcuts, so keys edited here appear before they are saved.
        new WelcomeWindow(_draft, guideOnly: true) { Owner = this }.ShowDialog();
    }
    private void ResetShortcuts_Click(object sender, RoutedEventArgs e)
    {
        StopRecording();
        var toggle = ShortcutBindings.Default(ShortcutAction.TogglePanel);
        _draft.HotkeyModifiers = toggle.Modifiers; _draft.HotkeyKey = toggle.Key; _draft.Shortcuts = [];
        SetError(null);
        RefreshShortcutButtons();
    }
    private void RecordKey(object sender, KeyEventArgs e)
    {
        // One Escape press dismisses one layer only (picker, search, recording,
        // then the dialog). Holding it must not discard the remaining draft.
        if (e.Key == Key.Escape && e.IsRepeat) { e.Handled = true; return; }
        if (e.Key == Key.Escape && CloseColorPopup()) { e.Handled = true; return; }
        if (!_recording && e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { FocusSettingsSearch(); e.Handled = true; return; }
        if (!_recording && e.Key == Key.Escape && SearchSettings.Text.Length > 0) { SearchSettings.Clear(); e.Handled = true; return; }
        if (!_recording && e.Key == Key.Escape && _searchExpanded)
        { SettingsSearchTrigger.Focus(); ExpandSettingsSearch(false); e.Handled = true; return; }
        if (!_recording) return;
        RecordShortcut(e, ShortcutInput.FromEvent(e));
    }
    internal void RecordShortcut(KeyEventArgs e, ShortcutGesture gesture)
    {
        if (!_recording) return;
        // Esc cancels recording (its default use is restored with the reset button); Tab keeps moving focus.
        if (e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None) { StopRecording(); e.Handled = true; return; }
        if (e.Key == Key.Tab) { StopRecording(); return; }
        e.Handled = true;
        if (gesture.IsEmpty) return; // A modifier on its own: keep waiting for the key.
        if (!ShortcutBindings.IsValid(_recordingAction, gesture))
        {
            SetError(UserMessage.FromKey(ShortcutBindings.IsGlobal(_recordingAction) ? "ShortcutInvalidGlobal" : "ShortcutInvalidPanel"));
            return;
        }
        var action = _recordingAction;
        _recording = false;
        if (!TryAssignShortcut(action, gesture)) { _recording = true; return; }
    }
}
