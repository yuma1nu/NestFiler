using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Windows.ApplicationModel.DataTransfer;
using WinRT;
using Windows.Storage.Streams;

namespace FilerApp.Services;

// StorageFile cannot represent .url/.lnk/.wsh. Use the same CF_HDROP physical
// file paths as Explorer instead of resolving a shortcut to its remote target.
internal static class FileTransferData
{
    public static bool ContainsFiles(DataPackageView view) => view.Contains("FileDrop") || view.Contains(StandardDataFormats.StorageItems);
    public static void SetPaths(DataPackage package, string[] paths, bool move = false, Action<DataPackageOperation>? completed = null)
    {
        var snapshot = paths.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        package.As<IDataObjectProvider>().SetDataObject(new PathDataObject(snapshot, move, completed));
        package.RequestedOperation = move ? DataPackageOperation.Move : DataPackageOperation.Copy;
    }
    public static async Task<string[]> GetPathsAsync(DataPackageView view)
    {
        // FileDrop is the WinRT projection of CF_HDROP. Unlike StorageItems it
        // carries the original physical shortcut filenames without dereferencing.
        if (view.Contains("FileDrop"))
        {
            var raw = await view.GetDataAsync("FileDrop");
            using var stream = raw.As<IRandomAccessStream>();
            if (stream.Size < 22 || stream.Size > 8 * 1024 * 1024) throw new IOException("ファイルの転送情報が不正です。");
            using var reader = new DataReader(stream.GetInputStreamAt(0));
            var size = (uint)stream.Size;
            if (await reader.LoadAsync(size) != size) throw new IOException("ファイルの転送情報を読み取れません。");
            var bytes = new byte[size]; reader.ReadBytes(bytes);
            var offset = BitConverter.ToInt32(bytes, 0); bool wide = BitConverter.ToInt32(bytes, 16) != 0;
            if (offset < 20 || offset >= bytes.Length || wide && (offset % 2 != 0 || (bytes.Length - offset) % 2 != 0)) throw new IOException("ファイルの転送情報が不正です。");
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var encoding = wide ? new System.Text.UnicodeEncoding(false, false, true) : Encoding.GetEncoding((int)GetACP());
            var names = encoding.GetString(bytes, offset, bytes.Length - offset);
            int terminator = names.IndexOf("\0\0", StringComparison.Ordinal);
            if (terminator < 0) throw new IOException("ファイルの転送情報が途切れています。");
            var paths = names[..terminator].Split('\0', StringSplitOptions.RemoveEmptyEntries);
            if (paths.Any(x => !Path.IsPathFullyQualified(x) || x.Any(char.IsControl))) throw new IOException("ファイルの転送パスが不正です。");
            return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        return (await view.GetStorageItemsAsync()).Select(x => x.Path).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
    internal static string[]? ReadNativePaths(object packageOrView)
    {
        IDataObject? data = null;
        try
        {
            packageOrView.As<IDataObjectProvider>().GetDataObject(out data);
            var format = Format(15);
            if (data.QueryGetData(ref format) != 0) return null;
            data.GetData(ref format, out var medium);
            try
            {
                uint count = DragQueryFileW(medium.unionmember, uint.MaxValue, null, 0);
                if (count > 100000) throw new IOException("転送するファイルが多すぎます。");
                var paths = new string[count];
                for (uint i = 0; i < count; i++)
                {
                    var name = new StringBuilder((int)DragQueryFileW(medium.unionmember, i, null, 0) + 1);
                    DragQueryFileW(medium.unionmember, i, name, (uint)name.Capacity); paths[i] = name.ToString();
                }
                return paths;
            }
            finally { ReleaseStgMedium(ref medium); }
        }
        catch (InvalidCastException) { return null; }
        catch (COMException) { return null; }
        finally { if (data != null && Marshal.IsComObject(data)) Marshal.ReleaseComObject(data); }
    }
    private static FORMATETC Format(short id) => new() { cfFormat = id, dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = -1, tymed = TYMED.TYMED_HGLOBAL };
    private static readonly short PreferredEffect = (short)RegisterClipboardFormatW("Preferred DropEffect");
    private static readonly short PerformedEffect = (short)RegisterClipboardFormatW("Performed DropEffect");
    private static readonly short PasteSucceeded = (short)RegisterClipboardFormatW("Paste Succeeded");
    [ComImport, Guid("3D25F6D6-4B2A-433C-9184-7C33AD35D001"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDataObjectProvider
    {
        void GetDataObject([MarshalAs(UnmanagedType.Interface)] out IDataObject data);
        void SetDataObject([MarshalAs(UnmanagedType.Interface)] IDataObject data);
    }
    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    private sealed class PathDataObject(string[] paths, bool move, Action<DataPackageOperation>? completed) : IDataObject
    {
        public void GetData(ref FORMATETC format, out STGMEDIUM medium)
        {
            Marshal.ThrowExceptionForHR(QueryGetData(ref format));
            byte[] bytes;
            if (format.cfFormat == 15)
            {
                var names = Encoding.Unicode.GetBytes(string.Join('\0', paths) + "\0\0");
                bytes = new byte[20 + names.Length]; BitConverter.GetBytes(20).CopyTo(bytes, 0); BitConverter.GetBytes(1).CopyTo(bytes, 16); names.CopyTo(bytes, 20);
            }
            else bytes = BitConverter.GetBytes(move ? 2 : 1);
            var handle = GlobalAlloc(2, (nuint)bytes.Length);
            if (handle == 0) throw new OutOfMemoryException();
            var pointer = GlobalLock(handle);
            if (pointer == 0) { GlobalFree(handle); throw new OutOfMemoryException(); }
            try { Marshal.Copy(bytes, 0, pointer, bytes.Length); } finally { GlobalUnlock(handle); }
            medium = new() { tymed = TYMED.TYMED_HGLOBAL, unionmember = handle };
        }
        public int QueryGetData(ref FORMATETC format) => format.dwAspect == DVASPECT.DVASPECT_CONTENT && format.lindex == -1 && (format.tymed & TYMED.TYMED_HGLOBAL) != 0 && (format.cfFormat == 15 || format.cfFormat == PreferredEffect) ? 0 : unchecked((int)0x80040064);
        public IEnumFORMATETC EnumFormatEtc(DATADIR direction) => direction == DATADIR.DATADIR_GET ? new Formats([Format(15), Format(PreferredEffect)]) : throw new COMException("Unsupported direction", unchecked((int)0x80004001));
        public void SetData(ref FORMATETC format, ref STGMEDIUM medium, bool release)
        {
            try
            {
                if ((format.cfFormat == PerformedEffect || format.cfFormat == PasteSucceeded) && medium.tymed == TYMED.TYMED_HGLOBAL)
                {
                    var pointer = GlobalLock(medium.unionmember);
                    if (pointer == 0) return;
                    int effect; try { effect = Marshal.ReadInt32(pointer); } finally { GlobalUnlock(medium.unionmember); }
                    completed?.Invoke((DataPackageOperation)(effect & 3));
                }
            }
            finally { if (release) ReleaseStgMedium(ref medium); }
        }
        public void GetDataHere(ref FORMATETC f, ref STGMEDIUM m) => throw new COMException("Unsupported medium", unchecked((int)0x80040069));
        public int GetCanonicalFormatEtc(ref FORMATETC input, out FORMATETC output) { output = input; output.ptd = 0; return 0x40130; }
        public int DAdvise(ref FORMATETC f, ADVF flags, IAdviseSink sink, out int connection) { connection = 0; return unchecked((int)0x80040003); }
        public void DUnadvise(int connection) { }
        public int EnumDAdvise(out IEnumSTATDATA enumerator) { enumerator = null!; return unchecked((int)0x80040003); }
    }
    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    private sealed class Formats(FORMATETC[] formats) : IEnumFORMATETC
    {
        private int index;
        public int Next(int count, FORMATETC[] output, int[]? fetched)
        { int read = 0; while (read < count && index < formats.Length) output[read++] = formats[index++]; if (fetched?.Length > 0) fetched[0] = read; return read == count ? 0 : 1; }
        public int Skip(int count) { var next = Math.Min(index + count, formats.Length); var skipped = next - index; index = next; return skipped == count ? 0 : 1; }
        public int Reset() { index = 0; return 0; }
        public void Clone(out IEnumFORMATETC copy) { copy = new Formats(formats) { index = index }; }
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterClipboardFormatW(string name);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern uint DragQueryFileW(nint drop, uint index, StringBuilder? name, uint length);
    [DllImport("ole32.dll")] private static extern void ReleaseStgMedium(ref STGMEDIUM medium);
    [DllImport("kernel32.dll")] private static extern nint GlobalAlloc(uint flags, nuint size);
    [DllImport("kernel32.dll")] private static extern nint GlobalLock(nint handle);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(nint handle);
    [DllImport("kernel32.dll")] private static extern nint GlobalFree(nint handle);
    [DllImport("kernel32.dll")] private static extern uint GetACP();
}
