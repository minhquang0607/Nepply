using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Nepply.Models;

namespace Nepply.Views;

public partial class SettingsWindow
{
    internal event Action<AppSettings>? PreviewChanged;
    internal Func<Size>? PreviewViewport;
    private (int Columns, int Rows) _recommended;
    private void UpdateGridAdvice()
    {
        var size = PreviewViewport?.Invoke() ?? new Size();
        if (size.Width <= 0 || size.Height <= 0) return;
        GridPicker.ConfigureViewport(size, _gridEdge.IsHorizontal());
        var capacity = (Columns: GridPicker.AvailableColumns, Rows: GridPicker.AvailableRows);
        _recommended = (Nepply.Modules.Desktop.GridCapacity.Count(size.Width, 88, capacity.Columns),
            Nepply.Modules.Desktop.GridCapacity.Count(size.Height, 90, capacity.Rows));
        int columns = Math.Min(GridPicker.PreviewColumns, capacity.Columns), rows = Math.Min(GridPicker.PreviewRows, capacity.Rows);
        bool fits = columns == GridPicker.PreviewColumns && rows == GridPicker.PreviewRows;
        GridAdvice.Text = size.Height < Nepply.Modules.Desktop.GridCapacity.MinimumHeight
            ? Services.LocalizationService.Get("GridShort")
            : string.Format(Services.LocalizationService.Get(fits ? "GridPreview" : "GridAdaptive"), columns, rows);
        GridRecommend.Visibility = (GridPicker.Columns, GridPicker.Rows) == _recommended ? Visibility.Collapsed : Visibility.Visible;
    }
    private void RecommendGrid_Click(object sender, RoutedEventArgs e)
    { GridPicker.Columns = _recommended.Columns; GridPicker.Rows = _recommended.Rows; QueuePreview(); }
    private readonly DispatcherTimer _previewTimer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
    private bool _previewClosed, _readingPreview;

    private void InitializePreview()
    {
        _previewTimer.Tick += (_, _) => FlushPreview();
        ScreenChoice.SelectionChanged += (_, _) => { if (!_loadingDraft) ConfigureRelativeDimensions(); };
        AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler((_, _) => QueuePreview()));
        AddHandler(RangeBase.ValueChangedEvent, new RoutedPropertyChangedEventHandler<double>((_, _) => QueuePreview()));
        AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler((_, _) => QueuePreview()));
        AddHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler((_, _) => QueuePreview()));
        CustomAccent.TextChanged += (_, _) => QueuePreview();
        GridPicker.PreviewChanged += QueuePreview;
        Loaded += (_, _) => QueuePreview();
        Closed += (_, _) => { _previewClosed = true; _previewTimer.Stop(); GridPicker.PreviewChanged -= QueuePreview; PreviewChanged = null; };
    }
    private void QueuePreview()
    {
        if (_loadingDraft || _previewClosed || !IsLoaded || _previewTimer.IsEnabled) return;
        _previewTimer.Start();
    }
    internal void FlushPreview()
    {
        _previewTimer.Stop();
        if (_previewClosed || _loadingDraft) return;
        _readingPreview = true;
        try
        {
            // Language remains previewable even when another field is invalid.
            // The dialog's existing close/commit lifecycle owns rollback.
            Services.LocalizationService.Apply(LanguageChoice.SelectedValue as string ?? "vi");
            if (!ReadDraft()) return;
            var candidate = _draft.Clone();
            if (_gridEdge.IsHorizontal()) { candidate.TopGridColumns = GridPicker.PreviewColumns; candidate.TopGridRows = GridPicker.PreviewRows; }
            else { candidate.GridColumns = GridPicker.PreviewColumns; candidate.GridRows = GridPicker.PreviewRows; }
            candidate.Validate();
            Services.LocalizationService.Apply(candidate.Language);
            PreviewChanged?.Invoke(candidate);
            UpdateGridAdvice();
        }
        finally { _readingPreview = false; }
    }
}
