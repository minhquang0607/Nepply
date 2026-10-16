namespace Nepply.Services;

/// <summary>Automatic edge opening only; explicit tray/hotkey actions remain available.</summary>
internal sealed class PointerActivationGate
{
    private readonly EdgeActivationGate _edge = new();
    private nint _foreground;
    private uint _processId;
    private bool _needsInterior = true;

    internal void Disarm()
    {
        _needsInterior = true;
        _edge.Disarm();
    }

    internal bool Update(PointerContext? context, bool insideMonitor, bool onEdge,
        bool inReleaseZone, bool blocked, long nowMs, int dwellMs)
    {
        if (context is not { CursorVisible: true } sample || sample.Foreground == 0 || sample.ProcessId == 0)
        {
            Disarm();
            return false;
        }
        if (_foreground != sample.Foreground || _processId != sample.ProcessId)
        {
            _foreground = sample.Foreground;
            _processId = sample.ProcessId;
            Disarm();
        }
        if (blocked || !insideMonitor)
        {
            Disarm();
            return false;
        }
        // Another display cannot arm this panel; a restored cursor at the edge cannot either.
        if (insideMonitor && !inReleaseZone) _needsInterior = false;
        if (_needsInterior) return false;
        return _edge.Update(onEdge && insideMonitor, inReleaseZone, false, nowMs, dwellMs);
    }

    internal static bool CanCommit(PointerContext previous, PointerContext? current, bool onEdge) =>
        current is { CursorVisible: true } sample && onEdge &&
        sample.Foreground == previous.Foreground && sample.ProcessId == previous.ProcessId;

    internal static bool ShouldYield(PointerContext? context, uint ownProcessId, bool protectedInteraction) =>
        !protectedInteraction && context is { CursorVisible: false } sample &&
        sample.Foreground != 0 && sample.ProcessId != 0 && sample.ProcessId != ownProcessId;
}
