using System.Diagnostics;
using FilerApp.Core;
using FilerApp.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
using Windows.System;

namespace FilerApp.Views;
public sealed partial class QuickPreviewDialog : ContentDialog
{
    private FileEntry? entry;
    private CancellationTokenSource? load;
    private int generation;
    private bool closed;
    internal bool IsOpen { get; private set; }
    public event Action<int>? NavigateRequested;
    public event Action<string>? EncodingChanged;
    public event Action<FileEntry>? OpenedFile;
    public event Action? SpaceReleased;
    public QuickPreviewDialog(string encoding)
    {
        InitializeComponent(); PreviewEncoding.SelectedIndex = encoding == "CP932" ? 1 : 0;
        PreviewKeyDown += (_, args) =>
        {
            var focused = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
            if (IsEditor(focused)) return;
            if (DismissKey(args.Key, args.KeyStatus.WasKeyDown)) { args.Handled = true; return; }
            var alt = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
            if (alt && args.Key is VirtualKey.Left or VirtualKey.Right) { args.Handled = true; NavigateRequested?.Invoke(args.Key == VirtualKey.Left ? -1 : 1); }
        };
        PreviewKeyUp += (_, args) => { if (args.Key == VirtualKey.Space) { args.Handled = true; SpaceReleased?.Invoke(); } };
        Opened += (_, _) => IsOpen = true;
        Closed += (_, _) => { IsOpen = false; closed = true; generation++; load?.Cancel(); PreviewImage.Source = null; TextPreview.Content = null; };
    }
    public void FitTo(double width, double height)
    {
        var outerWidth = Math.Clamp(width - 64, 320, 1120);
        var outerHeight = Math.Clamp(height - 64, 280, 780);
        Resources["ContentDialogMaxWidth"] = outerWidth;
        MaxWidth = outerWidth; MaxHeight = outerHeight;
        PreviewContent.Width = Math.Max(260, outerWidth - 56);
        PreviewContent.Height = Math.Max(140, outerHeight - 180);
    }
    private static bool IsEditor(DependencyObject? node)
    { for (; node != null; node = VisualTreeHelper.GetParent(node)) if (node is TextBox or PasswordBox or RichEditBox) return true; return false; }
    private bool DismissKey(VirtualKey key, bool repeat)
    {
        if (key is not (VirtualKey.Space or VirtualKey.Escape)) return false;
        if (!repeat) Hide(); return true;
    }
    public async Task ShowEntryAsync(FileEntry? selected)
    {
        if (closed) return;
        entry = selected; load?.Cancel(); var version = ++generation;
        PreviewImage.Source = null; TextPreview.Content = null; TextPreview.Visibility = Visibility.Collapsed;
        IsPrimaryButtonEnabled = selected != null;
        if (selected == null) { Title = "クイックプレビュー"; PreviewPath.Text = ""; PreviewInfo.Text = "ファイルを選択してください。"; return; }
        Title = selected.Name; PreviewPath.Text = selected.Path; ToolTipService.SetToolTip(PreviewPath, selected.Path);
        PreviewInfo.Text = $"{selected.Kind} · {selected.SizeLabel} · 更新 {selected.ModifiedLabel}";
        var textProvider = new TextPreviewProvider((string?)PreviewEncoding.SelectedItem ?? "UTF-8");
        IPreviewProvider? provider = textProvider.Supports(selected) ? textProvider : new ImagePreviewProvider().Supports(selected) ? new ImagePreviewProvider() : null;
        if (provider == null) { PreviewInfo.Text += " — この形式のプレビューには未対応です。"; return; }
        using var cancellation = load = new CancellationTokenSource(); var token = cancellation.Token;
        try
        {
            var result = await provider.ReadAsync(selected, token).WaitAsync(TimeSpan.FromSeconds(10), token);
            if (closed || version != generation || token.IsCancellationRequested) return;
            PreviewInfo.Text = result.Description; RenderTextPreview(result);
            if (result.Image == null) return;
            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream.GetOutputStreamAt(0))) { writer.WriteBytes(result.Image); await writer.StoreAsync(); }
            stream.Seek(0); var bitmap = new BitmapImage { DecodePixelWidth = 1600 }; await bitmap.SetSourceAsync(stream);
            if (!closed && version == generation && !token.IsCancellationRequested) PreviewImage.Source = bitmap;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!closed && version == generation && !token.IsCancellationRequested) PreviewInfo.Text = "プレビューできません: " + ex.Message; }
        finally { cancellation.Cancel(); if (ReferenceEquals(load, cancellation)) load = null; }
    }
    private void Previous(object sender, RoutedEventArgs e) => NavigateRequested?.Invoke(-1);
    private void Next(object sender, RoutedEventArgs e) => NavigateRequested?.Invoke(1);
    private async void PreviewEncodingChanged(object sender, SelectionChangedEventArgs e)
    { if (PreviewEncoding.SelectedItem is string value) { EncodingChanged?.Invoke(value); if (entry != null) await ShowEntryAsync(entry); } }
    private async void OpenExternal(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true; if (entry is not { } selected) return;
        try { await Task.Run(() => Process.Start(new ProcessStartInfo(selected.Path) { UseShellExecute = true })); OpenedFile?.Invoke(selected); Hide(); }
        catch (Exception ex) { PreviewInfo.Text = "開けません: " + ex.Message; }
    }
#if DEBUG
    internal bool DebugDismiss(VirtualKey key) => DismissKey(key, false);
    internal bool DebugHasImage => PreviewImage.Source != null;
    internal bool DebugHasText => TextPreview.Content != null;
    internal string DebugPath => PreviewPath.Text;
    internal object? DebugTextContent => TextPreview.Content;
    internal string DebugDescription => PreviewInfo.Text;
#endif
}
