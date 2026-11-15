#if DEBUG
using System.Diagnostics;
using FilerApp.Core;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace FilerApp.Views;
public sealed partial class PaneView
{
    internal async Task DebugMeasureSelectionAsync(string root, List<string> results)
    {
        var folder = Directory.CreateDirectory(Path.Combine(root, "input-fixture")).FullName;
        await Task.Run(() => { for (int i = 0; i < 500; i++) File.WriteAllText(Path.Combine(folder, $"sample-{i:D3}.txt"), new string('x', i + 1)); });
        var oldRefresh = MayAutoRefresh; MayAutoRefresh = () => false;
        try
        {
            SetDisplayMode(FileViewMode.Details); await NavigateAsync(folder); await DebugWaitForViewAsync(FileViewMode.Details);
            var times = new List<double>();
            for (int i = 0; i < 6; i++)
            {
                Files.DeselectRange(new ItemIndexRange(0, 500)); await Task.Delay(30);
                var timer = Stopwatch.StartNew(); Files.SelectRange(new ItemIndexRange(0, 500));
                times.Add(timer.Elapsed.TotalMilliseconds); await Task.Delay(30);
            }
            results.Add($"MEASURE select 500 median={times.Order().ElementAt(3):F2}ms max={times.Max():F2}ms realized={Files.ItemsPanelRoot.Children.Count}");
            CapturePosition();
            var restoreTimer = Stopwatch.StartNew(); await RestorePositionAsync();
            results.Add($"MEASURE restore 500 elapsed={restoreTimer.Elapsed.TotalMilliseconds:F2}ms realized={Files.ItemsPanelRoot.Children.Count}");
            if (SelectedPaths.Length != 500) throw new InvalidOperationException("Selection benchmark lost items");
            Files.DeselectRange(new ItemIndexRange(0, 500)); await Task.Delay(30);
            restoreTimer.Restart(); await RestorePositionAsync();
            results.Add($"MEASURE restore cleared 500 elapsed={restoreTimer.Elapsed.TotalMilliseconds:F2}ms realized={Files.ItemsPanelRoot.Children.Count}");
            if (SelectedPaths.Length != 500) throw new InvalidOperationException("Range-based restoration lost selected paths");
            results.Add("PASS 500 selections restore with native ranges, including previously cleared selection");
            Files.DeselectRange(new ItemIndexRange(0, 500)); await Task.Delay(30);
            int publications = selectionPublicationCount;
            for (int i = 0; i < 50; i++) Files.SelectRange(new ItemIndexRange(i * 10, 10));
            await Task.Delay(60);
            if (selectionPublicationCount - publications != 1 || SelectedPaths.Length != 500 || !SelectionStatus.Text.StartsWith("500 選択")) throw new InvalidOperationException("Selection summary was not coalesced or is out of date");
            results.Add("PASS 50 selection notifications produce one accurate 500-file summary");
            var layout = NormalColumns.Capture();
            try
            {
                NormalColumns.Resize("Name", 80); NormalColumns.Resize("Modified", 88); NormalColumns.Resize("Kind", 40); NormalColumns.Resize("Size", 52);
                Files.ScrollIntoView(Current!.Entries[0], ScrollIntoViewAlignment.Leading); await Task.Delay(50); Files.UpdateLayout();
                var row = (ListViewItem)Files.ContainerFromIndex(0);
                if (Math.Abs(row.ActualHeight - 28) > 0.5 || row.ActualWidth < columnScroll!.ViewportWidth - 1 || IsRangeBackground(row) || !IsRangeBackground(SelectionGutter)) throw new InvalidOperationException("Full-row/gutter hit areas have incorrect geometry");
                foreach (var point in new[] { new Point(2, 1), new Point(row.ActualWidth - 2, 1), new Point(row.ActualWidth - 2, row.ActualHeight - 1) })
                {
                    var host = row.TransformToVisual(null).TransformPoint(point);
                    var hits = VisualTreeHelper.FindElementsInHostCoordinates(host, Files);
                    if (!hits.Any(hit => HasAncestor(hit, row))) throw new InvalidOperationException("Row edges or blank right area failed hit testing");
                }
                results.Add("PASS row top/bottom and right-side whitespace are hit-testable, with a separate marquee gutter");
                if (Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(row) != Current.Entries[0].Name) throw new InvalidOperationException("Accessible name includes implementation details");
                results.Add("PASS row accessibility uses the filename without the full record dump");
            }
            finally { NormalColumns.Restore(layout); }
            if (Files.ItemsPanelRoot.Children.Count >= 500) throw new InvalidOperationException("Selection devirtualized the entire folder");
            results.Add("PASS selection retains virtualized row containers");
        }
        finally { MayAutoRefresh = oldRefresh; }
    }
    private static bool HasAncestor(DependencyObject source, DependencyObject ancestor)
    { for (var node = source; node != null; node = VisualTreeHelper.GetParent(node)) if (node == ancestor) return true; return false; }
}
#endif
