using Nepply.Models;
using Nepply.Modules.Settings;

internal static class SettingsChangeSetTests
{
    internal static void Run(Action<string,bool> check)
    {
        var original=new AppSettings();
        check("Cloned settings have no changes",SettingsChangeSet.Between(original,original.Clone()).Changes==SettingsChange.None);
        var candidate=original.Clone(); candidate.GlowStrength=.3;
        var changes=SettingsChangeSet.Between(original,candidate);
        check("Appearance preview needs no structural rebuild",changes.Changes==SettingsChange.Appearance && changes.CanPreviewWithoutRebuild);
        check("Appearance does not request desktop, hotkey or OS changes",!changes.Has(SettingsChange.Desktop|SettingsChange.Hotkey|SettingsChange.AutoStart|SettingsChange.DesktopVisibility));
        candidate=original.Clone(); candidate.ShowHiddenFiles=!candidate.ShowHiddenFiles;
        check("Desktop options request catalog refresh",SettingsChangeSet.Between(original,candidate).Changes==SettingsChange.Desktop);
        candidate=original.Clone(); candidate.AutoStart=!candidate.AutoStart;
        check("Autostart is independent from desktop visibility",SettingsChangeSet.Between(original,candidate).Changes==SettingsChange.AutoStart);
        candidate=original.Clone(); candidate.IconOrder=["new"];
        check("Array contents participate in change detection",SettingsChangeSet.Between(original,candidate).Has(SettingsChange.Structure));
        candidate=original.Clone(); candidate.PanelWidthPercent=55; candidate.TopGridRows=2;
        check("Dimension and grid changes retain fast preview",SettingsChangeSet.Between(original,candidate).CanPreviewWithoutRebuild);
        candidate=original.Clone(); candidate.EdgePosition=EdgePosition.Right;
        check("Edge changes require structural refresh",!SettingsChangeSet.Between(original,candidate).CanPreviewWithoutRebuild);
        original.Validate();
        bool fail=false;
        var session=new SettingsEditSession(original,_=>{if(fail) throw new System.IO.IOException("fixture");});
        session.Draft.GlowStrength=.3; session.Commit();
        check("Commit publishes exact change classification",session.LastCommittedChanges.Changes==SettingsChange.Appearance);
        session.Commit();
        check("Second Apply without edits publishes no changes",session.LastCommittedChanges.Changes==SettingsChange.None);
        fail=true; session.Draft.ShowHiddenFiles=true;
        try {session.Commit();} catch(System.IO.IOException) { }
        check("Failed save does not publish new changes",session.LastCommittedChanges.Changes==SettingsChange.None && !original.ShowHiddenFiles);
    }
}
