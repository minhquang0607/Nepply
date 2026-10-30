using System.IO;
using Nepply;
using Nepply.Models;
using Nepply.Services;

internal static class BrandMigrationTests
{
    internal static void Run(string output, Action<string, bool> check)
    {
        string root = Path.Combine(output, "rebrand-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string old = Path.Combine(root, "HoverBox", "settings.json");
        string current = Path.Combine(root, "Nepply", "settings.json");
        var original = new AppSettings
        {
            PanelWidth = 456,
            Accent = "Neon Cyan",
            HasCompletedOnboarding = true,
            IconOrder = [@"C:\Desktop\example.lnk"],
            AutoStart = true
        };
        new SettingsStore(old).Save(original);
        byte[] before = File.ReadAllBytes(old);
        var migrationStore = new SettingsStore(current, old);
        var migrated = migrationStore.Load();
        check("Successful rebrand exposes an import notice", migrationStore.RecoveryNotice?.Key == "RecoveryLegacyImported");
        check("Rebrand keeps layout, icon order and onboarding", migrated.PanelWidth == 456 && migrated.HasCompletedOnboarding
            && migrated.IconOrder.SequenceEqual(original.IconOrder) && migrated.AutoStart);
        check("Rebrand persists new settings and leaves original byte-identical", File.Exists(current) && before.SequenceEqual(File.ReadAllBytes(old)));
        new SettingsStore(current).Save(new AppSettings { PanelWidth = 512 });
        check("Rebrand never overwrites existing Nepply choices", new SettingsStore(current, old).Load().PanelWidth == 512);

        string backupOnly = Path.Combine(root, "backup-only.json");
        File.Copy(current, backupOnly + ".bak");
        check("Nepply backup takes precedence over legacy settings", new SettingsStore(backupOnly, old).Load().PanelWidth == 512);
        File.WriteAllText(old, "broken JSON");
        File.WriteAllBytes(old + ".bak", before);
        string recovered = Path.Combine(root, "recovered.json");
        check("Legacy damaged primary imports its valid backup", new SettingsStore(recovered, old).Load().PanelWidth == 456);
        check("Legacy damaged file is preserved", File.ReadAllText(old) == "broken JSON");

        File.WriteAllText(old, "{\"SchemaVersion\":999}");
        string futurePath = Path.Combine(root, "future.json");
        var future = new SettingsStore(futurePath, old);
        future.Load();
        check("Future legacy schema exposes its own recovery reason", future.RecoveryNotice?.Key == "RecoveryLegacyNewer");
        bool rejected = false;
        try { future.Save(new()); } catch (IOException) { rejected = true; }
        check("Future legacy schema is neither downgraded nor overwritten", rejected && !File.Exists(futurePath)
            && File.ReadAllText(old) == "{\"SchemaVersion\":999}");
        string empty = Path.Combine(root, "fresh.json");
        var fresh = new SettingsStore(empty, Path.Combine(root, "absent.json")).Load();
        check("New Nepply install retains first-run onboarding", !fresh.HasCompletedOnboarding && !File.Exists(empty));
        check("Old and new app cannot acquire separate Desktop ownership", ProductIdentity.InstancePrefix == @"Local\HoverBox-"
            && Nepply.Helpers.InstanceOwnership.InstallerMutexName == @"Local\HoverBox-SetupGuard"
            && Path.GetDirectoryName(DesktopVisibilityLease.JournalPath)!.EndsWith("HoverBox", StringComparison.Ordinal));
        check("Executable assembly identity is Nepply", typeof(App).Assembly.GetName().Name == "Nepply");
    }
}
