using FilerApp.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Input;
using Windows.Foundation;
using Windows.System;

namespace FilerApp.Views;
public sealed partial class PaneView
{
    private sealed class RangeGesture(Point start, List<IndexSpan> original, SelectionRule rule, MarqueeLayout layout, object? source)
    {
        public Point Start = start, Last;
        public List<IndexSpan> Original = original;
        public SelectionRule Rule = rule;
        public MarqueeLayout Layout = layout;
        public object? Source = source;
        public Pointer? Pointer;
        public bool Dragging;
    }
    private RangeGesture? rangeGesture;
    private ScrollViewer? rangeScroll;
    private bool updatingMarqueeSelection;
    private readonly DispatcherTimer rangeTimer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    public bool IsRangeSelecting => rangeGesture != null;

    private void InitializeMarquee()
    {
        Files.BeginMarquee = BeginRangePointer;
        Files.MoveMarquee = MoveRangePointer;
        Files.ReleaseMarquee = ReleaseRangePointer;
        Files.PointerCaptureLost += (_, _) => CancelRangeSelection(true);
        Files.PointerCanceled += (_, _) => CancelRangeSelection(true);
        Files.SizeChanged += (_, _) => CancelRangeSelection(true);
        Files.RegisterPropertyChangedCallback(ItemsControl.ItemsSourceProperty, (_, _) => CancelRangeSelection(false));
        Unloaded += (_, _) => { CancelRangeSelection(false); rangeScroll = null; };
        rangeTimer.Tick += (_, _) =>
        {
            if (rangeGesture is not { Dragging: true } gesture || rangeScroll == null) { rangeTimer.Stop(); return; }
            var height = Math.Min(Files.ActualHeight, rangeScroll.ViewportHeight);
            // Ease into auto-scroll near the edge and cap the speed beyond it.
            double speed = gesture.Last.Y < 24 ? -Math.Clamp((24 - gesture.Last.Y) * 1.5, 2, 48) :
                gesture.Last.Y > height - 24 ? Math.Clamp((gesture.Last.Y - height + 24) * 1.5, 2, 48) : 0;
            if (speed != 0) rangeScroll.ChangeView(null, Math.Clamp(rangeScroll.VerticalOffset + speed, 0, rangeScroll.ScrollableHeight), null, true);
            Files.UpdateLayout(); UpdateRangeSelection();
        };
    }

