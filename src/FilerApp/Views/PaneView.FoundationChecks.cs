#if DEBUG
using FilerApp.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace FilerApp.Views;
public sealed partial class PaneView
{
    private int headerUpdateCount;
    internal async Task DebugCheckFoundationAsync(string root, List<string> results)
    {
        void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); results.Add("PASS " + name); }
        var folder = Directory.CreateDirectory(Path.Combine(root, "daily-use")).FullName;
        var child = Directory.CreateDirectory(Path.Combine(folder, "child")).FullName;
        await Task.Run(() => { for (int i = 0; i < 2000; i++) File.WriteAllText(Path.Combine(folder, $"run{i}.txt"), "daily-use fixture"); });
        SetDisplayMode(FileViewMode.Details); await NavigateAsync(folder); FocusFiles();
        for (int i = 0; i < 40 && watcher == null; i++) await Task.Delay(50);
        if (watcher == null) throw new InvalidOperationException("Folder watcher was not attached");
        var selected = Current!.Entries.Single(x => x.Name == "run250.txt");
        Files.SelectedItems.Clear(); Files.SelectedItem = selected; Files.ScrollIntoView(selected, ScrollIntoViewAlignment.Leading); Files.UpdateLayout();
        columnScroll!.ChangeView(null, columnScroll.VerticalOffset + 4, null, true); await Task.Delay(100); CapturePosition();
        var saved = Current.Positions[folder]; int headers = headerUpdateCount;
        columnScroll.ChangeView(null, columnScroll.VerticalOffset + 12, null, true); await Task.Delay(100);
        Check(headerUpdateCount == headers, "vertical scrolling does not update column headers");
        CapturePosition(); saved = Current.Positions[folder];
        await File.WriteAllTextAsync(Path.Combine(folder, "added-by-external.txt"), "external creation");
        for (int i = 0; i < 100 && !Current.Entries.Any(x => x.Name == "added-by-external.txt"); i++) await Task.Delay(60);
        await Task.Delay(150);
        Check(Current.Entries.Any(x => x.Name == "added-by-external.txt") && SelectedPaths.SequenceEqual(new[] { selected.Path }), "real FileSystemWatcher refresh retains selected file");
        Files.UpdateLayout();
        var anchor = Current.Entries.Single(x => EntryKey(x) == saved.Anchor);
        double y = ((ListViewItem)Files.ContainerFromItem(anchor)).TransformToVisual(Files).TransformPoint(new Point()).Y;
        Check(Math.Abs(y - saved.AnchorOffset) < 2, "automatic refresh preserves visible anchor and pixel offset");
        await WithStableFocusAsync(() => Current.SortAsync("Name", true));
        Check(SelectedPaths.SequenceEqual(new[] { selected.Path }), "sorting restores selection by path");
        var tab = Current; CapturePosition(); var position = tab.Positions[folder]; var tabIndex = Models.IndexOf(tab);
        await NavigateAsync(child, true); await File.WriteAllTextAsync(Path.Combine(folder, "created-while-tab-inactive.txt"), "external"); SelectTab(tabIndex + 1); await Task.Delay(100);
        Check(Current == tab && SelectedPaths.SequenceEqual(new[] { selected.Path }), "switching tabs restores selected file");
        Files.UpdateLayout();
        var anchorAfterTab = Current!.Entries.Single(x => EntryKey(x) == position.Anchor);
        double tabY = double.NaN;
        // TabChanged and ScrollIntoView finish asynchronously. Check the final
        // pixel position instead of treating a fixed 100ms delay as completion.
        for (int attempt = 0; attempt < 50; attempt++)
        {
            Files.UpdateLayout();
            if (Files.ContainerFromItem(anchorAfterTab) is ListViewItem anchorRow) tabY = anchorRow.TransformToVisual(Files).TransformPoint(new Point()).Y;
            if (!double.IsNaN(tabY) && Math.Abs(tabY - position.AnchorOffset) < 2) break;
            await Task.Delay(20);
        }
        if (double.IsNaN(tabY) || Math.Abs(tabY - position.AnchorOffset) >= 2) throw new InvalidOperationException($"switching tabs restores visible anchor: y={tabY:F2}, expected={position.AnchorOffset:F2}, offset={columnScroll!.VerticalOffset:F2}, savedOffset={position.VerticalOffset:F2}");
        Check(true, "switching tabs restores visible anchor");
        for (int i = 0; i < 100 && !Current.Entries.Any(x => x.Name == "created-while-tab-inactive.txt"); i++) await Task.Delay(50);
        Check(Current.Entries.Any(x => x.Name == "created-while-tab-inactive.txt"), "reactivating an inactive tab rechecks its folder");
        await NavigateAsync(child); await GoUpAsync();
        Check(Current!.Path == folder && SelectedPaths.SequenceEqual(new[] { child }), "parent navigation selects the folder just left");
        Files.ScrollIntoView(Current.Entries.Single(x => x.Path == child), ScrollIntoViewAlignment.Leading); Files.UpdateLayout();
        var childRow = (ListViewItem)Files.ContainerFromItem(Current.Entries.Single(x => x.Path == child));
        var childPoint = childRow.TransformToVisual(Files).TransformPoint(new Point(Math.Max(1, childRow.ActualWidth - 2), childRow.ActualHeight / 2));
        Check(DropFolder(childPoint) == child && DropFolder(new Point(-1, -1)) == folder, "folder row and background resolve to different drop destinations");
        SetCutPaths([child]); Check(Math.Abs(childRow.Opacity - 0.45) < 0.01, "cut paths dim realized rows without changing selection");
        SetCutPaths([]); Check(childRow.Opacity == 1, "clearing cut state restores row opacity");
        FocusFilter(); ListFilter.Text = "run2*.txt";
        for (int i = 0; i < 40 && Current.Filter != "run2*.txt"; i++) await Task.Delay(50);
        await Task.Delay(100);
        if (!ReferenceEquals(Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(XamlRoot), ListFilter)) throw new InvalidOperationException("Filtering stole editor focus");
        Check(Current.Entries.Count == 111 && Current.Entries.All(x => x.Name.StartsWith("run2")), "normal list filters asynchronously with wildcard");
        await WithStableFocusAsync(() => Current.SetFilterAsync(""));
        var snapshot = tab.Capture(); var restored = new TabViewModel(new FolderService()); await restored.RestoreAsync(snapshot);
        Check(restored.CanBack && restored.SortDescending && restored.Positions.Count > 0, "tab snapshot retains navigation, sort and position state");
    }
}
#endif
