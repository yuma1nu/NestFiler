using FilerApp.Services;

internal static class CollisionChecks
{
    internal static async Task RunAsync(string root, Action<bool, string> check)
    {
        var folder = Directory.CreateDirectory(Path.Combine(root, "collision-check")).FullName;
        var source = Directory.CreateDirectory(Path.Combine(folder, "source")).FullName;
        var target = Directory.CreateDirectory(Path.Combine(folder, "target")).FullName;
        var service = new FileOperationService { SuppressErrorDialogsForChecks = true };
        Task<List<OperationResult>> Transfer(FileAction action, string[] paths, string destination, CollisionChoice choice = CollisionChoice.KeepBoth) =>
            service.ExecuteAsync(action, paths, destination, 0, default, resolveCollision: (_, _) => Task.FromResult(new CollisionResolution(choice)));
        var file = Path.Combine(source, "報告.txt"); await File.WriteAllTextAsync(file, "source");
        int prompts = 0;
        var duplicated = await service.ExecuteAsync(FileAction.Copy, [file], source, 0, default, resolveCollision: (_, _) => { prompts++; return Task.FromResult(new CollisionResolution(CollisionChoice.Cancel)); });
        check(prompts == 0 && duplicated.All(x => x.Succeeded) && await File.ReadAllTextAsync(file) == "source" && await File.ReadAllTextAsync(Path.Combine(source, "報告 (1).txt")) == "source", "同じフォルダへのコピーは確認せず(1)を付けて実複製・元を保持");
        await Transfer(FileAction.Copy, [file], source);
        check(await File.ReadAllTextAsync(Path.Combine(source, "報告 (2).txt")) == "source", "同じクリップボードから再コピーしても(2)で衝突を回避");
        var dotfile = Path.Combine(source, ".gitignore"); await File.WriteAllTextAsync(dotfile, "bin");
        await Transfer(FileAction.Copy, [dotfile], source);
        check(File.Exists(Path.Combine(source, ".gitignore (1)")), "ドットファイルの複製でも名前が空にならない");
        var shortcut = Path.Combine(source, "資料.pptx.url"); await File.WriteAllTextAsync(shortcut, "[InternetShortcut]\r\nURL=ms-powerpoint:ofe|u|https://tenant.sharepoint.com/a.pptx");
        await Transfer(FileAction.Copy, [shortcut], source);
        check(File.Exists(Path.Combine(source, "資料 (1).pptx.url")), "Officeショートカットは二重拡張子を保って両方残す");
        var tree = Directory.CreateDirectory(Path.Combine(source, "data.v1")).FullName; await File.WriteAllTextAsync(Path.Combine(tree, "child.txt"), "child");
        var duplicatedTree = await Transfer(FileAction.Copy, [tree], source);
        check(duplicatedTree.Any(x => x.Succeeded) && File.Exists(Path.Combine(source, "data.v1 (1)", "child.txt")) && File.Exists(Path.Combine(tree, "child.txt")), "同一フォルダへのフォルダ複製は拡張子扱いせず内容も保持");
        var existing = Path.Combine(target, "報告.txt"); await File.WriteAllTextAsync(existing, "original-target");
        var kept = await Transfer(FileAction.Copy, [file], target);
        check(await File.ReadAllTextAsync(existing) == "original-target" && await File.ReadAllTextAsync(Path.Combine(target, "報告 (1).txt")) == "source" && kept.Any(x => x.Destination == Path.Combine(target, "報告 (1).txt")), "別フォルダの衝突で両方残す・実際の新しい配置先を結果に記録");
        var mover = Path.Combine(source, "move.txt"); await File.WriteAllTextAsync(mover, "move-source"); await File.WriteAllTextAsync(Path.Combine(target, "move.txt"), "move-target");
        var moved = await Transfer(FileAction.Move, [mover], target);
        check(!File.Exists(mover) && await File.ReadAllTextAsync(Path.Combine(target, "move.txt")) == "move-target" && await File.ReadAllTextAsync(Path.Combine(target, "move (1).txt")) == "move-source" && moved.Any(x => x.Succeeded), "移動で両方残す場合も既存の配置先を保持して新しい名前へ移す");
        var skipped = await Transfer(FileAction.Move, [file], target, CollisionChoice.Skip);
        check(skipped.All(x => x.Result == "スキップ") && File.Exists(file) && await File.ReadAllTextAsync(existing) == "original-target", "スキップでは移動元も配置先も変更しない");
        var later = Path.Combine(source, "later.txt"); await File.WriteAllTextAsync(later, "later");
        var cancelled = await Transfer(FileAction.Move, [file, later], target, CollisionChoice.Cancel);
        check(File.Exists(file) && File.Exists(later) && !File.Exists(Path.Combine(target, "later.txt")) && cancelled.Any(x => x.Result.StartsWith("中断")) && cancelled.Any(x => x.Result.StartsWith("未実行")), "衝突確認のキャンセルで後続も停止して元データを保持");
        var replaced = await Transfer(FileAction.Copy, [file], target, CollisionChoice.Replace);
        check(replaced.Any(x => x.Succeeded) && await File.ReadAllTextAsync(existing) == "source" && File.Exists(file), "明示的に置き換えを選んだファイルだけ実上書き");
        var targetTree = Directory.CreateDirectory(Path.Combine(target, "data.v1")).FullName; await File.WriteAllTextAsync(Path.Combine(targetTree, "old.txt"), "old");
        await Transfer(FileAction.Copy, [tree], target);
        check(File.Exists(Path.Combine(target, "data.v1 (1)", "child.txt")) && File.Exists(Path.Combine(targetTree, "old.txt")) && !File.Exists(Path.Combine(targetTree, "child.txt")), "同名フォルダの両方残すでは既存フォルダへ統合しない");
        foreach (var name in new[] { "all-a.txt", "all-b.txt" }) { await File.WriteAllTextAsync(Path.Combine(source, name), "new"); await File.WriteAllTextAsync(Path.Combine(target, name), "old"); }
        prompts = 0;
        await service.ExecuteAsync(FileAction.Copy, [Path.Combine(source, "all-a.txt"), Path.Combine(source, "all-b.txt")], target, 0, default, resolveCollision: (_, _) => { prompts++; return Task.FromResult(new CollisionResolution(CollisionChoice.KeepBoth, true)); });
        check(prompts == 1 && File.Exists(Path.Combine(target, "all-a (1).txt")) && File.Exists(Path.Combine(target, "all-b (1).txt")), "以降にも適用は同じバッチ内の衝突にだけ選択を再利用");
        prompts = 0;
        await service.ExecuteAsync(FileAction.Copy, [file], target, 0, default, resolveCollision: (_, _) => { prompts++; return Task.FromResult(new CollisionResolution(CollisionChoice.Skip)); });
        check(prompts == 1, "次の操作には以前の衝突選択を持ち越さない");
        var before = Directory.GetFileSystemEntries(source).Length;
        var noMove = await Transfer(FileAction.Move, [file], source);
        check(File.Exists(file) && noMove.All(x => x.Result.StartsWith("未実行")) && Directory.GetFileSystemEntries(source).Length == before, "同じフォルダへの切り取り貼り付けは複製せず元を保持");
        var race = Path.Combine(source, "race.txt"); await File.WriteAllTextAsync(race, "race-content");
        var parallel = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Transfer(FileAction.Copy, [race], source)));
        var destinations = parallel.SelectMany(x => x).Where(x => x.Succeeded && x.Destination != null).Select(x => x.Destination!).ToArray();
        check(destinations.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 3 && destinations.All(File.Exists) && destinations.All(x => File.ReadAllText(x) == "race-content") && File.Exists(race), "同時複製で候補名が競合してもShellが既存を上書きせず3件を保持");
        var invalid = await Transfer(FileAction.Copy, [tree], Path.Combine(tree, "child-folder"));
        check(invalid.All(x => !x.Succeeded) && File.Exists(Path.Combine(tree, "child.txt")), "コピー失敗でフォルダの元データを保持");
    }
}
