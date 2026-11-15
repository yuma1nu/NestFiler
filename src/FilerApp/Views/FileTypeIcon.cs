using FilerApp.Core;
using FilerApp.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
namespace FilerApp.Views;

public sealed class FileTypeIcon : Grid
{
    private readonly FontIcon fallback = new() { FontSize = 14, IsHitTestVisible = false };
    private readonly Image image = new() { Width = 16, Height = 16, IsHitTestVisible = false };
    private int revision;
    private CancellationTokenSource? thumbnailLoad;
    public static readonly DependencyProperty ShowThumbnailProperty = DependencyProperty.Register(nameof(ShowThumbnail), typeof(bool), typeof(FileTypeIcon), new PropertyMetadata(false, (owner, _) => ((FileTypeIcon)owner).Refresh()));
    public bool ShowThumbnail { get => (bool)GetValue(ShowThumbnailProperty); set => SetValue(ShowThumbnailProperty, value); }
    internal bool IsShowingThumbnail { get; private set; }
    public static readonly DependencyProperty EntryProperty = DependencyProperty.Register(nameof(Entry), typeof(FileEntry), typeof(FileTypeIcon), new PropertyMetadata(null, (owner, _) => ((FileTypeIcon)owner).Refresh()));
    public FileEntry? Entry { get => (FileEntry?)GetValue(EntryProperty); set => SetValue(EntryProperty, value); }
    public static readonly DependencyProperty IconSizeProperty = DependencyProperty.Register(nameof(IconSize), typeof(double), typeof(FileTypeIcon), new PropertyMetadata(16d, (owner, _) => { var icon = (FileTypeIcon)owner; icon.UpdateSize(); icon.Refresh(); }));
    public double IconSize { get => (double)GetValue(IconSizeProperty); set => SetValue(IconSizeProperty, value); }
    public FileTypeIcon()
    {
        UpdateSize(); VerticalAlignment = VerticalAlignment.Center;
        Children.Add(fallback); Children.Add(image);
        Loaded += (_, _) => Refresh();
        Unloaded += (_, _) => { revision++; thumbnailLoad?.Cancel(); thumbnailLoad?.Dispose(); thumbnailLoad = null; image.Source = null; IsShowingThumbnail = false; };
    }
    private void UpdateSize()
    {
        var size = Math.Clamp(IconSize, 12, 96);
        Width = Height = size + 2;
        image.Width = image.Height = size; fallback.FontSize = Math.Max(12, size - 2);
    }
    private async void Refresh()
    {
        thumbnailLoad?.Cancel(); thumbnailLoad?.Dispose(); thumbnailLoad = null; IsShowingThumbnail = false;
        var version = ++revision; image.Source = null; fallback.Visibility = Visibility.Visible;
        var entry = Entry; fallback.Glyph = entry?.Icon ?? "\uE8A5";
        if (entry == null || !IsLoaded) return;
        var cancellation = thumbnailLoad = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            PaneView? pane = null;
            for (var node = VisualTreeHelper.GetParent(this); node != null; node = VisualTreeHelper.GetParent(node)) if (node is PaneView owner) { pane = owner; break; }
            var extension = entry.IconExtension; int size = IconSize > 32 ? 48 : 32;
            var icon = FileIconService.TryGet(entry.IsDirectory, extension, size);
            if (icon == null)
            {
                if (pane != null) await pane.WhenVisualWorkAllowedAsync(cancellation.Token);
                icon = await FileIconService.GetAsync(entry.IsDirectory, extension, size).WaitAsync(cancellation.Token);
            }
            if (version != revision || !IsLoaded || cancellation.IsCancellationRequested) return;
            if (icon != null) { image.Source = icon; fallback.Visibility = Visibility.Collapsed; }
            if (!ShowThumbnail || !ThumbnailService.Supports(entry)) return;
            int edge = (int)Math.Ceiling(IconSize * (XamlRoot?.RasterizationScale ?? 1));
            var thumbnail = ThumbnailService.TryGet(entry, edge);
            if (thumbnail == null)
            {
                if (pane != null) await pane.WhenVisualWorkAllowedAsync(cancellation.Token);
                thumbnail = await ThumbnailService.GetAsync(entry, edge, cancellation.Token);
            }
            if (version != revision || !IsLoaded || cancellation.IsCancellationRequested || thumbnail == null) return;
            image.Source = thumbnail; fallback.Visibility = Visibility.Collapsed; IsShowingThumbnail = true;
        }
        catch (OperationCanceledException) { }
        finally { if (ReferenceEquals(thumbnailLoad, cancellation)) { thumbnailLoad = null; cancellation.Dispose(); } }
    }
}
