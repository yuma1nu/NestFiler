using Microsoft.UI.Xaml;

namespace FilerApp.Views;
public sealed partial class PaneView
{
    private FileSystemWatcher? watcher;
    private System.Threading.Timer? watcherEvents;
    private string? watchingPath;
    private int watcherVersion;
    private bool folderDirty, watchUnavailable, autoRefreshing, watchingStopped;
    private DateTime firstChange, lastChange, lastCheck = DateTime.UtcNow;
    private readonly DispatcherTimer watchTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    public Func<bool>? MayAutoRefresh { get; set; }
    private void InitializeWatching()
    {
        watchTimer.Tick += async (_, _) =>
        {
            if (watchingStopped || Current is not { IsSearch: false, Busy: false } tab || autoRefreshing || IsRangeSelecting || MayAutoRefresh?.Invoke() == false) return;
            var now = DateTime.UtcNow;
            bool fallback = (watchUnavailable || tab.Path.StartsWith(@"\\")) && now - lastCheck > TimeSpan.FromSeconds(30);
            if (!fallback && (!folderDirty || now - lastChange < TimeSpan.FromMilliseconds(700) && now - firstChange < TimeSpan.FromSeconds(2))) return;
            folderDirty = false; lastCheck = now; autoRefreshing = true;
            try { await RefreshAsync(); if (watchUnavailable) ConfigureWatcher(); }
            catch (Exception ex) { Error?.Invoke("自動更新できません: " + ex.Message); }
            finally { autoRefreshing = false; }
        };
        Loaded += (_, _) => { watchingStopped = false; watchTimer.Start(); ConfigureWatcher(); };
        Unloaded += (_, _) => StopWatching();
    }
    private void MarkFolderDirty()
    {
        if (!folderDirty) firstChange = DateTime.UtcNow;
        folderDirty = true; lastChange = DateTime.UtcNow;
    }
    public void RecheckFolder() { if (Current?.IsSearch == false) MarkFolderDirty(); }
    private void ConfigureWatcher()
    {
        var path = Current?.IsSearch == false ? Current.Path : null;
        if (watchingStopped || string.Equals(watchingPath, path, StringComparison.OrdinalIgnoreCase) && !watchUnavailable) return;
        var version = ++watcherVersion; watcher?.Dispose(); watcherEvents?.Dispose(); watcherEvents = null; watcher = null; watchingPath = path; watchUnavailable = false; folderDirty = false;
        if (string.IsNullOrWhiteSpace(path)) return;
        // Creating a watcher on an unreachable share may block. Never do this on the UI thread.
        _ = Task.Run(() =>
        {
            FileSystemWatcher? candidate = null;
            System.Threading.Timer? events = null;
            try
            {
                int queued = 0;
                events = new System.Threading.Timer(_ =>
                {
                    if (!DispatcherQueue.TryEnqueue(() => { Interlocked.Exchange(ref queued, 0); if (version == watcherVersion) MarkFolderDirty(); })) Interlocked.Exchange(ref queued, 0);
                }, null, Timeout.Infinite, Timeout.Infinite);
                candidate = new(path) { IncludeSubdirectories = false, NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.Attributes };
                void Changed(object? sender, FileSystemEventArgs args)
                {
                    if (Interlocked.CompareExchange(ref queued, 1, 0) != 0) return;
                    try { events.Change(100, Timeout.Infinite); }
                    catch (ObjectDisposedException) { Interlocked.Exchange(ref queued, 0); }
                }
                candidate.Changed += Changed; candidate.Created += Changed; candidate.Deleted += Changed; candidate.Renamed += (_, args) => Changed(null, args);
                candidate.Error += (_, _) => DispatcherQueue.TryEnqueue(() => { if (version == watcherVersion) { watchUnavailable = true; MarkFolderDirty(); } });
                candidate.EnableRaisingEvents = true;
                var completed = candidate;
                var completedEvents = events;
                if (!DispatcherQueue.TryEnqueue(() => { if (version == watcherVersion && !watchingStopped) { watcher = completed; watcherEvents = completedEvents; } else { completed.Dispose(); completedEvents.Dispose(); } })) { completed.Dispose(); completedEvents.Dispose(); }
            }
            catch
            {
                candidate?.Dispose(); events?.Dispose(); DispatcherQueue.TryEnqueue(() => { if (version == watcherVersion) watchUnavailable = true; });
            }
        });
    }
    private void StopWatching() { watchingStopped = true; watcherVersion++; watcher?.Dispose(); watcherEvents?.Dispose(); watcherEvents = null; watcher = null; watchingPath = null; watchTimer.Stop(); filterTimer.Stop(); }
}
