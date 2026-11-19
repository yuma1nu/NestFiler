#if DEBUG
using FilerApp.Services;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace FilerApp;
public sealed partial class MainWindow
{
    private async Task RunCopyDropChecksAsync()
    {
        var results = new List<string>();
        void Check(bool success, string message) { if (!success) throw new InvalidOperationException(message); results.Add("PASS " + message); }
        var root = Directory.CreateDirectory(Path.Combine(AppLog.DataDirectory, "drop-fixture")).FullName;
        var target = Directory.CreateDirectory(Path.Combine(root, "TARGET")).FullName;
        await File.WriteAllTextAsync(Path.Combine(root, "move-me.txt"), "move fixture");
        await File.WriteAllTextAsync(Path.Combine(root, "copy-me.txt"), "copy fixture");
        var movingFolder = Directory.CreateDirectory(Path.Combine(root, "move-folder")).FullName;
        await File.WriteAllTextAsync(Path.Combine(movingFolder, "nested.txt"), "nested fixture");
        await LeftPane.NavigateAsync(root); await RightPane.NavigateAsync(target); SetActive(LeftPane); LeftPane.FocusFiles();
        if (Environment.GetEnvironmentVariable("FILERAPP_COPY_DROP_HOLD") == "1") { Status.Text = "隔離したフォルダへのドロップ確認用"; return; }
        try
        {
            await LeftPane.DebugCheckFolderDropsAsync(root, results, RightPane);
            var sourceFolder = Directory.CreateDirectory(Path.Combine(AppLog.DataDirectory, "collision-source")).FullName;
            var destination = Directory.CreateDirectory(Path.Combine(AppLog.DataDirectory, "collision-target")).FullName;
            var source = Path.Combine(sourceFolder, "report.txt"); var existing = Path.Combine(destination, "report.txt");
            await File.WriteAllTextAsync(source, "new-source"); await File.WriteAllTextAsync(existing, "original-target");
            var (dialog, choice, all) = CreateCollisionDialog(new(source, existing, false, false));
            Check(choice.Items.Count == 3 && choice.SelectedIndex == 0 && dialog.DefaultButton == ContentDialogButton.Close && all.IsChecked != true, "collision dialog offers Keep both / Replace / Skip, defaults to Keep both and Cancel");
            var showing = dialog.ShowAsync(); await Task.Delay(80); dialog.Hide();
            Check(await showing == ContentDialogResult.None && await File.ReadAllTextAsync(existing) == "original-target", "dismissed collision dialog leaves existing file intact");
            int prompts = 0;
            debugCollisionResolver = (_, _) => { prompts++; return Task.FromResult(new CollisionResolution(CollisionChoice.KeepBoth)); };
            var data = new DataPackage(); FileTransferData.SetPaths(data, [source]);
            await PasteFilesAsync(data.GetView(), destination);
            Check(prompts == 1 && File.Exists(Path.Combine(destination, "report (1).txt")) && await File.ReadAllTextAsync(existing) == "original-target", "clipboard collision uses Keep both and retains existing destination");
            debugCollisionResolver = (_, _) => throw new InvalidOperationException("Same-folder copy should not prompt");
            data = new DataPackage(); FileTransferData.SetPaths(data, [existing]);
            await PasteFilesAsync(data.GetView(), destination);
            Check(await File.ReadAllTextAsync(Path.Combine(destination, "report (2).txt")) == "original-target" && await File.ReadAllTextAsync(existing) == "original-target", "same-folder clipboard paste automatically duplicates with next suffix and no prompt");
            DataPackageOperation? completion = null; debugReportedCompletion = operation => completion = operation;
            debugCollisionResolver = (_, _) => Task.FromResult(new CollisionResolution(CollisionChoice.Skip));
            data = new DataPackage(); FileTransferData.SetPaths(data, [source], true);
            await PasteFilesAsync(data.GetView(), destination);
            Check(completion == DataPackageOperation.None && File.Exists(source) && operationResults.All(x => x.Result == "スキップ"), "skipped cut-paste never reports a completed move or deletes source");
            debugCollisionResolver = (_, _) => Task.FromResult(new CollisionResolution(CollisionChoice.Cancel));
            var later = Path.Combine(sourceFolder, "later.txt"); await File.WriteAllTextAsync(later, "later fixture");
            data = new DataPackage(); FileTransferData.SetPaths(data, [source, later], true);
            await PasteFilesAsync(data.GetView(), destination);
            Check(completion == DataPackageOperation.None && File.Exists(source) && File.Exists(later) && !File.Exists(Path.Combine(destination, "later.txt")), "cancelled cut-paste retains all remaining source items and reports no move completion");
            debugCollisionResolver = (_, _) => Task.FromResult(new CollisionResolution(CollisionChoice.Replace));
            data = new DataPackage(); FileTransferData.SetPaths(data, [source]);
            await PasteFilesAsync(data.GetView(), destination);
            Check(await File.ReadAllTextAsync(existing) == "new-source" && operationResults.Any(x => x.Result == "成功"), "explicit clipboard Replace overwrites only the reviewed fixture");
        }
        catch (Exception ex) { results.Add("FAIL " + ex); }
        finally
        {
            debugCollisionResolver = null; debugReportedCompletion = null;
            await File.WriteAllLinesAsync(Path.Combine(AppLog.DataDirectory, "copy-drop-check.txt"), results);
            closing = true; Close();
        }
    }
}
#endif
