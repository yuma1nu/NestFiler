using FilerApp.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Graphics.Imaging;

namespace FilerApp.Views;
public sealed partial class PaneView
{
    private SoftwareBitmap? dragVisualBitmap;
    private async void ConfigureFileDrag(UIElement sender, DragStartingEventArgs args)
    {
        if (!fileDragInProgress) return; // Do not interfere with column-header drags.
        if (dragSnapshot.Length == 0) { args.Cancel = true; return; }
        args.AllowedOperations = FileDragOperations;
        PopulateDragPackage(args.Data, dragSnapshot);
        var deferral = args.GetDeferral();
        try
        {
            var bitmap = dragVisualBitmap = await RenderFileDragAsync(dragSnapshot);
            // Keep the pointer clear; the compact name/icon follows below it.
            args.DragUI.SetContentFromSoftwareBitmap(bitmap, new Point(-12, -8));
        }
        catch (Exception ex)
        {
            Services.AppLog.Error("Drag preview: " + ex.Message);
            args.Cancel = true;
        }
        finally { deferral.Complete(); }
    }

    private async Task<SoftwareBitmap> RenderFileDragAsync(FileEntry[] entries)
    {
        var entry = entries[0];
        var content = new Grid { ColumnSpacing = 6 };
        content.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        content.Children.Add(new FileTypeIcon { Entry = entry, IconSize = 18 });
        var name = new TextBlock
        {
            Text = entry.Name + (entries.Length > 1 ? $"  ほか{entries.Length - 1}件" : ""),
            FontSize = 12, MaxWidth = 240, TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
        };
        Grid.SetColumn(name, 1); content.Children.Add(name);
        var visual = new Border
        {
            Background = (Brush)Application.Current.Resources["ApplicationPageBackgroundThemeBrush"],
            CornerRadius = new CornerRadius(5), Padding = new Thickness(7, 5, 7, 5), Child = content
        };
        DragPreviewHost.Child = visual; DragPreviewHost.Visibility = Visibility.Visible;
        try
        {
            DragPreviewHost.UpdateLayout();
            var render = new RenderTargetBitmap(); await render.RenderAsync(visual);
            return SoftwareBitmap.CreateCopyFromBuffer(await render.GetPixelsAsync(), BitmapPixelFormat.Bgra8,
                render.PixelWidth, render.PixelHeight, BitmapAlphaMode.Premultiplied);
        }
        finally { DragPreviewHost.Visibility = Visibility.Collapsed; DragPreviewHost.Child = null; }
    }
}
