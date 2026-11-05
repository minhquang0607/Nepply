namespace Nepply.Services;

internal interface IDesktopGuardianSession : IAsyncDisposable
{
    Task Exited { get; }
    int? ExitCode { get; }
    Task<bool> ArmAsync(CancellationToken cancellation = default);
    Task<bool> RestoreAsync(CancellationToken cancellation = default);
    Task<bool> CloseAfterLocalRestoreAsync(Func<bool> restoreAndVerify);
}
