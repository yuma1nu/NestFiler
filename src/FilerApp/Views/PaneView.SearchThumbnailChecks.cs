#if DEBUG
using FilerApp.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FilerApp.Views;
public sealed partial class PaneView
{
    internal async Task DebugWaitForThumbnailAsync(string path)
    {
        var entry = Current!.Entries.Single(x => x.Path == path);
        Files.ScrollIntoView(entry); Files.UpdateLayout();
        var cell = (DependencyObject)Files.ContainerFromItem(entry);
        var icon = FindVisualChild<FileTypeIcon>(cell)!;
        for (int i = 0; i < 60 && !icon.IsShowingThumbnail; i++) await Task.Delay(50);
        if (!icon.ShowThumbnail || !icon.IsShowingThumbnail) throw new InvalidOperationException("Image cell did not display a thumbnail");
    }
    internal Task DebugRunSearchAsync(SearchOptions options) => RunPaneSearchAsync(Current!, options);
    internal async Task DebugAssertSearchTabAsync(TabViewModel tab)
    {
        await Task.Delay(50);
        Files.UpdateLayout();
        if (!ReferenceEquals(Files.ItemsSource, tab.Entries) || SearchModeChoice.Visibility != Visibility.Visible || SearchOptionsButton.Visibility != Visibility.Visible || BackButton.Visibility != Visibility.Collapsed || BuildSearchOptionsMenu()?.Items.Count < 8) throw new InvalidOperationException("Search layout/options did not use the file pane");
        if (ViewModeButtons.Visibility != Visibility.Collapsed || PaneSearch.ActualWidth < 100) throw new InvalidOperationException("Search controls left insufficient query width");
        var entry = tab.Entries[0]; Files.ScrollIntoView(entry); Files.UpdateLayout();
        var row = (DependencyObject)Files.ContainerFromItem(entry);
        var details = FindVisualChild<FileDetailsRow>(row);
        for (int attempt = 0; attempt < 60 && details?.DebugDetailsReady != true; attempt++) await Task.Delay(20);
        Files.UpdateLayout();
        bool HasText(DependencyObject node, string value)
        {
            if (node is TextBlock text && text.Text == value) return true;
            for (int i = 0; i < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(node); i++)
                if (HasText(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(node, i), value)) return true;
            return false;
        }
        if (!HasText(row, entry.Name) || !HasText(row, entry.Path) || !HasText(row, entry.SearchMatch) || Current != tab || (row is FrameworkElement measured && measured.ActualHeight > 32)) throw new InvalidOperationException($"Search filename/path/match columns were not displayed in one row: name={HasText(row, entry.Name)}, path={HasText(row, entry.Path)}, match={HasText(row, entry.SearchMatch)}, height={(row as FrameworkElement)?.ActualHeight}, fields={string.Join(',', CurrentColumns.Columns.Select(x => x.Key))}, detail={FindVisualChild<FileDetailsRow>(row)?.ActualHeight}");
    }
}
#endif
