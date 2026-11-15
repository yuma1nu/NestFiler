#if DEBUG
using FilerApp.Core;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace FilerApp.Views;
public sealed partial class PaneView
{
    internal async Task DebugCheckColumnsAsync(bool search)
    {
        SetDisplayMode(FileViewMode.Details); await Task.Delay(60); Files.UpdateLayout(); AttachColumnScroll();
        var selected = SelectedPaths.Order().ToArray();
        var layout = CurrentColumns;
        if (search)
        {
            layout.Resize("Match", 340); layout.Move("Path", "Name", true);
        }
        else
        {
            layout.Resize("Name", 280); layout.Move("Size", "Name", true);
        }
        Files.UpdateLayout(); await Task.Delay(50);
        var item = Current!.Entries[0]; Files.ScrollIntoView(item); Files.UpdateLayout();
        var row = FindVisualChild<FileDetailsRow>((DependencyObject)Files.ContainerFromItem(item))!;
        for (int attempt = 0; attempt < 50 && row?.DebugDetailsReady != true; attempt++) await Task.Delay(20);
        if (row == null || Math.Abs(row.ActualHeight - 28) > 0.5 || row.RowDefinitions.Count != 0 || row.ColumnDefinitions.Count != layout.Columns.Count) throw new InvalidOperationException("Detail/search row is not a single line with expected columns");
        for (int i = 0; i < layout.Columns.Count; i++)
        {
            var column = layout.Columns[i];
            if (headerOrder[i] != column.Key || Microsoft.UI.Xaml.Controls.Grid.GetColumn(row.DebugCell(column.Key)) != i || Math.Abs(row.ColumnDefinitions[i].ActualWidth - column.Width) > 0.5 || Math.Abs(ColumnHeaderGrid.ColumnDefinitions[i].ActualWidth - column.Width) > 0.5) throw new InvalidOperationException("Header/cell widths or order disagree for " + column.Key);
        }
        if (!SelectedPaths.Order().SequenceEqual(selected)) throw new InvalidOperationException("Column manipulation lost selection");
        columnScroll!.ChangeView(90, null, null, true); await Task.Delay(70); Files.UpdateLayout();
        var rowX = row.TransformToVisual(this).TransformPoint(new Point()).X;
        var headerX = ColumnHeaderGrid.TransformToVisual(this).TransformPoint(new Point()).X;
        if (columnScroll.HorizontalOffset <= 0 || Math.Abs(columnScroll.HorizontalOffset - HeaderHorizontalOffset) > 0.5 || Math.Abs(rowX - headerX) > 1) throw new InvalidOperationException($"Header and rows did not scroll together: offset={columnScroll.HorizontalOffset}/{HeaderHorizontalOffset}, x={rowX}/{headerX}");
        if (search)
        {
            var before = columnScroll.HorizontalOffset; ScrollColumnEdge(ColumnHeaders.ActualWidth + 10); await Task.Delay(60);
            if (columnScroll.HorizontalOffset <= before || Math.Abs(columnScroll.HorizontalOffset - HeaderHorizontalOffset) > 0.5) throw new InvalidOperationException("Column edge scrolling failed");
        }
        rangeScroll = columnScroll;
        BeginRange(new Point(30, 2), SelectionRule.Replace);
        MoveRange(new Point(31, 3));
        if (rangeGesture!.Dragging) throw new InvalidOperationException("Horizontal scroll offset incorrectly triggered a drag");
        MoveRange(new Point(80, 20)); UpdateRangeSelection();
        if (!rangeGesture.Dragging || Files.SelectedItems.Count == 0) throw new InvalidOperationException("Marquee did not select rows after horizontal scrolling");
        CancelRangeSelection(true);
        columnScroll.ChangeView(0, null, null, true); await Task.Delay(40);
    }
    internal void DebugSelectFile(string path)
    {
        Files.SelectedItems.Clear(); Files.SelectedItem = Current!.Entries.Single(x => x.Path == path); Files.ScrollIntoView(Files.SelectedItem); FocusFiles();
    }
}
#endif
