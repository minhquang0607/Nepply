namespace Nepply.Services;

internal enum AutoHideAction
{
    Cancel,
    RestartCountdown,
    Hide
}

internal static class AutoHidePolicy
{
    internal static AutoHideAction Decide(bool pinned, bool suppressed, bool animating, bool pointerInside, bool keyboardInteraction=false)
    {
        if (pinned || suppressed || pointerInside || keyboardInteraction) return AutoHideAction.Cancel;
        if (animating) return AutoHideAction.RestartCountdown;
        return AutoHideAction.Hide;
    }
}
