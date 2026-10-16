using System.Windows;
using System.Windows.Controls;

namespace Nepply.Views;

/// <summary>Presentation only: no sampling, subscriptions, timers or private data context.</summary>
public partial class MetricReadingView : UserControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label),typeof(string),typeof(MetricReadingView),new PropertyMetadata(""));
    public static readonly DependencyProperty ValueTextProperty = DependencyProperty.Register(nameof(ValueText),typeof(string),typeof(MetricReadingView),new PropertyMetadata("—"));
    public static readonly DependencyProperty SecondaryTextProperty = DependencyProperty.Register(nameof(SecondaryText),typeof(string),typeof(MetricReadingView),new PropertyMetadata(""));
    public static readonly DependencyProperty FootnoteProperty = DependencyProperty.Register(nameof(Footnote),typeof(string),typeof(MetricReadingView),new PropertyMetadata(""));
    public static readonly DependencyProperty LoadProperty = DependencyProperty.Register(nameof(Load),typeof(double),typeof(MetricReadingView),new PropertyMetadata(0d));
    public string Label {get=>(string)GetValue(LabelProperty);set=>SetValue(LabelProperty,value);}
    public string ValueText {get=>(string)GetValue(ValueTextProperty);set=>SetValue(ValueTextProperty,value);}
    public string SecondaryText {get=>(string)GetValue(SecondaryTextProperty);set=>SetValue(SecondaryTextProperty,value);}
    public string Footnote {get=>(string)GetValue(FootnoteProperty);set=>SetValue(FootnoteProperty,value);}
    public double Load {get=>(double)GetValue(LoadProperty);set=>SetValue(LoadProperty,value);}
    public MetricReadingView() => InitializeComponent();
}
