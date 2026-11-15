namespace FilerApp.Core;

public sealed record TabSnapshot(string Path, string[] History, int HistoryIndex, string SortColumn, bool SortDescending, SearchOptions? Search = null,
    Dictionary<string, ListingPosition>? Positions = null, string Filter = "", bool ShowHidden = false, bool ShowSystem = false);

public sealed partial class TabViewModel(IFolderService folders) : Observable
{
    private string path = "", status = "", title = "新しいタブ";
    private bool busy;
    private IReadOnlyList<FileEntry> entries = Array.Empty<FileEntry>();
    private CancellationTokenSource? load;
    private int generation;
    private readonly List<string> history = [];
    private int historyIndex = -1;
    private int sortGeneration;
    private IReadOnlyList<FileEntry> allEntries = Array.Empty<FileEntry>();
    private string filter = "";
    public string Filter => filter;
    public bool ShowHidden { get; private set; }
    public bool ShowSystem { get; private set; }
    public Dictionary<string, ListingPosition> Positions { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string SortColumn { get; private set; } = "Name";
    public bool SortDescending { get; private set; }
    public string Path { get => path; private set => Set(ref path, value); }
    public string Title { get => title; private set => Set(ref title, value); }
    public string Status { get => status; private set => Set(ref status, value); }
    public bool Busy { get => busy; private set => Set(ref busy, value); }
    public IReadOnlyList<FileEntry> Entries { get => entries; private set => Set(ref entries, value); }
    public bool CanBack => !IsSearch && historyIndex > 0;
    public bool CanForward => !IsSearch && historyIndex < history.Count - 1;
    public async Task NavigateAsync(string target, bool addHistory = true)
    {
        SetSearch(null);
        load?.Cancel(); load?.Dispose(); load = new();
        var token = load.Token; var version = ++generation;
        Busy = true; Status = "読み込み中…";
        try
        {
            target = System.IO.Path.GetFullPath(Environment.ExpandEnvironmentVariables(target));
            if (!StringComparer.OrdinalIgnoreCase.Equals(Path, target)) { allEntries = Array.Empty<FileEntry>(); Entries = allEntries; }
            Path = target; Title = System.IO.Path.GetFileName(System.IO.Path.TrimEndingDirectorySeparator(target));
            if (Title.Length == 0) Title = target;
            if (addHistory && (historyIndex < 0 || !StringComparer.OrdinalIgnoreCase.Equals(history[historyIndex], target)))
            { history.RemoveRange(historyIndex + 1, history.Count - historyIndex - 1); history.Add(target); if (history.Count > 200) history.RemoveAt(0); historyIndex = history.Count - 1; }
            Changed(nameof(CanBack)); Changed(nameof(CanForward));
            var result = await folders.ReadAsync(target, token).WaitAsync(TimeSpan.FromSeconds(15), token);
            if (version != generation) return;
            allEntries = result;
            await RebuildListingAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or TimeoutException)
        { if (version == generation) Status = $"開けません: {ex.Message}"; }
        finally { if (version == generation) { Busy = false; load?.Cancel(); } }
    }
    public Task BackAsync() => CanBack ? NavigateAsync(history[--historyIndex], false) : Task.CompletedTask;
    public Task ForwardAsync() => CanForward ? NavigateAsync(history[++historyIndex], false) : Task.CompletedTask;
    public Task UpAsync() => Directory.GetParent(Path) is { } p ? NavigateAsync(p.FullName) : Task.CompletedTask;
    public Task RefreshAsync() => Search is { } options ? RunSearchAsync(options) : NavigateAsync(Path, false);
    public void Cancel() { generation++; load?.Cancel(); }
    public TabSnapshot Capture() => new(Path, history.ToArray(), historyIndex, SortColumn, SortDescending, Search,
        Positions.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase), Filter, ShowHidden, ShowSystem);
    public async Task RestoreAsync(TabSnapshot snapshot)
    {
        int start = Math.Clamp(snapshot.HistoryIndex - 100, 0, Math.Max(0, snapshot.History.Length - 200));
        history.Clear(); history.AddRange(snapshot.History.Skip(start).Take(200));
        historyIndex = Math.Clamp(snapshot.HistoryIndex - start, -1, history.Count - 1);
        SortColumn = snapshot.SortColumn; SortDescending = snapshot.SortDescending;
        filter = snapshot.Filter; ShowHidden = snapshot.ShowHidden; ShowSystem = snapshot.ShowSystem;
        Positions.Clear(); if (snapshot.Positions != null) foreach (var item in snapshot.Positions.TakeLast(100)) Positions[item.Key] = item.Value;
        if (snapshot.Search is { } search) await RunSearchAsync(search);
        else await NavigateAsync(snapshot.Path, false);
    }
    public async Task SortAsync(string column, bool descending)
    {
        SortColumn = column; SortDescending = descending;
        if (IsSearch)
        {
            searchEntries = new(Entries.Order(new SearchEntryComparer(column, descending)));
            Entries = searchEntries; return;
        }
        await RebuildListingAsync();
    }
    public async Task SetFilterAsync(string query)
    { if (IsSearch) return; filter = query; Changed(nameof(Filter)); await RebuildListingAsync(); }
    public async Task SetVisibilityAsync(bool hidden, bool system)
    { ShowHidden = hidden; ShowSystem = system; await RebuildListingAsync(); }
    private async Task RebuildListingAsync()
    {
        var version = ++sortGeneration; var listingGeneration = generation;
        var original = allEntries; var column = SortColumn; var descending = SortDescending;
        var query = filter; var hidden = ShowHidden; var system = ShowSystem;
        var sorted = await Task.Run(() =>
        {
            var visible = original.Where(x => (hidden || !x.Attributes.HasFlag(FileAttributes.Hidden)) && (system || !x.Attributes.HasFlag(FileAttributes.System)) && FileNames.Matches(x.Name, query));
            return visible.Order(new SearchEntryComparer(column, descending)).ToArray();
        });
        if (version != sortGeneration || listingGeneration != generation || !ReferenceEquals(allEntries, original)) return;
        // Reuse both the collection and unchanged entries for a no-op refresh.
        if (Entries.Count != sorted.Length || !Entries.Zip(sorted).All(pair => SameVisibleData(pair.First, pair.Second)))
        {
            var previous = Entries.ToDictionary(x => x.Path, StringComparer.OrdinalIgnoreCase);
            Entries = sorted.Select(x => previous.TryGetValue(x.Path, out var old) && SameVisibleData(old, x) ? old : x).ToArray();
        }
        Status = sorted.Length == original.Count ? $"{sorted.Length:N0} 項目" : $"{sorted.Length:N0} / {original.Count:N0} 項目（絞り込み・非表示を含む）";
    }
    // Cloud hydration/archive flags do not change any current visible column.
    // allEntries still holds the latest metadata; avoid resetting the UI solely
    // for invisible attribute churn. Hidden/system changes affect filtering.
    private static bool SameVisibleData(FileEntry a, FileEntry b) => a.Path == b.Path && a.Name == b.Name && a.IsDirectory == b.IsDirectory && a.Size == b.Size && a.Modified == b.Modified &&
        (a.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == (b.Attributes & (FileAttributes.Hidden | FileAttributes.System));
}
