namespace Nepply.Services;

// One retained configuration notification per lane. Infinite waits allocate no periodic timer.
internal sealed class SamplingWake : IDisposable
{
    private readonly SemaphoreSlim _signal = new(0, 1);
    internal void Signal()
    {
        try { _signal.Release(); }
        catch (SemaphoreFullException) { } // Latest configuration is read from its snapshot.
    }
    internal void ConsumePending() => _signal.Wait(0);
    internal Task<bool> WaitAsync(int milliseconds, CancellationToken token) => _signal.WaitAsync(milliseconds, token);
    public void Dispose() => _signal.Dispose();
}
