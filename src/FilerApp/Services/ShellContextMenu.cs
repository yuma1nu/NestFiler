using System.Runtime.InteropServices;
namespace FilerApp.Services;

// Hosts the classic Windows Shell menu. This is not a reimplementation of its verbs.
internal static class ShellContextMenu
{
    private static readonly Guid FolderId = new("000214e6-0000-0000-c000-000000000046");
    private static readonly Guid MenuId = new("000214e4-0000-0000-c000-000000000046");
    public static bool Show(nint owner, string folderPath, string[] paths, bool extended)
    {
        using var session = Create(owner, folderPath, paths);
        var menu = CreatePopupMenu(); if (menu == 0) throw new InvalidOperationException("Windowsメニューを作成できません。");
        SubclassProc? callback = null; bool hooked = false;
        try
        {
            Marshal.ThrowExceptionForHR(session.Menu.QueryContextMenu(menu, 0, 1, 0x7fff, extended ? 0x100u : 0));
            var menu3 = session.Menu as IMenu3; var menu2 = session.Menu as IMenu2;
            callback = (hwnd, message, wp, lp, id, data) =>
            {
                try
                {
                    if (message is 0x117 or 0x2b or 0x2c or 0x120)
                    {
                        if (menu3 != null && menu3.HandleMenuMsg2(message, wp, lp, out var result) == 0) return result;
                        if (menu2 != null && menu2.HandleMenuMsg(message, wp, lp) == 0) return message is 0x2b or 0x2c ? 1 : 0;
                    }
                }
                catch { /* Never let a managed exception cross the window procedure. */ }
                return DefSubclassProc(hwnd, message, wp, lp);
            };
            hooked = SetWindowSubclass(owner, callback, 0xF11E, 0);
            if (!hooked) throw new InvalidOperationException("Windowsメニューのメッセージ処理を接続できません。");
            GetCursorPos(out var point);
            var command = TrackPopupMenuEx(menu, 0x100 | 0x2, point.X, point.Y, owner, 0);
            if (command == 0) return false;
            var directory = Marshal.StringToCoTaskMemUni(folderPath);
            try
            {
                var info = new InvokeInfo { Size = (uint)Marshal.SizeOf<InvokeInfo>(), Mask = 0x4000 | 0x20000000, Owner = owner, Verb = (nint)(command - 1), VerbW = (nint)(command - 1), DirectoryW = directory, Show = 1, Point = point };
                Marshal.ThrowExceptionForHR(session.Menu.InvokeCommand(ref info));
                return true;
            }
            finally { Marshal.FreeCoTaskMem(directory); }
        }
        finally
        {
            if (hooked) RemoveWindowSubclass(owner, callback!, 0xF11E);
            DestroyMenu(menu); GC.KeepAlive(callback);
        }
    }

