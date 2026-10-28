using Nepply.Models;
using Nepply.Modules.Desktop;
using Nepply.ViewModels;
using Nepply.Views;

internal static class DesktopContractTests
{
    internal static void Run(Action<string, bool> check, Action<int> pump)
    {
        var catalog = new Catalog();
        var vm = new DesktopIconViewModel();
        vm.Initialize(catalog);
        pump(30);
        check("Desktop ViewModel runs with catalog only, without Shell service", vm.IsLoading && catalog.Subscribers == 2);
        catalog.Publish([new DesktopIcon { Name = "Tài liệu", Path = "fixture-doc" }]);
        pump(30);
        check("Catalog publication reaches the presentation", !vm.IsLoading && vm.Icons.Count == 1 && vm.Icons[0].Name == "Tài liệu");
        var snapshotField = typeof(DesktopIconViewModel).GetField("_all", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        check("Catalog consumer retains published snapshot without membership copy", ReferenceEquals(snapshotField.GetValue(vm), catalog.GetIcons()));
        var original = catalog.GetIcons();
        int collectionChanges = 0;
        vm.Icons.CollectionChanged += (_, _) => collectionChanges++;
        catalog.CaptureIconsCallback()?.Invoke(); pump(30);
        check("Repeated catalog signal preserves snapshot and presentation containers", ReferenceEquals(snapshotField.GetValue(vm), original) && collectionChanges == 0);
        vm.SearchQuery = "tai lieu";
        check("Catalog abstraction preserves Vietnamese search", vm.Icons.Count == 1);
        var stale = catalog.CaptureIconsCallback();
        var replacement = new Catalog();
        vm.Initialize(replacement);
        replacement.Publish([new DesktopIcon { Name = "New", Path = "replacement" }]);
        vm.SearchQuery = "";
        stale?.Invoke();
        pump(30);
        check("Replacing catalog detaches old source and rejects late callbacks", catalog.Subscribers == 0 && vm.Icons.Single().Path == "replacement");
        vm.Cleanup();
        check("ViewModel cleanup releases catalog subscriptions without disposing owner", replacement.Subscribers == 0);
        check("ViewModel cleanup releases retained snapshot", !ReferenceEquals(snapshotField.GetValue(vm), replacement.GetIcons()) && ((IReadOnlyList<DesktopIcon>)snapshotField.GetValue(vm)!).Count == 0);

        var actions = new Actions();
        var panel = new IconPanel();
        panel.Initialize(catalog, actions, new AppSettings());
        pump(30);
        var retry = typeof(IconPanel).GetMethod("RetryScan_Click", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        retry.Invoke(panel, [panel, new System.Windows.RoutedEventArgs()]);
        check("Panel routes retry to action boundary", actions.Refreshes == 1);
        panel.Cleanup();
        retry.Invoke(panel, [panel, new System.Windows.RoutedEventArgs()]);
        check("Cleaned panel cannot invoke stale Desktop actions", actions.Refreshes == 1 && catalog.Subscribers == 0);
        CollectionReset(check, pump);
    }

    private static void CollectionReset(Action<string, bool> check, Action<int> pump)
    {
        var catalog = new Catalog();
        var items = Enumerable.Range(0, 500).Select(i => new DesktopIcon { Name = $"Item {i:D3}", Path = $"fixture-{i}" }).ToArray();
        catalog.Publish(items);
        var panel = new IconPanel();
        panel.Initialize(catalog, new Actions(), new AppSettings());
        var window = new System.Windows.Window { Content = panel, Width = 380, Height = 540, Left = -10000, Top = -10000, ShowInTaskbar = false, ShowActivated = false };
        try
        {
            window.Show(); pump(60); window.UpdateLayout();
            var list = (System.Windows.Controls.ListBox)panel.FindName("IconList");
            var grid = FindGrid(list)!;
            var vm = (DesktopIconViewModel)panel.DataContext;
            vm.SelectedIcon = items[240];
            grid.SetVerticalOffset(grid.RowHeight * 60); pump(30); window.UpdateLayout();
            int first = (int)(grid.VerticalOffset / grid.RowHeight) * grid.ColumnCount;
            var anchor = vm.Icons[first];
            var next = items.Where((_, i) => i >= 100).ToArray();
            catalog.Publish(next); pump(60); window.UpdateLayout();
            int anchorIndex = vm.Icons.IndexOf(anchor);
            var bounds = grid.SlotBounds(anchorIndex);
            check("Batched catalog keeps selected identity through WPF binding", vm.SelectedIcon == items[240] && list.SelectedItem == items[240]);
            check("Batched catalog keeps surviving scroll anchor visible", anchorIndex >= 0 && bounds.Top >= -1 && bounds.Top < grid.RowHeight + 1);
            check("500-item batched catalog keeps WPF containers bounded", System.Windows.Media.VisualTreeHelper.GetChildrenCount(grid) < 60);
            catalog.Publish([]); pump(30); window.UpdateLayout();
            check("Empty batched catalog clears selection and virtualized containers", vm.SelectedIcon == null && grid.VerticalOffset == 0 && System.Windows.Media.VisualTreeHelper.GetChildrenCount(grid) == 0);
        }
        finally { panel.Cleanup(); window.Close(); }
    }

    private static IconGridPanel? FindGrid(System.Windows.DependencyObject root)
    {
        if (root is IconGridPanel grid) return grid;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
            if (FindGrid(System.Windows.Media.VisualTreeHelper.GetChild(root, i)) is { } found) return found;
        return null;
    }

    private sealed class Catalog : IDesktopCatalog
    {
        private DesktopIcon[] _items = [];
        public IReadOnlyList<DesktopIcon> GetIcons() => _items;
        public DesktopScanState ScanState { get; private set; } = new(false, false);
        public event Action? IconsChanged;
        public event Action? ScanStateChanged;
        internal int Subscribers => (IconsChanged?.GetInvocationList().Length ?? 0) + (ScanStateChanged?.GetInvocationList().Length ?? 0);
        internal Action? CaptureIconsCallback() => IconsChanged;
        internal void Publish(DesktopIcon[] items)
        {
            _items = items;
            ScanState = new(false, true);
            ScanStateChanged?.Invoke();
            IconsChanged?.Invoke();
        }
    }

    private sealed class Actions : IDesktopActions
    {
        internal int Refreshes;
        public void Refresh() => Refreshes++;
        public Nepply.UserMessage? LaunchIcon(DesktopIcon icon) => throw new InvalidOperationException("Native action must not run in this fixture");
        public Nepply.UserMessage? ShowShellContextMenu(DesktopIcon icon, IntPtr hwnd, System.Windows.Point? screenAnchor = null)
            => throw new InvalidOperationException("Native menu must not run in this fixture");
    }
}
