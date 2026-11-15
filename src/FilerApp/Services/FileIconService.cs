using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
namespace FilerApp.Services;

// UI-thread cache, shared by both panes. Only realized rows request icons.
internal static class FileIconService
{
    private static readonly Dictionary<string, Task<ImageSource?>> cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly SemaphoreSlim workers = new(2);
    public static ImageSource? TryGet(bool directory, string extension, int size)
    {
        var key = (directory ? "<folder>" : extension.ToLowerInvariant()) + ":" + (size > 32 ? 48 : 32);
        return cache.TryGetValue(key, out var hit) && hit.IsCompletedSuccessfully ? hit.Result : null;
    }
    public static Task<ImageSource?> GetAsync(bool directory, string extension, int size = 32)
    {
        size = size > 32 ? 48 : 32;
        var key = (directory ? "<folder>" : extension.ToLowerInvariant()) + ":" + size;
        if (cache.TryGetValue(key, out var pending)) return pending;
        if (cache.Count >= 256)
        {
            foreach (var old in cache.Where(x => x.Value.IsCompleted).Take(64).Select(x => x.Key).ToArray()) cache.Remove(old);
            if (cache.Count >= 256) return Task.FromResult<ImageSource?>(null);
        }
        return cache[key] = LoadAsync(directory, extension, size);
    }
    private static async Task<ImageSource?> LoadAsync(bool directory, string extension, int size)
    {
        try
        {
            await workers.WaitAsync();
            byte[]? pixels;
            try { pixels = await Task.Run(() => ShellFileIcon.ReadPixels(directory, extension, size)); }
            finally { workers.Release(); }
            if (pixels == null) return null;
            var bitmap = new WriteableBitmap(size, size);
            using var stream = bitmap.PixelBuffer.AsStream(); await stream.WriteAsync(pixels);
            bitmap.Invalidate(); return bitmap;
        }
        catch { return null; } // Keep the built-in fallback glyph if the Shell fails.
    }
}


