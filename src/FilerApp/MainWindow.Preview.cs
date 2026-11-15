using FilerApp.Views;
using Microsoft.UI.Xaml;
namespace FilerApp;
public sealed partial class MainWindow
{
    private QuickPreviewDialog? previewDialog;
    private string previewEncoding = "UTF-8";
    private bool previewVisible;
    private async Task UpdatePreviewAsync()
    {
        if (previewDialog is { } visible)
        {
            if (!previewVisible) visible.Hide();
            else await visible.ShowEntryAsync(active.Selected);
            return;
        }
        if (!previewVisible) return;
        if (active?.Selected == null) { previewVisible = false; Status.Text = "プレビューするファイルを選択してください。"; return; }
        var pane = active;
        var dialog = previewDialog = new QuickPreviewDialog(previewEncoding) { XamlRoot = Root.XamlRoot, RequestedTheme = Root.ActualTheme };
        dialog.FitTo(Root.ActualWidth, Root.ActualHeight);
        dialog.NavigateRequested += delta => pane.SelectRelativeFile(delta);
        dialog.EncodingChanged += value => { previewEncoding = value; ScheduleSave(); };
        dialog.SpaceReleased += () => previewSpaceDown = false;
        dialog.OpenedFile += file => RecordRecent(file.Path, file.IsDirectory);
        dialog.Opened += async (_, _) => await dialog.ShowEntryAsync(pane.Selected);
        try { await dialog.ShowAsync(); }
        catch (Exception ex) { ShowError("プレビューを開けません: " + ex.Message); }
        finally { if (ReferenceEquals(previewDialog, dialog)) previewDialog = null; previewVisible = false; previewSpaceDown = false; }
    }
}
