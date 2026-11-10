using System.Windows;
using System.Windows.Input;
using Nepply.Services;

namespace Nepply.Modules.Updates;

internal sealed partial class UpdateWindow : Window
{
    private readonly UpdateService _service;
    private readonly CancellationTokenSource _cancel = new();
    private UpdateRelease? _release;
    private bool _closed, _busy, _released;
    internal PreparedUpdate? Prepared { get; private set; }
    internal string StatusText => _status.Text + " " + ReleaseVersionLabel.Text;
    internal string ReleaseNotes => _notes.Text;
    internal bool CanInstall => _install.IsEnabled;
    internal bool CanRetry => RetryButton.Visibility == Visibility.Visible;
    internal bool CanOpenRelease => OpenReleaseButton.Visibility == Visibility.Visible;
    internal UpdateWindow(UpdateService? service = null)
    {
        _service = service ?? new();
        InitializeComponent();
        CurrentVersionLabel.Text = LocalizationService.Get("UpdateCurrent") + "  " + UpdateService.CurrentVersion.Split('+')[0];
        Loaded += (_, e) => { Nepply.Helpers.DialogPlacement.FitInitial(this); Check(this, e); };
        // Closing only cancels. A check or download still awaiting owns the service and
        // token until it unwinds, so it observes cancellation rather than disposal.
        Closed += (_, _) => { _closed = true; _cancel.Cancel(); if (!_busy) ReleaseResources(); };
    }
    private void ReleaseResources()
    {
        if (_released) return;
        _released = true;
        _service.Dispose(); _cancel.Dispose();
    }
    private void DragTitle(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Notice(string key)
    {
        NoticeText.Text = LocalizationService.Get(key); NoticeCard.Visibility = Visibility.Visible;
    }
    // This build cannot install the update itself: point to the official release page
    // instead of leaving a disabled install button as the only action.
    private void ManualOnly(string key)
    {
        Notice(key);
        OpenReleaseButton.Visibility = Visibility.Visible;
    }
    private void OpenRelease(object sender, RoutedEventArgs e)
    {
        if (_release == null) return;
        try { using var _ = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(UpdatePolicy.ReleasePageUri(_release.Tag).AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex) { AppLogger.Error(ex, "Open release page"); Notice("OpenBrowserError"); }
    }
    private void Failed(bool checking)
    {
        _status.Text = LocalizationService.Get("UpdateFailed");
        ProgressSection.Visibility = Visibility.Collapsed;
        RetryButton.Visibility = checking ? Visibility.Visible : Visibility.Collapsed;
    }
    private async void Check(object sender, RoutedEventArgs e)
    {
        if (_busy || _closed) return;
        _busy = true;
        RetryButton.Visibility = _install.Visibility = OpenReleaseButton.Visibility = NoticeCard.Visibility = NotesSection.Visibility = VersionBadge.Visibility = Visibility.Collapsed;
        ReleaseVersionLabel.Text = ""; _release = null;
        try
        {
            if (ProcessPackageIdentity.IsPackaged) { _status.Text = LocalizationService.Get("UpdateStore"); return; }
            _status.Text = LocalizationService.Get("UpdateChecking");
            ProgressSection.Visibility = Visibility.Visible; _progress.IsIndeterminate = true;
            ProgressLabel.Text = LocalizationService.Get("UpdateChecking"); ProgressPercent.Text = "";
            var release = await _service.CheckAsync(_cancel.Token);
            if (_closed) return;
            _release = release;
            if (release == null) { _status.Text = LocalizationService.Get("UpdateNone"); return; }
            _status.Text = LocalizationService.Get("UpdateAvailable").TrimEnd(':', '：', ' ');
            ReleaseVersionLabel.Text = release.Tag; VersionBadge.Visibility = Visibility.Visible;
            DownloadSizeLabel.Text = release.Bytes < 1024 * 1024
                ? $"{release.Bytes / 1024d:0.#} KiB" : $"{release.Bytes / (1024d * 1024):0.#} MiB";
            // Short summaries in the app's language for every version since this one; releases
            // made before summaries existed fall back to the release page's own notes.
            var summaries = await _service.GetSummariesAsync(release, _cancel.Token);
            if (_closed) return;
            string notes = ReleaseSummaryFile.Build(summaries, LocalizationService.CurrentLanguage, LocalizationService.Get("UpdateNotesOnPage"))
                ?? ReleaseNotesText.ToPlain(release.Notes);
            _notes.Text = notes.Length == 0 ? LocalizationService.Get("UpdateNoNotes") : notes;
            NotesSection.Visibility = Visibility.Visible;
            _install.IsEnabled = false;
            if (UpdatePolicy.InstalledDirectory() == null) ManualOnly("UpdatePortable");
            else if (UpdatePolicy.Signers.Length == 0) ManualOnly("UpdateSigningPending");
            else { _install.Visibility = Visibility.Visible; _install.IsEnabled = true; }
        }
        catch (OperationCanceledException) { if (!_closed) Failed(checking: true); }
        catch (Exception ex) { AppLogger.Error(ex, "Check updates"); if (!_closed) Failed(checking: true); }
        finally
        {
            _busy = false;
            if (_closed) ReleaseResources();
            else { ProgressSection.Visibility = Visibility.Collapsed; _progress.IsIndeterminate = false; }
        }
    }
    internal void PresentDownloadProgress(double value)
    {
        if (_closed) return;
        bool verifying = value >= 100;
        _progress.Value = Math.Clamp(value, 0, 100); _progress.IsIndeterminate = verifying;
        ProgressLabel.Text = LocalizationService.Get(verifying ? "UpdateVerifying" : "UpdateDownloading");
        ProgressPercent.Text = verifying ? "" : $"{value:0}%";
    }
    private async void Install(object sender, RoutedEventArgs e)
    {
        if (_busy || _release == null) return;
        _busy = true; _install.IsEnabled = false; ProgressSection.Visibility = Visibility.Visible;
        LaterButton.SetResourceReference(System.Windows.Controls.ContentControl.ContentProperty, "Loc.Cancel");
        _status.Text = LocalizationService.Get("UpdateDownloading"); PresentDownloadProgress(0);
        PreparedUpdate? prepared = null;
        try
        {
            prepared = await _service.DownloadAsync(_release, new Progress<double>(PresentDownloadProgress), _cancel.Token);
            if (_closed) return;
            Prepared = prepared; prepared = null; DialogResult = true;
        }
        catch (OperationCanceledException) { if (!_closed) Failed(checking: false); }
        catch (Exception ex) { AppLogger.Error(ex, "Download/verify update"); if (!_closed) Failed(checking: false); }
        finally
        {
            prepared?.Dispose(); _busy = false;
            if (_closed) ReleaseResources();
            else { _install.IsEnabled = true; _progress.IsIndeterminate = false; LaterButton.SetResourceReference(System.Windows.Controls.ContentControl.ContentProperty, "Loc.UpdateLater"); }
        }
    }
}
