using Nepply.Models;

namespace Nepply.Services;

public static class OnboardingService
{
    public static void Complete(AppSettings target, EdgePosition edge, bool hideDesktopIcons, Action<AppSettings> save)
    {
        var draft = target.Clone();
        draft.EdgePosition = edge;
        draft.HideDesktopIcons = hideDesktopIcons;
        draft.HasCompletedOnboarding = true;
        draft.Validate();
        save(draft);
        target.CopyFrom(draft);
    }
}
