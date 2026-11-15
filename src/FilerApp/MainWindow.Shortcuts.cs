using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
namespace FilerApp;
public sealed partial class MainWindow
{
    private static bool KeyDown(VirtualKey key) => Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
    private string? ListingCommand(VirtualKey key, bool control, bool shift)
    {
        if (!CanPasteIntoListing() || !active.ContainsFocus()) return null;
        if (!control && shift && key == VirtualKey.Insert) return "paste";
        return control && shift ? key switch { VirtualKey.N => "mkdir", VirtualKey.C => "copyPath", _ => null } :
            control ? key switch { VirtualKey.C => "copy", VirtualKey.X => "cut", VirtualKey.V => "paste", VirtualKey.A => "selectAll", _ => null } :
            key switch { VirtualKey.F2 => "rename", VirtualKey.Delete => "recycle", _ => null };
    }
    private async void RootKey(object sender, KeyRoutedEventArgs e)
    {
        if (e.Handled) return;
        if (active.IsRangeSelecting) { e.Handled = true; if (e.Key == VirtualKey.Escape) active.CancelRangeSelection(true); return; }
        var focused = FocusManager.GetFocusedElement(Root.XamlRoot) as DependencyObject;
        for (var node = focused; node != null; node = VisualTreeHelper.GetParent(node)) if (node is ContentDialog) return;
        bool control = KeyDown(VirtualKey.Control), shift = KeyDown(VirtualKey.Shift), alt = KeyDown(VirtualKey.Menu), editing = IsEditing(focused);
        bool inPane = active.ContainsFocus();
        var command = ListingCommand(e.Key, control, shift);
        if (command != null) { e.Handled = true; if (!e.KeyStatus.WasKeyDown) { if (command == "selectAll") active.SelectAllFiles(); else await CommandAsync(command); } return; }
        if (control && shift && e.Key == VirtualKey.T) { e.Handled = true; if (!e.KeyStatus.WasKeyDown) await active.RestoreClosedTabAsync(); }
        else if (control && e.Key is VirtualKey.W or VirtualKey.F4) { e.Handled = true; if (!e.KeyStatus.WasKeyDown) active.CloseCurrent(); }
        else if (control && e.Key == VirtualKey.T) { e.Handled = true; if (!e.KeyStatus.WasKeyDown) await active.NavigateAsync(active.Current?.Path ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), true); }
        else if (control && e.Key == VirtualKey.Tab) { e.Handled = true; active.CycleTab(shift ? -1 : 1); }
        else if (control && e.Key is VirtualKey.PageUp or VirtualKey.PageDown) { e.Handled = true; active.CycleTab(e.Key == VirtualKey.PageUp ? -1 : 1); }
        else if (control && e.Key is >= VirtualKey.Number1 and <= VirtualKey.Number9) { e.Handled = true; active.SelectTab((int)e.Key - (int)VirtualKey.Number0); }
        else if (control && e.Key == VirtualKey.L || alt && e.Key == VirtualKey.D) { e.Handled = true; active.FocusAddress(); }
        else if (control && shift && e.Key == VirtualKey.F) { e.Handled = true; active.FocusFilter(); }
        else if (control && e.Key == VirtualKey.F) { e.Handled = true; ShowPaneSearch(active, ""); }
        else if (control && e.Key == VirtualKey.D && !editing) { e.Handled = true; if (!e.KeyStatus.WasKeyDown) await CommandAsync("favorite"); }
        else if (alt && e.Key == VirtualKey.Left || !control && !alt && !editing && inPane && e.Key == VirtualKey.Back) { e.Handled = true; await active.GoBackAsync(); }
        else if (alt && e.Key == VirtualKey.Right) { e.Handled = true; await active.GoForwardAsync(); }
        else if (alt && e.Key == VirtualKey.Up) { e.Handled = true; await active.GoUpAsync(); }
        else if (e.Key == VirtualKey.F5 || control && e.Key == VirtualKey.R) { e.Handled = true; await active.RefreshAsync(); }
        else if (e.Key == VirtualKey.F6) { e.Handled = true; SetActive(active == LeftPane && RightPane.Visibility == Visibility.Visible ? RightPane : LeftPane); active.FocusFiles(); }
        else if (!editing && inPane && (e.Key == VirtualKey.Application || shift && e.Key == VirtualKey.F10)) { e.Handled = true; active.RequestKeyboardContext(control); }
        else if (e.Key == VirtualKey.Escape && !editing) { e.Handled = true; previewVisible = false; await UpdatePreviewAsync(); }
    }
}
