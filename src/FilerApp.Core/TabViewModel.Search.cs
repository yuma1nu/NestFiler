using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace FilerApp.Core;
public sealed partial class TabViewModel
{
    private SearchOptions? search;
    private ObservableCollection<FileEntry>? searchEntries;
    private readonly List<SearchHit> searchWarnings = [];
    public SearchOptions? Search => search;
    public bool IsSearch => search != null;
    public IReadOnlyList<SearchHit> SearchWarnings => searchWarnings;
    public Task? SearchWork { get; private set; }
    public void PrepareSearch(SearchOptions options)
    {
        SetSearch(options); Path = options.Folder; Title = "検索";
        Status = "検索文字列を入力してEnter";
    }
    private void SetSearch(SearchOptions? value)
    {
        if (Set(ref search, value, nameof(Search))) { Changed(nameof(IsSearch)); Changed(nameof(CanBack)); Changed(nameof(CanForward)); }
    }
    public Task RunSearchAsync(SearchOptions options) => SearchWork = RunSearchCoreAsync(options);
    private async Task RunSearchCoreAsync(SearchOptions options)
    {
        load?.Cancel(); load?.Dispose(); load = new(); var token = load.Token; var version = ++generation;
        SetSearch(options); Path = options.Folder;
        var titleQuery = options.Query.Length > 24 ? options.Query[..24] + "…" : options.Query;
        Title = string.IsNullOrWhiteSpace(titleQuery) ? "検索" : "検索: " + titleQuery;
        searchEntries = []; Entries = searchEntries; searchWarnings.Clear();
        Busy = true; Status = "検索中…";
        try
        {
            options = options with { Folder = System.IO.Path.GetFullPath(Environment.ExpandEnvironmentVariables(options.Folder)) };
            SetSearch(options); Path = options.Folder;
            var queue = new ConcurrentQueue<SearchHit>();
            var work = new SearchService().SearchAsync(options, new QueuedSearchProgress(queue), token);
            _ = work.ContinueWith(t => _ = t.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            void Drain(int maximum)
            {
                if (version != generation || token.IsCancellationRequested) return;
                var comparer = new SearchEntryComparer(SortColumn, SortDescending);
                for (int i = 0; i < maximum && queue.TryDequeue(out var hit); i++)
                {
                    if (hit.Error) { searchWarnings.Add(hit); continue; }
                    if (hit.Entry is not { } file) continue;
                    var location = System.IO.Path.GetDirectoryName(file.Path) ?? file.Path;
                    var detail = hit.Line > 0 ? $"{hit.Line}行: {hit.Text}  ｜  {location}" : $"名前に一致  ｜  {location}";
                    var entry = file with { SearchDetail = detail, SearchLine = hit.Line, SearchText = hit.Text };
                    var entries = searchEntries!;
                    int left = 0, right = entries.Count;
                    while (left < right) { int middle = (left + right) / 2; if (comparer.Compare(entries[middle], entry) <= 0) left = middle + 1; else right = middle; }
                    entries.Insert(left, entry);
                }
                Status = $"検索中… {searchEntries!.Count:N0} 件 / {searchWarnings.Count:N0} 警告";
            }
            while (!work.IsCompleted)
            {
                token.ThrowIfCancellationRequested();
                await Task.WhenAny(work, Task.Delay(60, token)).WaitAsync(token);
                if (version != generation) return;
                Drain(250);
            }
            var summary = await work.WaitAsync(token);
            while (!queue.IsEmpty) { token.ThrowIfCancellationRequested(); if (version != generation) return; Drain(250); await Task.Yield(); }
            if (version == generation) Status = $"{summary.Matches:N0} 一致 / {summary.Warnings:N0} 警告" + (summary.LimitReached ? " — 5,000一致で打ち切り" : " — 完了");
        }
        catch (OperationCanceledException) { if (version == generation) Status = $"検索を中断 — {Entries.Count:N0} 件の結果を保持"; }
        catch (Exception ex) { if (version == generation) Status = "検索できません: " + ex.Message; }
        finally { if (version == generation) { Busy = false; load?.Cancel(); } }
    }
    public void StopSearch() { if (IsSearch) load?.Cancel(); }
    private sealed class QueuedSearchProgress(ConcurrentQueue<SearchHit> queue) : IProgress<SearchHit>
    { public void Report(SearchHit hit) => queue.Enqueue(hit); }
    private sealed class SearchEntryComparer(string column, bool descending) : IComparer<FileEntry>
    {
        public int Compare(FileEntry? a, FileEntry? b)
        {
            if (a == null || b == null) return a == b ? 0 : a == null ? -1 : 1;
            if (a.IsDirectory != b.IsDirectory) return a.IsDirectory ? -1 : 1;
            int value = column switch { "Size" => a.Size.CompareTo(b.Size), "Modified" => a.Modified.CompareTo(b.Modified), "Kind" => StringComparer.CurrentCultureIgnoreCase.Compare(a.Kind, b.Kind), "Line" => a.SearchLine.CompareTo(b.SearchLine), "Path" => StringComparer.OrdinalIgnoreCase.Compare(a.Path, b.Path), "Match" => StringComparer.CurrentCultureIgnoreCase.Compare(a.SearchMatch, b.SearchMatch), _ => NaturalNameComparer.Instance.Compare(a.Name, b.Name) };
            if (value != 0) return descending ? -value : value;
            value = StringComparer.OrdinalIgnoreCase.Compare(a.Path, b.Path);
            return value != 0 ? value : a.SearchLine.CompareTo(b.SearchLine);
        }
    }
}
