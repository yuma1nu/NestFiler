using FilerApp.Core;
namespace FilerApp.Services;
public sealed class TextPreviewProvider(string encoding) : IPreviewProvider
{
    public bool Supports(FileEntry entry) => !entry.IsDirectory && TextFiles.Extensions.Contains(Path.GetExtension(entry.Path));
    public async Task<PreviewResult> ReadAsync(FileEntry entry, CancellationToken token)
    {
        var sample = await Task.Run(() => TextFiles.ReadAsync(entry.Path, encoding, 128 * 1024, token), token);
        return new(null, $"{entry.Name} · {encoding}" + (sample.Truncated ? "\n先頭128 KBのみ表示" : "")) { Text = sample.Text, Format = Path.GetExtension(entry.Path).ToLowerInvariant() };
    }
}
