#if DEBUG
using FilerApp.Core;
using FilerApp.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.DragDrop;
using Windows.Foundation;

namespace FilerApp.Views;
public sealed partial class PaneView
{
    internal async Task DebugCheckFolderDropsAsync(string root, List<string> results, PaneView other)
    {
        var target = Path.Combine(root, "TARGET");
        Point TargetPoint()
        {
            var entry = Current!.Entries.Single(x => x.Path == target);
            Files.ScrollIntoView(entry, ScrollIntoViewAlignment.Leading); Files.UpdateLayout();
            if (Files.ContainerFromItem(entry) is not ListViewItem row) throw new InvalidOperationException("Target folder row is not realized");
            return row.TransformToVisual(Files).TransformPoint(new Point(Math.Max(2, row.ActualWidth - 2), row.ActualHeight / 2));
        }
        try
        {
            foreach (var mode in Enum.GetValues<FileViewMode>())
            {
                SetDisplayMode(mode); await Task.Delay(80);
                var point = TargetPoint(); var row = DropRowAt(point)!;
                var top = row.TransformToVisual(Files).TransformPoint(new Point(2, 2));
                var bottom = row.TransformToVisual(Files).TransformPoint(new Point(2, row.ActualHeight - 2));
                if (row.AllowDrop || Files.AllowDrop || Files.CanDragItems || !FileArea.AllowDrop || DropFolder(point) != target || DropFolder(top) != target || DropFolder(bottom) != target || DropFolder(new Point(-1, -1)) != root)
                    throw new InvalidOperationException("Folder row whitespace/edges resolve to the current folder in " + mode);
                results.Add("PASS " + mode + " folder row edges and right whitespace resolve by coordinates; background resolves to current folder");
            }
            fileDragInProgress = false;
            bool externalCopy = !MoveDrop(DragDropModifiers.None) && MoveDrop(DragDropModifiers.Shift);
            fileDragInProgress = true;
            if (!externalCopy || !MoveDrop(DragDropModifiers.None) || MoveDrop(DragDropModifiers.Control)) throw new InvalidOperationException("Drop operation modifier rules are wrong");
            results.Add("PASS same-pane drag defaults to Move and Ctrl selects Copy; external/other pane defaults to Copy and Shift selects Move");
            SetDisplayMode(FileViewMode.Details); await Task.Delay(50);
            var source = Path.Combine(root, "move-me.txt");
            dragSnapshot = [Current!.Entries.Single(x => x.Path == source)];
            var package = new DataPackage(); PopulateDragPackage(package, dragSnapshot);
            if (package.RequestedOperation != DataPackageOperation.Move || !FileDragOperations.HasFlag(DataPackageOperation.Copy) || !FileDragOperations.HasFlag(DataPackageOperation.Move) || FileTransferData.ReadNativePaths(package)?.Single() != source || DropCaption(target, true) != "移動 → TARGET" || DropCaption(target, false) != "コピー → TARGET")
                throw new InvalidOperationException($"Drag package/caption mismatch: requested={package.RequestedOperation}; paths={string.Join(",", FileTransferData.ReadNativePaths(package) ?? [])}; move={DropCaption(target, true)}; copy={DropCaption(target, false)}");
            using (var visual = await RenderFileDragAsync(dragSnapshot))
            {
                using var output = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, output);
                encoder.SetSoftwareBitmap(visual); await encoder.FlushAsync();
                using var reader = new Windows.Storage.Streams.DataReader(output.GetInputStreamAt(0));
                await reader.LoadAsync((uint)output.Size); var bytes = new byte[output.Size]; reader.ReadBytes(bytes);
                await File.WriteAllBytesAsync(Path.Combine(Services.AppLog.DataDirectory, "drag-item-preview.png"), bytes);
                if (visual.PixelWidth > 600 || visual.PixelHeight > 120) throw new InvalidOperationException("Drag preview unexpectedly contains a full details row");
            }
            results.Add("PASS actual drag package offers Copy and Move with original paths; compact icon/name preview and destination-name-only captions");
            var selection = SelectedPaths;
            var oldRow = DropRowAt(TargetPoint())!;
            Files.SetDragFeedback(true); HighlightDropTarget(TargetPoint());
            HighlightDropTarget(new Point(-1, -1)); Files.SetDragFeedback(false);
            if (dropTarget != null || oldRow.ReadLocalValue(Control.BackgroundProperty) != DependencyProperty.UnsetValue || !selection.SequenceEqual(SelectedPaths))
                throw new InvalidOperationException("Drop feedback left a row highlight or changed selection");
            results.Add("PASS target feedback is cleared when crossing rows/leaving, without changing file selection");
            var plannedDestination = DropFolder(TargetPoint()); var plannedMove = MoveDrop(DragDropModifiers.None);
            fileDragInProgress = false; dragSnapshot = [];
            var operation = await DropFilesToAsync(package.GetView(), plannedDestination, plannedMove);
            if (operation != DataPackageOperation.Move || File.Exists(source) || !File.Exists(Path.Combine(target, "move-me.txt")) || Current!.Path != root) throw new InvalidOperationException("Same-pane drop did not physically move into the folder");
            results.Add("PASS drop keeps its captured Move after source drag state ends, and physically moves into the hovered folder");
            fileDragInProgress = true;
            source = Path.Combine(root, "copy-me.txt"); dragSnapshot = [Current!.Entries.Single(x => x.Path == source)];
            package = new DataPackage(); FileTransferData.SetPaths(package, [source]);
            operation = await DropFilesAtAsync(package.GetView(), TargetPoint(), DragDropModifiers.Control);
            if (operation != DataPackageOperation.Copy || !File.Exists(source) || !File.Exists(Path.Combine(target, "copy-me.txt"))) throw new InvalidOperationException("Ctrl drop did not copy and retain the source");
            results.Add("PASS Ctrl folder drop copies fixture into hovered folder and retains source");
            source = Path.Combine(root, "move-folder"); dragSnapshot = [Current!.Entries.Single(x => x.Path == source)];
            package = new DataPackage(); PopulateDragPackage(package, dragSnapshot);
            operation = await DropFilesAtAsync(package.GetView(), TargetPoint(), DragDropModifiers.None);
            if (operation != DataPackageOperation.Move || Directory.Exists(source) || await File.ReadAllTextAsync(Path.Combine(target, "move-folder", "nested.txt")) != "nested fixture")
                throw new InvalidOperationException("Folder drop did not move the folder with its contents");
            results.Add("PASS folder drop physically moves the folder and nested contents, removing the source only on success");
            dragSnapshot = [Current!.Entries.Single(x => x.Path == target)];
            package = new DataPackage(); FileTransferData.SetPaths(package, [target]);
            operation = await DropFilesAtAsync(package.GetView(), TargetPoint(), DragDropModifiers.None);
            if (operation != DataPackageOperation.None || Directory.Exists(Path.Combine(target, "TARGET"))) throw new InvalidOperationException("Folder accepted a drop onto itself");
            results.Add("PASS folder self-drop is rejected before any filesystem operation");
            dragSourcePane = this;
            if (other.ValidDrop(target) || other.ValidDrop(Path.Combine(target, "descendant")) || other.MoveDrop(DragDropModifiers.None) || !other.MoveDrop(DragDropModifiers.Shift))
                throw new InvalidOperationException("Cross-pane target lost the source folder guard or modifier rules");
            dragSourcePane = null;
            results.Add("PASS cross-pane drag shares self/descendant guards while keeping default Copy and Shift Move");
            fileDragInProgress = false; dragSnapshot = [];
            var search = OpenSearchTab("copy", new SearchOptions(root, "copy", false, false, false, false, "", "", "UTF-8")); await search.SearchWork!;
            operation = await DropFilesAtAsync(package.GetView(), new Point(2, 2), DragDropModifiers.None);
            if (operation != DataPackageOperation.None) throw new InvalidOperationException("Search results accepted a drop");
            results.Add("PASS search results remain a non-drop destination even with handled-event routing");
        }
        finally { fileDragInProgress = false; dragSnapshot = []; dragSourcePane = null; DragSessionEnded?.Invoke(); ClearDropTarget(); }
    }
}
#endif
