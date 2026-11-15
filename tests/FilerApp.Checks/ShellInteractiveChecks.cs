using FilerApp.Services;

static class ShellInteractiveChecks
{
    // Opt-in only. Operates exclusively in a unique directory under artifacts.
    public static async Task RunAsync()
    {
        Console.OutputEncoding = new System.Text.UTF8Encoding(false);
        var workspace = Path.GetFullPath(Environment.CurrentDirectory);
        if (!File.Exists(Path.Combine(workspace, "FilerApp.sln"))) throw new InvalidOperationException("Run from the solution directory.");
        var root = Directory.CreateDirectory(Path.Combine(workspace, "artifacts", "shell-ui-check-" + Guid.NewGuid().ToString("N"))).FullName;
        Console.WriteLine("Fixture: " + root);
        foreach (var choice in new[] { "skip", "overwrite", "cancel" })
        {
            var sourceFolder = Directory.CreateDirectory(Path.Combine(root, choice, "source")).FullName;
            var target = Directory.CreateDirectory(Path.Combine(root, choice, "target")).FullName;
            var file = Path.Combine(sourceFolder, choice + ".txt");
            var existing = Path.Combine(target, choice + ".txt");
            await File.WriteAllTextAsync(file, "new-content"); await File.WriteAllTextAsync(existing, "old-content");
            Console.WriteLine("WAIT " + choice); Console.Out.Flush();
            var results = await new FileOperationService().ExecuteAsync(FileAction.Copy, [file], target, 0, default);
            var actual = await File.ReadAllTextAsync(existing);
            bool success = choice == "overwrite" ? actual == "new-content" && results.Count > 0 && results.All(x => x.Result == "成功") : actual == "old-content" && results.Any(x => x.Result != "成功");
            if (!success || await File.ReadAllTextAsync(file) != "new-content") throw new Exception("FAIL " + choice + ": " + string.Join("; ", results));
            Console.WriteLine("PASS native collision " + choice + " — " + string.Join("; ", results.Select(x => x.Result))); Console.Out.Flush();
        }
        Console.WriteLine("PASS all native collision choices preserved the expected source/target content");
    }
}
