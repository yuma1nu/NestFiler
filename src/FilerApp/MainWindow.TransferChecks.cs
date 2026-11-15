#if DEBUG
using FilerApp.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace FilerApp;
public sealed partial class MainWindow
{
    private async Task RunTransferChecksAsync(string root, List<string> results)
    {
        var source = Directory.CreateDirectory(Path.Combine(root, "package-source")).FullName;
        var target = Directory.CreateDirectory(Path.Combine(root, "package-target")).FullName;
        var file = Path.Combine(source, "move.txt"); await File.WriteAllTextAsync(file, "move-content");
        var package = new DataPackage { RequestedOperation = DataPackageOperation.Move };
        package.SetStorageItems(new[] { await StorageFile.GetFileFromPathAsync(file) });
        DataPackageOperation? completed = null; debugReportedCompletion = operation => completed = operation;
        // Use a DataPackage directly. The user's real Clipboard is never replaced.
        await PasteFilesAsync(package.GetView(), target); await Task.Delay(50);
        if (completed != DataPackageOperation.Move || File.Exists(file) || await File.ReadAllTextAsync(Path.Combine(target, "move.txt")) != "move-content") throw new InvalidOperationException("Move completion handshake failed");
        results.Add("PASS successful storage-item move reports Move after the physical transfer");
        var good = Path.Combine(source, "good.txt"); var missing = Path.Combine(source, "missing.txt");
        await File.WriteAllTextAsync(good, "good"); await File.WriteAllTextAsync(missing, "will-disappear");
        var partial = new DataPackage { RequestedOperation = DataPackageOperation.Move };
        partial.SetStorageItems(new[] { await StorageFile.GetFileFromPathAsync(good), await StorageFile.GetFileFromPathAsync(missing) });
        completed = null;
        File.Delete(missing);
        await PasteFilesAsync(partial.GetView(), target); await Task.Delay(50);
        if (completed != DataPackageOperation.None || !File.Exists(Path.Combine(target, "good.txt")) || !operationResults.Any(x => x.Result.StartsWith("失敗")) || !operationResults.Any(x => x.Result == "成功")) throw new InvalidOperationException("Partial move incorrectly completed the entire package");
        results.Add("PASS partially successful move reports None for the whole package and retains per-item failures");
        debugReportedCompletion = null;
        var search = LeftPane.OpenSearchTab("good", new Core.SearchOptions(target, "good", false, false, false, false, "", "", "UTF-8")); await search.SearchWork!;
        LeftPane.DebugSelectFile(Path.Combine(target, "good.txt"));
        await LeftPane.OpenContainingFolderAsync();
        if (LeftPane.Current?.IsSearch == true || !LeftPane.SelectedPaths.Contains(Path.Combine(target, "good.txt"))) throw new InvalidOperationException("Containing folder did not select the search result");
        results.Add("PASS containing folder navigation selects the originating search result");
    }
}
#endif
