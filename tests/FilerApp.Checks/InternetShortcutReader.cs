using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

// Read through Windows' real InternetShortcut COM object. This does not invoke
// the URL, launch Office, contact a tenant, or change the user's clipboard.
internal static class InternetShortcutReader
{
    public static Task<string?> ReadAsync(string file)
    {
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            object? shortcut = null; nint address = 0;
            try
            {
                shortcut = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("FBF23B40-E3F0-101B-8488-00AA003E56F8"), true)!);
                ((IPersistFile)shortcut!).Load(file, 0);
                ((IUniformResourceLocatorW)shortcut).GetURL(out address);
                completion.SetResult(Marshal.PtrToStringUni(address));
            }
            catch (Exception ex) { completion.SetException(ex); }
            finally { if (address != 0) Marshal.FreeCoTaskMem(address); if (shortcut != null) Marshal.FinalReleaseComObject(shortcut); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        return completion.Task;
    }
    [ComImport, Guid("CABB0DA0-DA57-11CF-9974-0020AFD79762"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IUniformResourceLocatorW
    {
        void SetURL([MarshalAs(UnmanagedType.LPWStr)] string url, uint flags);
        void GetURL(out nint url);
        void InvokeCommand(nint command);
    }
}
