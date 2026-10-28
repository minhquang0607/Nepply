using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Nepply.Views;

/// <summary>Viewport-bounded FLIP motion; never animates layout size or scales icon text.</summary>
internal sealed class GridReflow
{
    private sealed class Entry(object? item, Rect slot, TranslateTransform transform)
    {
        internal object? Item = item;
        internal Rect Slot = slot;
        internal TranslateTransform Transform = transform;
        internal int Generation;
    }
    private readonly Dictionary<UIElement, Entry> _entries = [];
    internal int Count => _entries.Count;
    internal void Arrange(UIElement child, Rect slot, bool animate)
    {
        object? item = (child as FrameworkElement)?.DataContext;
        if (!_entries.TryGetValue(child, out var entry))
        {
            // Do not replace a consumer-owned transform.
            if (!ReferenceEquals(child.RenderTransform, Transform.Identity)) { child.Arrange(slot); return; }
            entry = new(item, slot, new TranslateTransform()); _entries.Add(child, entry);
            child.RenderTransform = entry.Transform;
        }
        var old = entry.Slot;
        bool sameItem = ReferenceEquals(item, entry.Item);
        child.Arrange(slot);
        if (old == slot && sameItem && animate) return;
        var offset = new Vector(old.X - slot.X + entry.Transform.X, old.Y - slot.Y + entry.Transform.Y);
        Stop(entry); entry.Slot = slot; entry.Item = item;
        if (!animate || !sameItem || !Nepply.Modules.Appearance.MotionPolicy.Enabled || offset.Length < .5) return;
        int generation = entry.Generation;
        var x = new DoubleAnimation(offset.X, 0, Nepply.Modules.Appearance.MotionPolicy.Duration(200)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
        var y = new DoubleAnimation(offset.Y, 0, Nepply.Modules.Appearance.MotionPolicy.Duration(200)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
        y.Completed += (_, _) => { if (entry.Generation == generation) Stop(entry); };
        entry.Transform.BeginAnimation(TranslateTransform.XProperty, x);
        entry.Transform.BeginAnimation(TranslateTransform.YProperty, y);
    }
    private static void Stop(Entry entry)
    {
        entry.Generation++;
        entry.Transform.BeginAnimation(TranslateTransform.XProperty, null);
        entry.Transform.BeginAnimation(TranslateTransform.YProperty, null);
        entry.Transform.X = entry.Transform.Y = 0;
    }
    internal void Retain(IReadOnlySet<UIElement> live)
    {
        foreach (var child in _entries.Keys.Where(c => !live.Contains(c)).ToArray())
        { Release(child, _entries[child]); _entries.Remove(child); }
    }
    private static void Release(UIElement child, Entry entry)
    {
        Stop(entry);
        if (ReferenceEquals(child.RenderTransform, entry.Transform)) child.ClearValue(UIElement.RenderTransformProperty);
    }
    internal void Clear()
    {
        foreach (var pair in _entries) Release(pair.Key, pair.Value);
        _entries.Clear();
    }
}
