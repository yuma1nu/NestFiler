using System.Threading.Channels;
namespace FilerApp.Services;
public static class AppLog
{
    // Keep the existing profile location when the display/assembly name changes.
    // Favorites, history, tabs and single-instance identity must remain shared.
    public static string DataDirectory => Environment.GetEnvironmentVariable("FILERAPP_PROFILE_DIR") is { Length: > 0 } profile ? Path.GetFullPath(profile) : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FilerApp");
    private static readonly Channel<string> queue = Channel.CreateBounded<string>(new BoundedChannelOptions(200) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
    private static readonly Task writer = Task.Run(async () =>
    {
        await foreach (var message in queue.Reader.ReadAllAsync())
        {
            try
            {
                var folder = Path.Combine(DataDirectory, "logs"); Directory.CreateDirectory(folder);
                var path = Path.Combine(folder, "app.log");
                if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024) File.Move(path, Path.Combine(folder, "app.previous.log"), true);
                await File.AppendAllTextAsync(path, message + Environment.NewLine);
            }
            catch { /* Logging must not prevent normal error reporting. */ }
        }
    });
    public static void Error(string message) => queue.Writer.TryWrite($"{DateTimeOffset.Now:O} ERROR {message}");
}
