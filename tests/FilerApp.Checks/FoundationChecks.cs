using FilerApp.Core;
using FilerApp.Services;
using Microsoft.Data.Sqlite;
using System.Security.AccessControl;
using System.Security.Principal;

static class FoundationChecks
{
    public static async Task RunAsync(string root, Action<bool, string> check)
    {
        var folder = Directory.CreateDirectory(Path.Combine(root, "foundation")).FullName;
        foreach (var name in new[] { "run10.csv", "run2.csv", "run1.csv", "run0002.csv", "notes.txt" }) await File.WriteAllTextAsync(Path.Combine(folder, name), "fixture");
        string hidden = Path.Combine(folder, "hidden.txt"); await File.WriteAllTextAsync(hidden, "hidden"); File.SetAttributes(hidden, FileAttributes.Hidden);
        string system = Path.Combine(folder, "system.txt"); await File.WriteAllTextAsync(system, "system"); File.SetAttributes(system, FileAttributes.System);
        var tab = new TabViewModel(new FolderService()); await tab.NavigateAsync(folder);
        check(tab.Entries.Select(x => x.Name).SequenceEqual(new[] { "notes.txt", "run1.csv", "run2.csv", "run0002.csv", "run10.csv" }), "自然順ソートと隠し・システム項目の既定非表示");
        var source = tab.Entries; bool emptied = false;
        tab.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(tab.Entries) && tab.Entries.Count == 0) emptied = true; };
        await tab.RefreshAsync(); check(ReferenceEquals(source, tab.Entries) && !emptied, "変更のない更新で一覧ソースを維持");
        var note = Path.Combine(folder, "notes.txt"); var originalAttributes = File.GetAttributes(note);
        File.SetAttributes(note, originalAttributes ^ FileAttributes.Archive); await tab.RefreshAsync();
        check(ReferenceEquals(source, tab.Entries), "表示に関係しない属性の通知だけでは一覧をリセットしない");
        File.SetAttributes(note, originalAttributes | FileAttributes.Hidden); await tab.RefreshAsync();
        check(!tab.Entries.Any(x => x.Path == note), "属性通知の集約後も隠し属性の変更は一覧へ反映");
        File.SetAttributes(note, originalAttributes); await tab.RefreshAsync(); source = tab.Entries;
        await File.WriteAllTextAsync(Path.Combine(folder, "new.txt"), "new"); await tab.RefreshAsync();
        check(!emptied && tab.Entries.Any(x => x.Name == "new.txt") && tab.Entries.Single(x => x.Name == "notes.txt") == source.Single(x => x.Name == "notes.txt"), "外部追加を反映し中間の空一覧を通知しない");
        await tab.SetVisibilityAsync(true, false); check(tab.Entries.Any(x => x.Name == "hidden.txt") && !tab.Entries.Any(x => x.Name == "system.txt"), "隠しとシステムを個別に表示");
        await tab.SetVisibilityAsync(true, true); check(tab.Entries.Any(x => x.Name == "system.txt"), "システム項目の表示切替");
        await tab.SetFilterAsync("run?.csv"); check(tab.Entries.Count == 2, "現在一覧のワイルドカード絞り込み");
        await tab.SetFilterAsync("RUN"); check(tab.Entries.Count == 4, "現在一覧の大小文字を区別しない部分一致");
        await tab.SortAsync("Name", true); check(tab.Entries.First().Name == "run10.csv", "自然順の降順");
        tab.Positions[folder] = new([Path.Combine(folder, "run2.csv")], Path.Combine(folder, "run2.csv"), -4, 200, 35);
        var dbPath = Path.Combine(folder, "settings.db"); var store = new StateStore(dbPath);
        await store.SaveAsync(new() { LeftTabs = [tab.Capture()] });
        var saved = (await store.LoadAsync()).LeftTabs!.Single();
        var restored = new TabViewModel(new FolderService()); await restored.RestoreAsync(saved);
        check(restored.Filter == "RUN" && restored.ShowHidden && restored.ShowSystem && restored.SortDescending && restored.Positions[folder].AnchorOffset == -4, "通常タブの条件と表示位置をSQLite経由で復元");
        var a = new StateStore(dbPath); var b = new StateStore(dbPath); var stateA = await a.LoadAsync(); var stateB = await b.LoadAsync();
        stateA.Left = ["A"]; stateB.Left = ["B"]; await a.SaveAsync(stateA);
        try { await b.SaveAsync(stateB); check(false, "設定競合未検出"); }
        catch (StateConflictException) { check((await new StateStore(dbPath).LoadAsync()).Left.Single() == "A", "別保存主体の古い設定による上書きを拒否"); }
        stateA.Left = ["C"]; await a.SaveAsync(stateA);
        using (var connection = new SqliteConnection($"Data Source={dbPath}")) { connection.Open(); using var cmd = connection.CreateCommand(); cmd.CommandText = "UPDATE state SET json='invalid' WHERE id=1"; cmd.ExecuteNonQuery(); }
        var recovery = new StateStore(dbPath); var recovered = await recovery.LoadAsync();
        check(recovery.RecoveredBackup && recovered.Left.Single() == "A", "壊れたJSONから直前の保存へ復旧");
        await recovery.SaveAsync(recovered); check((await new StateStore(dbPath).LoadAsync()).Left.Single() == "A", "復旧後の保存を継続");
        var legacyWriter = new StateStore(dbPath); var beforeLegacyWrite = await legacyWriter.LoadAsync();
        using (var connection = new SqliteConnection($"Data Source={dbPath}")) { connection.Open(); using var cmd = connection.CreateCommand(); cmd.CommandText = "UPDATE state SET json='{}' WHERE id=1"; cmd.ExecuteNonQuery(); }
        try { await legacyWriter.SaveAsync(beforeLegacyWrite); check(false, "旧版からの変更未検出"); }
        catch (StateConflictException) { check(true, "リビジョンを更新しない旧版からの設定変更も検出"); }
        var hits = new List<SearchHit>(); await new SearchService().SearchAsync(new(folder, "run?.csv", false, false, false, false, "", "", "UTF-8"), new Collector(hits), default);
        check(hits.Count == 2, "名前検索のワイルドカード");
        check(FileNames.Matches("日本語.csv", "*.CSV") && !FileNames.Matches("日本語.csv", "*.CSV", true), "日本語名と大小文字条件のワイルドカード");
        var originalTree = Directory.CreateDirectory(Path.Combine(folder, "tree", "child", "grandchild")).FullName;
        await File.WriteAllTextAsync(Path.Combine(originalTree, "nested.txt"), "nested-content");
        var tree = Path.Combine(folder, "tree"); var destination = Directory.CreateDirectory(Path.Combine(folder, "copies")).FullName;
        var operations = new FileOperationService { SuppressErrorDialogsForChecks = true };
        var copied = await operations.ExecuteAsync(FileAction.Copy, [tree], destination, 0, default);
        check(File.ReadAllText(Path.Combine(destination, "tree", "child", "grandchild", "nested.txt")) == "nested-content" && copied.All(x => x.Result == "成功"), "Shellで入れ子フォルダを実コピー");
        var moved = Directory.CreateDirectory(Path.Combine(folder, "moved")).FullName;
        var movedResults = await operations.ExecuteAsync(FileAction.Move, [Path.Combine(destination, "tree")], moved, 0, default);
        check(File.Exists(Path.Combine(moved, "tree", "child", "grandchild", "nested.txt")) && !Directory.Exists(Path.Combine(destination, "tree")) && movedResults.All(x => x.Result == "成功"), "Shellで入れ子フォルダを実移動");
        var self = await operations.ExecuteAsync(FileAction.Move, [tree], originalTree, 0, default);
        check(self.All(x => x.Result.StartsWith("失敗")) && File.Exists(Path.Combine(originalTree, "nested.txt")), "子孫への移動を拒否して元データを保持");
        string locked = Path.Combine(folder, "locked.txt"); await File.WriteAllTextAsync(locked, "locked-original");
        using (var exclusive = new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var result = await operations.ExecuteAsync(FileAction.Move, [locked], moved, 0, default);
            check(result.Any(x => x.Result != "成功") && File.Exists(locked) && !File.Exists(Path.Combine(moved, "locked.txt")), "使用中ファイルの実移動失敗で元データを維持");
        }
        string deniedFile = Path.Combine(folder, "permission-denied.txt"); await File.WriteAllTextAsync(deniedFile, "permission-original");
        var fileInfo = new FileInfo(deniedFile); var originalAcl = fileInfo.GetAccessControl().GetSecurityDescriptorBinaryForm();
        try
        {
            var deniedAcl = fileInfo.GetAccessControl();
            deniedAcl.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.ReadData, AccessControlType.Deny));
            fileInfo.SetAccessControl(deniedAcl);
            var result = await operations.ExecuteAsync(FileAction.Copy, [deniedFile], moved, 0, default);
            check(result.Any(x => x.Result != "成功") && !File.Exists(Path.Combine(moved, "permission-denied.txt")), "専用ファイルの実読み取り権限不足を失敗として報告");
        }
        finally { var restoreAcl = new FileSecurity(); restoreAcl.SetSecurityDescriptorBinaryForm(originalAcl, AccessControlSections.Access); fileInfo.SetAccessControl(restoreAcl); }
        check(File.ReadAllText(deniedFile) == "permission-original", "権限不足の試験後に元データと元ACLを復元");
        using var cancellation = new CancellationTokenSource();
        var progress = new Canceller(cancellation);
        var cancelled = await operations.ExecuteAsync(FileAction.Copy, [tree, locked], destination, 0, cancellation.Token, progress);
        check(cancelled.Any(x => x.Result != "成功") && File.Exists(locked) && File.Exists(Path.Combine(originalTree, "nested.txt")), "実Shell操作の進捗で中断し残りのコピー元を維持");
        string recycle = Path.Combine(folder, "FilerApp-recycle-check-" + Guid.NewGuid().ToString("N") + ".txt");
        await File.WriteAllTextAsync(recycle, "Only this generated fixture is recycled.");
        var recycleResults = await operations.ExecuteAsync(FileAction.Recycle, [recycle], null, 0, default);
        check(!File.Exists(recycle) && recycleResults.Any(x => x.Result == "成功" && x.Destination != null), "実ごみ箱への移動とShellの移動先通知を確認");
        var workspace = Path.GetFullPath(Environment.CurrentDirectory);
        if (File.Exists(Path.Combine(workspace, "FilerApp.sln")) && Path.GetPathRoot(workspace) != Path.GetPathRoot(root))
        {
            var across = Directory.CreateDirectory(Path.Combine(workspace, "artifacts", "cross-volume-check-" + Guid.NewGuid().ToString("N"))).FullName;
            try
            {
                var cross = await operations.ExecuteAsync(FileAction.Move, [locked], across, 0, default);
                check(!File.Exists(locked) && File.ReadAllText(Path.Combine(across, "locked.txt")) == "locked-original" && cross.All(x => x.Result == "成功"), "異なるドライブへのShell実移動で内容を保持");
            }
            finally { if (across.StartsWith(Path.Combine(workspace, "artifacts") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(across).StartsWith("cross-volume-check-")) Directory.Delete(across, true); }
        }
    }
    private sealed class Collector(List<SearchHit> hits) : IProgress<SearchHit> { public void Report(SearchHit hit) => hits.Add(hit); }
    private sealed class Canceller(CancellationTokenSource cancellation) : IProgress<string> { public void Report(string value) => cancellation.Cancel(); }
}
