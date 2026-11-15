using FilerApp.Core;
using Microsoft.Data.Sqlite;

if (Environment.GetEnvironmentVariable("FILERAPP_SHELL_UI_CHECK") == "1") { await ShellInteractiveChecks.RunAsync(); return; }

var root = Path.Combine(Path.GetTempPath(), "FilerApp-check-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var passed = 0;
void Check(bool success, string name) { if (!success) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); passed++; }
try
{
    var a = Directory.CreateDirectory(Path.Combine(root, "A")).FullName;
    var b = Directory.CreateDirectory(Path.Combine(root, "B")).FullName;
    await File.WriteAllTextAsync(Path.Combine(a, "日本語.csv"), "値,単位\n1,mm");
    Directory.CreateDirectory(Path.Combine(a, "folder"));
    var fs = new FolderService();
    var entries = await fs.ReadAsync(a, default);
    Check(entries.Count == 2 && entries[0].IsDirectory && entries[1].Name == "日本語.csv", "実フォルダ・日本語名・フォルダ優先");
    var tab = new TabViewModel(fs);
    await tab.NavigateAsync(a); await tab.NavigateAsync(b); await tab.BackAsync();
    Check(tab.Path == a && tab.CanForward, "タブの戻る履歴");
    await tab.ForwardAsync(); Check(tab.Path == b, "進む履歴");
    await tab.BackAsync(); await tab.SortAsync("Modified", true);
    var reopened = new TabViewModel(fs); await reopened.RestoreAsync(tab.Capture());
    Check(reopened.Path == a && reopened.CanForward && reopened.SortColumn == "Modified" && reopened.SortDescending, "閉じたタブのパス・履歴位置・ソート状態を復元");
    await reopened.ForwardAsync(); await reopened.BackAsync();
    Check(reopened.Path == a && reopened.CanForward, "復元したタブで戻る・進むを継続");
    await tab.NavigateAsync(Path.Combine(root, "missing"));
    Check(tab.Status.StartsWith("開けません") && !tab.Busy && tab.Entries.Count == 0, "存在しないパスで復帰可能なエラー");
    await tab.NavigateAsync(a); Check(tab.Entries.Count == 2, "エラー後の再移動");
    var race = new DelayedFolders(); var racingTab = new TabViewModel(race);
    var oldLoad = racingTab.NavigateAsync(a); var newLoad = racingTab.NavigateAsync(b);
    race.New.SetResult([new(b, "new", true, 0, DateTime.Now)]); await newLoad;
    race.Old.SetResult([new(a, "old", true, 0, DateTime.Now)]); await oldLoad;
    Check(racingTab.Path == b && racingTab.Entries.Single().Name == "new", "古い非同期結果を破棄");
    using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
    try { await fs.ReadAsync(a, cancelled.Token); throw new Exception("キャンセル失敗"); } catch (OperationCanceledException) { Check(true, "列挙キャンセル"); }
    var database = new StateStore(Path.Combine(root, "state.db"));
    var state = new SessionState { Left = [a, b], Right = [b], LeftIndex = 1, Dual = false, Favorites = [new() { Name = "実験", Items = [new("1", "データ", a)] }, new() { Name = "共有", Items = [new("2", "同じフォルダ", a)] }] };
    await database.SaveAsync(state); var restored = await database.LoadAsync();
    Check(restored.LeftIndex == 1 && !restored.Dual && restored.Favorites.Count == 2 && restored.Favorites[1].Items[0].Path == a, "SQLite状態復元・同じパスを複数グループに保存");
    restored.Favorites.Clear(); await database.SaveAsync(restored);
    Check(Directory.Exists(a) && File.Exists(entries[1].Path), "お気に入り解除で実データを変更しない");
    for (var i = 0; i < 3000; i++) await File.WriteAllTextAsync(Path.Combine(b, $"sample-{i:D4}.txt"), "test");
    var watch = System.Diagnostics.Stopwatch.StartNew(); var many = await fs.ReadAsync(b, default);
    Check(many.Count == 3000, $"3,000ファイル列挙 ({watch.ElapsedMilliseconds} ms)");
    await FilerApp.Services.FileOperationService.CreateDirectoryAsync(root, "created");
    Check(Directory.Exists(Path.Combine(root, "created")), "新規フォルダ作成");
    try { await FilerApp.Services.FileOperationService.CreateDirectoryAsync(root, "created"); throw new Exception("衝突未検出"); }
    catch (System.ComponentModel.Win32Exception) { Check(true, "同名フォルダ作成を成功扱いしない"); }
    var operations = new FilerApp.Services.FileOperationService();
    var cancelledResults = await operations.ExecuteAsync(FilerApp.Services.FileAction.Move, [entries[1].Path], b, 0, new CancellationToken(true));
    Check(cancelledResults[0].Result.StartsWith("未実行") && File.Exists(entries[1].Path), "移動の開始前キャンセルで元ファイル保持");
    var missingResults = await operations.ExecuteAsync(FilerApp.Services.FileAction.Copy, [Path.Combine(root, "missing")], b, 0, default);
    Check(missingResults[0].Result.StartsWith("失敗"), "存在しないコピー元を失敗として報告");
    var copyTarget = Directory.CreateDirectory(Path.Combine(root, "copy-target")).FullName;
    var realCopy = await operations.ExecuteAsync(FilerApp.Services.FileAction.Copy, [entries[1].Path], copyTarget, 0, default);
    Check(File.ReadAllText(Path.Combine(copyTarget, "日本語.csv")) == File.ReadAllText(entries[1].Path) && realCopy.Any(x => x.Result == "成功"), "Shell実コピーと項目結果コールバック");
    var movedTarget = Directory.CreateDirectory(Path.Combine(root, "move-target")).FullName;
    var realMove = await operations.ExecuteAsync(FilerApp.Services.FileAction.Move, [Path.Combine(copyTarget, "日本語.csv")], movedTarget, 0, default);
    Console.WriteLine(string.Join("; ", realMove.Select(x => x.Path + " " + x.Result)));
    Check(File.Exists(Path.Combine(movedTarget, "日本語.csv")) && !File.Exists(Path.Combine(copyTarget, "日本語.csv")) && realMove.Any(x => x.Result == "成功"), "Shell実移動と元ファイルの消去");
    var partial = await operations.ExecuteAsync(FilerApp.Services.FileAction.Copy, [Path.Combine(root, "missing"), entries[1].Path], copyTarget, 0, default);
    Check(partial.Any(x => x.Result.StartsWith("失敗")) && partial.Any(x => x.Result == "成功"), "一部失敗と一部成功を別々に記録");
    Check(FilerApp.Services.OperationSink.Describe(0x00270005) == "スキップ", "Shellのスキップを成功扱いしない");
    var denied = new List<FilerApp.Services.OperationResult>();
    var sinkCheck = new FilerApp.Services.OperationSink(denied, default, null);
    Check(sinkCheck.PreDeleteItem(0, 0) < 0 && denied.Count == 1, "ごみ箱フラグなしの削除を拒否");
    using var midCancel = new CancellationTokenSource();
    var cancellationSink = new FilerApp.Services.OperationSink([], midCancel.Token, null);
    Check(cancellationSink.UpdateProgress(100, 1) == 0, "キャンセル前の進捗継続");
    midCancel.Cancel();
    Check(cancellationSink.UpdateProgress(100, 2) < 0, "進捗コールバックで途中キャンセルをShellへ伝達");
    var badDestination = await operations.ExecuteAsync(FilerApp.Services.FileAction.Move, [entries[1].Path], Path.Combine(root, "absent-target"), 0, default);
    Check(badDestination.Any(x => x.Result.StartsWith("失敗")) && File.Exists(entries[1].Path), "到達不能な移動先で元データを維持");
    var searchRoot = Directory.CreateDirectory(Path.Combine(root, "search")).FullName;
    await File.WriteAllTextAsync(Path.Combine(searchRoot, "日本語.txt"), "alpha\n実験123\nALPHA", new System.Text.UTF8Encoding(false));
    await File.WriteAllBytesAsync(Path.Combine(searchRoot, "shift.csv"), TextFiles.GetEncoding("CP932").GetBytes("名前,値\n実験,42"));
    Directory.CreateDirectory(Path.Combine(searchRoot, "excluded")); await File.WriteAllTextAsync(Path.Combine(searchRoot, "excluded", "hidden.txt"), "alpha");
    var hits = new List<SearchHit>(); var collector = new HitCollector(hits);
    var options = new SearchOptions(searchRoot, "alpha", true, true, false, false, ".txt", "excluded", "UTF-8");
    var summary = await new SearchService().SearchAsync(options, collector, default);
    Check(summary.Matches == 2 && hits[1].Line == 3, "grep行番号・大小文字・除外フォルダ");
    hits.Clear(); await new SearchService().SearchAsync(options with { Query = "実験\\d+", Regex = true }, collector, default);
    Check(hits.Count == 1 && hits[0].Line == 2, "正規表現検索");
    hits.Clear(); await new SearchService().SearchAsync(options with { Query = "日本語", Content = false }, collector, default);
    Check(hits.Count == 1 && hits[0].Line == 0, "ファイル名検索と内容検索の区別");
    hits.Clear(); await new SearchService().SearchAsync(options with { Query = "実験", Encoding = "CP932", Extensions = ".csv" }, collector, default);
    Check(hits.Count == 1 && hits[0].Line == 2, "CP932の日本語grep");
    var csv = TextFiles.ParseCsv("名前,値\n\"改行\n項目\",\"a,b\"\n");
    Check(csv.Count == 2 && csv[1][0].Contains('\n') && csv[1][1] == "a,b", "CSV引用符・セル内改行・カンマ");
    var sample = await TextFiles.ReadAsync(Path.Combine(searchRoot, "日本語.txt"), "UTF-8", 8, default);
    Check(sample.Truncated && !sample.Text.Contains('\uFFFD'), "プレビュー上限と途中UTF-8文字の処理");
    await File.WriteAllBytesAsync(Path.Combine(searchRoot, "binary.txt"), [65, 0, 66]);
    hits.Clear(); await new SearchService().SearchAsync(options, collector, default);
    Check(hits.Any(x => x.Error && x.Path.EndsWith("binary.txt")), "バイナリを警告して継続");
    using var searchCancel = new CancellationTokenSource(); searchCancel.Cancel();
    try { await new SearchService().SearchAsync(options, collector, searchCancel.Token); throw new Exception("未中断"); } catch (OperationCanceledException) { Check(true, "検索キャンセル"); }
    state.Recent = [new(Path.Combine(searchRoot, "日本語.txt"), false, DateTimeOffset.Now)]; await database.SaveAsync(state);
    Check((await database.LoadAsync()).Recent.Count == 1, "最近使った項目のSQLite復元");
    foreach (var extension in new[] { ".pdf", ".xlsx", ".csv", ".png", ".dwg", ".zip", ".unknown-filerapp" })
    {
        var pixels = FilerApp.Services.ShellFileIcon.ReadPixels(false, extension);
        Check(pixels is { Length: 4096 } && Enumerable.Range(0, 1024).Any(i => pixels[i * 4 + 3] != 0), "Shell関連付けアイコン取得 " + extension);
    }
    var folderPixels = FilerApp.Services.ShellFileIcon.ReadPixels(true, "");
    Check(folderPixels is { Length: 4096 }, "フォルダアイコン取得");
    foreach (var (directory, extension) in new[] { (true, ""), (false, ".csv"), (false, ".unknown-filerapp") })
    {
        var largePixels = FilerApp.Services.ShellFileIcon.ReadPixels(directory, extension, 48);
        Check(largePixels is { Length: 9216 } && Enumerable.Range(0, 2304).Any(i => largePixels[i * 4 + 3] != 0), "大アイコン取得 " + (directory ? "フォルダ" : extension));
    }
    var legacy = new SessionState { Favorites = [new() { Name = "旧テーマ", Items = [new("legacy-link", "実験", a)] }] };
    var roots = FavoriteTree.GetRoots(legacy);
    Check(roots.Single().Children.Single().Path == a, "旧グループから仮想フォルダへの移行");
    var folderA = roots[0]; var folderB = new FavoriteNode { Name = "B" }; var folderC = new FavoriteNode { Name = "C" }; roots.AddRange([folderB, folderC]);
    FavoriteTree.Move(roots, folderC.Id, null, folderA.Id);
    Check(roots[0] == folderC && roots[1] == folderA, "仮想フォルダを最上位で並べ替え");
    FavoriteTree.Move(roots, folderB.Id, folderA.Id);
    Check(folderA.Children.Contains(folderB) && !roots.Contains(folderB), "仮想フォルダを別フォルダに入れ子移動");
    FavoriteTree.Move(roots, "legacy-link", folderB.Id);
    Check(folderB.Children.Single().Id == "legacy-link", "登録先を下の階層へ移動");
    try { FavoriteTree.Move(roots, folderA.Id, folderB.Id); throw new Exception("循環未拒否"); } catch (InvalidOperationException) { Check(roots.Contains(folderA) && folderA.Children.Contains(folderB), "子孫への循環移動を変更前に拒否"); }
    Check(!FavoriteTree.CanMove(roots, folderA.Id, folderA.Id) && !FavoriteTree.CanMove(roots, folderC.Id, "legacy-link"), "自分自身・リンク内への移動を拒否");
    FavoriteTree.Move(roots, "legacy-link", null, folderA.Id);
    Check(roots[1].Id == "legacy-link" && folderB.Children.Count == 0, "階層から最上位への取り出し");
    var originalOrder = roots.Select(x => x.Id).ToArray();
    try { FavoriteTree.Move(roots, folderC.Id, null, "absent"); throw new Exception("不明な挿入先未拒否"); } catch (InvalidOperationException) { Check(roots.Select(x => x.Id).SequenceEqual(originalOrder), "不明な挿入先でデータを失わない"); }
    await database.SaveAsync(legacy); var treeState = await database.LoadAsync();
    Check(FavoriteTree.GetRoots(treeState).Single(x => x.Id == folderA.Id).Children.Single().Id == folderB.Id, "入れ子と順序をSQLiteに保存・復元");
    Check(File.Exists(entries[1].Path) && Directory.Exists(a), "階層整理後も実ファイル維持");
    var driveCrumbs = PathBreadcrumbs.Build(@"C:\Users\Example\Documents");
    Check(driveCrumbs.Count == 4 && driveCrumbs[0].Path == @"C:\" && driveCrumbs[3].Name == "Documents", "ドライブパスのパンくずと移動先");
    var uncCrumbs = PathBreadcrumbs.Build(@"\\server\share\実験\data");
    Check(uncCrumbs.Count == 3 && uncCrumbs[0].Path.TrimEnd('\\') == @"\\server\share" && uncCrumbs[2].Path == @"\\server\share\実験\data", "共有パスのパンくず（ネットワークアクセスなし）");
    Check(PathBreadcrumbs.Build(@"C:\").Count == 1 && PathBreadcrumbs.Build(@"C:\Temp\..\Users").Last().Path == @"C:\Users", "ルートと相対要素の正規化");
    legacy.RightSidebarWidth = 280; legacy.LeftSidebarRatio = 1.5; legacy.RightSidebarRatio = 0.5;
    await database.SaveAsync(legacy); var layoutRestored = await database.LoadAsync();
    Check(layoutRestored.RightSidebarWidth == 280 && layoutRestored.LeftSidebarRatio == 1.5 && layoutRestored.RightSidebarRatio == 0.5, "左右の幅・上下の境界状態を復元");
    legacy.LeftViewMode = FileViewMode.SmallIcons; legacy.RightViewMode = FileViewMode.LargeIcons;
    await database.SaveAsync(legacy); var viewRestored = await database.LoadAsync();
    Check(viewRestored.LeftViewMode == FileViewMode.SmallIcons && viewRestored.RightViewMode == FileViewMode.LargeIcons, "A/Bそれぞれの一覧表示形式をSQLiteに保存・復元");
    InteractionChecks.Run(Check);
    await SearchTabChecks.RunAsync(root, Check);
    await ColumnChecks.RunAsync(root, Check);
    await FoundationChecks.RunAsync(root, Check);
    await SharePointChecks.RunAsync(root, Check);
    Console.WriteLine($"{passed} checks passed; fixture: {root}");
}
finally
{
    SqliteConnection.ClearAllPools();
    // Only delete the unique temporary directory created by this process.
    if (Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) && Path.GetFileName(root).StartsWith("FilerApp-check-")) Directory.Delete(root, true);
}
sealed class DelayedFolders : IFolderService
{
    public TaskCompletionSource<IReadOnlyList<FileEntry>> Old { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<IReadOnlyList<FileEntry>> New { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int call;
    public Task<IReadOnlyList<FileEntry>> ReadAsync(string path, CancellationToken token) => Interlocked.Increment(ref call) == 1 ? Old.Task : New.Task;
}




sealed class HitCollector(List<SearchHit> hits) : IProgress<SearchHit>
{
    public void Report(SearchHit hit) => hits.Add(hit);
}

