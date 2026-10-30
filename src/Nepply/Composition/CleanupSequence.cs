using Nepply.Services;

namespace Nepply.Helpers;

internal static class CleanupSequence
{
    internal static void Run(params (string Name, Action Action)[] steps)
    {
        foreach (var step in steps)
        {
            try { step.Action(); }
            catch (Exception ex) { AppLogger.Error(ex, "Cleanup: " + step.Name); }
        }
    }
}
