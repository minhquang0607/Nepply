namespace Nepply.Helpers;

internal static class InstanceOwnership
{
    // Must match AppMutex in Nepply.iss (case-sensitive Windows object name).
    internal const string InstallerMutexName = ProductIdentity.InstallerMutex;
    // Call once on the UI thread; release on that same thread during shutdown.
    internal static bool TryAcquire(Mutex mutex)
    {
        try { return mutex.WaitOne(0); }
        catch(AbandonedMutexException)
        {
            // WaitOne grants ownership before reporting abandonment. Startup
            // recovery must still restore the previous Desktop visibility lease.
            return true;
        }
    }
}
