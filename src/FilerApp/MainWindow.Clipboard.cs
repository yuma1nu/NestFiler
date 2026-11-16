using Windows.ApplicationModel.DataTransfer;
using FilerApp.Services;

namespace FilerApp;
public sealed partial class MainWindow
{
    private const string ClipboardMarker = "FilerApp.ClipboardToken";
    private string? clipboardToken;
    private string[] cutPaths = [];
    private int clipboardVersion;
#if DEBUG
    private Action<DataPackageOperation>? debugReportedCompletion;
#endif
    private void InitializeClipboard()
    {
        Clipboard.ContentChanged += ClipboardChanged;
        Closed += (_, _) => Clipboard.ContentChanged -= ClipboardChanged;
    }
    private async void ClipboardChanged(object? sender, object args)
    {
        int version = ++clipboardVersion;
        try
        {
            var marker = await ClipboardIdAsync(Clipboard.GetContent());
            if (version == clipboardVersion && marker != clipboardToken) ClearCutAppearance();
        }
        catch { if (version == clipboardVersion) ClearCutAppearance(); }
    }
    private static async Task<string?> ClipboardIdAsync(DataPackageView data) => data.Contains(ClipboardMarker) ? await data.GetDataAsync(ClipboardMarker) as string : null;
    private void ClearCutAppearance() { clipboardToken = null; cutPaths = []; LeftPane.SetCutPaths([]); RightPane.SetCutPaths([]); }
    private async Task SetFileClipboardAsync(string[] paths, bool move)
    {
        var snapshot = paths.ToArray();
        await Task.Run(() => { foreach (var path in snapshot) if (!File.Exists(path) && !Directory.Exists(path)) throw new IOException("コピー元が存在しないか、アクセスできません: " + path); });
        PublishFileClipboard(snapshot, move);
    }
    private void PublishFileClipboard(string[] paths, bool move)
    {
        var token = Guid.NewGuid().ToString("N");
        var package = new DataPackage();
        void Completed(DataPackageOperation operation) => DispatcherQueue.TryEnqueue(async () =>
        {
            // Storage-item targets perform the physical transfer. Never delete the source a second time.
            if (move && operation == DataPackageOperation.Move && clipboardToken == token)
            {
                try { if (await ClipboardIdAsync(Clipboard.GetContent()) == token) Clipboard.Clear(); }
                catch (Exception ex) { Services.AppLog.Error("移動完了後のクリップボード更新: " + ex.Message); }
                ClearCutAppearance(); LeftPane.RecheckFolder(); RightPane.RecheckFolder();
            }
        });
        FileTransferData.SetPaths(package, paths, move, Completed); package.SetData(ClipboardMarker, token);
        package.OperationCompleted += (_, args) => Completed(args.Operation);
        Clipboard.SetContent(package); clipboardToken = token; cutPaths = move ? paths.ToArray() : [];
        LeftPane.SetCutPaths(cutPaths); RightPane.SetCutPaths(cutPaths);
    }
    private async Task PasteFilesAsync(DataPackageView data, string folder)
    {
        var token = await ClipboardIdAsync(data);
        var paths = await FileTransferData.GetPathsAsync(data);
        bool move = data.RequestedOperation.HasFlag(DataPackageOperation.Move);
        var results = await RunOperationAsync(move ? FileAction.Move : FileAction.Copy, paths, folder);
        bool success = results.Count > 0 && results.All(x => x.Result == "成功") && paths.All(path => results.Any(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase) && x.Result == "成功"));
        var completedOperation = success ? move ? DataPackageOperation.Move : DataPackageOperation.Copy : DataPackageOperation.None;
        data.ReportOperationCompleted(completedOperation);
#if DEBUG
        debugReportedCompletion?.Invoke(completedOperation);
#endif
        if (!move || token == null || await ClipboardIdAsync(Clipboard.GetContent()) != token) return;
        if (success) { Clipboard.Clear(); ClearCutAppearance(); return; }
        // Retain failed/skipped top-level items, including folders with a failed child.
        var remaining = paths.Where(path => !results.Any(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase) && x.Result == "成功") || results.Any(x => x.Result != "成功" && (string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase) || x.Path.StartsWith(Path.TrimEndingDirectorySeparator(path) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))).ToArray();
        if (remaining.Length > 0)
        {
            PublishFileClipboard(remaining, true);
        }
    }
}
