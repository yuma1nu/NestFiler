using FilerApp.Views;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace FilerApp;
public sealed partial class MainWindow
{
    private async void MouseNavigationPressed(object sender, PointerRoutedEventArgs args)
    {
        var kind = args.GetCurrentPoint(Root).Properties.PointerUpdateKind;
        if (kind is not (PointerUpdateKind.XButton1Pressed or PointerUpdateKind.XButton2Pressed)) return;
        // A modal preview/dialog owns its input; do not navigate behind it.
        for (var node = FocusManager.GetFocusedElement(Root.XamlRoot) as DependencyObject; node != null; node = VisualTreeHelper.GetParent(node))
            if (node is ContentDialog) return;
        args.Handled = true;
        try { await NavigateMouseHistoryAsync(active, kind == PointerUpdateKind.XButton2Pressed); }
        catch (Exception ex) { ShowError("マウスでの履歴移動に失敗しました: " + ex.Message); }
    }
    private async Task NavigateMouseHistoryAsync(PaneView pane, bool forward)
    {
        SetActive(pane);
        if (forward) await pane.GoForwardAsync(); else await pane.GoBackAsync();
    }
}
