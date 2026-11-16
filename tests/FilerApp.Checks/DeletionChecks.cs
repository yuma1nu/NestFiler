using FilerApp.Services;

internal static class DeletionChecks
{
    public static async Task RunAsync(string root, Action<bool, string> check)
    {
        var unc = await DeletionPlan.PrepareAsync([@"\\nest-filer-check.invalid\share\file.txt", @"\\?\UNC\nest-filer-check.invalid\share\folder"]);
        check(unc.Items.Count == 2 && unc.PermanentCount == 2, "UNCと拡張UNCはサーバーへ接続せず完全削除対象と判定");
        var mapped = DeletionPlan.Create([@"Z:\data\file.txt"], _ => DriveType.Network);
        check(mapped.PermanentCount == 1, "割り当てネットワークドライブも完全削除対象と判定");
        var mixed = DeletionPlan.Create([Path.Combine(root, "local.txt"), @"\\server\share\remote.txt"], _ => DriveType.Fixed);
        check(mixed.PermanentCount == 1 && !mixed.Items[0].Permanent, "検索等で混在したローカル／サーバーは別の削除方式を保持");
        foreach (var invalid in new[] { @"C:\", @"\\server\share\", @"C:\folder\..\", "relative.txt" })
        {
            bool rejected = false; try { DeletionPlan.Create([invalid], _ => DriveType.Fixed); } catch (Exception ex) when (ex is ArgumentException or IOException) { rejected = true; }
            check(rejected, "ドライブ・共有ルートと相対パスの削除を拒否");
        }
        bool unknown = false; try { DeletionPlan.Create([@"Z:\unavailable\file.txt"], _ => DriveType.Unknown); } catch (IOException) { unknown = true; }
        check(unknown, "種類を判断できないドライブを無断で完全削除へ切り替えない");
        var folder = Directory.CreateDirectory(Path.Combine(root, "deletion-fixture")).FullName;
        var first = Path.Combine(folder, "server-mode.txt"); await File.WriteAllTextAsync(first, "temporary fixture only");
        var service = new FileOperationService { SuppressErrorDialogsForChecks = true };
        var plan = DeletionPlan.Create([first, first], _ => DriveType.Network);
        check(plan.Items.Count == 1, "削除対象の重複パスを整理");
        var unconfirmed = await service.ExecuteDeletionAsync(plan, false, 0, default);
        check(File.Exists(first) && unconfirmed.All(x => !x.Succeeded), "完全削除の明示的確認がないバッチは実行しない");
        var direct = await service.ExecuteAsync(FileAction.DeletePermanently, [first], null, 0, default);
        check(File.Exists(first) && direct.All(x => !x.Succeeded), "通常サービス入口から確認を迂回した完全削除を拒否");
        var cancelled = await service.ExecuteDeletionAsync(plan, true, 0, new CancellationToken(true));
        check(File.Exists(first) && cancelled.All(x => x.Result.StartsWith("未実行")), "確認済みでもキャンセル時はファイルを保持");
        var permanent = await service.ExecuteDeletionAsync(plan, true, 0, default);
        check(!File.Exists(first) && permanent.Any(x => x.Result == "成功（完全削除）" && x.Succeeded), "専用一時ファイルのShell完全削除と結果明示");
        var local = Path.Combine(folder, "local.txt"); var remoteMode = Path.Combine(folder, "server.txt");
        await File.WriteAllTextAsync(local, "local fixture"); await File.WriteAllTextAsync(remoteMode, "server-mode fixture");
        int classification = 0;
        var combined = DeletionPlan.Create([local, remoteMode], _ => classification++ == 0 ? DriveType.Fixed : DriveType.Network);
        var combinedResult = await service.ExecuteDeletionAsync(combined, true, 0, default);
        check(!File.Exists(local) && !File.Exists(remoteMode) && combinedResult.Any(x => x.Result == "成功" && x.Destination != null) && combinedResult.Any(x => x.Result == "成功（完全削除）"), "ローカルは実ごみ箱／確認済み対象は完全削除を1バッチで実行");
        var success = Path.Combine(folder, "success.txt"); var locked = Path.Combine(folder, "locked.txt");
        await File.WriteAllTextAsync(success, "delete fixture"); await File.WriteAllTextAsync(locked, "preserve locked fixture");
        using (var hold = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var partial = await service.ExecuteDeletionAsync(DeletionPlan.Create([success, locked], _ => DriveType.Network), true, 0, default);
            check(!File.Exists(success) && File.Exists(locked) && partial.Any(x => x.Succeeded) && partial.Any(x => !x.Succeeded), "使用中ファイルの完全削除失敗を部分成功と区別・失敗元を保持");
        }
        var target = Path.Combine(folder, "keep.txt"); await File.WriteAllTextAsync(target, "keep");
        var subtree = Directory.CreateDirectory(Path.Combine(folder, "tree")).FullName;
        await File.WriteAllTextAsync(Path.Combine(subtree, "link.url"), "[InternetShortcut]\r\nURL=" + new Uri(target).AbsoluteUri);
        var deletedTree = await service.ExecuteDeletionAsync(DeletionPlan.Create([subtree], _ => DriveType.Network), true, 0, default);
        check(!Directory.Exists(subtree) && File.Exists(target) && deletedTree.Any(x => x.Succeeded), "フォルダを完全削除しても内包ショートカットのリンク先を変更しない");
        var missing = await service.ExecuteDeletionAsync(DeletionPlan.Create([Path.Combine(folder, "missing")], _ => DriveType.Network), true, 0, default);
        check(missing.All(x => !x.Succeeded), "到達不能・不存在の削除対象を成功扱いしない");
        using var token = new CancellationTokenSource(); token.Cancel();
        check(new OperationSink([], token.Token, null, true).PreDeleteItem(0, 0) < 0, "完全削除でもShellコールバックのキャンセルを優先");
    }
}
