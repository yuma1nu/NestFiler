using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using FilerApp.Services;
using Microsoft.UI.Xaml.Media;

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
    private string? DropFolder(DependencyObject? source)
    {
        if (Current is not { IsSearch: false } tab) return null;
        while (source != null && source != Files)
        {
            if (source is ListViewItem row)
            {
                if (row.Content is Core.FileEntry { IsDirectory: true } folder) return folder.Path;
                break;
            }
            source = VisualTreeHelper.GetParent(source);
        }
        return tab.Path;
    }
    private void ClearDropTarget() { dropTarget?.ClearValue(Control.BackgroundProperty); dropTarget = null; }
    private void HighlightDropTarget(DependencyObject? source)
    {
        ListViewItem? target = null;
        while (source != null && source != Files) { if (source is ListViewItem row) { if (row.Content is Core.FileEntry { IsDirectory: true }) target = row; break; } source = VisualTreeHelper.GetParent(source); }
        if (target == dropTarget) return;
        ClearDropTarget(); dropTarget = target;
        if (target != null) target.Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
    }
    private void InitializeTransfer()
    {
        // Resolve the pressed row before WinUI starts its native list drag.
        Files.CanDragItems = true; Files.AllowDrop = true; FileArea.AllowDrop = true;
        Files.ItemPressed = PrepareTransferPress;
        Files.AddHandler(UIElement.PointerReleasedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler((_, _) => { pressedDragEntry = null; pressedDragSource = null; }), true);
        Files.PointerCanceled += (_, _) => { pressedDragEntry = null; pressedDragSource = null; };
        Files.ContainerContentChanging += (_, args) =>
        {
            if (args.InRecycleQueue) return;
            args.ItemContainer.Opacity = args.Item is Core.FileEntry entry && cutEntries.Contains(entry.Path) ? 0.45 : 1;
            if (args.Item is Core.FileEntry file)
            {
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(args.ItemContainer, file.Name);
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(args.ItemContainer, file.Path);
            }
        };
        FileArea.DragLeave += (_, _) => ClearDropTarget();
        Files.DragItemsCompleted += (_, _) => { fileDragInProgress = false; pressedDragEntry = null; pressedDragSource = null; ClearDropTarget(); RecheckFolder(); };
        Files.DragItemsStarting += (_, args) =>
        {
            if (IsRangeSelecting) { args.Cancel = true; return; }
            var selected = CaptureDragEntries(args.Items.OfType<Core.FileEntry>());
            if (selected.Length == 0) { args.Cancel = true; return; }
            fileDragInProgress = true;
            NoteListScroll();
            PopulateDragPackage(args.Data, selected);
        };
        FileArea.DragOver += (_, args) =>
        {
            if (!FileTransferData.ContainsFiles(args.DataView)) return;
            if (Current?.IsSearch == true) { args.AcceptedOperation = DataPackageOperation.None; args.Handled = true; return; }
            bool move = args.Modifiers.HasFlag(Windows.ApplicationModel.DataTransfer.DragDrop.DragDropModifiers.Shift);
            args.AcceptedOperation = move ? DataPackageOperation.Move : DataPackageOperation.Copy;
            var destination = DropFolder(args.OriginalSource as DependencyObject);
            HighlightDropTarget(args.OriginalSource as DependencyObject);
            args.DragUIOverride.Caption = (move ? "移動 → " : "コピー → ") + destination; args.Handled = true;
        };
        FileArea.Drop += async (_, args) =>
        {
            if (Current?.IsSearch == true) { args.AcceptedOperation = DataPackageOperation.None; args.Handled = true; return; }
            var deferral = args.GetDeferral();
            var destination = DropFolder(args.OriginalSource as DependencyObject);
            bool move = args.Modifiers.HasFlag(Windows.ApplicationModel.DataTransfer.DragDrop.DragDropModifiers.Shift);
            ClearDropTarget(); args.AcceptedOperation = DataPackageOperation.None; args.Handled = true;
            try
            {
                var paths = await FileTransferData.GetPathsAsync(args.DataView);
                if (Dropped is { } drop && destination != null)
                {
                    var results = await drop(paths, move, destination);
                    if (results.Count > 0 && results.All(x => x.Result == "成功")) args.AcceptedOperation = move ? DataPackageOperation.Move : DataPackageOperation.Copy;
                    // Partial success is not reported as a completed whole-package move.
                }
            }
            catch (Exception ex) { Error?.Invoke(ex.Message); }
            finally { deferral.Complete(); }
        };
    }
    private void PopulateDragPackage(DataPackage package, Core.FileEntry[] selected)
    {
        FileTransferData.SetPaths(package, selected.Select(x => x.Path).ToArray());
    }
}


