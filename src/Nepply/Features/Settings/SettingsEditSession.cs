using Nepply.Models;

namespace Nepply.Modules.Settings;

/// <summary>Isolated settings draft; publish only after persistence succeeds.</summary>
internal sealed class SettingsEditSession
{
    private readonly AppSettings _target;
    private readonly Action<AppSettings> _persist;
    internal AppSettings Draft { get; private set; }
    internal SettingsChangeSet LastCommittedChanges { get; private set; }
    internal SettingsEditSession(AppSettings target, Action<AppSettings> persist)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(persist);
        _target = target; _persist = persist; Draft = target.Clone();
    }
    internal void ReplaceDraft(AppSettings draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        Draft = draft.Clone();
    }
    internal void Commit()
    {
        var candidate = Draft.Clone();
        candidate.Validate();
        var changes = SettingsChangeSet.Between(_target, candidate);
        _persist(candidate);
        _target.CopyFrom(candidate.Clone());
        Draft = candidate.Clone();
        LastCommittedChanges = changes;
    }
}
