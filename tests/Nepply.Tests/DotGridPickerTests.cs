using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using Nepply.Models;
using Nepply.Services;
using Nepply.Views;
using Nepply.Modules.Desktop;

internal static class DotGridPickerTests
{
    internal static void Run(Action<string,bool> check,Action<int> pump,string output,Action<FrameworkElement,int,int,string> render)
    {
        SettingsPreviewTests.Run(check,pump);
        var target=new AppSettings {GridColumns=3,GridRows=7};
        var window=new SettingsWindow(target) {Width=620,Height=880,Left=-10000,Top=-10000,ShowActivated=false,ShowInTaskbar=false};
        try
        {
            window.Show();window.ShowSettingsSection(2);pump(30);window.UpdateLayout();
            var picker=(DotGridPicker)window.FindName("GridPicker");
            check("Dot grid loads persisted column and row counts",picker.Columns==3 && picker.Rows==7);
            var peer=UIElementAutomationPeer.CreatePeerForElement(picker)!;
            var value=(IValueProvider)peer.GetPattern(PatternInterface.Value)!;
            string originalLanguage=LocalizationService.CurrentLanguage;
            try
            {
                foreach(var language in LocalizationService.Languages)
                {
                    LocalizationService.Apply(language.Id);pump(5);
                    check($"Dot grid {language.Id} refreshes accessible capacity and selection",
                        peer.GetHelpText()==string.Format(LocalizationService.Get("GridAccessibleHelp"),picker.AvailableColumns,picker.AvailableRows)
                        && peer.GetItemStatus()==string.Format(LocalizationService.Get("GridUnits"),picker.Columns,picker.Rows));
                }
            }
            finally {LocalizationService.Apply(originalLanguage);}
            value.SetValue("4 × 4");
            check("Dot grid supports accessible count selection",picker.Columns==4 && picker.Rows==4 && value.Value=="4 × 4");
            check("Dot grid selection does not mutate live settings",target.GridColumns==3 && target.GridRows==7);
            check("Dot grid pointer maps last dot without overflow",picker.CellAt(new Point(picker.ActualWidth,picker.ActualHeight))==(5,10));
            check("Dot grid captured pointer clamps outside canvas",picker.CellAt(new Point(-100,-100))==(1,1));
            bool rejected=false;try{value.SetValue("999 x 4");}catch(ArgumentException){rejected=true;}
            check("Dot grid rejects unsupported counts",rejected && picker.Columns==4);
            check("Dot grid is an interactive search target",!SearchHitTest.IsBlank(picker,window));
            var preview=typeof(DotGridPicker).GetMethod("Preview",BindingFlags.Instance|BindingFlags.NonPublic)!;
            preview.Invoke(picker,new object?[]{(9,8)});
            check("Dot grid previews dimensions without committing",picker.PreviewColumns==9 && picker.PreviewRows==8 && picker.Columns==4 && picker.Rows==4);
            typeof(DotGridPicker).GetMethod("Cancel",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(picker,null);
            check("Dot grid cancellation restores committed preview",picker.PreviewColumns==4 && picker.PreviewRows==4);
            var key=new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,PresentationSource.FromVisual(window)!,0,System.Windows.Input.Key.Right);
            key.RoutedEvent=System.Windows.Input.Keyboard.KeyDownEvent;
            typeof(DotGridPicker).GetMethod("OnKeyDown",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(picker,new object[]{key});
            check("Dot grid supports keyboard adjustment",key.Handled && picker.Columns==5 && picker.Rows==4 && picker.PreviewColumns==5);
            value.SetValue("4 × 4");
            var read=typeof(SettingsWindow).GetMethod("ReadDraft",BindingFlags.Instance|BindingFlags.NonPublic)!;
            check("Dot grid writes settings draft",Equals(read.Invoke(window,null),true));
            var draft=(AppSettings)typeof(SettingsWindow).GetProperty("_draft",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
            check("Dot grid draft retains both dimensions",draft.GridColumns==4 && draft.GridRows==4);
            var store=new SettingsStore(Path.Combine(output,"dot-grid-settings.json"));store.Save(draft);
            var restored=store.Load();check("Dot grid persists and reloads",restored.GridColumns==4 && restored.GridRows==4);
            var profile=LayoutProfile.Parse(LayoutProfile.Capture(draft).ToJson()).ApplyTo(new());
            check("Layout profile round-trips dot grid",profile.GridColumns==4 && profile.GridRows==4);
            check("Layout presets preserve chosen grid",LayoutPresets.All.All(p=>{var result=LayoutPresets.Apply(p.Id,draft);return result.GridColumns==4 && result.GridRows==4;}));
            render((FrameworkElement)window.Content,620,880,Path.Combine(output,"settings-dot-grid.png"));
            picker.ConfigureViewport(new Size(300,480));window.UpdateLayout();
            check("Dot picker capacity follows panel",picker.AvailableColumns==4 && picker.AvailableRows==6 && picker.CellAt(new Point(picker.ActualWidth,picker.ActualHeight))==(4,6));
            check("Capacity change preserves saved intent",picker.Columns==4 && picker.Rows==4);
            bool overCapacity=false;try{value.SetValue("5 × 4");}catch(ArgumentException){overCapacity=true;}
            check("Accessible selection respects panel capacity",overCapacity);
            render((FrameworkElement)window.Content,620,880,Path.Combine(output,"settings-adaptive-grid.png"));
            foreach(var (w,h,c,r) in new[]{(300d,480d,12,10),(960d,180d,20,10),(125d,40d,12,10),(2800d,180d,35,2)})
            {
                var fit=IconGridGeometry.Fit(w,h,c,r);
                check($"Adaptive grid {w}x{h} keeps readable cells",fit.TileWidth>=64 && fit.TileHeight>=78 && fit.Columns<=c);
                var position=fit.Position(fit.Columns+1,0);
                check($"Adaptive grid {w}x{h} hit testing matches layout",fit.Insertion(position.X+fit.TileWidth*.25,position.Y+fit.TileHeight*.5,500)==fit.Columns+1);
            }
            picker.ConfigureViewport(new Size(2800,180),true);
            check("Wide top picker is not restricted to twelve columns",picker.AvailableColumns>12 && picker.AvailableRows==2);
            picker.ConfigureViewport(new Size(2800,900),true);
            check("Top picker caps visible row choices at three",picker.AvailableRows==3);
            picker.ConfigureViewport(new Size(900,900),false);
            check("Side picker caps visible column choices at five",picker.AvailableColumns==5);
            var limits=new AppSettings {GridColumns=20,TopGridRows=20};limits.Validate();
            check("Saved grids obey edge-specific limits",limits.GridColumns==5 && limits.TopGridRows==3);
            check("Expanded viewport restores requested column count",IconGridGeometry.Fit(960,480,6,4).Columns==6);
            foreach(var (width,height,columns,rows) in new[]{(300d,480d,3,6),(960d,180d,8,2),(125d,80d,12,10)})
            {
                var geometry=IconGridGeometry.Fixed(width,height,columns,rows);
                check($"Fixed grid {columns}x{rows} fills viewport",geometry.Columns==columns && Math.Abs(geometry.TileWidth*columns-width)<.001 && Math.Abs(geometry.TileHeight*rows-height)<.001);
                var position=geometry.Position(columns*rows-1,0);
                check($"Fixed grid {columns}x{rows} insertion matches arranged slot",geometry.Insertion(position.X+geometry.TileWidth*.25,position.Y+geometry.TileHeight*.5,500)==columns*rows-1);
            }
        }
        finally {window.Close();}
        check("Closing grid settings without saving preserves live grid",target.GridColumns==3 && target.GridRows==7);
    }
}
