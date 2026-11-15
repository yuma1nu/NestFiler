using FilerApp.Core;
namespace FilerApp.Services;
public sealed record PreviewResult(byte[]? Image, string Description) { public string? Text { get; init; } public string? Format { get; init; } }
public interface IPreviewProvider
{
    bool Supports(FileEntry entry);
    Task<PreviewResult> ReadAsync(FileEntry entry, CancellationToken token);
}
public sealed class ImagePreviewProvider : IPreviewProvider
{
    public bool Supports(FileEntry entry) => !entry.IsDirectory && new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".webp" }.Contains(Path.GetExtension(entry.Path), StringComparer.OrdinalIgnoreCase);
    public Task<PreviewResult> ReadAsync(FileEntry entry, CancellationToken token) => Task.Run(async () =>
    {
        await using var stream = new FileStream(entry.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 81920, true);
        if (stream.Length > 32 * 1024 * 1024) return new PreviewResult(null, "32 MBを超える画像のプレビューは省略します。関連付けアプリで開いてください。");
        var bytes = new byte[checked((int)stream.Length)];
        await stream.ReadExactlyAsync(bytes, token);
        if (stream.Length != bytes.Length) return new PreviewResult(null, "読み込み中にファイルサイズが変わりました。再選択してください。");
        return new PreviewResult(bytes, $"{entry.Name} · {entry.SizeLabel}");    }, token);
}


