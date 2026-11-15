#if DEBUG
using FilerApp.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using System.Diagnostics;

namespace FilerApp.Views;
public sealed partial class PaneView
{
    internal async Task DebugCheckColumnPositionsAsync(string folder, List<string> results)
    {
        SetDisplayMode(FileViewMode.Details); await NavigateAsync(folder); await Task.Delay(100);
        foreach (bool search in new[] { false, true })
        {
            if (search) { var tab = OpenSearchTab("image-"); await tab.SearchWork!; await Task.Delay(80); }
            var layout = CurrentColumns; var saved = layout.Capture();
            try
            {
                foreach (double width in new[] { 720d, 80d, 450d, 150d })
                foreach (double proportion in new[] { 0d, 0.5d, 1d })
                {
                    resizingColumn = true;
                    for (int i = 0; i < 20; i++) layout.Resize("Name", width + i);
                    await Task.Delay(25); resizingColumn = false; columnResizeTimer.Stop(); ApplyColumnChanges();
                    await Task.Delay(30); columnScroll!.ChangeView(columnScroll.ScrollableWidth * proportion, null, null, true); await Task.Delay(40); Files.UpdateLayout();
                    await WhenVisualWorkAllowedAsync(default); await Task.Delay(30);
                    for (int attempt = 0; attempt < 60; attempt++)
                    {
                        var pending = Files.ItemsPanelRoot.Children.OfType<ListViewItem>().Where(item =>
                        {
                            double y = item.TransformToVisual(Files).TransformPoint(new Point()).Y;
                            return y + item.ActualHeight > 0 && y < Files.ActualHeight;
                        }).Select(item => FindVisualChild<FileDetailsRow>(item)).Where(row => row?.IsLoaded == true).ToArray();
                        if (pending.Length > 0 && pending.All(row => row!.DebugDetailsReady)) break;
                        await Task.Delay(20);
                    }
                    Files.UpdateLayout();
                    foreach (var item in Files.ItemsPanelRoot.Children.OfType<ListViewItem>().Take(8))
                    {
                        var row = FindVisualChild<FileDetailsRow>(item); if (row == null) continue;
                        double y = item.TransformToVisual(Files).TransformPoint(new Point()).Y;
                        if (!row.IsLoaded || y + item.ActualHeight <= 0 || y >= Files.ActualHeight) continue;
                        if (!row.DebugDetailsReady) throw new InvalidOperationException("Visible detail cells did not finish their deferred update");
                        for (int i = 0; i < layout.Columns.Count; i++)
                        {
                            var cell = row.DebugCell(layout.Columns[i].Key);
                            double dataX = cell.TransformToVisual(this).TransformPoint(new Point()).X - cell.Margin.Left;
                            double headerX = ((FrameworkElement)ColumnHeaderGrid.Children[i]).TransformToVisual(this).TransformPoint(new Point()).X;
                            if (Math.Abs(dataX - headerX) > 1.1) throw new InvalidOperationException($"{(search ? "search" : "normal")} resize/scroll mismatch {layout.Columns[i].Key}: {dataX}/{headerX}, offset={columnScroll.HorizontalOffset}");
                        }
                    }
                }
                results.Add("PASS " + (search ? "search" : "normal") + " column boundaries agree after repeated resize at left/middle/right scroll limits");
            }
            finally { resizingColumn = false; layout.Restore(saved); if (search) CloseCurrent(); }
        }
    }
    internal async Task DebugCheckDragSnapshotAsync(string folder, List<string> results)
    {
        await NavigateAsync(folder); await Task.Delay(80); Files.UpdateLayout();
        var a = Current!.Entries.Single(x => x.Name == "A.txt"); var b = Current.Entries.Single(x => x.Name == "B.txt");
        Files.SelectedItem = a; pressedDragEntry = b; pressedDragSource = Files.ItemsSource; PrepareTransferSelection(b, false);
        var snapshot = CaptureDragEntries([a]);
        if (!SelectedPaths.SequenceEqual(new[] { b.Path }) || snapshot.Length != 1 || snapshot[0] != b) throw new InvalidOperationException("Dragging unselected B still chose selected A");
        results.Add("PASS pressing unselected B replaces A; stale native drag items cannot transfer A");
        Files.SelectRange(new ItemIndexRange(0, 2)); PrepareTransferSelection(b, false);
        if (CaptureDragEntries([a, b]).Length != 2 || SelectedPaths.Length != 2) throw new InvalidOperationException("Dragging within multiple selection collapsed it");
        results.Add("PASS dragging an already selected row preserves the multiple selection");
        var package = new DataPackage(); PopulateDragPackage(package, snapshot);
        Files.SelectedItem = a;
        var delivered = await package.GetView().GetStorageItemsAsync();
        if (delivered.Count != 1 || delivered[0].Path != b.Path) throw new InvalidOperationException("Delayed drag data changed with later selection");
        var destination = Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(folder)!, "drag-copy-" + Guid.NewGuid().ToString("N"))).FullName;
        var copied = await new Services.FileOperationService { SuppressErrorDialogsForChecks = true }.ExecuteAsync(Services.FileAction.Copy, delivered.Select(x => x.Path).ToArray(), destination, 0, CancellationToken.None);
        if (copied.Count != 1 || copied[0].Result != "成功" || File.Exists(Path.Combine(destination, "A.txt")) ||
            !File.Exists(a.Path) || !File.Exists(b.Path) || await File.ReadAllTextAsync(Path.Combine(destination, "B.txt")) != "B-content")
            throw new InvalidOperationException("Drag snapshot did not physically copy only B via Windows Shell");
        results.Add("PASS delayed drag snapshot physically copies only B via Windows Shell after selection changes to A");
        pressedDragEntry = null; pressedDragSource = null;
    }
    internal async Task DebugScrollImagesAsync(string folder, FileViewMode mode, Func<Task>? duringScroll = null, Action<string>? measure = null)
    {
        var clock = Stopwatch.StartNew();
        await NavigateAsync(folder); SetDisplayMode(mode); await Task.Delay(80); Files.UpdateLayout();
        measure?.Invoke($"MEASURE {mode} setup including navigation/mode/layout={clock.Elapsed.TotalMilliseconds:F1}ms");
        var frames = new List<double>();
        for (int i = 0; i < 55; i++)
        {
            if (Current?.Path != folder) break;
            clock.Restart();
            columnScroll?.ChangeView(null, columnScroll.ScrollableHeight * (i % 11) / 10d, null, true);
            if (i == 12 && duringScroll != null) await duringScroll();
            await Task.Delay(20);
            frames.Add(clock.Elapsed.TotalMilliseconds);
        }
        measure?.Invoke($"MEASURE {mode} scroll step p95={frames.Order().ElementAt((int)(frames.Count * 0.95)):F1}ms max={frames.Max():F1}ms samples={frames.Count}");
        if (Files.ItemsPanelRoot.Children.Count >= 500) throw new InvalidOperationException("Image scrolling realized all 500 rows");
    }
}
#endif
