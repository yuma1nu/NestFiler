using Microsoft.UI.Xaml;

namespace FilerApp.Views;
public sealed partial class PaneView
{
    private readonly DispatcherTimer visualQuietTimer = new() { Interval = TimeSpan.FromMilliseconds(160) };
    private TaskCompletionSource? visualQuiet;
    internal bool IsDeferringVisualWork => visualQuiet != null;
    private void InitializeVisualWork()
    {
        visualQuietTimer.Tick += (_, _) =>
        {
            if (fileDragInProgress || resizingColumn) return;
            visualQuietTimer.Stop(); var pending = visualQuiet; visualQuiet = null; pending?.TrySetResult();
        };
        Unloaded += (_, _) => { visualQuietTimer.Stop(); var pending = visualQuiet; visualQuiet = null; pending?.TrySetCanceled(); };
    }
    private void NoteListScroll()
    {
        visualQuiet ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
        visualQuietTimer.Stop(); visualQuietTimer.Start();
    }
    internal async Task WhenVisualWorkAllowedAsync(CancellationToken token)
    {
        // A new scroll can begin before an old quiet-period continuation runs.
        while (visualQuiet is { } pending) await pending.Task.WaitAsync(token);
    }
}
