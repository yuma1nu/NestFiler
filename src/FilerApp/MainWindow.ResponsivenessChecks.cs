#if DEBUG
using System.Diagnostics;
using FilerApp.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Windows.Graphics.Imaging;

namespace FilerApp;
public sealed partial class MainWindow
{
    private async Task RunResponsivenessChecksAsync()
    {
        var results = new List<string>(); var root = Services.AppLog.DataDirectory;
        var images = Directory.CreateDirectory(Path.Combine(root, "images-500")).FullName;
        var folders = Directory.CreateDirectory(Path.Combine(root, "tree-500")).FullName;
        var source = Directory.CreateDirectory(Path.Combine(root, "drag-source")).FullName;
        var target = Directory.CreateDirectory(Path.Combine(root, "drag-target")).FullName;
        var small = Directory.CreateDirectory(Path.Combine(root, "favorite-small")).FullName;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) };
        var gaps = new List<double>(); var clock = Stopwatch.StartNew(); double previous = 0;
        timer.Tick += (_, _) => { double now = clock.Elapsed.TotalMilliseconds; gaps.Add(now - previous); previous = now; };
        try
        {
            await Task.Run(async () =>
            {
                var seed = Path.Combine(root, "seed.jpg"); var pixels = new byte[1600 * 900 * 4];
                for (int i = 0; i < pixels.Length; i += 4) { pixels[i] = (byte)(i / 16); pixels[i + 1] = 80; pixels[i + 2] = (byte)(i / 6400); pixels[i + 3] = 255; }
                await using (var file = new FileStream(seed, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
                {
                    using var stream = file.AsRandomAccessStream(); var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
                    encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, 1600, 900, 96, 96, pixels); await encoder.FlushAsync();
                }
                for (int i = 0; i < 500; i++) { File.Copy(seed, Path.Combine(images, $"image-{i:D3}.jpg"), true); Directory.CreateDirectory(Path.Combine(folders, $"folder-{i:D3}")); }
                await File.WriteAllTextAsync(Path.Combine(source, "A.txt"), "A-content"); await File.WriteAllTextAsync(Path.Combine(source, "B.txt"), "B-content");
            });
            await LeftPane.DebugCheckColumnPositionsAsync(images, results);
            await LeftPane.DebugCheckDragSnapshotAsync(source, results);
            foreach (var mode in new[] { FileViewMode.Details, FileViewMode.LargeIcons })
            {
                gaps.Clear(); clock.Restart(); previous = 0; timer.Start();
                await LeftPane.DebugScrollImagesAsync(images, mode, measure: results.Add); await Task.Delay(200); timer.Stop();
                results.Add($"MEASURE {mode} 500-image scroll: heartbeat p95={gaps.Order().ElementAt((int)(gaps.Count * 0.95)):F1}ms max={gaps.Max():F1}ms samples={gaps.Count}");
                if (gaps.Max() > 1000 || gaps.Count < 10) throw new InvalidOperationException("UI heartbeat stalled during image scrolling");
                results.Add("PASS " + mode + " 500-image scrolling keeps the UI heartbeat and virtualization");
            }
            gaps.Clear(); clock.Restart(); previous = 0; timer.Start();
            var node = new TreeViewNode { Content = "500-folder check", HasUnrealizedChildren = true }; places[node] = folders; PlacesTree.RootNodes.Add(node);
            // Setting IsExpanded in code does not raise the user Expanding event.
            // Exercise the same population routine; mouse expansion is separate.
            await PopulatePlaceAsync(node, folders);
            node.IsExpanded = true; await Task.Delay(80);
            timer.Stop();
            if (node.Children.Count != 500 || node.HasUnrealizedChildren || gaps.Count == 0 || gaps.Max() > 1000) throw new InvalidOperationException($"500-folder tree expansion failed or stalled UI: children={node.Children.Count}, unrealized={node.HasUnrealizedChildren}, heartbeats={gaps.Count}, worst={(gaps.Count == 0 ? 0 : gaps.Max()):F1}ms, status={Status.Text}");
            results.Add($"MEASURE 500-folder expansion heartbeat max={gaps.Max():F1}ms samples={gaps.Count}");
            results.Add("PASS 500-folder real tree expansion yields to the UI queue");
            FavoriteRoots.Add(new() { Name = "response-favorite", Path = small }); RenderFavorites();
            var button = ((Border)FavoriteList.Children[^1]).Child as Button ?? throw new InvalidOperationException("Favorite check button missing");
            await LeftPane.DebugScrollImagesAsync(images, FileViewMode.LargeIcons, async () =>
            {
                SetActive(LeftPane); var invoke = (IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke); invoke.Invoke();
                for (int i = 0; i < 100 && LeftPane.Current?.Path != small; i++) await Task.Delay(20);
                if (LeftPane.Current?.Path != small) throw new InvalidOperationException("Favorite click was not handled during image scrolling");
            });
            results.Add("PASS real favorite button invokes navigation during 500-image scrolling");
            LeftPane.SetDisplayMode(FileViewMode.Details); await LeftPane.NavigateAsync(source); await RightPane.NavigateAsync(target); SetActive(LeftPane); LeftPane.DebugSelectFile(Path.Combine(source, "A.txt"));
        }
        catch (Exception ex) { results.Add("FAIL " + ex); }
        finally
        {
            timer.Stop(); await File.WriteAllLinesAsync(Path.Combine(root, "responsiveness-check.txt"), results);
            if (Environment.GetEnvironmentVariable("FILERAPP_RESPONSIVENESS_HOLD") != "1") { closing = true; Close(); }
        }
    }
}
#endif
