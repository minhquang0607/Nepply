using System.Collections.Specialized;
using Nepply.Models;
using Nepply.Modules.Desktop;

internal static class DesktopCollectionTests
{
    internal static void Run(Action<string, bool> check)
    {
        var items = Enumerable.Range(0, 500).Select(i => new DesktopIcon { Name = $"Item {i}", Path = $"fixture-{i}" }).ToArray();
        var collection = new DesktopIconCollection();
        var changes = new List<NotifyCollectionChangedAction>();
        collection.CollectionChanged += (_, e) => changes.Add(e.Action);
        collection.ApplySnapshot(items);
        check("500-item initial snapshot is one batch", changes.SequenceEqual([NotifyCollectionChangedAction.Reset]));
        foreach (int index in new[] { 0, 250, 499 })
        {
            collection.ApplySnapshot(items); changes.Clear();
            var remaining = items.Where((_, i) => i != index).ToArray();
            collection.ApplySnapshot(remaining);
            check("Single deletion preserves 499 items with one Remove: " + index,
                changes.SequenceEqual([NotifyCollectionChangedAction.Remove]) && collection.SequenceEqual(remaining));
            changes.Clear(); collection.ApplySnapshot(items);
            check("Single insertion uses one Add: " + index, changes.SequenceEqual([NotifyCollectionChangedAction.Add]) && collection.SequenceEqual(items));
        }
        changes.Clear(); collection.ApplySnapshot(items);
        check("Unchanged collection sends no notification", changes.Count == 0);
        changes.Clear(); collection.ApplySnapshot(items.Where((_, i) => i % 2 == 0).ToArray());
        check("Large scattered deletion is one Reset", changes.SequenceEqual([NotifyCollectionChangedAction.Reset]) && collection.Count == 250);
        var random = new Random(761);
        bool correct = true;
        for (int i = 0; i < 100; i++)
        {
            var next = items.Where(_ => random.Next(4) != 0).OrderBy(_ => random.Next()).ToArray();
            collection.ApplySnapshot(next);
            correct &= collection.SequenceEqual(next);
        }
        check("Collection reconciliation preserves exact randomized membership and order", correct);
    }
}
