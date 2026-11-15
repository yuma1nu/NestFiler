using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace FilerApp;
public sealed partial class MainWindow
{
    private bool previewSpaceDown;

    private async void PreviewSpaceKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Space) return;
        if (active?.IsRangeSelecting == true) { e.Handled = true; return; }
        var focused = FocusManager.GetFocusedElement(Root.XamlRoot) as DependencyObject;
        if (IsEditing(focused)) return;
        // Keep modal dialog input inside its own interaction scope.
        for (var node = focused; node != null; node = VisualTreeHelper.GetParent(node))
            if (node is ContentDialog) return;

        // The tunneling event runs before Button/ToggleButton/ListView handling.
        // Claim key-down and key-up so Space cannot also activate that control.
        e.Handled = true;
        previewSpaceDown = true;
        if (e.KeyStatus.WasKeyDown) return;
        previewVisible = !previewVisible;
        ScheduleSave();
        await UpdatePreviewAsync();
    }

    private void PreviewSpaceKeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Space || !previewSpaceDown) return;
        e.Handled = true;
        previewSpaceDown = false;
    }
}