    private bool BeginRangePointer(PointerRoutedEventArgs e)
    {
        if (e.Handled) return false;
        if (Current is { IsSearch: true, Busy: true }) return false;
        CancelRangeSelection(true);
        var point = e.GetCurrentPoint(Files);
        if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse || !point.Properties.IsLeftButtonPressed || !IsRangeBackground(e.OriginalSource as DependencyObject)) return false;
        rangeScroll = FindVisualChild<ScrollViewer>(Files);
        if (rangeScroll == null || point.Position.X >= rangeScroll.ViewportWidth || point.Position.Y >= rangeScroll.ViewportHeight) return false;
        if (!Files.CapturePointer(e.Pointer)) return false;
        Files.SuppressBackgroundTap();
        var control = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        var shift = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        BeginRange(point.Position, control ? SelectionRule.Toggle : shift ? SelectionRule.Add : SelectionRule.Replace);
        rangeGesture!.Pointer = e.Pointer;
        e.Handled = true; return true;
    }
    private void SelectionGutterPressed(object sender, PointerRoutedEventArgs e) => BeginRangePointer(e);
    private bool IsRangeBackground(DependencyObject? source)
    {
        for (var node = source; node != null && node != Files; node = VisualTreeHelper.GetParent(node))
            if (node is ListViewItem or TextBlock or FileTypeIcon or ModifiedDateText or ScrollBar or ButtonBase) return false;
        return true;
    }
    private bool MoveRangePointer(PointerRoutedEventArgs e)
    {
        if (rangeGesture?.Pointer?.PointerId != e.Pointer.PointerId) return false;
        var point = e.GetCurrentPoint(Files);
        if (!point.Properties.IsLeftButtonPressed) EndRange(false);
        else MoveRange(point.Position);
        e.Handled = true; return true;
    }
    private bool ReleaseRangePointer(PointerRoutedEventArgs e)
    {
        if (rangeGesture?.Pointer?.PointerId != e.Pointer.PointerId) return false;
        if (!e.GetCurrentPoint(Files).Properties.IsLeftButtonPressed)
        {
            MoveRange(e.GetCurrentPoint(Files).Position);
            if (rangeGesture?.Dragging == true) UpdateRangeSelection();
            EndRange(false);
        }
        e.Handled = true; return true;
    }
    private void BeginRange(Point point, SelectionRule rule)
    {
        Files.UpdateLayout();
        var offset = rangeScroll?.VerticalOffset ?? 0;
        rangeGesture = new(new(point.X + (rangeScroll?.HorizontalOffset ?? 0), point.Y + offset), CurrentSelectionSpans(), rule, MeasureRangeLayout(), Files.ItemsSource) { Last = point };
        Activated?.Invoke(this); NavigationRefresh.Focus(FocusState.Programmatic);
        if (rule == SelectionRule.Replace) ApplyRangeSelection([]);
    }
    private void MoveRange(Point point)
    {
        if (rangeGesture is not { } gesture) return;
        gesture.Last = point;
        if (!gesture.Dragging && (Math.Abs(point.X + (rangeScroll?.HorizontalOffset ?? 0) - gesture.Start.X) >= 5 || Math.Abs(point.Y + (rangeScroll?.VerticalOffset ?? 0) - gesture.Start.Y) >= 5))
        { gesture.Dragging = true; rangeTimer.Start(); UpdateRangeSelection(); }
    }
    private MarqueeLayout MeasureRangeLayout()
    {
        var panel = Files.ItemsPanelRoot;
        var first = panel?.Children.OfType<ListViewItem>().FirstOrDefault(x => Files.IndexFromContainer(x) >= 0);
        if (first == null) return new(Files.Items.Count, 1, 0, 0, Math.Max(1, Files.ActualWidth), 26, Math.Max(1, Files.ActualWidth), 26);
        var index = Files.IndexFromContainer(first);
        var position = first.TransformToVisual(Files).TransformPoint(new Point());
        int columns = 1;
        double stepX = first.ActualWidth, stepY = first.ActualHeight + first.Margin.Top + first.Margin.Bottom;
        if (panel is ItemsWrapGrid wrap)
        {
            stepX = wrap.ItemWidth; stepY = wrap.ItemHeight;
            columns = Math.Max(1, (int)Math.Floor((wrap.ActualWidth + 0.1) / stepX));
        }
        return new(Files.Items.Count, columns, position.X + (rangeScroll?.HorizontalOffset ?? 0) - index % columns * stepX,
            position.Y + (rangeScroll?.VerticalOffset ?? 0) - index / columns * stepY,
            stepX, stepY, first.ActualWidth, first.ActualHeight);
    }
    private void UpdateRangeSelection()
    {
        if (rangeGesture is not { Dragging: true } gesture || rangeScroll == null) return;
        if (!ReferenceEquals(gesture.Source, Files.ItemsSource)) { EndRange(false); return; }
        var width = Math.Max(0, Math.Min(Files.ActualWidth, rangeScroll.ViewportWidth));
        var height = Math.Max(0, Math.Min(Files.ActualHeight, rangeScroll.ViewportHeight));
        var end = new Point(Math.Clamp(gesture.Last.X, 0, width) + rangeScroll.HorizontalOffset, Math.Clamp(gesture.Last.Y, 0, height) + rangeScroll.VerticalOffset);
        var hits = gesture.Layout.HitTest(gesture.Start.X, gesture.Start.Y, end.X, end.Y);
        ApplyRangeSelection(SelectionSpans.Combine(gesture.Original, hits, gesture.Rule));
        var left = Math.Clamp(Math.Min(gesture.Start.X, end.X) - rangeScroll.HorizontalOffset, 0, width);
        var top = Math.Clamp(Math.Min(gesture.Start.Y, end.Y) - rangeScroll.VerticalOffset, 0, height);
        var right = Math.Clamp(Math.Max(gesture.Start.X, end.X) - rangeScroll.HorizontalOffset, 0, width);
        var bottom = Math.Clamp(Math.Max(gesture.Start.Y, end.Y) - rangeScroll.VerticalOffset, 0, height);
        Canvas.SetLeft(MarqueeBox, left); Canvas.SetTop(MarqueeBox, top);
        MarqueeBox.Width = right - left; MarqueeBox.Height = bottom - top;
        MarqueeOverlay.Visibility = Visibility.Visible;
    }
    private List<IndexSpan> CurrentSelectionSpans() => Files.SelectedRanges.Select(x => new IndexSpan(x.FirstIndex, checked((int)x.Length))).ToList();
    private void ApplyRangeSelection(IReadOnlyList<IndexSpan> desired)
    {
        var current = CurrentSelectionSpans();
        var remove = SelectionSpans.Combine(current, desired, SelectionRule.Subtract);
        var add = SelectionSpans.Combine(desired, current, SelectionRule.Subtract);
        if (remove.Count == 0 && add.Count == 0) return;
        updatingMarqueeSelection = true;
        try
        {
            foreach (var span in remove) Files.DeselectRange(new ItemIndexRange(span.Start, (uint)span.Length));
            foreach (var span in add) Files.SelectRange(new ItemIndexRange(span.Start, (uint)span.Length));
        }
        finally { updatingMarqueeSelection = false; PublishFileSelection(); }
    }
    public bool CancelRangeSelection(bool restore)
    {
        if (rangeGesture == null) return false;
        EndRange(restore); return true;
    }
    private void EndRange(bool restore)
    {
        var gesture = rangeGesture; rangeGesture = null; rangeTimer.Stop(); MarqueeOverlay.Visibility = Visibility.Collapsed;
        if (gesture == null) return;
        if (restore && ReferenceEquals(gesture.Source, Files.ItemsSource)) ApplyRangeSelection(gesture.Original);
        if (gesture.Pointer != null) Files.ReleasePointerCapture(gesture.Pointer);
        if (isActivePane && ContainsFocus()) FocusFiles();
    }
    private static T? FindVisualChild<T>(DependencyObject node) where T : DependencyObject
    {
        if (node is T match) return match;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) if (FindVisualChild<T>(VisualTreeHelper.GetChild(node, i)) is { } found) return found;
        return null;
    }
}
