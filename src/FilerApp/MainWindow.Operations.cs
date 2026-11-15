using FilerApp.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace FilerApp;
public sealed partial class MainWindow
{
    private bool operating;
    private CancellationTokenSource? operationCancellation;
    private List<OperationResult> operationResults = [];
    private void InitializeOperations()
    {
        InitializeClipboard();
        foreach (var pane in new[] { LeftPane, RightPane })
            pane.Dropped += async (paths, move, destination) => { SetActive(pane); return await RunOperationAsync(move ? FileAction.Move : FileAction.Copy, paths, destination); };
    }
    private async Task CommandAsync(string command)
    {
        try
        {
            if (command == "cancel") { operationCancellation?.Cancel(); return; }
            if (command == "results") { await new ContentDialog { XamlRoot = Root.XamlRoot, Title = "ファイル操作結果", Content = new ScrollViewer { MaxHeight = 450, Content = new TextBlock { Text = string.Join("\n\n", operationResults.Select(x => x.Path + "\n" + x.Result)), TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true } }, CloseButtonText = "閉じる" }.ShowAsync(); return; }
            if (operating) { Status.Text = "ファイル操作中です。"; return; }
            var pane = active; var paths = pane.SelectedPaths; var folder = pane.Current?.Path;
            if (folder == null) return;
            if (command == "locate") { await pane.OpenContainingFolderAsync(); return; }
            if (pane.Current?.IsSearch == true && command is "paste" or "mkdir") { Status.Text = "検索結果への貼り付け・新規作成はできません。格納フォルダを開いて操作してください。"; return; }
            if (command == "refresh") { await pane.RefreshAsync(); return; }
            if (command == "favorite") { await AddCurrentFavoriteAsync(); return; }
            if (command == "copyPath") { var text = new DataPackage(); text.SetText(string.Join(Environment.NewLine, paths.Length > 0 ? paths : new[] { folder })); Clipboard.SetContent(text); return; }
            if (command is "copy" or "cut")
            {
                if (paths.Length == 0) return;
                await SetFileClipboardAsync(paths, command == "cut"); Status.Text = $"{paths.Length}項目を{(command == "cut" ? "切り取り" : "コピー")}。貼り付け先を選んでください。";
            }
            else if (command == "paste")
            {
                await PasteIntoPaneAsync(pane, folder, Clipboard.GetContent());
            }
            else if (command is "copyOther" or "moveOther")
            {
                var target = (pane == LeftPane ? RightPane : LeftPane).Current;
                if (target?.IsSearch == true) { Status.Text = "反対ペインで格納先のフォルダを開いてください。"; return; }
                await RunOperationAsync(command == "moveOther" ? FileAction.Move : FileAction.Copy, paths, target?.Path);
            }
            else if (command == "recycle" && paths.Length > 0)
            {
                var confirm = new ContentDialog { XamlRoot = Root.XamlRoot, Title = $"{paths.Length}項目をごみ箱へ移動", Content = "ごみ箱を利用できない場合は削除を中止します。", PrimaryButtonText = "続ける", CloseButtonText = "キャンセル" };
                if (await confirm.ShowAsync() == ContentDialogResult.Primary) await RunOperationAsync(FileAction.Recycle, paths, null);
            }
            else if (command == "rename" && paths.Length == 1 && await AskAsync("新しい名前", Path.GetFileName(paths[0])) is { } name)
            { FileOperationService.ValidateName(name); await RunOperationAsync(FileAction.Rename, paths, Path.Combine(Path.GetDirectoryName(paths[0])!, name)); }
            else if (command == "mkdir" && await AskAsync("新規フォルダ", "新しいフォルダ") is { } directory)
            { await FileOperationService.CreateDirectoryAsync(folder, directory); await pane.RefreshAsync(); }
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }
    private async Task<List<OperationResult>> RunOperationAsync(FileAction action, string[] paths, string? destination)
    {
        if (operating || paths.Length == 0 || (destination == null && action != FileAction.Recycle)) return [];
        operating = true; operationCancellation = new(); Status.Text = "ファイル操作中 — 衝突の選択・進捗・キャンセルはWindows Shell画面で操作してください。";
        try
        {
            operationResults = await new FileOperationService().ExecuteAsync(action, paths, destination, WinRT.Interop.WindowNative.GetWindowHandle(this), operationCancellation.Token, new Progress<string>(message => Status.Text = message));
            await Task.WhenAll(LeftPane.RefreshAfterOperationAsync(), RightPane.RefreshAfterOperationAsync());
            Status.Text = $"操作終了: 成功 {operationResults.Count(x => x.Result == "成功")} 件 / その他 {operationResults.Count(x => x.Result != "成功")} 件。右クリック → ファイル操作の結果で明細を確認できます。";
            return operationResults;
        }
        catch (Exception ex) { ShowError(ex.Message); return paths.Select(path => new OperationResult(path, "失敗: " + ex.Message)).ToList(); }
        finally { operating = false; operationCancellation.Dispose(); operationCancellation = null; }
    }
}




