using FilerApp.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
namespace FilerApp.Views;
public sealed partial class PaneView
{
    public event Action<PaneView, Point, bool>? ContextMenuRequested;
    private void InitializeFileContext()
    {
        Files.RightTapped += (_, args) =>
        {
            var node = args.OriginalSource as DependencyObject;
            while (node != null && node != Files && node is not ListViewItem) node = VisualTreeHelper.GetParent(node);
            if (node is ListViewItem item && Files.ItemFromContainer(item) is FileEntry entry)
            { if (!Files.SelectedItems.Contains(entry)) { Files.SelectedItems.Clear(); Files.SelectedItem = entry; } }
            else Files.SelectedItems.Clear();
            args.Handled = true; Activated?.Invoke(this);
            var native = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
            ContextMenuRequested?.Invoke(this, args.GetPosition(Files), native);
        };
    }
    public void ShowFileContext(MenuFlyout menu, Point point) => menu.ShowAt(Files, new Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowOptions { Position = point });
    public void RequestKeyboardContext(bool native) { Activated?.Invoke(this); ContextMenuRequested?.Invoke(this, new Point(Math.Min(80, Files.ActualWidth / 2), 20), native); }
}
