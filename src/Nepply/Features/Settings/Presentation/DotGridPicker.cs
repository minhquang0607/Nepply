using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Input;
using System.Windows.Media;
namespace Nepply.Views;

public sealed class DotGridPicker : FrameworkElement
{
    public const int MaximumColumns=Nepply.Modules.Desktop.GridCapacity.MaximumColumns, MaximumRows=Nepply.Modules.Desktop.GridCapacity.MaximumRows;
    internal int AvailableColumns {get;private set;}=5;
    internal int AvailableRows {get;private set;}=10;
    internal void ConfigureViewport(Size size, bool top=false)
    {
        var capacity=Nepply.Modules.Desktop.GridCapacity.For(size.Width,size.Height);
        capacity=top ? (capacity.Columns,Math.Min(3,capacity.Rows)) : (Math.Min(5,capacity.Columns),capacity.Rows);
        if((AvailableColumns,AvailableRows)==capacity)return;
        Cancel();
        AvailableColumns=capacity.Columns;AvailableRows=capacity.Rows;
        Height=Math.Clamp(AvailableRows*26,78,260);MinHeight=78;
        InvalidateVisual();
        RefreshAccessibleText();
    }
    public static readonly DependencyProperty ColumnsProperty=DependencyProperty.Register(nameof(Columns),typeof(int),typeof(DotGridPicker),new FrameworkPropertyMetadata(4,FrameworkPropertyMetadataOptions.AffectsRender,Changed),v=>v is int n && n>=1 && n<=MaximumColumns);
    public static readonly DependencyProperty RowsProperty=DependencyProperty.Register(nameof(Rows),typeof(int),typeof(DotGridPicker),new FrameworkPropertyMetadata(5,FrameworkPropertyMetadataOptions.AffectsRender,Changed),v=>v is int n && n>=1 && n<=MaximumRows);
    private (int Columns,int Rows)? _preview;
    private static readonly DependencyPropertyKey PreviewColumnsPropertyKey=DependencyProperty.RegisterReadOnly(nameof(PreviewColumns),typeof(int),typeof(DotGridPicker),new PropertyMetadata(4));
    private static readonly DependencyPropertyKey PreviewRowsPropertyKey=DependencyProperty.RegisterReadOnly(nameof(PreviewRows),typeof(int),typeof(DotGridPicker),new PropertyMetadata(5));
    public static readonly DependencyProperty PreviewColumnsProperty=PreviewColumnsPropertyKey.DependencyProperty;
    public static readonly DependencyProperty PreviewRowsProperty=PreviewRowsPropertyKey.DependencyProperty;
    public int PreviewColumns=>(int)GetValue(PreviewColumnsProperty);
    public int PreviewRows=>(int)GetValue(PreviewRowsProperty);
    internal event Action? PreviewChanged;
    private void Preview((int Columns,int Rows)? value)
    {
        _preview=value;
        SetValue(PreviewColumnsPropertyKey,value?.Columns ?? Columns);
        SetValue(PreviewRowsPropertyKey,value?.Rows ?? Rows);
        InvalidateVisual();
        PreviewChanged?.Invoke();
    }
    private bool _dragging;
    public int Columns {get=>(int)GetValue(ColumnsProperty);set=>SetValue(ColumnsProperty,value);}
    public int Rows {get=>(int)GetValue(RowsProperty);set=>SetValue(RowsProperty,value);}
    public DotGridPicker()
    {
        Focusable=true; Cursor=Cursors.Hand; MinHeight=180; Height=230; MaxWidth=336;
        SetResourceReference(AccentProperty,"AccentBrush"); SetResourceReference(MutedProperty,"MutedBrush");
        SetResourceReference(SurfaceProperty,"CardBrush");
        SetResourceReference(AutomationProperties.NameProperty,"Loc.IconGrid");
        SetResourceReference(HelpTemplateProperty,"Loc.GridAccessibleHelp");
        SetResourceReference(StatusTemplateProperty,"Loc.GridUnits");
        RefreshAccessibleText();
        Unloaded+=(_,_)=>Cancel(); IsVisibleChanged+=(_,e)=>{if(!(bool)e.NewValue) Cancel();};
        IsEnabledChanged+=(_,e)=>{if(!(bool)e.NewValue) Cancel();};
    }
    private static readonly DependencyProperty AccentProperty=DependencyProperty.Register("Accent",typeof(Brush),typeof(DotGridPicker),new FrameworkPropertyMetadata(Brushes.Cyan,FrameworkPropertyMetadataOptions.AffectsRender));
    private static readonly DependencyProperty MutedProperty=DependencyProperty.Register("Muted",typeof(Brush),typeof(DotGridPicker),new FrameworkPropertyMetadata(Brushes.Gray,FrameworkPropertyMetadataOptions.AffectsRender));
    private static readonly DependencyProperty SurfaceProperty=DependencyProperty.Register("Surface",typeof(Brush),typeof(DotGridPicker),new FrameworkPropertyMetadata(Brushes.Transparent,FrameworkPropertyMetadataOptions.AffectsRender));
    private static readonly DependencyProperty HelpTemplateProperty=DependencyProperty.Register("HelpTemplate",typeof(string),typeof(DotGridPicker),new PropertyMetadata("{0} × {1}",AccessibleTemplateChanged));
    private static readonly DependencyProperty StatusTemplateProperty=DependencyProperty.Register("StatusTemplate",typeof(string),typeof(DotGridPicker),new PropertyMetadata("{0} × {1}",AccessibleTemplateChanged));
    private static void AccessibleTemplateChanged(DependencyObject d,DependencyPropertyChangedEventArgs e)=>((DotGridPicker)d).RefreshAccessibleText();
    private void RefreshAccessibleText()
    {
        AutomationProperties.SetHelpText(this,string.Format((string)GetValue(HelpTemplateProperty),AvailableColumns,AvailableRows));
        AutomationProperties.SetItemStatus(this,string.Format((string)GetValue(StatusTemplateProperty),Columns,Rows));
    }
    private static void Changed(DependencyObject d,DependencyPropertyChangedEventArgs e)
    {
        var picker=(DotGridPicker)d;
        picker.Preview(picker._preview);
        picker.RefreshAccessibleText();
        if(UIElementAutomationPeer.FromElement(picker) is DotGridPeer peer)
            peer.RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty,null,peer.Value);
    }
    internal (int Columns,int Rows) CellAt(Point p)
        => (Math.Clamp((int)Math.Floor(p.X/Math.Max(1,ActualWidth)*AvailableColumns)+1,1,AvailableColumns),
            Math.Clamp((int)Math.Floor(p.Y/Math.Max(1,ActualHeight)*AvailableRows)+1,1,AvailableRows));
    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRoundedRectangle((Brush)GetValue(SurfaceProperty),null,new Rect(RenderSize),12,12);
        var selection=_preview ?? (Columns,Rows);
        var accent=(Brush)GetValue(AccentProperty); var muted=(Brush)GetValue(MutedProperty);
        double width=ActualWidth/AvailableColumns,height=ActualHeight/AvailableRows;
        double radius=Math.Max(1,Math.Min(4,Math.Min(width,height)*.2));
        for(int row=0;row<AvailableRows;row++) for(int column=0;column<AvailableColumns;column++)
        {
            bool selected=column<selection.Item1 && row<selection.Item2;
            dc.PushOpacity(IsEnabled ? selected ? 1 : .35 : .3);
            dc.DrawEllipse(selected ? accent : muted,null,new((column+.5)*width,(row+.5)*height),radius,radius);
            dc.Pop();
        }
        if(IsKeyboardFocused) dc.DrawRoundedRectangle(null,new Pen(accent,1),new Rect(.5,.5,Math.Max(0,ActualWidth-1),Math.Max(0,ActualHeight-1)),12,12);
    }
    protected override void OnMouseMove(MouseEventArgs e) {base.OnMouseMove(e); Preview(CellAt(e.GetPosition(this)));}
    protected override void OnMouseLeave(MouseEventArgs e) {base.OnMouseLeave(e);if(!_dragging){Preview(null);}}
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e); Focus(); _dragging=CaptureMouse(); Preview(CellAt(e.GetPosition(this)));e.Handled=true;
    }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);if(!_dragging)return;
        var selected=CellAt(e.GetPosition(this)); _dragging=false; ReleaseMouseCapture();
        SetCurrentValue(ColumnsProperty,selected.Columns);SetCurrentValue(RowsProperty,selected.Rows);Preview(null);e.Handled=true;
    }
    protected override void OnLostMouseCapture(MouseEventArgs e) {base.OnLostMouseCapture(e);_dragging=false;Preview(null);}
    private void Cancel(){_dragging=false;if(IsMouseCaptured)ReleaseMouseCapture();Preview(null);}
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e){base.OnGotKeyboardFocus(e);InvalidateVisual();}
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e){base.OnLostKeyboardFocus(e);InvalidateVisual();}
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if(e.Key==Key.Escape && _dragging){Cancel();e.Handled=true;return;}
        int columns=Columns,rows=Rows;
        switch(e.Key){case Key.Left:columns--;break;case Key.Right:columns++;break;case Key.Up:rows--;break;case Key.Down:rows++;break;case Key.Home:columns=rows=1;break;case Key.End:columns=AvailableColumns;rows=AvailableRows;break;default:return;}
        Preview(null);SetCurrentValue(ColumnsProperty,Math.Clamp(columns,1,AvailableColumns));SetCurrentValue(RowsProperty,Math.Clamp(rows,1,AvailableRows));e.Handled=true;
    }
    protected override AutomationPeer OnCreateAutomationPeer()=>new DotGridPeer(this);
    private sealed class DotGridPeer(DotGridPicker owner) : FrameworkElementAutomationPeer(owner), IValueProvider
    {
        protected override string GetClassNameCore()=>nameof(DotGridPicker);
        protected override AutomationControlType GetAutomationControlTypeCore()=>AutomationControlType.Custom;
        public override object? GetPattern(PatternInterface pattern)=>pattern==PatternInterface.Value ? this : base.GetPattern(pattern);
        public bool IsReadOnly=>!owner.IsEnabled;
        public string Value=>$"{owner.Columns} × {owner.Rows}";
        public void SetValue(string value)
        {
            if(!owner.IsEnabled)throw new ElementNotEnabledException();
            var parts=value.Replace('×','x').Split('x');
            if(parts.Length!=2 || !int.TryParse(parts[0],NumberStyles.Integer,CultureInfo.InvariantCulture,out int c) || !int.TryParse(parts[1],NumberStyles.Integer,CultureInfo.InvariantCulture,out int r) || c<1 || c>owner.AvailableColumns || r<1 || r>owner.AvailableRows)throw new ArgumentException(string.Format(Services.LocalizationService.Get("GridCapacityError"),owner.AvailableColumns,owner.AvailableRows),nameof(value));
            owner.SetCurrentValue(ColumnsProperty,c);owner.SetCurrentValue(RowsProperty,r);
        }
    }
}