    // Read-only integration probe: creates and populates a Shell menu without
    // displaying it or invoking any command against the test fixture.
    internal static int Probe(string folder, string[] paths)
    {
        using var session = Create(0, folder, paths); var menu = CreatePopupMenu();
        if (menu == 0) throw new InvalidOperationException("Windowsメニューを作成できません。");
        try { Marshal.ThrowExceptionForHR(session.Menu.QueryContextMenu(menu, 0, 1, 0x7fff, 0)); return GetMenuItemCount(menu); }
        finally { if (menu != 0) DestroyMenu(menu); }
    }
    private static Session Create(nint owner, string path, string[] paths)
    {
        var session = new Session();
        try
        {
            var folderId = FolderId; var menuId = MenuId;
            if (paths.Length == 0)
            {
                SHParseDisplayName(System.IO.Path.GetFullPath(path), 0, out var pidl, 0, out _); session.Pidls.Add(pidl);
                SHBindToObject(0, pidl, 0, ref folderId, out var folder); session.Folder = folder;
                folder.CreateViewObject(owner, ref menuId, out var result); session.Menu = (IMenu)result;
            }
            else
            {
                var children = new nint[paths.Length];
                for (var i = 0; i < paths.Length; i++)
                {
                    var full = System.IO.Path.GetFullPath(paths[i]);
                    if (!StringComparer.OrdinalIgnoreCase.Equals(System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetDirectoryName(full)!), System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(path)))) throw new ArgumentException("同じフォルダ内の項目を選択してください。");
                    SHParseDisplayName(full, 0, out var pidl, 0, out _); session.Pidls.Add(pidl); children[i] = ILFindLastID(pidl);
                }
                SHBindToParent(session.Pidls[0], ref folderId, out var folder, out _); session.Folder = folder;
                folder.GetUIObjectOf(owner, (uint)children.Length, children, ref menuId, 0, out var result); session.Menu = (IMenu)result;
            }
            return session;
        }
        catch { session.Dispose(); throw; }
    }
    private sealed class Session : IDisposable
    {
        public readonly List<nint> Pidls = [];
        public IFolder? Folder;
        public IMenu Menu = null!;
        public void Dispose()
        {
            if (Menu != null) Marshal.ReleaseComObject(Menu);
            if (Folder != null) Marshal.ReleaseComObject(Folder);
            foreach (var pidl in Pidls) Marshal.FreeCoTaskMem(pidl);
        }
    }
    [ComImport, Guid("000214e6-0000-0000-c000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFolder
    {
        void ParseDisplayName(nint owner, nint context, [MarshalAs(UnmanagedType.LPWStr)] string name, ref uint eaten, out nint pidl, ref uint attributes);
        void EnumObjects(nint owner, uint flags, out nint objects);
        void BindToObject(nint pidl, nint context, ref Guid id, out nint obj);
        void BindToStorage(nint pidl, nint context, ref Guid id, out nint obj);
        [PreserveSig] int CompareIDs(nint parameter, nint first, nint second);
        void CreateViewObject(nint owner, ref Guid id, [MarshalAs(UnmanagedType.IUnknown)] out object result);
        void GetAttributesOf(uint count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] nint[] items, ref uint attributes);
        void GetUIObjectOf(nint owner, uint count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] nint[] items, ref Guid id, nint reserved, [MarshalAs(UnmanagedType.IUnknown)] out object result);
        void GetDisplayNameOf(nint pidl, uint flags, nint name);
        void SetNameOf(nint owner, nint pidl, [MarshalAs(UnmanagedType.LPWStr)] string name, uint flags, out nint renamed);
    }
    [ComImport, Guid("000214e4-0000-0000-c000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMenu
    {
        [PreserveSig] int QueryContextMenu(nint menu, uint index, uint first, uint last, uint flags);
        [PreserveSig] int InvokeCommand(ref InvokeInfo info);
        [PreserveSig] int GetCommandString(nuint command, uint flags, nint reserved, nint text, uint length);
    }
    [ComImport, Guid("000214f4-0000-0000-c000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMenu2
    {
        [PreserveSig] int QueryContextMenu(nint m, uint i, uint f, uint l, uint flags);
        [PreserveSig] int InvokeCommand(ref InvokeInfo info);
        [PreserveSig] int GetCommandString(nuint c, uint f, nint r, nint t, uint l);
        [PreserveSig] int HandleMenuMsg(uint message, nuint wp, nint lp);
    }
    [ComImport, Guid("bcfce0a0-ec17-11d0-8d10-00a0c90f2719"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMenu3
    {
        [PreserveSig] int QueryContextMenu(nint m, uint i, uint f, uint l, uint flags);
        [PreserveSig] int InvokeCommand(ref InvokeInfo info);
        [PreserveSig] int GetCommandString(nuint c, uint f, nint r, nint t, uint l);
        [PreserveSig] int HandleMenuMsg(uint message, nuint wp, nint lp);
        [PreserveSig] int HandleMenuMsg2(uint message, nuint wp, nint lp, out nint result);
    }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct InvokeInfo
    {
        public uint Size, Mask; public nint Owner, Verb, Parameters, Directory;
        public int Show; public uint HotKey; public nint Icon, Title, VerbW, ParametersW, DirectoryW, TitleW; public Point Point;
    }
    private delegate nint SubclassProc(nint window, uint message, nuint wp, nint lp, nuint id, nuint data);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)] private static extern void SHParseDisplayName(string path, nint context, out nint pidl, uint mask, out uint attributes);
    [DllImport("shell32.dll", PreserveSig = false)] private static extern void SHBindToParent(nint pidl, ref Guid id, out IFolder folder, out nint child);
    [DllImport("shell32.dll", PreserveSig = false)] private static extern void SHBindToObject(nint parent, nint pidl, nint context, ref Guid id, out IFolder folder);
    [DllImport("shell32.dll")] private static extern nint ILFindLastID(nint pidl);
    [DllImport("user32.dll")] private static extern nint CreatePopupMenu();
    [DllImport("user32.dll")] private static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll")] private static extern int GetMenuItemCount(nint menu);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint owner, nint parameters);
    [DllImport("comctl32.dll")] private static extern bool SetWindowSubclass(nint window, SubclassProc callback, nuint id, nuint data);
    [DllImport("comctl32.dll")] private static extern bool RemoveWindowSubclass(nint window, SubclassProc callback, nuint id);
    [DllImport("comctl32.dll")] private static extern nint DefSubclassProc(nint window, uint message, nuint wp, nint lp);
}
