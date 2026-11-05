using System.IO;
using Nepply;
using Nepply.Services;

internal static class BrandIdentityTests
{
    internal static void Run(string output, Action<string, bool> check)
    {
        string root = Path.Combine(output, "brand-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string empty = Path.Combine(root, "fresh.json");
        var fresh = new SettingsStore(empty).Load();
        check("New Nepply install retains first-run onboarding", !fresh.HasCompletedOnboarding && !File.Exists(empty));
        check("Session locks use the Nepply name", ProductIdentity.InstancePrefix == @"Local\Nepply-"
            && ProductIdentity.ActivationPrefix == @"Local\Nepply-activate-"
            && Nepply.Helpers.InstanceOwnership.InstallerMutexName == @"Local\Nepply-SetupGuard");
        check("Desktop recovery journal lives in the Nepply folder",
            Path.GetFileName(Path.GetDirectoryName(DesktopVisibilityLease.JournalPath)) == "Nepply"
            && DesktopRecoveryJournal.IsAllowedPath(DesktopVisibilityLease.JournalPath,
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)));
        check("Executable assembly identity is Nepply", typeof(App).Assembly.GetName().Name == "Nepply");
    }
}
