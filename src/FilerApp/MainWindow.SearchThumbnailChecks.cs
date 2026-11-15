#if DEBUG
using FilerApp.Core;
using FilerApp.Services;
using Microsoft.UI.Xaml;
using Windows.Graphics.Imaging;

namespace FilerApp;
public sealed partial class MainWindow
{
    private async Task RunSearchThumbnailChecksAsync(string root, List<string> results)
    {
        var folder = Directory.CreateDirectory(Path.Combine(root, "search-thumbnails")).FullName;
        var picture = Path.Combine(folder, "needle-image.png");
        var pixels = new byte[400 * 200 * 4];
        for (int y = 0; y < 200; y++) for (int x = 0; x < 400; x++) { int i = (y * 400 + x) * 4; pixels[i + (x < 200 ? 2 : 0)] = 255; pixels[i + 3] = 255; }
        await using (var file = new FileStream(picture, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
        {
            using var stream = file.AsRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, 400, 200, 96, 96, pixels);
            await encoder.FlushAsync();
        }
        var decoded = await Task.Run(() => ThumbnailService.ReadAsync(picture, 96, default));
        if (decoded is not { Width: 96, Height: 48 } || decoded.Pixels[2] < 200 || decoded.Pixels[(96 - 1) * 4] < 200) throw new InvalidOperationException("Thumbnail dimensions/aspect ratio/content were wrong");
        using (var exclusive = new FileStream(picture, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        results.Add("PASS thumbnail contains actual image pixels, aspect ratio and decode bounds; source lock released");
        await File.WriteAllTextAsync(Path.Combine(folder, "needle-report.txt"), "header\nneedle line");
        await File.WriteAllTextAsync(Path.Combine(folder, "other.txt"), "needle\nother");
        var broken = Path.Combine(folder, "broken.png"); await File.WriteAllTextAsync(broken, "not an image");
        await LeftPane.NavigateAsync(folder); SetActive(LeftPane);
        foreach (var mode in new[] { FileViewMode.SmallIcons, FileViewMode.LargeIcons })
        {
            LeftPane.SetDisplayMode(mode); await LeftPane.DebugWaitForThumbnailAsync(picture);
        }
        var info = new FileInfo(broken);
        if (await ThumbnailService.GetAsync(new(broken, info.Name, false, info.Length, info.LastWriteTime), 96, default) != null) throw new InvalidOperationException("Invalid image did not fall back");
        if (await ThumbnailService.GetAsync(new(picture, "image.png", false, 33L * 1024 * 1024, DateTime.Now), 96, default) != null) throw new InvalidOperationException("Oversized image was not rejected");
        results.Add("PASS realized small/large thumbnail cells, corrupt-image fallback and source-size limit");
        LeftPane.SetDisplayMode(FileViewMode.Details);
        var normal = LeftPane.Current!; var normalEntries = normal.Entries; int aCount = LeftPane.Models.Count, bCount = RightPane.Models.Count;
        var search = LeftPane.OpenSearchTab("needle"); await search.SearchWork!;
        await LeftPane.DebugAssertSearchTabAsync(search);
        if (LeftPane.Models.Count != aCount + 1 || RightPane.Models.Count != bCount || LeftPane.Current != search || !ReferenceEquals(normalEntries, normal.Entries) || search.Entries.Count != 2 || active != LeftPane) throw new InvalidOperationException("Search did not stay in a new A tab");
        await LeftPane.DebugRunSearchAsync(search.Search! with { Content = true, IncludeNames = true, Extensions = ".txt" });
        LeftPane.SelectAllFiles();
        if (search.Entries.Count != 3 || LeftPane.SelectedPaths.Length != 2 || !search.Entries.Any(x => x.SearchLine == 2 && x.SearchDetail.Contains("needle line"))) throw new InvalidOperationException("Search names/content or deduplicated file operations were wrong");
        results.Add("PASS search uses new tab in same pane, normal file template, names/location/grep lines, deduplicated paths and compact option menus");
        var right = RightPane.OpenSearchTab("other", new(folder, "other", false, true, false, false, "", "", "UTF-8"));
        await right.SearchWork!;
        if (right.Entries.Count != 1 || right.Entries[0].Name != "other.txt" || LeftPane.Current != search || search.Search?.Query != "needle" || active != RightPane) throw new InvalidOperationException("B search affected A");
        results.Add("PASS A and B keep independent search tabs, results and active pane");
        SetActive(LeftPane);
        await LeftPane.OpenContainingFolderAsync();
        if (LeftPane.Current?.IsSearch != false || !LeftPane.Models.Contains(search)) throw new InvalidOperationException("Opening containing folder overwrote search results");
        LeftPane.CloseCurrent();
        var options = search.Search; LeftPane.CloseCurrent(); await LeftPane.RestoreClosedTabAsync();
        if (LeftPane.Current?.Search != options || LeftPane.Current.Entries.Count != 3 || active != LeftPane) throw new InvalidOperationException("Closed search tab did not restore its query");
        results.Add("PASS containing folder opens separately and closed search tab restores conditions/results");
    }
}
#endif
