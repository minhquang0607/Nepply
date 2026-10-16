namespace Nepply.Services;

/// <summary>One reveal per edge entry; rearm only outside the wider release zone.</summary>
public sealed class EdgeActivationGate
{
    private bool _armed = true;
    private long? _entered;
    public void Disarm() { _armed = false; _entered = null; }
    public bool Update(bool onEdge, bool inReleaseZone, bool blocked, long nowMs, int dwellMs)
    {
        if (!inReleaseZone) { _armed = true; _entered = null; }
        if (blocked || !_armed || !onEdge) { _entered = null; return false; }
        _entered ??= nowMs;
        if (nowMs - _entered.Value < dwellMs) return false;
        Disarm(); return true;
    }
}
