using FilerApp.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Data;

namespace FilerApp.Views;
public sealed partial class PaneView
{
    private bool selectionUpdateQueued;
#if DEBUG
    private int selectionPublicationCount;
#endif
    // Use model indexes, not SelectedItems: crossing the WinRT collection for
    // every selected item on every selection notification is needlessly costly.
    private IEnumerable<FileEntry> SelectedEntries()
    {
        var entries = Files.ItemsSource as IReadOnlyList<FileEntry>;
        if (entries == null) yield break;
        foreach (var range in Files.SelectedRanges)
            for (int i = range.FirstIndex, end = Math.Min(entries.Count, range.LastIndex + 1); i < end; i++)
                yield return entries[i];
    }
    private void QueueSelectionUpdate()
    {
        if (selectionUpdateQueued) return;
        selectionUpdateQueued = true;
        if (!DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => { if (selectionUpdateQueued) PublishFileSelection(); })) selectionUpdateQueued = false;
    }
    private void PublishFileSelection()
    {
        selectionUpdateQueued = false;
#if DEBUG
        selectionPublicationCount++;
#endif
        int count = 0; long bytes = 0;
        HashSet<string>? paths = Current?.IsSearch == true ? new(StringComparer.OrdinalIgnoreCase) : null;
        foreach (var entry in SelectedEntries())
        {
            if (paths != null && !paths.Add(entry.Path)) continue;
            count++; if (!entry.IsDirectory) bytes += entry.Size;
        }
        SelectionStatus.Text = $"{count} 選択" + (count > 0 ? $" · {bytes / 1048576d:N2} MB（ファイル）" : "");
        SelectionUpdated?.Invoke(Selected);
    }
    private void SetSelectedSpans(IReadOnlyList<IndexSpan> desired)
    {
        var current = CurrentSelectionSpans();
        foreach (var span in SelectionSpans.Combine(current, desired, SelectionRule.Subtract)) Files.DeselectRange(new ItemIndexRange(span.Start, (uint)span.Length));
        foreach (var span in SelectionSpans.Combine(desired, current, SelectionRule.Subtract)) Files.SelectRange(new ItemIndexRange(span.Start, (uint)span.Length));
    }
}
