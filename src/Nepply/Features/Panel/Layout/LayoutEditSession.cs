using Nepply.Models;
using Nepply.Services;

namespace Nepply.Modules.Panel;

/// <summary>UI-thread layout transaction; persistence is supplied by the composition host.</summary>
internal sealed class LayoutEditSession
{
    private AppSettings? _target, _before;
    internal bool IsActive => _target != null;
    internal bool HasChanges => _target != null && _before != null && !LayoutProfile.SameLayout(_before, _target);

    internal void Begin(AppSettings target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (IsActive) throw new InvalidOperationException("A layout edit is already active.");
        var before = target.Clone();
        _target = target; _before = before;
    }

    internal bool Complete(bool commit, Action<AppSettings> persist)
    {
        ArgumentNullException.ThrowIfNull(persist);
        if (_target == null || _before == null) return false;
        // Do not release the snapshot until persistence succeeds: retry/cancel must remain possible.
        if (commit && HasChanges) persist(_target);
        if (!commit) _target.CopyFrom(_before);
        _target = null; _before = null;
        return true;
    }
}
