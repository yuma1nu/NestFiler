using FilerApp.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace FilerApp.Views;
public sealed partial class PaneView
{
    private bool restoringPosition;
    private int positionVersion;
    private static string EntryKey(FileEntry entry) => entry.SearchLine > 0 ? entry.Path + "\u001f" + entry.SearchLine : entry.Path;
    public void CapturePosition()
    {
        if (restoringPosition || Current is not { } tab || tab.Entries.Count == 0 || Files.ItemsSource != tab.Entries) return;
        CapturePosition(tab);
    }
    private void CapturePosition(TabViewModel tab)
    {
        if (restoringPosition || Files.Items.Count == 0) return;
        var scroll = columnScroll ?? FindVisualChild<ScrollViewer>(Files);
        var first = Files.ItemsPanelRoot?.Children.OfType<ListViewItem>()
            .Select(row => (Row: row, Y: row.TransformToVisual(Files).TransformPoint(new Point()).Y))
            .Where(x => x.Y + x.Row.ActualHeight > 0).OrderBy(x => x.Y).FirstOrDefault();
        var anchor = first?.Row.Content as FileEntry;
        tab.Positions[tab.Path] = new(SelectedEntries().Select(EntryKey).ToArray(), anchor == null ? null : EntryKey(anchor), first?.Y ?? 0,
            scroll?.VerticalOffset ?? 0, scroll?.HorizontalOffset ?? 0);
        while (tab.Positions.Count > 100) tab.Positions.Remove(tab.Positions.Keys.First());
    }
    private async Task RestorePositionAsync(TabViewModel? expected = null, string? selectPath = null)
    {
        var tab = Current; if (tab == null || expected != null && tab != expected) return;
        var version = ++positionVersion;
        tab.Positions.TryGetValue(tab.Path, out var saved);
        // Let ItemsSource bindings finish before touching the new containers.
        await Task.Yield();
        if (version != positionVersion || Current != tab || Files.ItemsSource != tab.Entries) return;
        var previousFocus = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(XamlRoot) as Control;
        bool hadFocus = ContainsFocus();
        restoringPosition = true; updatingMarqueeSelection = true;
        try
        {
            var selected = (selectPath != null ? new[] { selectPath } : saved?.Selected ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var spans = new List<IndexSpan>(); int start = -1;
            for (int i = 0; i <= tab.Entries.Count; i++)
            {
                bool chosen = i < tab.Entries.Count && selected.Contains(EntryKey(tab.Entries[i]));
                if (chosen && start < 0) start = i;
                else if (!chosen && start >= 0) { spans.Add(new(start, i - start)); start = -1; }
            }
            SetSelectedSpans(spans);
            Files.UpdateLayout();
            var scroll = columnScroll ?? FindVisualChild<ScrollViewer>(Files);
            if (selectPath != null && Files.SelectedItem != null) Files.ScrollIntoView(Files.SelectedItem, ScrollIntoViewAlignment.Leading);
            else if (saved != null && scroll != null)
            {
                var anchor = tab.Entries.FirstOrDefault(x => StringComparer.OrdinalIgnoreCase.Equals(EntryKey(x), saved.Anchor));
                if (anchor != null)
                {
                    Files.ScrollIntoView(anchor, ScrollIntoViewAlignment.Leading); Files.UpdateLayout();
                    if (Files.ContainerFromItem(anchor) is ListViewItem row)
                    {
                        double y = row.TransformToVisual(Files).TransformPoint(new Point()).Y;
                        scroll.ChangeView(saved.HorizontalOffset, Math.Max(0, scroll.VerticalOffset + y - saved.AnchorOffset), null, true);
                    }
                }
                else scroll.ChangeView(saved.HorizontalOffset, saved.VerticalOffset, null, true);
            }
            else scroll?.ChangeView(0, 0, null, true);
        }
        finally
        {
            if (!hadFocus && ContainsFocus()) previousFocus?.Focus(FocusState.Programmatic);
            restoringPosition = false; updatingMarqueeSelection = false; PublishFileSelection();
        }
    }
    public async Task NavigateAndSelectAsync(string folder, string path, bool newTab = false)
    {
        await NavigateAsync(folder, newTab);
        if (Current is { IsSearch: false } tab && tab.Filter.Length > 0 && !tab.Entries.Any(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase)))
            await WithStableFocusAsync(() => tab.SetFilterAsync(""));
        UpdateFilterControls(); await RestorePositionAsync(Current, path);
    }
}
