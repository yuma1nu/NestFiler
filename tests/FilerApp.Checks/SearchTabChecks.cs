using FilerApp.Core;

internal static class SearchTabChecks
{
    public static async Task RunAsync(string root, Action<bool, string> check)
    {
        var folder = Directory.CreateDirectory(Path.Combine(root, "search-tabs")).FullName;
        Directory.CreateDirectory(Path.Combine(folder, "needle-folder"));
        await File.WriteAllTextAsync(Path.Combine(folder, "needle-report.txt"), "header\nneedle line\nnext");
        await File.WriteAllTextAsync(Path.Combine(folder, "other.txt"), "needle\nother");
        var options = new SearchOptions(folder, "needle", false, true, false, false, "", ".git", "UTF-8");
        var hits = new List<SearchHit>();
        await new SearchService().SearchAsync(options, new Collector(hits), default);
        check(hits.Count == 2 && hits.All(x => x.Entry != null && x.Name.Contains("needle")) && hits.Any(x => x.Entry!.IsDirectory), "検索: ファイル名・フォルダ名と表示用メタデータ");
        hits.Clear();
        var combined = options with { Content = true, IncludeNames = true, Extensions = ".txt" };
        await new SearchService().SearchAsync(combined, new Collector(hits), default);
        check(hits.Count == 3 && hits.Any(x => x.Line == 0 && x.Name == "needle-report.txt") && hits.Any(x => x.Line == 2 && x.Text == "needle line"), "検索: 名前＋内容で両方を対象にし行番号も保持");
        var normal = new TabViewModel(new FolderService()); await normal.NavigateAsync(folder);
        var normalEntries = normal.Entries;
        var tab = new TabViewModel(new FolderService()); await tab.RunSearchAsync(combined);
        check(tab.IsSearch && !tab.Busy && tab.Entries.Count == 3 && tab.Entries.All(x => x.SearchDetail.Contains(folder)) && tab.Entries.Any(x => x.SearchLine == 2 && x.SearchDetail.Contains("needle line")) && ReferenceEquals(normalEntries, normal.Entries), "検索タブ: 通常一覧形式・格納先・一致行、元タブを維持");
        await tab.SortAsync("Line", true);
        check(tab.Entries.Select(x => x.SearchLine).SequenceEqual(new[] { 2, 1, 0 }) && tab.Entries[0].SearchMatch == "needle line", "検索列: 一致内容と行番号を独立して表示・並べ替え");
        await tab.SortAsync("Name", true); var snapshot = tab.Capture();
        var restored = new TabViewModel(new FolderService()); await restored.RestoreAsync(snapshot);
        check(restored.Search == combined && restored.SortDescending && restored.Entries.Count == 3 && !restored.CanBack && !restored.CanForward, "検索タブ: 閉じたタブを条件と並べ替え状態付きで復元");
        var old = tab.RunSearchAsync(options);
        var latest = tab.RunSearchAsync(options with { Query = "other" });
        await Task.WhenAll(old, latest);
        check(tab.Entries.Count == 1 && tab.Entries[0].Name == "other.txt" && tab.Search?.Query == "other", "検索タブ: 古い検索結果で最新の条件を上書きしない");
        await tab.RunSearchAsync(options with { Folder = Path.Combine(folder, "missing") });
        check(!tab.Busy && tab.SearchWarnings.Count > 0 && tab.Entries.Count == 0, "検索タブ: 到達不能な対象を警告として表示し復帰");
        var largeFolder = Directory.CreateDirectory(Path.Combine(folder, "large")).FullName;
        await File.WriteAllTextAsync(Path.Combine(largeFolder, "bulk.txt"), string.Join('\n', Enumerable.Repeat("needle", 6000)));
        var largeOptions = combined with { Folder = largeFolder };
        var streamed = false;
        tab.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(TabViewModel.Status) && tab.Busy && tab.Entries.Count > 0 && tab.Entries.Count < 5000) streamed = true; };
        await tab.RunSearchAsync(largeOptions);
        check(streamed && tab.Entries.Count == 5000 && tab.Status.Contains("打ち切り"), "検索タブ: 結果を段階表示し5,000一致で打ち切り");
        var cancelled = tab.RunSearchAsync(largeOptions); tab.StopSearch(); await cancelled;
        check(!tab.Busy && tab.Status.Contains("中断"), "検索タブ: 中断でUI待機を解除し表示済み結果を維持");
        var state = new SessionState { Left = [folder, folder], LeftSearches = [null, combined], LeftIndex = 1 };
        var store = new StateStore(Path.Combine(folder, "search-state.db")); await store.SaveAsync(state);
        var loaded = await store.LoadAsync();
        check(loaded.LeftSearches.Count == 2 && loaded.LeftSearches[0] == null && loaded.LeftSearches[1] == combined && loaded.LeftIndex == 1, "検索タブ: 通常タブとの並び・選択・条件をSQLiteへ保存");
    }
    private sealed class Collector(List<SearchHit> hits) : IProgress<SearchHit> { public void Report(SearchHit hit) => hits.Add(hit); }
}
