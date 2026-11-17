#if DEBUG
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;

namespace FilerApp;
public sealed partial class MainWindow
{
    // Export this app's own XAML rendering for visual review in a disposable
    // profile. No desktop capture, input injection or user folders are involved.
    private async Task ExportNavigationPreviewAsync()
    {
        var output = Services.AppLog.DataDirectory;
        try
        {
            var report = new List<string>();
            var workspace = Path.Combine(output, "workspace");
            var data = Directory.CreateDirectory(Path.Combine(workspace, "開発・検証", "実験データ", "2026-11")).FullName;
            var cad = Directory.CreateDirectory(Path.Combine(workspace, "CAD")).FullName;
            var reports = Directory.CreateDirectory(Path.Combine(workspace, "報告書")).FullName;
            await File.WriteAllTextAsync(Path.Combine(data, "測定結果.csv"), "名前,値\n試験1,42");
            await File.WriteAllTextAsync(Path.Combine(data, "README.md"), "# 実験データ");
            await LeftPane.NavigateAsync(cad); await LeftPane.NavigateAsync(reports, true); await LeftPane.NavigateAsync(data, true);
            await RightPane.NavigateAsync(reports); await RightPane.NavigateAsync(cad, true);
            state.FavoriteNodes = [new() { Name = "開発プロジェクト", Children = [new() { Name = "実験データ", Path = data }, new() { Name = "CAD", Path = cad }, new() { Name = "報告書", Path = reports }] }];
            RenderFavorites(); SetActive(LeftPane);
            foreach (var theme in new[] { ElementTheme.Dark, ElementTheme.Light })
            {
                Root.RequestedTheme = theme; await Task.Delay(300); Root.UpdateLayout();
                if (Microsoft.UI.Windowing.AppWindowTitleBar.IsCustomizationSupported() && !titleBarThemeSettings.HighContrast)
                {
                    var bar = AppWindow.TitleBar;
                    var background = ((Microsoft.UI.Xaml.Media.SolidColorBrush)Root.Background).Color;
                    var foreground = bar.ForegroundColor ?? throw new InvalidOperationException("Title bar foreground is unset.");
                    if (bar.BackgroundColor != background || bar.InactiveBackgroundColor != background ||
                        bar.ButtonBackgroundColor != background || bar.ButtonInactiveBackgroundColor != background)
                        throw new InvalidOperationException($"{theme}: native title bar does not match the window background.");
                    int Brightness(Windows.UI.Color color) => color.R + color.G + color.B;
                    if (theme == ElementTheme.Dark ? Brightness(background) >= 384 || Brightness(foreground) <= Brightness(background)
                        : Brightness(background) <= 384 || Brightness(foreground) >= Brightness(background))
                        throw new InvalidOperationException($"{theme}: native caption colors have the wrong brightness.");
                    report.Add($"PASS {theme}: native title bar active/inactive/button backgrounds match content #{background.R:X2}{background.G:X2}{background.B:X2}; text #{foreground.R:X2}{foreground.G:X2}{foreground.B:X2}.");
                }
                var bitmap = new RenderTargetBitmap(); await bitmap.RenderAsync(Root);
                var pixels = (await bitmap.GetPixelsAsync()).ToArray();
                await using var file = new FileStream(Path.Combine(output, "navigation-" + theme.ToString().ToLowerInvariant() + ".png"), FileMode.Create, FileAccess.ReadWrite);
                using var stream = file.AsRandomAccessStream();
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels);
                encoder.BitmapTransform.Bounds = new BitmapBounds { Width = (uint)bitmap.PixelWidth, Height = (uint)Math.Min(bitmap.PixelHeight, Math.Ceiling(270 * Root.XamlRoot.RasterizationScale)) };
                await encoder.FlushAsync();
            }
            report.Add("Exported Dark and Light content previews from the app's XAML renderer (native title bar is outside these images).");
            await File.WriteAllLinesAsync(Path.Combine(output, "navigation-preview.txt"), report);
        }
        catch (Exception ex) { await File.WriteAllTextAsync(Path.Combine(output, "navigation-preview.txt"), ex.ToString()); }
        finally { closing = true; Close(); }
    }
}
#endif
