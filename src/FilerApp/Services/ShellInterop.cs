using System.Runtime.InteropServices;
namespace FilerApp.Services;

[ComImport, Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellOperation
{
    void Advise(IProgressSink sink, out uint cookie);
    void Unadvise(uint cookie);
    void SetOperationFlags(uint flags);
    void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string message);
    void SetProgressDialog(nint dialog);
    void SetProperties(nint properties);
    void SetOwnerWindow(nint window);
    void ApplyPropertiesToItem(nint item);
    void ApplyPropertiesToItems(nint items);
    void RenameItem(nint item, [MarshalAs(UnmanagedType.LPWStr)] string name, nint sink);
    void RenameItems(nint items, [MarshalAs(UnmanagedType.LPWStr)] string name);
    void MoveItem(nint item, nint destination, [MarshalAs(UnmanagedType.LPWStr)] string? name, nint sink);
    void MoveItems(nint items, nint destination);
    void CopyItem(nint item, nint destination, [MarshalAs(UnmanagedType.LPWStr)] string? name, nint sink);
    void CopyItems(nint items, nint destination);
    void DeleteItem(nint item, nint sink);
    void DeleteItems(nint items);
    void NewItem(nint destination, uint attributes, [MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] string? template, nint sink);
    [PreserveSig] int PerformOperations();
    void GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool aborted);
}

[ComVisible(true), Guid("04b0f1a7-9490-44bc-96e1-4296a31252e2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IProgressSink
{
    [PreserveSig] int StartOperations();
    [PreserveSig] int FinishOperations(int result);
    [PreserveSig] int PreRenameItem(uint flags, nint item, [MarshalAs(UnmanagedType.LPWStr)] string name);
    [PreserveSig] int PostRenameItem(uint flags, nint item, [MarshalAs(UnmanagedType.LPWStr)] string name, int result, nint created);
    [PreserveSig] int PreMoveItem(uint flags, nint item, nint destination, [MarshalAs(UnmanagedType.LPWStr)] string? name);
    [PreserveSig] int PostMoveItem(uint flags, nint item, nint destination, [MarshalAs(UnmanagedType.LPWStr)] string? name, int result, nint created);
    [PreserveSig] int PreCopyItem(uint flags, nint item, nint destination, [MarshalAs(UnmanagedType.LPWStr)] string? name);
    [PreserveSig] int PostCopyItem(uint flags, nint item, nint destination, [MarshalAs(UnmanagedType.LPWStr)] string? name, int result, nint created);
    [PreserveSig] int PreDeleteItem(uint flags, nint item);
    [PreserveSig] int PostDeleteItem(uint flags, nint item, int result, nint created);
    [PreserveSig] int PreNewItem(uint flags, nint destination, [MarshalAs(UnmanagedType.LPWStr)] string name);
    [PreserveSig] int PostNewItem(uint flags, nint destination, [MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] string? template, uint attributes, int result, nint created);
    [PreserveSig] int UpdateProgress(uint total, uint completed);
    [PreserveSig] int ResetTimer();
    [PreserveSig] int PauseTimer();
    [PreserveSig] int ResumeTimer();
}

internal static class ShellInterop
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(string path, nint context, ref Guid iid, out nint item);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHGetNameFromIDList(nint id, uint kind, out nint name);
    [DllImport("shell32.dll", PreserveSig = false)] private static extern void SHGetIDListFromObject(nint item, out nint id);
    public static nint Item(string path) { var id = new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"); SHCreateItemFromParsingName(path, 0, ref id, out var item); return item; }
    public static string Name(nint item)
    {
        nint id = 0, name = 0;
        try { SHGetIDListFromObject(item, out id); SHGetNameFromIDList(id, 0x80058000, out name); return Marshal.PtrToStringUni(name) ?? "不明"; }
        catch { return "パス取得不可"; }
        finally { if (name != 0) Marshal.FreeCoTaskMem(name); if (id != 0) Marshal.FreeCoTaskMem(id); }
    }
}

[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public sealed class OperationSink(List<OperationResult> results, CancellationToken token, IProgress<string>? progress, bool allowPermanentDelete = false) : IProgressSink
{
    private int Check => token.IsCancellationRequested ? unchecked((int)0x800704C7) : 0;
    public static string Describe(int hr) => hr switch
    {
        0 => "成功",
        0x00270005 => "スキップ",
        unchecked((int)0x80270000) or unchecked((int)0x80270001) or unchecked((int)0x800704C7) => "中断",
        < 0 => $"失敗: {Marshal.GetExceptionForHR(hr)?.Message} (0x{hr:X8})",
        _ => $"完了未確定: Shell 0x{hr:X8}"
    };
    private int Record(nint item, int result, nint created = 0)
    {
        // COPYENGINE_S_PENDING is an intermediate collision/error notification.
        // A final callback or PerformOperations/aborted result follows it.
        if (result == 0x0027000B) return 0;
        if (result == 0x00270008 && created != 0) result = 0;
        results.Add(new(ShellInterop.Name(item), Describe(result), created != 0 ? ShellInterop.Name(created) : null)); return 0;
    }
    public int StartOperations() => Check;
    public int FinishOperations(int result) => 0;
    public int PreRenameItem(uint f, nint i, string n) => Check;
    public int PostRenameItem(uint f, nint i, string n, int r, nint c) => Record(i, r, c);
    public int PreMoveItem(uint f, nint i, nint d, string? n) => Check;
    public int PostMoveItem(uint f, nint i, nint d, string? n, int r, nint c) => Record(i, r, c);
    public int PreCopyItem(uint f, nint i, nint d, string? n) => Check;
    public int PostCopyItem(uint f, nint i, nint d, string? n, int r, nint c) => Record(i, r, c);
    public int PreDeleteItem(uint f, nint i)
    {
        if (Check != 0) return Check;
        if (allowPermanentDelete || (f & 0x80) != 0) return 0;
        results.Add(new(ShellInterop.Name(i), "拒否: ごみ箱を使用できないため削除を中止"));
        return unchecked((int)0x800704C7);
    }
    public int PostDeleteItem(uint f, nint i, int r, nint c)
    {
        if (r == 0x0027000B) return 0;
        if (allowPermanentDelete && r is 0 or 0x00270008) { results.Add(new(ShellInterop.Name(i), "成功（完全削除）")); return 0; }
        if (r >= 0 && r != 0x00270005 && c == 0) { results.Add(new(ShellInterop.Name(i), "完了未確定: ごみ箱への移動先をShellから確認できません")); return 0; }
        return Record(i, r, c);
    }
    public int PreNewItem(uint f, nint d, string n) => Check;
    public int PostNewItem(uint f, nint d, string n, string? t, uint a, int r, nint c) => Record(d, r);
    public int UpdateProgress(uint total, uint completed) { progress?.Report($"処理中: {completed:N0} / {total:N0}"); return Check; }
    public int ResetTimer() => 0;
    public int PauseTimer() => 0;
    public int ResumeTimer() => 0;
}

