using System.Security.Principal;

namespace Nepply.Services;

/// <summary>Serializes guardian generations in the interactive user/session.</summary>
internal sealed class DesktopGuardianFence : IDisposable
{
    private readonly Mutex _mutex;
    private readonly int _thread;
    private bool _disposed;

    private DesktopGuardianFence(Mutex mutex)
    {
        _mutex = mutex;
        _thread = Environment.CurrentManagedThreadId;
    }

    // The acquiring thread must release; never carry this scope across an await.
    internal static DesktopGuardianFence Acquire(TimeSpan timeout)
    {
        using var identity = WindowsIdentity.GetCurrent();
        string sid = identity.User?.Value ?? throw new UnauthorizedAccessException("Missing guardian user identity.");
        var mutex = new Mutex(false, @"Local\Nepply-DesktopGuardian-" + sid);
        try
        {
            bool acquired;
            try { acquired = mutex.WaitOne(timeout); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired) throw new TimeoutException("Previous Desktop guardian has not finished.");
            return new DesktopGuardianFence(mutex);
        }
        catch { mutex.Dispose(); throw; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (Environment.CurrentManagedThreadId != _thread) throw new InvalidOperationException("Guardian fence released on a different thread.");
        _mutex.ReleaseMutex();
        _disposed = true;
        _mutex.Dispose();
    }
}
