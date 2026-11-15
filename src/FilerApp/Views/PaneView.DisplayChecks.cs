#if DEBUG
using FilerApp.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace FilerApp.Views;
public sealed partial class PaneView
{
    internal async Task DebugWaitForViewAsync(FileViewMode mode)
    {
        // Loaded and template replacement are dispatched separately by WinUI.
        // Wait for the requested presentation, rather than assuming a 50 ms frame.
        for (int attempt = 0; attempt < 40; attempt++)
        {
            await Task.Delay(25);
            try { DebugAssertViewLayout(mode); return; }
            catch (InvalidOperationException) when (attempt < 39) { }
        }
    }
    // Exercise the same Checked/Unchecked paths as pointer and automation input.
    internal void DebugToggleView(FileViewMode mode)
    {
        var button = mode switch { FileViewMode.SmallIcons => SmallIconsViewButton, FileViewMode.LargeIcons => LargeIconsViewButton, _ => DetailsViewButton };
        button.IsChecked = button.IsChecked != true;
    }
    internal int DebugAssertViewLayout(FileViewMode mode)
    {
        Files.UpdateLayout();
        var checkedCount = new[] { DetailsViewButton, SmallIconsViewButton, LargeIconsViewButton }.Count(x => x.IsChecked == true);
        if (DisplayMode != mode || checkedCount != 1) throw new InvalidOperationException("View mode selection is not exclusive");
        bool details = mode == FileViewMode.Details;
        if ((ColumnHeaders.Visibility == Visibility.Visible) != details || (details ? Files.ItemsPanelRoot is not ItemsStackPanel : Files.ItemsPanelRoot is not ItemsWrapGrid)) throw new InvalidOperationException("Incorrect header visibility or virtualizing panel");
        var realized = Enumerable.Range(0, Files.Items.Count).Count(i => Files.ContainerFromIndex(i) != null);
        if (Files.Items.Count > 0)
        {
            if (Files.ContainerFromIndex(0) is not ListViewItem first) throw new InvalidOperationException("First item was not realized");
            FileTypeIcon? FindIcon(DependencyObject node)
            {
                if (node is FileTypeIcon icon) return icon;
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) if (FindIcon(VisualTreeHelper.GetChild(node, i)) is { } found) return found;
                return null;
            }
            var icon = FindIcon(first);
            var expectedSize = details ? 16 : mode == FileViewMode.SmallIcons ? 48 : 96;
            if (icon == null || icon.IconSize != expectedSize || Math.Abs(icon.ActualWidth - expectedSize - 2) > 1) throw new InvalidOperationException($"Icon has incorrect rendered dimensions: mode={mode}, expected={expectedSize}, actual={icon?.IconSize}/{icon?.ActualWidth}, type={Files.ItemTemplate}");
            if (!details && Files.Items.Count > 1 && Files.ContainerFromIndex(1) is FrameworkElement second)
            {
                var a = first.TransformToVisual(Files).TransformPoint(new Point());
                var b = second.TransformToVisual(Files).TransformPoint(new Point());
                if (b.X <= a.X || Math.Abs(b.Y - a.Y) > 1) throw new InvalidOperationException("Icon tiles did not form a horizontal row");
            }
            if (Files.Items.Count >= 300 && (realized == 0 || realized >= Files.Items.Count)) throw new InvalidOperationException("View did not virtualize the fixture");
        }
        return realized;
    }
}
#endif
