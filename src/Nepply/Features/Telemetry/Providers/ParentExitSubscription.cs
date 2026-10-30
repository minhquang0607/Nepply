using System.Diagnostics;

namespace Nepply.Services.Telemetry;

// Process.Exited registers a process-handle wait; there is no polling timer.
internal sealed class ParentExitSubscription : IDisposable
{
    private readonly Process _parent;
    private readonly Action _exited;
    private int _completed;
    internal ParentExitSubscription(Process parent, Action exited)
    {
        _parent = parent; _exited = exited;
        parent.Exited += OnExit;
        try
        {
            parent.EnableRaisingEvents = true;
            if (parent.HasExited) OnExit(parent, EventArgs.Empty);
        }
        catch { parent.Exited -= OnExit; throw; }
    }
    private void OnExit(object? sender, EventArgs args)
    {
        if (Interlocked.Exchange(ref _completed, 1) == 0) _exited();
    }
    public void Dispose()
    {
        Interlocked.Exchange(ref _completed, 1);
        _parent.Exited -= OnExit;
    }
}
