using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;

namespace FilerApp.Services;
internal sealed record ThumbnailData(int Width, int Height, byte[] Pixels);

// UI-thread cache; only realized cells request thumbnails. File reads and WIC
// decode run on workers, with a fixed concurrency and bounded cache reservation.
internal static class ThumbnailService
{
    private sealed class Cached(Task<ImageSource?> task, int bytes, CancellationTokenSource cancellation)
    { public Task<ImageSource?> Task = task; public int Bytes = bytes, Waiters; public DateTime Used = DateTime.UtcNow; public CancellationTokenSource Cancellation = cancellation; }
    private static readonly Dictionary<string, Cached> cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly SemaphoreSlim workers = new(2);
    private static int reserved;
    private const int Budget = 12 * 1024 * 1024;
    private static readonly HashSet<string> extensions = new([".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".webp"], StringComparer.OrdinalIgnoreCase);
    internal static bool Supports(Core.FileEntry entry) => !entry.IsDirectory && extensions.Contains(Path.GetExtension(entry.Path));
    private static string Key(Core.FileEntry entry, int edge) => $"{entry.Path}|{entry.Modified.Ticks}|{entry.Size}|{Math.Clamp(edge, 32, 192)}";
    public static ImageSource? TryGet(Core.FileEntry entry, int edge) => cache.TryGetValue(Key(entry, edge), out var hit) && hit.Task.IsCompletedSuccessfully ? hit.Task.Result : null;
    public static async Task<ImageSource?> GetAsync(Core.FileEntry entry, int edge, CancellationToken token)
    {
        if (!Supports(entry) || entry.Size > 32 * 1024 * 1024) return null;
        edge = Math.Clamp(edge, 32, 192);
        var key = Key(entry, edge);
        if (cache.TryGetValue(key, out var hit)) { hit.Used = DateTime.UtcNow; return await AwaitAsync(key, hit, token); }
        int cost = edge * edge * 4;
        while (cache.Count >= 96 || reserved + cost > Budget)
        {
            var oldest = cache.Where(x => x.Value.Task.IsCompleted).OrderBy(x => x.Value.Used).FirstOrDefault();
            if (oldest.Key == null) return null;
            reserved -= oldest.Value.Bytes; cache.Remove(oldest.Key); oldest.Value.Cancellation.Dispose();
        }
        var cancellation = new CancellationTokenSource();
        var task = LoadAsync(entry.Path, edge, cancellation.Token);
        var value = new Cached(task, cost, cancellation); cache[key] = value; reserved += cost;
        return await AwaitAsync(key, value, token);
    }
    private static async Task<ImageSource?> AwaitAsync(string key, Cached value, CancellationToken token)
    {
        value.Waiters++;
        try { return await value.Task.WaitAsync(token); }
        finally
        {
            if (--value.Waiters == 0 && !value.Task.IsCompleted)
            {
                if (cache.TryGetValue(key, out var current) && ReferenceEquals(current, value)) { cache.Remove(key); reserved -= value.Bytes; }
                await value.Cancellation.CancelAsync();
                _ = value.Task.ContinueWith(_ => value.Cancellation.Dispose(), TaskScheduler.Default);
            }
        }
    }
    private static async Task<ImageSource?> LoadAsync(string path, int edge, CancellationToken token)
    {
        try
        {
            await workers.WaitAsync(token); ThumbnailData? decoded;
            try { decoded = await Task.Run(() => ReadAsync(path, edge, token), token); }
            finally { workers.Release(); }
            if (decoded == null || token.IsCancellationRequested) return null;
            var bitmap = new WriteableBitmap(decoded.Width, decoded.Height);
            using var output = bitmap.PixelBuffer.AsStream(); await output.WriteAsync(decoded.Pixels);
            bitmap.Invalidate(); return bitmap;
        }
        catch { return null; }
    }
    internal static async Task<ThumbnailData?> ReadAsync(string path, int edge, CancellationToken token)
    {
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536, true);
        if (input.Length == 0 || input.Length > 32 * 1024 * 1024) return null;
        using var stream = input.AsRandomAccessStream();
        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(token);
        if (decoder.PixelWidth == 0 || decoder.PixelHeight == 0 || (ulong)decoder.PixelWidth * decoder.PixelHeight > 64_000_000) return null;
        var scale = Math.Min(1d, Math.Clamp(edge, 32, 192) / (double)Math.Max(decoder.PixelWidth, decoder.PixelHeight));
        var transform = new BitmapTransform { ScaledWidth = Math.Max(1, (uint)(decoder.PixelWidth * scale)), ScaledHeight = Math.Max(1, (uint)(decoder.PixelHeight * scale)), InterpolationMode = BitmapInterpolationMode.Fant };
        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.ColorManageToSRgb).AsTask(token);
        if (bitmap.PixelWidth > 192 || bitmap.PixelHeight > 192) return null;
        var pixels = new byte[checked(bitmap.PixelWidth * bitmap.PixelHeight * 4)];
        bitmap.CopyToBuffer(pixels.AsBuffer());
        return new(bitmap.PixelWidth, bitmap.PixelHeight, pixels);
    }
}
