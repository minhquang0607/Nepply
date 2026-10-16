using Nepply.Models;
using Nepply.Modules.Panel;

internal static class LayoutEditSessionTests
{
    internal static void Run(Action<string,bool> check)
    {
        var session = new LayoutEditSession();
        var settings = new AppSettings { IconOrder = ["a", "b"], WidgetOrder = ["summary", "gpus", "network", "details"] };
        int saves = 0;
        check("Layout inactive completion is a no-op", !session.Complete(true, _ => saves++) && saves == 0);
        session.Begin(settings);
        check("Layout begins clean", session.IsActive && !session.HasChanges);
        bool rejected = false;
        try { session.Begin(settings); } catch (InvalidOperationException) { rejected = true; }
        check("Nested layout cannot overwrite rollback snapshot", rejected && session.IsActive);
        settings.IconOrder[0] = "changed";
        settings.GridCellWidth = 140;
        check("Layout tracks array and size edits", session.HasChanges);
        bool failed = false;
        try { session.Complete(true, _ => throw new System.IO.IOException("Fixture save failure")); }
        catch (System.IO.IOException) { failed = true; }
        check("Failed persistence retains draft and rollback", failed && session.IsActive && session.HasChanges && settings.GridCellWidth == 140);
        session.Complete(false, _ => saves++);
        check("Cancel restores copied arrays without writing disk", settings.IconOrder.SequenceEqual(new[] { "a", "b" })
            && settings.GridCellWidth != 140 && !session.IsActive && saves == 0);
        session.Begin(settings);
        session.Complete(true, _ => saves++);
        check("Unchanged layout avoids persistence", !session.IsActive && saves == 0);
        session.Begin(settings); settings.IconsFirst = !settings.IconsFirst;
        session.Complete(true, current => { check("Commit uses current target", ReferenceEquals(current, settings)); saves++; });
        check("Successful layout commit releases transaction", !session.IsActive && !session.HasChanges && saves == 1);
        check("Repeated completion cannot save twice", !session.Complete(true, _ => saves++) && saves == 1);
        session.Begin(settings); settings.GridCellHeight = 150;
        try { session.Complete(true, _ => throw new System.IO.IOException()); } catch (System.IO.IOException) { }
        session.Complete(true, _ => saves++);
        check("Save can retry after failure", !session.IsActive && settings.GridCellHeight == 150 && saves == 2);
    }
}
