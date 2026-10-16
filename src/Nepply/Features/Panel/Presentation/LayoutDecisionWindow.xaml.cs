using System.Windows;

namespace Nepply.Views;

public partial class LayoutDecisionWindow : Window
{
    internal bool SaveRequested { get; private set; }
    public LayoutDecisionWindow() => InitializeComponent();
    private void Save_Click(object sender,RoutedEventArgs e) { SaveRequested=true; DialogResult=true; }
    private void Discard_Click(object sender,RoutedEventArgs e) { SaveRequested=false; DialogResult=true; }
    private void Continue_Click(object sender,RoutedEventArgs e) => DialogResult=false;
}
