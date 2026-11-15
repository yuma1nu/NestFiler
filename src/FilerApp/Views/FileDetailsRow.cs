using FilerApp.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace FilerApp.Views;
public sealed class FileDetailsRow : Grid
{
    private PaneView? owner;
    private readonly Dictionary<string, FrameworkElement> cells = [];
    private FileTypeIcon? icon;
    private TextBlock? name;
    private FileEntry? displayedEntry;
    private bool detailsReady;
    private CancellationTokenSource? detailsLoad;
    private int detailVersion;
    public static readonly DependencyProperty EntryProperty = DependencyProperty.Register(nameof(Entry), typeof(FileEntry), typeof(FileDetailsRow), new PropertyMetadata(null, (sender, _) => ((FileDetailsRow)sender).UpdateEntry()));
    public FileEntry? Entry { get => (FileEntry?)GetValue(EntryProperty); set => SetValue(EntryProperty, value); }
    public FileDetailsRow()
    {
        Height = (double)Application.Current.Resources["FileRowHeight"]; HorizontalAlignment = HorizontalAlignment.Left;
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        Loaded += (_, _) =>
        {
            AttachToPane();
            // A recycled template can receive Loaded before its visual parent
            // chain is complete. Retry after the template/layout pass.
            if (owner == null) DispatcherQueue.TryEnqueue(() => { if (IsLoaded && owner == null) AttachToPane(); });
        };
        Unloaded += (_, _) => { detailVersion++; detailsLoad?.Cancel(); detailsLoad?.Dispose(); detailsLoad = null; if (owner != null) owner.ColumnsChanged -= UpdateLayoutColumns; owner = null; displayedEntry = null; };
    }
    private void AttachToPane()
    {
        if (owner != null) return;
        var parent = VisualTreeHelper.GetParent(this);
        while (parent != null && parent is not PaneView) parent = VisualTreeHelper.GetParent(parent);
        if (parent is PaneView pane) { owner = pane; pane.ColumnsChanged += UpdateLayoutColumns; UpdateLayoutColumns(); }
    }
    private FrameworkElement CreateCell(string key)
    {
        if (key == "Name")
        {
            var grid = new Grid { Padding = new Thickness(6, 0, 6, 0), ColumnSpacing = 6 };
            grid.ColumnDefinitions.Add(new() { Width = new(20) }); grid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
            icon = new FileTypeIcon(); name = new TextBlock { FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(name, 1); grid.Children.Add(icon); grid.Children.Add(name); return grid;
        }
        if (key == "Modified") return new ModifiedDateText { Margin = new Thickness(6, 0, 6, 0) };
        var text = new TextBlock { FontSize = key is "Match" or "Path" ? 12 : 13, Margin = new Thickness(6, 0, 6, 0), TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap, VerticalAlignment = VerticalAlignment.Center };
        if (key is "Size" or "Line") text.TextAlignment = TextAlignment.Right;
        return text;
    }
    private void UpdateLayoutColumns()
    {
        if (RebuildColumns()) displayedEntry = null;
        UpdateEntry();
    }
    private bool RebuildColumns()
    {
        if (owner == null) return false;
        var columns = owner.CurrentColumns.Columns;
        bool newCells = false;
        foreach (var obsolete in cells.Keys.Where(key => !columns.Any(c => c.Key == key)).ToArray()) { Children.Remove(cells[obsolete]); cells.Remove(obsolete); }
        while (ColumnDefinitions.Count > columns.Count) ColumnDefinitions.RemoveAt(ColumnDefinitions.Count - 1);
        while (ColumnDefinitions.Count < columns.Count) ColumnDefinitions.Add(new());
        for (int i = 0; i < columns.Count; i++)
        {
            var column = columns[i]; ColumnDefinitions[i].Width = new(column.Width);
            if (!cells.TryGetValue(column.Key, out var cell))
            {
                if (column.Key != "Name" && !detailsReady) continue;
                cell = CreateCell(column.Key); cells[column.Key] = cell; Children.Add(cell); newCells |= column.Key == "Name";
            }
            Grid.SetColumn(cell, i);
            var bounds = new Rect(0, 0, Math.Max(1, column.Width - (column.Key == "Modified" ? 12 : 0)), Height);
            if (cell.Clip is not RectangleGeometry geometry || geometry.Rect != bounds) cell.Clip = new RectangleGeometry { Rect = bounds };
        }
        Width = Math.Max(owner.CurrentColumns.TotalWidth, owner.DetailsViewportWidth);
        return newCells;
    }
    private async void UpdateEntry()
    {
        if (Entry is not { } entry)
        {
            detailVersion++; detailsLoad?.Cancel(); detailsLoad?.Dispose(); detailsLoad = null; displayedEntry = null;
            if (name != null) name.Text = ""; if (icon != null) icon.Entry = null;
            foreach (var (key, cell) in cells) if (key != "Name") cell.Visibility = Visibility.Collapsed;
            return;
        }
        if (cells.Count == 0 || ReferenceEquals(displayedEntry, entry)) return;
        displayedEntry = entry;
        if (icon != null) icon.Entry = entry;
        if (name != null) name.Text = entry.Name;
        ToolTipService.SetToolTip(this, entry.DisplayTooltip);
        detailsLoad?.Cancel(); detailsLoad?.Dispose();
        var cancellation = detailsLoad = new CancellationTokenSource(); var token = cancellation.Token; var version = ++detailVersion;
        try
        {
            // Render the name first. Creating and binding all date/type/size
            // controls during fast scrolling makes every newly visible row costly.
            foreach (var (key, cell) in cells) if (key != "Name") cell.Visibility = Visibility.Collapsed;
            if (owner != null) await owner.WhenVisualWorkAllowedAsync(token);
            var available = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => available.TrySetResult())) return;
            await available.Task.WaitAsync(token);
            if (owner != null) await owner.WhenVisualWorkAllowedAsync(token);
            if (version != detailVersion || !IsLoaded || owner == null) return;
            detailsReady = true; RebuildColumns(); FillDetails(entry);
        }
        catch (OperationCanceledException) { }
        finally { if (ReferenceEquals(detailsLoad, cancellation)) { detailsLoad = null; cancellation.Dispose(); } }
    }
    private void FillDetails(FileEntry entry)
    {
        foreach (var (key, cell) in cells)
        {
            cell.Visibility = Visibility.Visible;
            if (cell is ModifiedDateText date) { date.Entry = entry; date.DateText.TextTrimming = TextTrimming.CharacterEllipsis; }
            else if (cell is TextBlock text) text.Text = key switch { "Kind" => entry.Kind, "Size" => entry.SizeLabel, "Line" => entry.SearchLineLabel, "Path" => entry.Path, "Match" => entry.SearchMatch, _ => "" };
        }
    }
#if DEBUG
    internal bool DebugDetailsReady => cells.Count == owner?.CurrentColumns.Columns.Count && cells.Values.All(x => x.Visibility == Visibility.Visible);
    internal FrameworkElement DebugCell(string key) => cells[key];
#endif
}
