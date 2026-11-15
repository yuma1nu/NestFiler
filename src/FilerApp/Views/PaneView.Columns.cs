using FilerApp.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;

namespace FilerApp.Views;
public sealed partial class PaneView
{
    public FileColumns NormalColumns { get; } = new(false);
    public FileColumns SearchColumns { get; } = new(true);
    public FileColumns CurrentColumns => Current?.IsSearch == true ? SearchColumns : NormalColumns;
    public event Action? ColumnsChanged;
    private readonly Dictionary<string, Button> columnButtons = [];
    private readonly List<string> headerOrder = [];
    private ScrollViewer? columnScroll;
    public double DetailsViewportWidth => Math.Max(0, columnScroll?.ViewportWidth ?? Files.ActualWidth);
    private double lastColumnViewport;
    private bool columnsReady, updatingColumns, suppressColumnSort, resizingColumn;
    private readonly DispatcherTimer columnResizeTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private double HeaderHorizontalOffset => -HeaderTranslation.X;
    private FileColumns? draggedColumnLayout;
    private string? draggedColumnKey;
    private Button? pressedColumn;
    private Point pressedColumnPoint;
    private bool startingColumnDrag;
    private double columnDragX;
    private readonly DispatcherTimer columnDragTimer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private void InitializeColumns()
    {
        columnsReady = true;
        columnResizeTimer.Tick += (_, _) => { columnResizeTimer.Stop(); ApplyColumnChanges(); };
        columnDragTimer.Tick += (_, _) => { if (draggedColumnKey == null || draggedColumnLayout != CurrentColumns) columnDragTimer.Stop(); else ScrollColumnEdge(columnDragX); };
        NormalColumns.Changed += OnColumnsChanged; SearchColumns.Changed += OnColumnsChanged;
        Files.Loaded += (_, _) => AttachColumnScroll();
        Files.SizeChanged += (_, _) => AttachColumnScroll();
        Files.Unloaded += (_, _) => { columnResizeTimer.Stop(); columnDragTimer.Stop(); if (columnScroll != null) { columnScroll.ViewChanged -= DataColumnsScrolled; columnScroll.ViewChanging -= DataColumnsChanging; } columnScroll = null; };
        ColumnHeaders.AddHandler(UIElement.DragOverEvent, new DragEventHandler((_, args) =>
        {
            if (draggedColumnKey == null || draggedColumnLayout != CurrentColumns) return;
            columnDragX = args.GetPosition(ColumnHeaders).X; columnDragTimer.Start();
        }), true);
        ColumnHeaders.DragLeave += (_, _) => columnDragTimer.Stop();
        ColumnHeaders.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(async (_, args) =>
        {
            if (pressedColumn is not { } source || startingColumnDrag) return;
            var point = args.GetCurrentPoint(ColumnHeaders);
            if (!point.Properties.IsLeftButtonPressed) { pressedColumn = null; return; }
            if (Math.Abs(point.Position.X - pressedColumnPoint.X) < 6 && Math.Abs(point.Position.Y - pressedColumnPoint.Y) < 6) return;
            pressedColumn = null; startingColumnDrag = true; suppressColumnSort = true; args.Handled = true;
            source.ReleasePointerCaptures();
            try { await source.StartDragAsync(args.GetCurrentPoint(source)); }
            catch (Exception ex) { Error?.Invoke("列を移動できません: " + ex.Message); }
            finally { columnDragTimer.Stop(); startingColumnDrag = false; draggedColumnKey = null; draggedColumnLayout = null; suppressColumnSort = false; }
        }), true);
        ColumnHeaders.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler((_, _) => pressedColumn = null), true);
        var reset = new MenuFlyoutItem { Text = "列の順序・幅を初期状態に戻す" };
        reset.Click += (_, _) => { NavigationRefresh.Focus(FocusState.Programmatic); CurrentColumns.Reset(); };
        var menu = new MenuFlyout(); menu.Items.Add(reset); ColumnHeaders.ContextFlyout = menu;
        UpdateColumnView();
    }
    public void RestoreColumns(ColumnLayoutState? normal, ColumnLayoutState? search)
    { NormalColumns.Restore(normal); SearchColumns.Restore(search); UpdateColumnView(); }
    private void ScrollColumnEdge(double x)
    {
        if (columnScroll == null) return;
        var delta = x < 24 ? -24 : x > ColumnHeaders.ActualWidth - 24 ? 24 : 0;
        if (delta != 0) columnScroll.ChangeView(Math.Clamp(columnScroll.HorizontalOffset + delta, 0, columnScroll.ScrollableWidth), null, null, true);
    }
    private void OnColumnsChanged()
    {
        // Resize pointer events can arrive faster than frames. Update header and
        // rows together, once per frame, rather than laying out every event.
        if (resizingColumn) { if (!columnResizeTimer.IsEnabled) columnResizeTimer.Start(); return; }
        ApplyColumnChanges();
    }
    private void ApplyColumnChanges() { UpdateColumnView(); ColumnsChanged?.Invoke(); StateChanged?.Invoke(); }
    private void AttachColumnScroll()
    {
        if (!columnsReady || !Files.IsLoaded) return;
        var scroll = FindVisualChild<ScrollViewer>(Files);
        if (scroll != columnScroll)
        {
            if (columnScroll != null) { columnScroll.ViewChanged -= DataColumnsScrolled; columnScroll.ViewChanging -= DataColumnsChanging; }
            columnScroll = scroll;
            if (columnScroll != null) { columnScroll.ViewChanged += DataColumnsScrolled; columnScroll.ViewChanging += DataColumnsChanging; }
        }
        UpdateColumnView();
    }
    private void DataColumnsChanging(object? sender, ScrollViewerViewChangingEventArgs e) => NoteListScroll();
    private void DataColumnsScrolled(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        NoteListScroll();
        SynchronizeHeaderPosition();
    }
    private void SynchronizeHeaderPosition()
    {
        // A single ScrollViewer owns the position. The header has no independent
        // scroll extent/clamping or ViewChanged -> ChangeView feedback loop.
        double offset = columnScroll?.HorizontalOffset ?? 0;
        if (Math.Abs(HeaderTranslation.X + offset) > 0.01) HeaderTranslation.X = -offset;
    }
    private void UpdateColumnView()
    {
        if (!columnsReady || updatingColumns) return;
        updatingColumns = true;
        try
        {
            bool details = DisplayMode == FileViewMode.Details;
            ScrollViewer.SetHorizontalScrollBarVisibility(Files, details ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled);
            ScrollViewer.SetHorizontalScrollMode(Files, details ? ScrollMode.Enabled : ScrollMode.Disabled);
            if (!details && columnScroll?.HorizontalOffset > 0) columnScroll.ChangeView(0, null, null, true);
            var viewport = columnScroll?.ViewportWidth ?? Files.ActualWidth;
            if (viewport > 0)
            {
                ColumnHeaders.Width = viewport; CurrentColumns.Fit(viewport);
                var bounds = new Rect(0, 0, viewport, 24);
                if (ColumnHeaders.Clip is not RectangleGeometry clip || clip.Rect != bounds) ColumnHeaders.Clip = new RectangleGeometry { Rect = bounds };
            }
            var columns = CurrentColumns.Columns;
            if (!headerOrder.SequenceEqual(columns.Select(x => x.Key))) BuildColumnHeaders();
            for (int i = 0; i < columns.Count; i++) ColumnHeaderGrid.ColumnDefinitions[i].Width = new(columns[i].Width);
            ColumnHeaderGrid.Width = CurrentColumns.TotalWidth;
            UpdateSortHeaders();
            SynchronizeHeaderPosition();
        }
        finally { updatingColumns = false; }
        if (Math.Abs(lastColumnViewport - DetailsViewportWidth) > 0.5)
        { lastColumnViewport = DetailsViewportWidth; ColumnsChanged?.Invoke(); }
    }
    private void BuildColumnHeaders()
    {
        ColumnHeaderGrid.Children.Clear(); ColumnHeaderGrid.ColumnDefinitions.Clear(); columnButtons.Clear(); headerOrder.Clear();
        foreach (var column in CurrentColumns.Columns)
        {
            int index = headerOrder.Count; headerOrder.Add(column.Key); ColumnHeaderGrid.ColumnDefinitions.Add(new() { Width = new(column.Width) });
            var cell = new Grid { Background = (Brush)Application.Current.Resources["SubtleFillColorTransparentBrush"] };
            var button = new Button { Style = (Style)Application.Current.Resources["ColumnHeaderButton"], Tag = column.Key, HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(6, 0, 8, 0), CanDrag = true };
            button.Click += Sort; columnButtons[column.Key] = button;
            var grip = new ResizeGrip { Width = 6, HorizontalAlignment = HorizontalAlignment.Right };
            ToolTipService.SetToolTip(grip, "ドラッグで列幅を調整");
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(grip, FileColumns.Title(column.Key) + "列の幅");
            grip.DragStarted += (_, _) => { pressedColumn = null; resizingColumn = true; NoteListScroll(); suppressColumnSort = true; Activated?.Invoke(this); };
            grip.DragDelta += (_, args) => CurrentColumns.Resize(column.Key, CurrentColumns.Columns.Single(x => x.Key == column.Key).Width + args.HorizontalChange);
            grip.DragCompleted += (_, _) => { resizingColumn = false; columnResizeTimer.Stop(); ApplyColumnChanges(); suppressColumnSort = false; };
            button.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, args) =>
            {
                if (args.GetCurrentPoint(ColumnHeaders).Properties.IsLeftButtonPressed) { pressedColumn = button; pressedColumnPoint = args.GetCurrentPoint(ColumnHeaders).Position; }
            }), true);
            button.DragStarting += (_, args) =>
            {
                pressedColumn = null; suppressColumnSort = true; draggedColumnKey = column.Key; draggedColumnLayout = CurrentColumns;
                args.Data.SetData("FilerApp.Column", column.Key); args.Data.RequestedOperation = DataPackageOperation.Move;
            };
            button.DropCompleted += (_, _) => { columnDragTimer.Stop(); draggedColumnKey = null; draggedColumnLayout = null; suppressColumnSort = false; };
            cell.AllowDrop = true;
            cell.DragOver += (_, args) =>
            {
                if (draggedColumnKey == null || draggedColumnLayout != CurrentColumns || !args.DataView.Contains("FilerApp.Column")) return;
                args.AcceptedOperation = DataPackageOperation.Move; args.Handled = true;
                bool before = args.GetPosition(cell).X < cell.ActualWidth / 2;
                args.DragUIOverride.Caption = FileColumns.Title(column.Key) + (before ? " の前へ" : " の後へ");
                cell.Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
            };
            cell.DragLeave += (_, _) => cell.Background = null;
            cell.Drop += (_, args) =>
            {
                if (draggedColumnKey is not { } key || draggedColumnLayout != CurrentColumns) return;
                bool before = args.GetPosition(cell).X < cell.ActualWidth / 2;
                NavigationRefresh.Focus(FocusState.Programmatic);
                CurrentColumns.Move(key, column.Key, before);
                if (columnButtons.TryGetValue(key, out var moved)) moved.Focus(FocusState.Programmatic);
                args.AcceptedOperation = DataPackageOperation.Move; args.Handled = true;
            };
            cell.Children.Add(button); cell.Children.Add(grip); Grid.SetColumn(cell, index); ColumnHeaderGrid.Children.Add(cell);
        }
    }
}
