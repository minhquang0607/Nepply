using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
namespace Nepply.Views;

/// <summary>Two-dimensional saturation/value field plus hue rail; emits RGB colors.</summary>
public sealed class ColorField : FrameworkElement
{
    public static readonly DependencyProperty SelectedColorProperty = DependencyProperty.Register(nameof(SelectedColor), typeof(Color), typeof(ColorField), new FrameworkPropertyMetadata(Colors.Cyan, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, ColorChanged));
    public Color SelectedColor { get => (Color)GetValue(SelectedColorProperty); set => SetValue(SelectedColorProperty, value); }
    public event EventHandler? SelectionChanged;
    private double _h = 180, _s = 1, _v = 1;
    private bool _updating, _hueDrag;
    private static readonly Brush WhiteOverlay = Frozen(new LinearGradientBrush(Colors.White, Colors.Transparent, 0));
    private static readonly Brush BlackOverlay = Frozen(new LinearGradientBrush(Colors.Transparent, Colors.Black, 90));
    private static readonly Brush HueRail = CreateHueRail();
    private static readonly Pen MarkerShadow = Frozen(new Pen(Brushes.Black, 3));
    private static readonly Pen MarkerStroke = Frozen(new Pen(Brushes.White, 1.5));
    private static readonly Pen HueMarkerStroke = Frozen(new Pen(Brushes.White, 2));
    private static T Frozen<T>(T value) where T : Freezable { value.Freeze(); return value; }
    private static Brush CreateHueRail()
    {
        var brush = new LinearGradientBrush { StartPoint = new(0,0), EndPoint = new(0,1) };
        for (int i = 0; i <= 6; i++) brush.GradientStops.Add(new(FromHsv(i*60, 1, 1), i/6d));
        return Frozen(brush);
    }
    public ColorField()
    {
        Focusable = true; Cursor = Cursors.Cross; MinWidth = 240; MinHeight = 190;
        Unloaded+=(_,_)=>EndDrag();
        IsVisibleChanged+=(_,e)=> {if(!(bool)e.NewValue) EndDrag();};
        IsEnabledChanged+=(_,e)=> {if(!(bool)e.NewValue) EndDrag();};
    }
    private static void ColorChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var field = (ColorField)sender;
        if (!field._updating)
        {
            var c = (Color)e.NewValue; double r = c.R / 255d, g = c.G / 255d, b = c.B / 255d;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), delta = max - min;
            field._v = max; field._s = max == 0 ? 0 : delta / max;
            if (delta != 0) field._h = ((max == r ? (g - b) / delta : max == g ? (b - r) / delta + 2 : (r - g) / delta + 4) * 60 + 360) % 360;
        }
        field.InvalidateVisual(); field.SelectionChanged?.Invoke(field, EventArgs.Empty);
        if (AutomationPeer.ListenerExists(AutomationEvents.PropertyChanged)
            && UIElementAutomationPeer.FromElement(field) is { } peer)
            peer.RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, Hex((Color)e.OldValue), Hex((Color)e.NewValue));
    }
    internal static string Hex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    protected override AutomationPeer OnCreateAutomationPeer() => new ColorFieldAutomationPeer(this);
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnGotKeyboardFocus(e); InvalidateVisual(); }
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnLostKeyboardFocus(e); InvalidateVisual(); }
    public static Color FromHsv(double hue, double saturation, double value)
    {
        hue = ((hue % 360) + 360) % 360; saturation = Math.Clamp(saturation, 0, 1); value = Math.Clamp(value, 0, 1);
        double c = value * saturation, x = c * (1 - Math.Abs(hue / 60 % 2 - 1)), m = value - c;
        (double r, double g, double b) = hue switch { < 60 => (c,x,0d), < 120 => (x,c,0d), < 180 => (0d,c,x), < 240 => (0d,x,c), < 300 => (x,0d,c), _ => (c,0d,x) };
        return Color.FromRgb((byte)Math.Round((r+m)*255), (byte)Math.Round((g+m)*255), (byte)Math.Round((b+m)*255));
    }
    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        double width = Math.Max(1, ActualWidth - 38), height = Math.Max(1, ActualHeight);
        var area = new Rect(0, 0, width, height);
        dc.PushClip(new RectangleGeometry(area, 10, 10));
        dc.DrawRectangle(new SolidColorBrush(FromHsv(_h, 1, 1)), null, area);
        dc.DrawRectangle(WhiteOverlay, null, area);
        dc.DrawRectangle(BlackOverlay, null, area); dc.Pop();
        dc.DrawRoundedRectangle(HueRail, null, new Rect(width+14,0,24,height), 10, 10);
        var marker = new Point(Math.Clamp(_s*width,7,Math.Max(7,width-7)),Math.Clamp((1-_v)*height,7,Math.Max(7,height-7)));
        dc.DrawEllipse(null, MarkerShadow, marker, 6, 6);
        dc.DrawEllipse(null, MarkerStroke, marker, 6, 6);
        dc.DrawRoundedRectangle(null, HueMarkerStroke, new Rect(width+15, Math.Clamp(_h/360*height-3,1,Math.Max(1,height-7)),22,6),3,3);
        if (IsKeyboardFocused)
        {
            var focus = new Rect(2,2,Math.Max(0,width-4),Math.Max(0,height-4));
            dc.DrawRoundedRectangle(null, MarkerShadow, focus, 8, 8);
            dc.DrawRoundedRectangle(null, MarkerStroke, focus, 8, 8);
        }
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        Focus();
        if(!CaptureMouse()) return;
        _hueDrag = e.GetPosition(this).X > ActualWidth-38;
        Pick(e.GetPosition(this)); e.Handled=true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if(!IsMouseCaptured) return;
        if(e.LeftButton!=MouseButtonState.Pressed) {EndDrag();return;}
        Pick(e.GetPosition(this));
    }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if(!IsMouseCaptured) return;
        try {Pick(e.GetPosition(this));e.Handled=true;}
        finally {EndDrag();}
    }
    private void EndDrag() {if(IsMouseCaptured) ReleaseMouseCapture();_hueDrag=false;}
    private void Pick(Point point)
    {
        if (_hueDrag) _h = Math.Clamp(point.Y / Math.Max(1,ActualHeight),0,.99999)*360;
        else { _s = Math.Clamp(point.X/Math.Max(1,ActualWidth-38),0,1); _v = 1-Math.Clamp(point.Y/Math.Max(1,ActualHeight),0,1); }
        Commit();
    }
    private void Commit()
    {
        _updating=true;
        try {SetCurrentValue(SelectedColorProperty,FromHsv(_h,_s,_v));}
        finally {_updating=false;}
        InvalidateVisual();
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down)) return;
        if((Keyboard.Modifiers & ~ModifierKeys.Shift)!=ModifierKeys.None) return;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) _h = (_h + (e.Key is Key.Left or Key.Down ? -3 : 3) + 360) % 360;
        else if (e.Key is Key.Left or Key.Right) _s = Math.Clamp(_s + (e.Key == Key.Left ? -.01 : .01),0,1);
        else _v = Math.Clamp(_v + (e.Key == Key.Down ? -.01 : .01),0,1);
        Commit(); e.Handled = true;
    }
}

internal sealed class ColorFieldAutomationPeer(ColorField owner) : FrameworkElementAutomationPeer(owner), IValueProvider
{
    protected override string GetClassNameCore() => nameof(ColorField);
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;
    public override object? GetPattern(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.Value ? this : base.GetPattern(patternInterface);
    public bool IsReadOnly => false;
    public string Value => ColorField.Hex(((ColorField)Owner).SelectedColor);
    public void SetValue(string value)
    {
        if (!IsEnabled()) throw new ElementNotEnabledException();
        if (!Models.AppSettings.IsHexColor(value)) throw new ArgumentException("Color must use the #RRGGBB format.", nameof(value));
        ((ColorField)Owner).SetCurrentValue(ColorField.SelectedColorProperty, (Color)ColorConverter.ConvertFromString(value));
    }
}
