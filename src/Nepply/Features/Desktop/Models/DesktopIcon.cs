using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Nepply.Models;

public class DesktopIcon : INotifyPropertyChanged
{
    internal string? GroupIdentity { get; set; }
    public string[] SourcePaths { get; set; } = [];
    public UserMessage SourceDescription => SourcePaths.Length > 1
        ? UserMessage.FromKey("GroupedShortcutDescription", SourcePaths.Length, Path, string.Join("\n", SourcePaths))
        : UserMessage.FromKey("DesktopPathDescription", Path);
    internal void RefreshSourceDescription() => OnPropertyChanged(nameof(SourceDescription));
    private string _name = string.Empty;
    private string _path = string.Empty;
    private ImageSource? _icon;
    private bool _isSystemIcon;

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string Path
    {
        get => _path;
        set { _path = value; OnPropertyChanged(); }
    }

    public ImageSource? Icon
    {
        get => _icon;
        set { _icon = value; OnPropertyChanged(); }
    }

    public bool IsSystemIcon
    {
        get => _isSystemIcon;
        set { _isSystemIcon = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
