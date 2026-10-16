using System.Windows.Threading;

namespace Nepply.Modules.Search;

/// <summary>
/// Owns one search interaction on the UI dispatcher. It does not own the query,
/// filtering, keyboard focus or animation: those remain adapters of the host.
/// No timer runs while idle; leaving an expanded search schedules one check.
/// </summary>
internal sealed class SearchSession : IDisposable
{
    private readonly DispatcherTimer _dismissTimer;
    private readonly Func<bool> _shouldKeepOpen;
    private readonly Action<bool> _present;
    private bool _disposed;

    internal SearchSession(Func<bool> shouldKeepOpen, Action<bool> present)
    {
        _shouldKeepOpen = shouldKeepOpen;
        _present = present;
        _dismissTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(240) };
        _dismissTimer.Tick += DismissTick;
    }

    internal bool IsExpanded { get; private set; }
    internal bool IsDismissPending => _dismissTimer.IsEnabled;

    internal void SetExpanded(bool expanded)
    {
        if (_disposed) return;
        _dismissTimer.Stop();
        if (IsExpanded == expanded) return;
        IsExpanded = expanded;
        _present(expanded);
    }

    internal void RequestDismiss()
    {
        if (_disposed || !IsExpanded) return;
        _dismissTimer.Stop();
        _dismissTimer.Start();
    }

    private void DismissTick(object? sender, EventArgs e) => EvaluateDismiss();

    internal void EvaluateDismiss()
    {
        _dismissTimer.Stop();
        if (!_disposed && IsExpanded && !_shouldKeepOpen()) SetExpanded(false);
    }

    internal void Suspend(bool hasQuery)
    {
        _dismissTimer.Stop();
        if (!hasQuery) SetExpanded(false);
    }

    internal void DismissAndClear(Action clearQuery)
    {
        if (_disposed || !IsExpanded) return;
        // Clearing text can synchronously request another dismiss through TextChanged.
        // The explicit collapse below cancels that pending request.
        clearQuery();
        SetExpanded(false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _dismissTimer.Stop();
        _dismissTimer.Tick -= DismissTick;
    }
}
