namespace Nepply.Services;

// Called under the watcher's lock. Uses monotonic milliseconds, not wall-clock time.
internal sealed class BoundedDebounce(int quietMs = 350, int maximumMs = 2000)
{
    private long? _first;
    private long? _due;
    public int Signal(long now)
    {
        _first ??= now;
        int delay = (int)Math.Clamp(maximumMs - Math.Max(0, now - _first.Value), 0, quietMs);
        _due = now + delay;
        return delay;
    }
    public int? Remaining(long now) => _due is { } due ? (int)Math.Clamp(due - now, 0, int.MaxValue) : null;
    public void Reset() { _first = null; _due = null; }
}
