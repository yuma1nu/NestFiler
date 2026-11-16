using System.Runtime.InteropServices;

namespace FilerApp.Services;

public enum FileAction : uint { Move = 1, Copy = 2, Recycle = 3, Rename = 4, DeletePermanently = 5 }
public sealed record OperationResult(string Path, string Result, string? Destination = null)
{
    public bool Succeeded => Result is "成功" or "成功（完全削除）";
}

// Shell owns collision prompts and the cancellable progress window. Never suppress
// copy/move confirmations. Recycle-only mode rejects deletion without recycle
// flags; permanent deletion requires an explicitly approved deletion plan.
public sealed class FileOperationService
{
    // Dedicated fixture checks only: errors are returned without opening a modal
    // error window. Collision/overwrite confirmation is never suppressed.
    internal bool SuppressErrorDialogsForChecks { get; init; }
    public Task<List<OperationResult>> ExecuteAsync(FileAction action, string[] sources, string? destination, nint owner, CancellationToken token, IProgress<string>? progress = null)
        => ExecuteBatchAsync(sources.Select(source => (Action: action, Source: source, Destination: destination)).ToArray(), false, owner, token, progress);

    public Task<List<OperationResult>> ExecuteDeletionAsync(DeletionPlan plan, bool permanentDeletionConfirmed, nint owner, CancellationToken token, IProgress<string>? progress = null)
        => ExecuteBatchAsync(plan.Items.Select(item => (Action: item.Permanent ? FileAction.DeletePermanently : FileAction.Recycle, Source: item.Path, Destination: (string?)null)).ToArray(), permanentDeletionConfirmed, owner, token, progress);

    private Task<List<OperationResult>> ExecuteBatchAsync((FileAction Action, string Source, string? Destination)[] batch, bool permanentDeletionConfirmed, nint owner, CancellationToken token, IProgress<string>? progress)
    {
        if (!permanentDeletionConfirmed && batch.Any(x => x.Action == FileAction.DeletePermanently))
            return Task.FromResult(batch.Select(x => new OperationResult(x.Source, "未実行: 完全削除が確認されていません")).ToList());
        var completion = new TaskCompletionSource<List<OperationResult>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = new Thread(() =>
        {
            var results = new List<OperationResult>();
            try
            {
                foreach (var (action, source, destination) in batch)
                {
                    if (token.IsCancellationRequested) { results.Add(new(source, "未実行（中断）")); continue; }
                    try
                    {
                        var full = Path.GetFullPath(source);
                        if (!File.Exists(full) && !Directory.Exists(full)) throw new IOException("存在しないかアクセスできません。");
                        if (action is FileAction.Copy or FileAction.Move)
                        {
                            var folder = Path.GetFullPath(destination!);
                            if (!Directory.Exists(folder)) throw new IOException("転送先フォルダにアクセスできません。");
                            if (Directory.Exists(full) && (string.Equals(Path.TrimEndingDirectorySeparator(folder), Path.TrimEndingDirectorySeparator(full), StringComparison.OrdinalIgnoreCase) || folder.StartsWith(Path.TrimEndingDirectorySeparator(full) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                                throw new IOException("フォルダ自身やその中には転送できません。");
                        }
                        if (action == FileAction.Recycle && (full.StartsWith(@"\\") || new DriveInfo(Path.GetPathRoot(full)!).DriveType != DriveType.Fixed))
                            throw new IOException("共有・リムーバブルドライブからのごみ箱削除は対応していません。");
                        IShellOperation? operation = null; nint item = 0, target = 0; uint cookie = 0;
                        try
                        {
                            operation = (IShellOperation)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("3ad05575-8857-4850-9277-11b85bdb8e09"))!)!;
                            operation.SetOwnerWindow(owner);
                            // Only an explicitly approved permanent-delete batch
                            // omits recycling. Its application dialog is the confirmation.
                            bool permanent = action == FileAction.DeletePermanently;
                            uint flags = permanent ? 0x0010u : 0x40u | 0x4000u | 0x00080000u;
                            operation.SetOperationFlags(flags | (SuppressErrorDialogsForChecks ? 0x0400u | 0x0004u : 0));
                            var sink = new OperationSink(results, token, progress, permanent); operation.Advise(sink, out cookie);
                            item = ShellInterop.Item(full);
                            if (action is FileAction.Copy or FileAction.Move) target = ShellInterop.Item(Path.GetFullPath(destination!));
                            switch (action)
                            {
                                case FileAction.Copy: operation.CopyItem(item, target, null, 0); break;
                                case FileAction.Move: operation.MoveItem(item, target, null, 0); break;
                                case FileAction.Rename: operation.RenameItem(item, Path.GetFileName(destination!), 0); break;
                                case FileAction.Recycle: operation.DeleteItem(item, 0); break;
                                case FileAction.DeletePermanently: operation.DeleteItem(item, 0); break;
                            }
                            var before = results.Count;
                            var code = operation.PerformOperations(); operation.GetAnyOperationsAborted(out var aborted);
                            if (code < 0 || aborted || results.Count == before)
                                results.Add(new(full, aborted ? "中断（一部処理済みの場合は明細参照）" : code < 0 ? OperationSink.Describe(code) : "完了未確定: Shellから結果通知なし"));
                            if (aborted) token = new CancellationToken(true);
                            GC.KeepAlive(sink);
                        }
                        finally
                        {
                            if (operation != null) { if (cookie != 0) operation.Unadvise(cookie); Marshal.FinalReleaseComObject(operation); }
                            if (item != 0) Marshal.Release(item); if (target != 0) Marshal.Release(target);
                        }                    }
                    catch (Exception ex) { results.Add(new(source, "失敗: " + ex.Message)); }
                }
                completion.SetResult(results);
            }
            catch (Exception ex) { completion.SetException(ex); }
        });
        worker.SetApartmentState(ApartmentState.STA); worker.IsBackground = true; worker.Start();
        return completion.Task;
    }
    public static Task CreateDirectoryAsync(string parent, string name) => Task.Run(() =>
    {
        ValidateName(name); var target = Path.Combine(parent, name);
        if (!CreateDirectoryW(target, 0)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    });
    public static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith('.') || name.EndsWith(' ')) throw new ArgumentException("有効な名前を入力してください。");
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CreateDirectoryW(string path, nint security);
}


