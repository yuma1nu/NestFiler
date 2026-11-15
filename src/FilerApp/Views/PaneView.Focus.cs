using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
namespace FilerApp.Views;
public sealed partial class PaneView
{
    private bool isActivePane;
    private object? focusOperation;
#if DEBUG
    internal Action<string>? DebugFocusTrace;
    private void TraceFocus(string phase)
    {
        if (DebugFocusTrace == null) return;
        var focused = FocusManager.GetFocusedElement(XamlRoot) as FrameworkElement;
        DebugFocusTrace?.Invoke($"TRACE {Name} {phase}: {focused?.GetType().Name}/{focused?.Name} [{focused?.FocusState}]");
    }
#endif

    public bool ContainsFocus()
    {
        var focused = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
        while (focused != null)
        {
            if (ReferenceEquals(focused, this)) return true;
            focused = VisualTreeHelper.GetParent(focused);
        }
        return false;
    }

    private async Task WithStableFocusAsync(Func<Task> operation)
    {
        CancelRangeSelection(true);
        CapturePosition();
        var originalTab = Current; var originalEntries = Current?.Entries; var originalPath = Current?.Path;
        var owner = focusOperation = new object();
#if DEBUG
        TraceFocus("before anchor");
#endif
        // Removing a focused row or breadcrumb otherwise lets WinUI find a new
        // focus target outside this pane. The refresh control survives collection changes.
        var editing = FocusManager.GetFocusedElement(XamlRoot) is Microsoft.UI.Xaml.Controls.TextBox or Microsoft.UI.Xaml.Controls.PasswordBox or Microsoft.UI.Xaml.Controls.RichEditBox;
        if (ContainsFocus() && !editing) NavigationRefresh.Focus(FocusState.Programmatic);
#if DEBUG
        TraceFocus("after anchor");
#endif
        try { await operation(); }
        finally
        {
            if (ReferenceEquals(focusOperation, owner))
            {
                if (Current != originalTab || Current?.Path != originalPath || !ReferenceEquals(Current?.Entries, originalEntries)) await RestorePositionAsync();
                ConfigureWatcher();
            }
#if DEBUG
            TraceFocus("before restore");
#endif
            // Never take focus back if the user switched panes or began editing
            // another control while enumeration was in progress.
            if (ReferenceEquals(focusOperation, owner) && isActivePane &&
                ReferenceEquals(FocusManager.GetFocusedElement(XamlRoot), NavigationRefresh))
                FocusFiles();
#if DEBUG
            TraceFocus("after restore");
#endif
        }
    }

    private void ActivatePaneFocus(object sender, RoutedEventArgs e)
    {
#if DEBUG
        TraceFocus("GotFocus");
#endif
        // Pointer activation is owned by PointerPressed. WinUI can retain a
        // previous Pointer focus state during programmatic collection updates.
        // Only a real Tab traversal should activate a pane through GotFocus.
        if (FocusManager.GetFocusedElement(XamlRoot) is UIElement focused &&
            focused.FocusState == FocusState.Keyboard &&
            Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Tab).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
            Activated?.Invoke(this);
    }
#if DEBUG
    internal async Task DebugOpenFolderAsync(string path)
    {
        await Task.Delay(50);
        Files.SelectedItem = Current!.Entries.Single(x => x.Path == path);
        Files.ScrollIntoView(Files.SelectedItem); Files.UpdateLayout();
        if (Files.ContainerFromItem(Files.SelectedItem) is not Microsoft.UI.Xaml.Controls.ListViewItem row)
            throw new InvalidOperationException("Test folder row was not realized");
        row.Focus(FocusState.Pointer);
        await Task.Yield();
        if (!ContainsFocus()) throw new InvalidOperationException("Test did not start with focus in the source pane");
        await OpenAsync();
    }
    internal Task DebugWaitWithStableFocusAsync(Task pending) => WithStableFocusAsync(() => pending);
    internal bool DebugContainsFocus() => ContainsFocus();
#endif
}

