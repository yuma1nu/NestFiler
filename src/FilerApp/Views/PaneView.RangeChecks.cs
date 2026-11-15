#if DEBUG
using FilerApp.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace FilerApp.Views;
public sealed partial class PaneView
{
    internal async Task DebugCheckRangeAsync(FileViewMode mode)
    {
        SetDisplayMode(mode);
        rangeScroll = FindVisualChild<ScrollViewer>(Files)!;
        rangeScroll.ChangeView(null, 0, null, true);
        await Task.Delay(70); Files.UpdateLayout();
        var layout = MeasureRangeLayout();
        var start = new Point(layout.OriginX - 1, layout.OriginY - 1);
        var end = new Point(layout.OriginX + layout.ItemWidth / 2, layout.OriginY + layout.StepY + layout.ItemHeight / 2);
        var expected = new List<int>();
        // Compare computed layout to real container positions, including wrapping.
        foreach (var item in Files.ItemsPanelRoot.Children.OfType<ListViewItem>())
        {
            int index = Files.IndexFromContainer(item); if (index < 0) continue;
            var p = item.TransformToVisual(Files).TransformPoint(new Point());
            if (Math.Abs(p.X - layout.OriginX - index % layout.Columns * layout.StepX) > 1 || Math.Abs(p.Y - layout.OriginY - index / layout.Columns * layout.StepY) > 1) throw new InvalidOperationException(mode + ": measured marquee layout disagrees with container positions");
            if (p.X < end.X && p.X + item.ActualWidth > start.X && p.Y < end.Y && p.Y + item.ActualHeight > start.Y) expected.Add(index);
        }
        if (expected.Count != 2) throw new InvalidOperationException(mode + ": test rectangle did not cross exactly two items");
        int last = Files.Items.Count - 1;
        var baseline = new[] { 0, last };
        var source = Files.ItemsSource;
        int[] Indices() => CurrentSelectionSpans().SelectMany(x => Enumerable.Range(x.Start, x.Length)).Order().ToArray();
        void AssertIndices(IEnumerable<int> indices, string stage)
        {
            if (!Indices().SequenceEqual(indices.Order())) throw new InvalidOperationException(mode + ": incorrect marquee selection at " + stage + ": " + string.Join(',', Indices()));
        }
        ApplyRangeSelection([new(0, 1), new(last, 1)]);
        BeginRange(start, SelectionRule.Replace); MoveRange(end); UpdateRangeSelection();
        AssertIndices(expected, "replace");
        CancelRangeSelection(true); AssertIndices(baseline, "cancel");
        BeginRange(start, SelectionRule.Add); MoveRange(end); UpdateRangeSelection();
        AssertIndices(baseline.Union(expected), "shift-add"); EndRange(false);
        ApplyRangeSelection([new(0, 1), new(last, 1)]);
        BeginRange(start, SelectionRule.Toggle); MoveRange(end); UpdateRangeSelection();
        AssertIndices(baseline.Except(expected).Union(expected.Except(baseline)), "ctrl-toggle");
        MoveRange(start); UpdateRangeSelection(); AssertIndices(baseline, "shrink");
        CancelRangeSelection(true);
        BeginRange(start, SelectionRule.Replace);
        MoveRange(new(end.X, Files.ActualHeight + 80)); UpdateRangeSelection();
        var beforeScroll = rangeScroll.VerticalOffset;
        await Task.Delay(180);
        if (rangeScroll.VerticalOffset <= beforeScroll || Files.SelectedItems.Count <= expected.Count) throw new InvalidOperationException(mode + ": marquee did not auto-scroll and extend selection");
        var offscreen = Indices();
        if (!offscreen.Contains(0) || !offscreen.SequenceEqual(layout.HitTest(start.X, start.Y, end.X, rangeScroll.ViewportHeight + rangeScroll.VerticalOffset).SelectMany(x => Enumerable.Range(x.Start, x.Length)))) throw new InvalidOperationException(mode + ": offscreen marquee selection was incorrect");
        CancelRangeSelection(true); AssertIndices(baseline, "cancel after scroll");
        if (MarqueeOverlay.Visibility != Visibility.Collapsed || IsRangeSelecting || !ContainsFocus() || !ReferenceEquals(source, Files.ItemsSource)) throw new InvalidOperationException("Range operation lost state/focus or left an overlay behind");
        rangeScroll.ChangeView(null, 0, null, true); await Task.Delay(50);
        // Native file drags retain their route; blank space starts selection.
        var first = (ListViewItem)Files.ContainerFromIndex(0);
        var icon = FindVisualChild<FileTypeIcon>(first)!;
        var name = FindVisualChild<TextBlock>(first)!;
        if (IsRangeBackground(icon) || IsRangeBackground(name) || IsRangeBackground(first) || !IsRangeBackground(SelectionGutter)) throw new InvalidOperationException("Marquee/file-drag hit areas overlap incorrectly");
    }
    internal void DebugAssertPaneSurface(bool active, Brush pageBackground)
    {
        if (Frame.BorderThickness != new Thickness(0) || (ActivePaneSurface.Visibility == Visibility.Visible) != active) throw new InvalidOperationException("Pane still uses a border or has wrong active background state");
        if (Frame.Background is SolidColorBrush background && pageBackground is SolidColorBrush page && background.Color != page.Color) throw new InvalidOperationException("Inactive pane background differs from the application background");
    }
    internal async Task DebugCheckDateTextAsync()
    {
        SetDisplayMode(FileViewMode.Details); await Task.Delay(50); Files.UpdateLayout();
        ModifiedDateText? date = null;
        for (int attempt = 0; attempt < 50 && date == null; attempt++) { date = FindVisualChild<ModifiedDateText>((DependencyObject)Files.ContainerFromIndex(0)); if (date == null) await Task.Delay(20); }
        if (date == null) throw new InvalidOperationException("Deferred date cell did not appear");
        var original = date.Entry!;
        var highContrast = new Windows.UI.ViewManagement.AccessibilitySettings().HighContrast;
        foreach (var theme in new[] { ElementTheme.Light, ElementTheme.Dark })
        {
            date.RequestedTheme = theme; await Task.Delay(30);
            date.Entry = original with { Modified = DateTime.Now };
            var recentColor = ((SolidColorBrush)date.DateText.Foreground).Color; var recentWeight = date.DateText.FontWeight;
            date.Entry = original with { Modified = DateTime.Now.AddYears(-2) };
            var oldColor = ((SolidColorBrush)date.DateText.Foreground).Color;
            if ((!highContrast && recentColor == oldColor) || date.DateText.FontWeight == recentWeight || date.DateText.Text != date.Entry.ModifiedLabel) throw new InvalidOperationException("Date text did not distinguish recent/old dates in " + theme);
        }
        date.RequestedTheme = ElementTheme.Default; date.Entry = original;
    }
}
#endif
