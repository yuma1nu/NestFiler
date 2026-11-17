using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using FilerApp.Services;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.ApplicationModel.DataTransfer.DragDrop;

namespace FilerApp.Views;
public sealed partial class PaneView
{
    public string[] SelectedPaths => SelectedEntries().Select(x => x.Path).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    public event Func<string[], bool, string, Task<List<OperationResult>>>? Dropped;
    private HashSet<string> cutEntries = new(StringComparer.OrdinalIgnoreCase);
    private ListViewItem? dropTarget;
    private Core.FileEntry? pressedDragEntry;
    private object? pressedDragSource;
    private bool fileDragInProgress;
    private Core.FileEntry[] dragSnapshot = [];
    public bool IsFileDragActive => fileDragInProgress || pressedDragEntry != null;
    private void PrepareTransferPress(ListViewItem row, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
    {
        if (!args.GetCurrentPoint(row).Properties.IsLeftButtonPressed || row.Content is not Core.FileEntry entry) return;
        pressedDragEntry = entry; pressedDragSource = Files.ItemsSource;
        bool modified = args.KeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Control) || args.KeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Shift);
        PrepareTransferSelection(entry, modified);
    }
    private void PrepareTransferSelection(Core.FileEntry entry, bool modified)
    {
        // WinUI normally finalizes selection on release. A drag starts earlier,
        // so an unselected pressed row must replace the old selection now.
        if (modified || Files.ContainerFromItem(entry) is ListViewItem { IsSelected: true }) return;
        var index = Files.Items.IndexOf(entry);
        if (index < 0) return;
        Files.DeselectRange(new Microsoft.UI.Xaml.Data.ItemIndexRange(0, (uint)Files.Items.Count));
        Files.SelectedIndex = index;
    }
    private Core.FileEntry[] CaptureDragEntries(IEnumerable<Core.FileEntry> nativeItems)
    {
        var items = nativeItems.DistinctBy(x => x.Path, StringComparer.OrdinalIgnoreCase).ToArray();
        if (pressedDragEntry is { } pressed && ReferenceEquals(pressedDragSource, Files.ItemsSource) && !items.Any(x => string.Equals(x.Path, pressed.Path, StringComparison.OrdinalIgnoreCase))) return [pressed];
        return items;
    }
    public void SetCutPaths(IEnumerable<string> paths) { cutEntries = paths.ToHashSet(StringComparer.OrdinalIgnoreCase); UpdateCutAppearance(); }
    private void UpdateCutAppearance()
    {
        if (Files.ItemsPanelRoot == null) return;
        foreach (var row in Files.ItemsPanelRoot.Children.OfType<ListViewItem>())
            row.Opacity = row.Content is Core.FileEntry entry && cutEntries.Contains(entry.Path) ? 0.45 : 1;
    }
    private ListViewItem? DropRowAt(Point point)
    {
        if (point.X < 0 || point.Y < 0 || point.X >= Files.ActualWidth || point.Y >= Files.ActualHeight || Files.ItemsPanelRoot == null) return null;
        foreach (var row in Files.ItemsPanelRoot.Children.OfType<ListViewItem>())
        {
            var origin = row.TransformToVisual(Files).TransformPoint(new Point());
            if (point.X >= origin.X && point.X < origin.X + row.ActualWidth && point.Y >= origin.Y && point.Y < origin.Y + row.ActualHeight) return row;
        }
        return null;
    }
    private string? DropFolder(Point point) => Current is not { IsSearch: false } tab ? null :
        DropRowAt(point)?.Content is Core.FileEntry { IsDirectory: true } folder ? folder.Path : tab.Path;
    private bool MoveDrop(DragDropModifiers modifiers) => !modifiers.HasFlag(DragDropModifiers.Control) &&
        (modifiers.HasFlag(DragDropModifiers.Shift) || fileDragInProgress);
    private bool ValidDrop(string? folder)
    {
        if (folder == null) return false;
        return !fileDragInProgress || !dragSnapshot.Any(entry => entry.IsDirectory &&
            (string.Equals(Path.TrimEndingDirectorySeparator(entry.Path), Path.TrimEndingDirectorySeparator(folder), StringComparison.OrdinalIgnoreCase) ||
             folder.StartsWith(Path.TrimEndingDirectorySeparator(entry.Path) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)));
    }
    private void ClearDropTarget() { dropTarget?.ClearValue(Control.BackgroundProperty); dropTarget = null; }
    private void HighlightDropTarget(Point point)
    {
        var target = DropRowAt(point);
        if (target?.Content is not Core.FileEntry { IsDirectory: true }) target = null;
        if (target == dropTarget) return;
        ClearDropTarget(); dropTarget = target;
        if (target != null) target.Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
    }
    private void InitializeTransfer()
    {
        // Resolve the pressed row before WinUI starts its native list drag.
        Files.CanDragItems = true; Files.AllowDrop = true; FileArea.AllowDrop = true;
        Files.DragStarting += (_, args) => args.AllowedOperations = DataPackageOperation.Copy | DataPackageOperation.Move;
        Files.ItemPressed = PrepareTransferPress;
        Files.AddHandler(UIElement.PointerReleasedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler((_, _) => { pressedDragEntry = null; pressedDragSource = null; }), true);
        Files.PointerCanceled += (_, _) => { pressedDragEntry = null; pressedDragSource = null; };
        Files.ContainerContentChanging += (_, args) =>
        {
            if (args.InRecycleQueue) return;
            args.ItemContainer.AllowDrop = args.Item is Core.FileEntry { IsDirectory: true };
            args.ItemContainer.Opacity = args.Item is Core.FileEntry entry && cutEntries.Contains(entry.Path) ? 0.45 : 1;
            if (args.Item is Core.FileEntry file)
            {
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(args.ItemContainer, file.Name);
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(args.ItemContainer, file.Path);
            }
        };
        FileArea.DragLeave += (_, _) => ClearDropTarget();
        Files.DragItemsCompleted += (_, _) => { fileDragInProgress = false; dragSnapshot = []; pressedDragEntry = null; pressedDragSource = null; ClearDropTarget(); RecheckFolder(); };
        Files.DragItemsStarting += (_, args) =>
        {
            if (IsRangeSelecting) { args.Cancel = true; return; }
            var selected = CaptureDragEntries(args.Items.OfType<Core.FileEntry>());
            if (selected.Length == 0) { args.Cancel = true; return; }
            fileDragInProgress = true;
            dragSnapshot = selected;
            NoteListScroll();
            PopulateDragPackage(args.Data, selected);
        };
        // ListView handles some self-drags internally. Observe handled events
        // too, and hit-test the realized row geometries instead of OriginalSource.
        FileArea.AddHandler(UIElement.DragOverEvent, new DragEventHandler((_, args) =>
        {
            if (!FileTransferData.ContainsFiles(args.DataView)) return;
            if (Current?.IsSearch == true) { args.AcceptedOperation = DataPackageOperation.None; args.Handled = true; return; }
            bool move = MoveDrop(args.Modifiers);
            var point = args.GetPosition(Files); var destination = DropFolder(point);
            args.AcceptedOperation = !ValidDrop(destination) ? DataPackageOperation.None : move ? DataPackageOperation.Move : DataPackageOperation.Copy;
            if (args.AcceptedOperation == DataPackageOperation.None) { ClearDropTarget(); args.DragUIOverride.Caption = "自分自身やその中へは配置できません"; args.Handled = true; return; }
            HighlightDropTarget(point);
            args.DragUIOverride.Caption = (move ? "移動 → " : "コピー → ") + destination; args.Handled = true;
        }), true);
        FileArea.AddHandler(UIElement.DropEvent, new DragEventHandler(async (_, args) =>
        {
            if (Current?.IsSearch == true || !FileTransferData.ContainsFiles(args.DataView)) { args.AcceptedOperation = DataPackageOperation.None; args.Handled = true; return; }
            var deferral = args.GetDeferral();
            ClearDropTarget(); args.AcceptedOperation = DataPackageOperation.None; args.Handled = true;
            try
            {
                args.AcceptedOperation = await DropFilesAtAsync(args.DataView, args.GetPosition(Files), args.Modifiers);
            }
            catch (Exception ex) { Error?.Invoke(ex.Message); }
            finally { deferral.Complete(); }
        }), true);
    }
    private async Task<DataPackageOperation> DropFilesAtAsync(DataPackageView data, Point point, DragDropModifiers modifiers)
    {
        if (Current?.IsSearch == true || !FileTransferData.ContainsFiles(data)) return DataPackageOperation.None;
        var destination = DropFolder(point); bool move = MoveDrop(modifiers);
        if (!ValidDrop(destination) || Dropped is not { } drop) return DataPackageOperation.None;
        var paths = await FileTransferData.GetPathsAsync(data);
        var results = await drop(paths, move, destination!);
        // Partial success is never reported as a whole-package move.
        return results.Count > 0 && results.All(x => x.Result == "成功") ? move ? DataPackageOperation.Move : DataPackageOperation.Copy : DataPackageOperation.None;
    }
    private void PopulateDragPackage(DataPackage package, Core.FileEntry[] selected)
    {
        FileTransferData.SetPaths(package, selected.Select(x => x.Path).ToArray());
    }
}


