namespace Nepply.Services;

/// <summary>
/// In-memory recovery authority for one authenticated guardian session.
/// Transport must authenticate both peers and fence old/new owners separately.
/// A missing journal is never evidence that an armed session is restored.
/// </summary>
internal sealed class DesktopRecoverySession
{
    private enum SessionState { Created, Armed, Completed }
    private readonly object _gate = new();
    private readonly Guid _generation;
    private readonly Func<bool?> _isVisible;
    private readonly Func<bool> _show;
    private SessionState _state;

    internal DesktopRecoverySession(Guid generation, Func<bool?> isVisible, Func<bool> show)
    {
        if (generation == Guid.Empty) throw new ArgumentException("Empty recovery generation.", nameof(generation));
        _generation = generation;
        _isVisible = isVisible ?? throw new ArgumentNullException(nameof(isVisible));
        _show = show ?? throw new ArgumentNullException(nameof(show));
    }

    internal bool IsArmed { get { lock (_gate) return _state == SessionState.Armed; } }
    internal bool IsCompleted { get { lock (_gate) return _state == SessionState.Completed; } }

    // Must succeed BEFORE the owner is allowed to hide Desktop.
    internal bool Arm(Guid generation)
    {
        lock (_gate)
        {
            if (generation != _generation || _state != SessionState.Created) return false;
            if (_isVisible() != true) return false; // Never claim a pre-hidden or unavailable Desktop.
            _state = SessionState.Armed;
            return true;
        }
    }

    // Normal shutdown: caller already restored. Do not trust only a remote "done" message.
    internal bool Disarm(Guid generation)
    {
        lock (_gate)
        {
            if (generation != _generation || _state != SessionState.Armed) return false;
            if (_isVisible() != true) return false;
            _state = SessionState.Completed;
            return true;
        }
    }

    // Called only after the transport establishes owner loss or an explicit restore request.
    // Failure/exception retains intent so the bounded retry policy can try again.
    internal bool Recover(Guid generation)
    {
        lock (_gate)
        {
            if (generation != _generation) return false;
            if (_state == SessionState.Completed) return true;
            if (_state != SessionState.Armed) return false;
            bool? visible = _isVisible();
            if (visible is null) return false;
            if (!visible.Value && (!_show() || _isVisible() != true)) return false;
            _state = SessionState.Completed;
            return true;
        }
    }
}
