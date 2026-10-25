using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Nepply;
using Nepply.Models;
using Nepply.Services;
using Nepply.Views;

internal static class PanelLocalizationTests
{
    internal static void Run(Action<string,bool> check,Action<int> pump)
    {
        int before=Subscribers();
        var main=new MainWindow(preview:true,monitor:new Monitor(),icons:new DesktopIconService(_=>Task.FromResult(new List<DesktopIcon>())))
            {ShowActivated=false,ShowInTaskbar=false};
        try
        {
            main.Show();main.Left=-10000;main.Top=-10000;pump(20);
            var icons=(IconPanel)main.FindName("IconPanelView");
            var notice=(Border)icons.FindName("LaunchNotice");
            var noticeText=(TextBlock)icons.FindName("LaunchNoticeText");
            var hotkey=(TextBlock)main.FindName("HotkeyText");
            main.PresentHotkeyStatus(false);
            typeof(MainWindow).GetMethod("EditLayout_Click",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,[main,new RoutedEventArgs()]);
            main.TogglePin();main.FocusDesktopSearch();
            var monitor=(MonitorPanel)main.FindName("MonitorPanelView");
            var widgets=(Panel)monitor.FindName("Widgets");
            var summary=widgets.Children.OfType<FrameworkElement>().Single(e=>Equals(e.Tag,"summary"));
            var network=widgets.Children.OfType<FrameworkElement>().Single(e=>Equals(e.Tag,"network"));
            var menu=summary.ContextMenu!;
            foreach(var language in LocalizationService.Languages)
            {
                LocalizationService.Apply(language.Id);pump(5);
                check($"Language {language.Id} refreshes existing hotkey status and help",hotkey.Text==string.Format(LocalizationService.Get("HotkeyUnavailableLabel"),MainWindow.HotkeyLabel(main.Settings))
                    && Equals(hotkey.ToolTip,LocalizationService.Get("HotkeyChangeHint")));
                check($"Language {language.Id} refreshes existing hotkey warning",notice.Visibility==Visibility.Visible
                    && noticeText.Text==string.Format(LocalizationService.Get("HotkeyUnavailableNotice"),MainWindow.HotkeyLabel(main.Settings)));
                check($"Language {language.Id} localizes the layout section swap button",
                    Equals(((Button)main.FindName("SwapSectionsButton")).Content,LocalizationService.Get("SwapSectionsShort")));
                check($"Language {language.Id} refreshes layout footer",((TextBlock)main.FindName("FooterState")).Text==LocalizationService.Get("LayoutEditingHint"));
                check($"Language {language.Id} refreshes panel accessibility states",
                    AutomationProperties.GetItemStatus((DependencyObject)main.FindName("PinButton"))==LocalizationService.Get("Pinned")
                    && AutomationProperties.GetItemStatus((DependencyObject)main.FindName("EditLayoutButton"))==LocalizationService.Get("EditingStatus")
                    && AutomationProperties.GetItemStatus((DependencyObject)main.FindName("DesktopSearchButton"))==LocalizationService.Get("SearchOpenStatus"));
                check($"Language {language.Id} refreshes widget names and move help",AutomationProperties.GetName(summary)==LocalizationService.Get("WidgetSummary")
                    && AutomationProperties.GetName(network)==LocalizationService.Get("WidgetNetwork")
                    && AutomationProperties.GetHelpText(summary)==LocalizationService.Get("WidgetMoveHint"));
                check($"Language {language.Id} updates menu wording without replacing command objects",ReferenceEquals(menu,summary.ContextMenu)
                    && menu.Items.OfType<MenuItem>().Select(i=>i.Header?.ToString()).SequenceEqual(new[]{"WidgetAutoWidth","WidgetOneColumn","WidgetTwoColumns"}.Select(LocalizationService.Get)));
            }
            main.PresentHotkeyStatus(true);
            check("Hotkey recovery dismisses its notice after language changes",notice.Visibility==Visibility.Collapsed);
            main.PresentHotkeyStatus(false);
            icons.ShowActionFailure(UserMessage.FromKey("DesktopMenuError","separate failure"));
            main.PresentHotkeyStatus(true);
            check("Hotkey recovery preserves a different structured notice",notice.Visibility==Visibility.Visible && noticeText.Text.Contains("separate failure"));
            icons.ShowActionFailure(null);
            LocalizationService.Apply("en");
            check("Language refresh never resurrects a dismissed warning",notice.Visibility==Visibility.Collapsed);
            main.FinishLayout(false);
            check("Leaving layout clears editing-only accessibility help",AutomationProperties.GetHelpText(summary)=="");
        }
        finally {main.Cleanup();main.Close();LocalizationService.Apply("vi");}
        check("Panel language lifetime returns static subscriber count to baseline",Subscribers()==before);
    }
    private static int Subscribers()=>(typeof(LocalizationService).GetField("Changed",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null) as Delegate)?.GetInvocationList().Length ?? 0;
    private sealed class Monitor : Nepply.Services.Telemetry.IHardwareMonitor
    {
        public event Action<HardwareInfoSnapshot>? HardwareUpdated {add {} remove {}}
        public HardwareInfoSnapshot GetSnapshot()=>HardwareInfoSnapshot.Empty;
        public void Configure(AppSettings settings,bool visible) {}
        public void Start() {}
        public void Dispose() {}
    }
}
