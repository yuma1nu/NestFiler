using System.Security.Cryptography;
using System.Text;
using System.Runtime.InteropServices;

namespace FilerApp.Services;

// The profile identity also isolates integration tests from the user's session.
internal sealed class SingleInstance : IDisposable
{
    private readonly Mutex mutex;
    private readonly EventWaitHandle activation;
    private RegisteredWaitHandle? wait;
    public bool IsPrimary { get; }
    public SingleInstance(string profile)
    {
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.TrimEndingDirectorySeparator(Path.GetFullPath(profile)).ToUpperInvariant())));
        activation = new(false, EventResetMode.AutoReset, @"Local\FilerApp.Activate." + key);
        mutex = new(true, @"Local\FilerApp.Instance." + key, out bool created);
        IsPrimary = created;
        if (!created) activation.Set();
    }
    public void Listen(Action activated) => wait = ThreadPool.RegisterWaitForSingleObject(activation, (_, _) => activated(), null, Timeout.Infinite, false);
    public static void Activate(Microsoft.UI.Xaml.Window window)
    {
        nint handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
        if (IsIconic(handle)) ShowWindow(handle, 9);
        window.Activate(); SetForegroundWindow(handle);
    }
    public void Dispose() { wait?.Unregister(null); if (IsPrimary) mutex.ReleaseMutex(); mutex.Dispose(); activation.Dispose(); }
    [DllImport("user32.dll")] private static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint hwnd);
}
