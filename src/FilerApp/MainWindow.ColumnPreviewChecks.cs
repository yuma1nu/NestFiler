#if DEBUG
using FilerApp.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace FilerApp;
public sealed partial class MainWindow
{
    private async Task RunColumnPreviewChecksAsync(string root, List<string> results)
    {
        var folder = Directory.CreateDirectory(Path.Combine(root, "columns-preview")).FullName;
        var picture = Path.Combine(folder, "01-image.png"); File.Copy(Path.Combine(root, "search-thumbnails", "needle-image.png"), picture);
        var csv = Path.Combine(folder, "02-data.csv"); await File.WriteAllTextAsync(csv, "name,value\nA,12");
        await File.WriteAllTextAsync(Path.Combine(folder, "03-note.txt"), "preview text");
        var unsupported = Path.Combine(folder, "04-cad.dwg"); await File.WriteAllTextAsync(unsupported, "unsupported preview fixture");
        await LeftPane.NavigateAsync(folder); SetActive(LeftPane); LeftPane.SelectAllFiles();
        var rightNormal = RightPane.NormalColumns.Capture(); var rightSearch = RightPane.SearchColumns.Capture();
        await LeftPane.DebugCheckColumnsAsync(false);
        var normal = LeftPane.NormalColumns.Capture();
        results.Add("PASS normal columns: resize/reorder applied to headers and single-line rows, horizontal scroll synchronized, selection retained");
        var search = LeftPane.OpenSearchTab("02-data"); await search.SearchWork!;
        await LeftPane.DebugCheckColumnsAsync(true);
        if (!LeftPane.NormalColumns.Capture().Columns.SequenceEqual(normal.Columns) || !RightPane.NormalColumns.Capture().Columns.SequenceEqual(rightNormal.Columns) || !RightPane.SearchColumns.Capture().Columns.SequenceEqual(rightSearch.Columns)) throw new InvalidOperationException("Changing search columns affected normal or other pane settings");
        results.Add("PASS search columns: dedicated match/line/path, independent order and widths, one-line rows and synchronized scrolling");
        LeftPane.CloseCurrent(); await Task.Delay(50); LeftPane.DebugSelectFile(picture); SetActive(LeftPane);
        if (Root.FindName("PreviewPanel") != null) throw new InvalidOperationException("Bottom preview panel still exists");
        previewVisible = true; var shown = UpdatePreviewAsync();
        for (int i = 0; i < 60 && previewDialog?.DebugHasImage != true; i++) await Task.Delay(50);
        if (previewDialog is not { IsOpen: true, DebugHasImage: true } dialog || dialog.DebugPath != picture) throw new InvalidOperationException("Modal image preview did not load");
        using (var exclusive = new FileStream(picture, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        dialog.DebugDismiss(VirtualKey.Space); await shown.WaitAsync(TimeSpan.FromSeconds(5));
        if (previewDialog != null || previewVisible || active != LeftPane) throw new InvalidOperationException("Space did not close modal preview cleanly");
        results.Add("PASS modal image preview displayed, source unlocked, Space closes and returns to original pane");
        LeftPane.DebugSelectFile(picture); previewVisible = true; shown = UpdatePreviewAsync();
        for (int i = 0; i < 30 && previewDialog?.IsOpen != true; i++) await Task.Delay(30);
        LeftPane.SelectRelativeFile(1);
        for (int i = 0; i < 40 && previewDialog?.DebugHasText != true; i++) await Task.Delay(50);
        if (previewDialog?.DebugPath != csv || previewDialog.DebugTextContent is not Grid table || table.RowDefinitions.Count != 2 || table.ColumnDefinitions.Count != 2) throw new InvalidOperationException("Modal preview did not follow selection and render CSV table");
        previewDialog.DebugDismiss(VirtualKey.Escape); await shown.WaitAsync(TimeSpan.FromSeconds(5));
        results.Add("PASS modal preview follows previous/next selection, CSV renders as table, Esc closes");
        LeftPane.DebugSelectFile(unsupported); previewVisible = true; shown = UpdatePreviewAsync();
        for (int i = 0; i < 30 && previewDialog?.IsOpen != true; i++) await Task.Delay(30);
        if (previewDialog?.DebugDescription.Contains("未対応") != true || previewDialog.DebugHasImage || previewDialog.DebugHasText) throw new InvalidOperationException("Unsupported format did not show basic information");
        previewDialog.DebugDismiss(VirtualKey.Escape); await shown.WaitAsync(TimeSpan.FromSeconds(5));
        results.Add("PASS unsupported format shows basic information in modal preview without stale content");
    }
}
#endif
