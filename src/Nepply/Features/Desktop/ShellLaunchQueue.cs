namespace Nepply.Services;

/// <summary>
/// Runs Shell launches away from the UI thread, because a shortcut to a
/// disconnected network drive can block ShellExecute for tens of seconds. Each
/// launch gets its own STA background thread: Shell extensions expect STA, and a
/// launch stuck in Windows must neither hold a pool thread nor delay app exit.
/// While a path is launching, repeated clicks on it are ignored instead of
/// stacking more stuck threads.
/// </summary>
internal sealed class ShellLaunchQueue
{
    private readonly HashSet<string> _pending = new(StringComparer.OrdinalIgnoreCase);

    internal Task<UserMessage?> Run(string path, Func<UserMessage?> launch)
    {
        lock (_pending)
        {
            if (!_pending.Add(path)) return Task.FromResult<UserMessage?>(null);
        }
        var completion = new TaskCompletionSource<UserMessage?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            UserMessage? result = null;
            Exception? failure = null;
            try { result = launch(); }
            catch (Exception ex) { failure = ex; }
            lock (_pending) _pending.Remove(path);
            if (failure == null) completion.SetResult(result);
            else completion.SetException(failure);
        })
        { IsBackground = true, Name = "Desktop item launch" };
        thread.SetApartmentState(ApartmentState.STA);
        try { thread.Start(); }
        catch
        {
            lock (_pending) _pending.Remove(path);
            throw;
        }
        return completion.Task;
    }
}
