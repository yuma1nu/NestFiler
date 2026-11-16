#if DEBUG
using FilerApp.Services;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace FilerApp;
public sealed partial class MainWindow
{
    private async Task RunDeletionChecksAsync()
    {
        var results = new List<string>();
        void Check(bool success, string message) { if (!success) throw new InvalidOperationException(message); results.Add("PASS " + message); }
        var root = AppLog.DataDirectory;
        try
        {
            var folder = Directory.CreateDirectory(Path.Combine(root, "delete-fixture")).FullName;
            var file = Path.Combine(folder, "server-mode.txt"); await File.WriteAllTextAsync(file, "fixture only");
            var network = DeletionPlan.Create([file], _ => DriveType.Network);
            var dialog = CreateDeletionDialog(network);
            var explanation = ((StackPanel)dialog.Content).Children.OfType<TextBlock>().First().Text;
            Check(dialog.DefaultButton == ContentDialogButton.Close && dialog.PrimaryButtonText == "完全削除" && explanation.Contains("ごみ箱には入りません"), "network confirmation explicitly says permanent deletion and defaults to Cancel");
            var showing = dialog.ShowAsync(); await Task.Delay(80); dialog.Hide();
            Check(await showing == ContentDialogResult.None && File.Exists(file), "displayed network deletion dialog can be dismissed without deleting anything");
            var local = CreateDeletionDialog(DeletionPlan.Create([file], _ => DriveType.Fixed));
            Check(local.PrimaryButtonText == "ごみ箱へ移動" && local.DefaultButton == ContentDialogButton.Close, "local confirmation keeps recycling and Cancel default");
            var mixed = CreateDeletionDialog(DeletionPlan.Create([file, @"\\server\share\sample.txt"], _ => DriveType.Fixed));
            var mixedRows = (ListView)((StackPanel)mixed.Content).Children.Last();
            Check(mixedRows.Items.Cast<string>().Any(x => x.StartsWith("完全削除:")) && mixedRows.Items.Cast<string>().Any(x => x.StartsWith("ごみ箱:")), "mixed selection displays the deletion mode of each exact path");
            await LeftPane.NavigateAsync(folder); SetActive(LeftPane); LeftPane.DebugSelectFile(file); LeftPane.FocusFiles();
            Check(ListingCommand(VirtualKey.Delete, false, false) == "recycle", "Delete key still routes through the common confirmation command");
            debugDeletionPlan = captured => DeletionPlan.Create(captured.Items.Select(x => x.Path), _ => DriveType.Network);
            debugDeletionConfirmation = _ => Task.FromResult(false);
            await CommandAsync("recycle");
            Check(File.Exists(file) && !operating && !deletePromptOpen, "declined permanent deletion preserves selection source and releases operation guards");
            debugDeletionConfirmation = _ => Task.FromResult(true);
            await CommandAsync("recycle");
            Check(!File.Exists(file) && operationResults.Any(x => x.Result == "成功（完全削除）") && Status.Text.Contains("成功 1 件"), "confirmed Delete command permanently removes only its fixture and reports success");
            Check(!LeftPane.Current!.Entries.Any(x => x.Path == file) && !operating && !deletePromptOpen, "completed deletion refreshes the listing and leaves the app operable");
        }
        catch (Exception ex) { results.Add("FAIL " + ex); }
        finally
        {
            debugDeletionPlan = null; debugDeletionConfirmation = null;
            await File.WriteAllLinesAsync(Path.Combine(root, "deletion-check.txt"), results);
            closing = true; Close();
        }
    }
}
#endif
