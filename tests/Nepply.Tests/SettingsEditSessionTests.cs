using Nepply.Models;
using Nepply.Modules.Settings;

internal static class SettingsEditSessionTests
{
    internal static void Run(Action<string,bool> check)
    {
        var target = new AppSettings { IconOrder = ["a", "b"] };
        var original = target.Clone();
        bool fail = true;
        int writes = 0;
        AppSettings? persisted = null;
        var session = new SettingsEditSession(target, candidate =>
        {
            writes++;
            if (fail) throw new System.IO.IOException("Fixture failure");
            check("Settings target unchanged until persistence completes", target.PanelWidth == original.PanelWidth);
            persisted = candidate;
        });
        session.Draft.IconOrder[0] = "draft";
        session.Draft.PanelWidth = 500;
        check("Settings draft arrays do not mutate target", target.IconOrder[0] == "a" && target.PanelWidth == original.PanelWidth);
        try { session.Commit(); } catch (System.IO.IOException) { }
        check("Failed settings save keeps target and editable draft", writes == 1 && target.PanelWidth == original.PanelWidth && session.Draft.PanelWidth == 500);
        fail = false; session.Commit();
        check("Settings retry publishes persisted values", writes == 2 && target.PanelWidth == 500 && target.IconOrder[0] == "draft");
        session.Draft.IconOrder[0] = "later";
        persisted!.IconOrder[1] = "writer";
        check("Committed settings do not alias draft or persistence arrays", target.IconOrder.SequenceEqual(new[] { "draft", "b" }));
        var replacement = new AppSettings { IconOrder = ["replacement"] };
        session.ReplaceDraft(replacement); replacement.IconOrder[0] = "outside";
        check("Imported draft does not retain caller array ownership", session.Draft.IconOrder[0] == "replacement" && target.PanelWidth == 500);
    }
}
