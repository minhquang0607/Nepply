using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Nepply.Models;
using Nepply.Services;
using Nepply.Modules.Desktop;
namespace Nepply.ViewModels;

public sealed class DesktopIconViewModel : INotifyPropertyChanged
{
    private AppSettings? _settings;
    private IReadOnlyList<DesktopIcon> _all = [];
    private IReadOnlyList<DesktopIcon>? _rankedSnapshot;
    private string[] _rankedOrder = [];
    private readonly Dictionary<string, int> _ranks = new(StringComparer.OrdinalIgnoreCase);
    private string _search = "";
    private bool _loading = true;
    private bool _scanning;
    public bool IsScanning => _scanning;
    public bool CanRefresh => !_scanning;
    public string ScanCaption => LocalizationService.Get(_scanning ? "Updating" : "Desktop");
    private void SetScanning(bool scanning)
    {
        if (_scanning == scanning) return;
        _scanning = scanning; Changed(nameof(IsScanning)); Changed(nameof(CanRefresh)); Changed(nameof(ScanCaption));
    }
    private sealed class Subscription
    {
        internal required IDesktopCatalog Service;
        internal Action Handler = null!;
        internal Action StateHandler = null!;
        internal int Pending;
        internal int IconsDirty;
    }
    private Subscription? _subscription;
    private DesktopIcon? _selectedIcon;
    public DesktopIcon? SelectedIcon
    {
        get => _selectedIcon;
        set { if (ReferenceEquals(_selectedIcon, value)) return; _selectedIcon = value; Changed(); }
    }
    private readonly DesktopIconCollection _icons = new();
    public ObservableCollection<DesktopIcon> Icons => _icons;
    public string SearchQuery { get => _search; set { if (_search == value) return; _search = value; Changed(); Filter(); } }
    public bool IsLoading { get => _loading; private set { _loading = value; Changed(); Changed(nameof(IsEmpty)); } }
    private string? _scanError;
    public string? ScanError { get => _scanError == null ? null : LocalizationService.Get(_scanError); private set => _scanError = value; }
    public bool HasScanError => ScanError != null;
    public bool IsEmpty => !IsLoading && !HasScanError && Icons.Count == 0;
    public bool HasSearch => !string.IsNullOrWhiteSpace(_search);
    public string CountText => string.Format(LocalizationService.Get("Items"), HasSearch ? $"{Icons.Count} / {_all.Count}" : Icons.Count.ToString());
    public string EmptyTitle => LocalizationService.Get(HasSearch ? "NoResults" : "EmptyDesktop");
    public string EmptyHint => LocalizationService.Get(HasSearch ? "SearchHint" : "EmptyHint");
    private void LanguageChanged()
    {
        Changed(nameof(ScanCaption)); Changed(nameof(CountText)); Changed(nameof(EmptyTitle)); Changed(nameof(EmptyHint)); Changed(nameof(ScanError));
        foreach (var icon in Icons) icon.RefreshSourceDescription();
    }
    public void Initialize(IDesktopCatalog service, AppSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(service);
        Cleanup(); _settings = settings;
        LocalizationService.Changed += LanguageChanged;
        var subscription = new Subscription { Service = service };
        subscription.Handler = () => Refresh(subscription, true);
        subscription.StateHandler = () => Refresh(subscription, false);
        Volatile.Write(ref _subscription, subscription);
        service.IconsChanged += subscription.Handler; service.ScanStateChanged += subscription.StateHandler;
        Refresh(subscription, true);
    }
    public void Cleanup()
    {
        LocalizationService.Changed -= LanguageChanged;
        var previous = Interlocked.Exchange(ref _subscription, null);
        if (previous != null)
        {
            previous.Service.IconsChanged -= previous.Handler;
            previous.Service.ScanStateChanged -= previous.StateHandler;
        }
        _all = []; SelectedIcon = null; Icons.Clear(); ScanError = null;
        _rankedSnapshot = null; _rankedOrder = []; _ranks.Clear();
        SetScanning(false);
        IsLoading = true; Changed(nameof(HasScanError)); Changed(nameof(ScanError)); Changed(nameof(CountText));
    }
    private void Refresh(Subscription subscription, bool iconsChanged)
    {
        if (!ReferenceEquals(Volatile.Read(ref _subscription), subscription)) return;
        // Mark before coalescing: a queued state-only update must not swallow a
        // subsequent icon update. Each subscription owns its own pending flags.
        if (iconsChanged) Interlocked.Exchange(ref subscription.IconsDirty, 1);
        if (Interlocked.Exchange(ref subscription.Pending, 1) == 1) return;
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            Interlocked.Exchange(ref subscription.Pending, 0);
            if (!ReferenceEquals(_subscription, subscription)) return;
            bool refreshIcons = Interlocked.Exchange(ref subscription.IconsDirty, 0) == 1;
            // Catalog owns stable snapshot membership; retain it until publication
            // changes rather than copying and invalidating rank caches on every signal.
            if (refreshIcons) _all = subscription.Service.GetIcons();
            var state = subscription.Service.ScanState;
            SetScanning(state.IsScanning);
            ScanError = state.Error;
            Changed(nameof(ScanError)); Changed(nameof(HasScanError));
            IsLoading = !state.HasSnapshot && state.Error == null;
            if (refreshIcons) Filter();
        });
    }
    private void Filter(string? movedPath = null)
    {
        // Collection replacements can clear the ListBox selection through its
        // two-way binding. Capture identity before touching the collection.
        string? selectedPath = SelectedIcon?.Path;
        var words = SearchText.Words(_search);
        var result = Ordered().Where(i => SearchText.Matches(i.Name, words)).ToArray();
        bool moved = false;
        if (movedPath != null && result.Length == Icons.Count)
        {
            int from = -1, to = -1;
            for (int i = 0; i < result.Length; i++)
            {
                if (string.Equals(Icons[i].Path, movedPath, StringComparison.OrdinalIgnoreCase)) from = i;
                if (string.Equals(result[i].Path, movedPath, StringComparison.OrdinalIgnoreCase)) to = i;
            }
            if (from >= 0 && to >= 0)
            {
                // Only use Move when it produces the exact requested snapshot.
                // Scanner replacements still take the normal reconciliation path.
                bool same = true;
                for (int i = 0; i < result.Length && same; i++)
                {
                    int old = i == to ? from : from < to && i >= from && i < to ? i + 1
                        : from > to && i > to && i <= from ? i - 1 : i;
                    same = ReferenceEquals(Icons[old], result[i]);
                }
                if (same) { if (from != to) Icons.Move(from, to); moved = true; }
            }
        }
        if (!moved) _icons.ApplySnapshot(result);
        SelectedIcon = selectedPath == null ? null : result.FirstOrDefault(i =>
            string.Equals(i.Path, selectedPath, StringComparison.OrdinalIgnoreCase));
        Changed(nameof(IsEmpty)); Changed(nameof(CountText));
        Changed(nameof(HasSearch)); Changed(nameof(EmptyTitle)); Changed(nameof(EmptyHint));
    }
    public void RefreshOrder() => Filter();
    public event PropertyChangedEventHandler? PropertyChanged;
    private IEnumerable<DesktopIcon> Ordered()
    {
        var order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in _settings?.IconOrder ?? [])
            if (!string.IsNullOrWhiteSpace(path)) order.TryAdd(path, order.Count);
        return _all.OrderBy(i => order.GetValueOrDefault(i.Path, int.MaxValue));
    }
    public bool Reorder(string source, string target, bool after = false)
    {
        if (!TryPlanReorder(source, target, after, out var ordered, out int sourceIndex, out int insertion)) return false;
        string canonicalSource = ordered[sourceIndex];
        ordered.RemoveAt(sourceIndex); ordered.Insert(insertion, canonicalSource);
        _settings!.IconOrder = ordered.ToArray(); Filter(canonicalSource); return true;
    }
    internal bool MoveVisible(string source, int direction)
    {
        if (direction is not (-1 or 1)) return false;
        int index = -1;
        for (int i = 0; i < Icons.Count; i++)
            if (string.Equals(Icons[i].Path, source, StringComparison.OrdinalIgnoreCase)) { index = i; break; }
        int next = index + direction;
        if (index < 0 || next < 0 || next >= Icons.Count) return false;
        return Reorder(source, Icons[next].Path, after: direction > 0);
    }
    internal bool CanReorder(string source, string target, bool after = false)
    {
        if (_settings == null || string.Equals(source, target, StringComparison.OrdinalIgnoreCase)) return false;
        var order = _settings.IconOrder ?? [];
        // DragOver fires repeatedly. Sort only when the desktop snapshot or saved
        // order changes; also detect in-place edits of the settings array.
        if (!ReferenceEquals(_rankedSnapshot, _all) || !_rankedOrder.SequenceEqual(order, StringComparer.OrdinalIgnoreCase))
        {
            _ranks.Clear();
            int index = 0;
            foreach (var icon in Ordered()) _ranks.TryAdd(icon.Path, index++);
            _rankedOrder = order.ToArray();
            _rankedSnapshot = _all;
        }
        if (!_ranks.TryGetValue(source, out int sourceIndex) || !_ranks.TryGetValue(target, out int targetIndex)) return false;
        int insertion = targetIndex + (after ? 1 : 0);
        if (sourceIndex < insertion) insertion--;
        return insertion != sourceIndex;
    }
    private bool TryPlanReorder(string source, string target, bool after, out List<string> ordered, out int sourceIndex, out int insertion)
    {
        ordered = []; sourceIndex = -1; insertion = -1;
        if (_settings == null || string.Equals(source, target, StringComparison.OrdinalIgnoreCase)) return false;
        ordered = Ordered().Select(i => i.Path).ToList();
        sourceIndex = ordered.FindIndex(path => string.Equals(path, source, StringComparison.OrdinalIgnoreCase));
        int targetIndex = ordered.FindIndex(path => string.Equals(path, target, StringComparison.OrdinalIgnoreCase));
        if (sourceIndex < 0 || targetIndex < 0) return false;
        insertion = targetIndex + (after ? 1 : 0);
        if (sourceIndex < insertion) insertion--;
        return insertion != sourceIndex;
    }
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
