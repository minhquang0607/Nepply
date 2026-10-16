using Windows.ApplicationModel;

namespace Nepply.Services;

internal sealed class PackagedStartupBackend : IStartupBackend
{
    public async Task<StartupOutcome> ApplyAsync(bool enabled, bool userInitiated, Func<bool> isCurrent)
    {
        if (!ProcessPackageIdentity.IsPackaged) throw new InvalidOperationException("StartupTask requires package identity.");
        if (System.Windows.Application.Current?.Dispatcher.CheckAccess() != true)
            throw new InvalidOperationException("StartupTask requests require the UI thread.");
        var task = await StartupTask.GetAsync("NepplyStartup");
        if (!isCurrent()) return new(null, null);
        // Startup only reads OS state. Never undo a user's Windows Startup choice.
        if (userInitiated)
        {
            if (enabled && task.State == StartupTaskState.Disabled)
                await task.RequestEnableAsync();
            else if (!enabled && task.State == StartupTaskState.Enabled)
                task.Disable();
        }
        bool actual = task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
        return new(actual, userInitiated && actual != enabled ? UserMessage.FromKey("AutoStartUpdateError") : null);
    }
}
