namespace Nepply;

internal static class ProductIdentity
{
    public const string Name = "Nepply";
    internal const string InstancePrefix = @"Local\" + Name + "-";
    internal const string ActivationPrefix = InstancePrefix + "activate-";
    internal const string InstallerMutex = InstancePrefix + "SetupGuard";
}
