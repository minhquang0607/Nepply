using Nepply.Modules.Search;

internal static class SearchSessionTests
{
    internal static void Run(Action<string, bool> check)
    {
        var editor = new System.Windows.Controls.Border { Width = 300, Height = 32, Visibility = System.Windows.Visibility.Collapsed };
        var anchor = new System.Windows.Controls.Button();
        var reveal = new SearchReveal();
        reveal.Set(editor, anchor, true);
        check("Unloaded search reveal settles without leaving clips", editor.Visibility == System.Windows.Visibility.Visible && editor.Clip == null);
        reveal.Settle(editor, false);
        reveal.Set(editor, anchor, true);
        check("Search reveal can reopen after lifecycle reset", editor.Visibility == System.Windows.Visibility.Visible);
        reveal.Settle(editor, false);
        check("Search lifecycle reset releases presentation", editor.Visibility == System.Windows.Visibility.Collapsed && editor.Clip == null && ReferenceEquals(editor.RenderTransform, System.Windows.Media.Transform.Identity));
        bool keepOpen = false;
        var changes = new List<bool>();
        using var session = new SearchSession(() => keepOpen, changes.Add);
        check("Search starts collapsed with no idle timer", !session.IsExpanded && !session.IsDismissPending);
        session.RequestDismiss();
        check("Collapsed search never schedules work", !session.IsDismissPending);
        session.SetExpanded(true);
        session.SetExpanded(true);
        check("Search expansion is idempotent", session.IsExpanded && changes.SequenceEqual(new[] { true }));
        session.RequestDismiss();
        check("Leaving expanded search schedules dismissal", session.IsDismissPending);
        session.SetExpanded(true);
        check("Reenter cancels pending dismissal without replaying presentation", !session.IsDismissPending && changes.Count == 1);
        keepOpen = true;
        session.RequestDismiss();
        session.EvaluateDismiss();
        check("Query or focus keeps search open without a polling timer", session.IsExpanded && !session.IsDismissPending);
        keepOpen = false;
        session.RequestDismiss();
        session.EvaluateDismiss();
        check("Empty unfocused search collapses once", !session.IsExpanded && changes.SequenceEqual(new[] { true, false }));
        session.SetExpanded(true);
        bool cleared = false;
        session.DismissAndClear(() => { cleared = true; session.RequestDismiss(); });
        check("Blank-click clear cancels reentrant TextChanged dismissal", cleared && !session.IsExpanded && !session.IsDismissPending);
        session.SetExpanded(true);
        session.RequestDismiss();
        session.Suspend(hasQuery: true);
        check("Hidden search retains query presentation without timer", session.IsExpanded && !session.IsDismissPending);
        session.Suspend(hasQuery: false);
        check("Hidden empty search collapses", !session.IsExpanded);
        session.SetExpanded(true);
        session.RequestDismiss();
        session.Dispose();
        int count = changes.Count;
        session.RequestDismiss(); session.EvaluateDismiss(); session.SetExpanded(false);
        session.DismissAndClear(() => throw new Exception("Disposed session called host"));
        check("Disposed search stops timer and never calls its host", !session.IsDismissPending && changes.Count == count);
    }
}
