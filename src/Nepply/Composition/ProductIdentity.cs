namespace Nepply;

internal static class ProductIdentity
{
    public const string Name = "Nepply";
    // Stable compatibility identity: old and new builds must not own Desktop simultaneously.
    internal const string LegacyName = "HoverBox";
    internal const string InstancePrefix = @"Local\" + LegacyName + "-";
    internal const string ActivationPrefix = InstancePrefix + "activate-";
    internal const string InstallerMutex = InstancePrefix + "SetupGuard";
}
