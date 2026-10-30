using Nepply.Models;

namespace Nepply.Services;

public static class OnboardingService
{
    public static void Complete(AppSettings target, EdgePosition edge, bool hideDesktopIcons, Action<AppSettings> save, string? language = null)
    {
        var draft = target.Clone();
        if (language != null) draft.Language = language;
        draft.EdgePosition = edge;
        draft.HideDesktopIcons = hideDesktopIcons;
        draft.HasCompletedOnboarding = true;
        draft.Validate();
        save(draft);
        target.CopyFrom(draft);
    }
}
