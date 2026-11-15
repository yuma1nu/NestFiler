using System.Runtime.InteropServices;
namespace FilerApp.Services;
internal static class ShellFileIcon
{
    public static byte[]? ReadPixels(bool directory, string extension, int size = 32)
    {
        size = size > 32 ? 48 : 32;
        // USEFILEATTRIBUTES retrieves the association without accessing a user file,
        // network share, shortcut target, or executable's embedded resources.
        var initialized = CoInitializeEx(0, 0) >= 0;
        nint dc = 0, dib = 0, previous = 0;
        var info = new FileInfoNative();
        try
        {
            var name = directory ? "folder" : "file" + extension;
            if (SHGetFileInfoW(name, directory ? 0x10u : 0x80u, ref info, (uint)Marshal.SizeOf<FileInfoNative>(), 0x100 | 0x10 | 0x4000) == 0 || info.Icon == 0) return null;
            if (size > 32 && ReadLargeIcon(info.Index) is var large && large != 0) { DestroyIcon(info.Icon); info.Icon = large; }
            dc = CreateCompatibleDC(0); if (dc == 0) return null;
            var header = new BitmapInfo { Size = 40, Width = size, Height = -size, Planes = 1, BitCount = 32 };
            dib = CreateDIBSection(dc, ref header, 0, out var bits, 0, 0);
            if (dib == 0 || bits == 0) return null;
            previous = SelectObject(dc, dib);
            var pixels = new byte[size * size * 4]; Marshal.Copy(pixels, 0, bits, pixels.Length);
            if (!DrawIconEx(dc, 0, 0, info.Icon, size, size, 0, 0, 3)) return null;
            Marshal.Copy(bits, pixels, 0, pixels.Length);
            // Old mask-based icons don't populate alpha. Draw a second time over
            // white to reconstruct their transparency without a black rectangle.
            if (!Enumerable.Range(0, size * size).Any(i => pixels[i * 4 + 3] != 0))
            {
                var white = Enumerable.Repeat((byte)255, pixels.Length).ToArray(); Marshal.Copy(white, 0, bits, white.Length);
                if (!DrawIconEx(dc, 0, 0, info.Icon, size, size, 0, 0, 3)) return null;
                Marshal.Copy(bits, white, 0, white.Length);
                for (var i = 0; i < pixels.Length; i += 4) pixels[i + 3] = (byte)(255 - Math.Clamp(white[i] - pixels[i], 0, 255));
            }
            return pixels;
        }
        finally
        {
            if (previous != 0 && dc != 0) SelectObject(dc, previous);
            if (dib != 0) DeleteObject(dib);
            if (dc != 0) DeleteDC(dc);
            if (info.Icon != 0) DestroyIcon(info.Icon);
            if (initialized) CoUninitialize();
        }
    }
    private static nint ReadLargeIcon(int index)
    {
        IImageList? images = null;
        try
        {
            var id = typeof(IImageList).GUID;
            SHGetImageList(2, ref id, out images); // SHIL_EXTRALARGE; use file-type association index.
            return images.GetIcon(index, 1, out var icon) >= 0 ? icon : 0;
        }
        catch (COMException) { return 0; }
        finally { if (images != null) Marshal.ReleaseComObject(images); }
    }
    [ComImport, Guid("46eb5926-582e-4017-9fdf-e8998daa0950"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IImageList
    {
        [PreserveSig] int Add(nint image, nint mask, out int index);
        [PreserveSig] int ReplaceIcon(int index, nint icon, out int added);
        [PreserveSig] int SetOverlayImage(int image, int overlay);
        [PreserveSig] int Replace(int index, nint image, nint mask);
        [PreserveSig] int AddMasked(nint image, uint color, out int index);
        [PreserveSig] int Draw(nint parameters);
        [PreserveSig] int Remove(int index);
        [PreserveSig] int GetIcon(int index, uint flags, out nint icon);
    }
    [DllImport("shell32.dll", PreserveSig = false)] private static extern void SHGetImageList(int size, ref Guid id, out IImageList images);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FileInfoNative
    {
        public nint Icon; public int Index; public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName;
    }
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo
    {
        public uint Size; public int Width, Height; public ushort Planes, BitCount;
        public uint Compression, SizeImage; public int XPels, YPels; public uint Used, Important;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern nint SHGetFileInfoW(string path, uint attributes, ref FileInfoNative info, uint size, uint flags);
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(nint reserved, uint mode);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyIcon(nint icon);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DrawIconEx(nint dc, int x, int y, nint icon, int width, int height, uint step, nint brush, uint flags);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateDIBSection(nint dc, ref BitmapInfo info, uint usage, out nint bits, nint section, uint offset);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc, nint obj);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteDC(nint dc);
}


