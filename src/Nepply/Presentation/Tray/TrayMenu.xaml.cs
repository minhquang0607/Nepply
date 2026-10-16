using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Nepply.Models;

namespace Nepply.Views;

public partial class TrayMenu : ContextMenu
{
    private readonly MainWindow _window;
    private readonly Action _openLogs, _exit;
    private bool _holdingPanel;

    public TrayMenu(MainWindow window, Action openLogs, Action exit)
    {
        _window=window; _openLogs=openLogs; _exit=exit;
        InitializeComponent();
        var version=typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        VersionLabel.Text=version?.Split('+')[0] ?? "";
        RefreshStatus();
    }

    internal void RefreshStatus()
    {
        // A closed tray popup is detached from the window tree. Refresh its
        // palette on opening, including changes made while it was closed.
        foreach(string key in new[] {"IslandBrush","SurfaceBrush","BorderBrush","TextBrush","MutedBrush","AccentBrush","AccentSourceBrush","AccentWashBrush","HoverBrush"})
            Resources[key]=Application.Current.FindResource(key);
        string edge=Services.LocalizationService.Get(_window.Settings.EdgePosition.ToString());
        StatusLabel.Text=$"{edge} · {Services.LocalizationService.Get(_window.IsPinned ? "Pinned" : "AutoHide")}";
        PinItem.IsChecked=_window.IsPinned;
        OpenItem.InputGestureText=MainWindow.HotkeyLabel(_window.Settings);
    }
    private void Menu_Opened(object sender,RoutedEventArgs e)
    {
        RefreshStatus();
        if (_holdingPanel) return;
        _holdingPanel=true; _window.SuppressHide();
    }
    private void Menu_Closed(object sender,RoutedEventArgs e)
    {
        if (!_holdingPanel) return;
        _holdingPanel=false; _window.ResumeHide();
    }
    private void Run(Action action) { IsOpen=false; action(); }
    private void Open_Click(object sender,RoutedEventArgs e) => Run(_window.OpenForInteraction);
    private void Pin_Click(object sender,RoutedEventArgs e) => Run(()=> { _window.TogglePin(); if(_window.IsPinned) _window.OpenForInteraction(); });
    private void Refresh_Click(object sender,RoutedEventArgs e) => Run(_window.RefreshIcons);
    private void Settings_Click(object sender,RoutedEventArgs e) => Run(_window.OpenSettings);
    private void Logs_Click(object sender,RoutedEventArgs e) => Run(_openLogs);
    private void Exit_Click(object sender,RoutedEventArgs e) => Run(_exit);
}
