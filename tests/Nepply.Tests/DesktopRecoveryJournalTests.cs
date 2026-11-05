using System.IO;
using Nepply.Services;

internal static class DesktopRecoveryJournalTests
{
    internal static void Run(string output, Action<string, bool> check)
    {
        string root = Path.Combine(output, "recovery-journal-" + Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(root, "Nepply"); Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "desktop-visibility.lease");
        check("Journal accepts fixed recovery name below allowed root", DesktopRecoveryJournal.IsAllowedPath(path, root));
        check("Journal rejects sibling prefix path", !DesktopRecoveryJournal.IsAllowedPath(Path.Combine(root + "-other", "Nepply", "desktop-visibility.lease"), root));
        check("Journal rejects traversal outside root", !DesktopRecoveryJournal.IsAllowedPath(Path.Combine(root, "..", "other", "Nepply", "desktop-visibility.lease"), root));
        check("Journal rejects arbitrary filename", !DesktopRecoveryJournal.IsAllowedPath(Path.Combine(folder, "settings.json"), root));
        check("Journal rejects the fixed name in another folder", !DesktopRecoveryJournal.IsAllowedPath(Path.Combine(root, "Other", "desktop-visibility.lease"), root));
        check("Journal rejects relative path", !DesktopRecoveryJournal.IsAllowedPath("Nepply/desktop-visibility.lease", root));
        File.WriteAllText(path, "restore-visible-v1");
        bool visible = true; Guid generation = Guid.NewGuid();
        var session = new DesktopRecoverySession(generation, () => visible, () => { visible = true; return true; });
        session.Arm(generation); visible = false;
        check("Guardian completion clears only after verified restoration", session.Recover(generation) && DesktopRecoveryJournal.ClearCompleted(path) && visible && !File.Exists(path));
        visible = false; // The user changes visibility after successful recovery.
        check("Completed journal cleanup retry never overrides later visibility", session.Recover(generation) && DesktopRecoveryJournal.ClearCompleted(path) && !visible);
        File.WriteAllText(path, "unrelated data");
        check("Journal cleanup preserves unexpected content", !DesktopRecoveryJournal.ClearCompleted(path) && File.ReadAllText(path) == "unrelated data");
        File.WriteAllText(path, new string('x', 129));
        check("Journal cleanup rejects oversized marker", !DesktopRecoveryJournal.ClearCompleted(path) && File.Exists(path));
        File.WriteAllText(path, "restore-visible-v1");
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            bool refused = false;
            try { DesktopRecoveryJournal.ClearCompleted(path); } catch (IOException) { refused = true; }
            check("Journal cleanup failure leaves recovery record for retry", refused && File.Exists(path));
        }
        check("Journal cleanup succeeds after sharing lock releases", DesktopRecoveryJournal.ClearCompleted(path) && !File.Exists(path));
    }
}
