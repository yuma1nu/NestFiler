using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace FilerApp.Views;

// Marquee starts on list background/gutter. Every part of an item retains native
// click, Shift/Ctrl selection, double-click and file-drag behavior.
public sealed class FileListView : ListView
{
    internal Func<PointerRoutedEventArgs, bool>? BeginMarquee;
    internal Func<PointerRoutedEventArgs, bool>? MoveMarquee;
    internal Func<PointerRoutedEventArgs, bool>? ReleaseMarquee;
    internal Action<ListViewItem, PointerRoutedEventArgs>? ItemPressed;
    private bool blankPress;
    internal void SuppressBackgroundTap() => blankPress = true;
    public FileListView() { DefaultStyleKey = typeof(ListView); ItemContainerTransitions = new Microsoft.UI.Xaml.Media.Animation.TransitionCollection(); }
    protected override DependencyObject GetContainerForItemOverride() => new FileListItem(this);
    // FileEntry is a managed data object, never a container. Resolve this in
    // managed code instead of making WinUI probe its WinRT interfaces per item.
    protected override bool IsItemItsOwnContainerOverride(object item) => item is ListViewItem;
    protected override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        if (!e.Handled) blankPress = BeginMarquee?.Invoke(e) == true;
        if (!blankPress) base.OnPointerPressed(e);
    }
    protected override void OnPointerMoved(PointerRoutedEventArgs e) { if (MoveMarquee?.Invoke(e) == true) return; base.OnPointerMoved(e); }
    protected override void OnPointerReleased(PointerRoutedEventArgs e) { if (ReleaseMarquee?.Invoke(e) == true) return; base.OnPointerReleased(e); }
    protected override void OnTapped(TappedRoutedEventArgs e) { if (blankPress) { e.Handled = true; return; } base.OnTapped(e); }
    protected override void OnDoubleTapped(DoubleTappedRoutedEventArgs e) { if (blankPress) { e.Handled = true; return; } base.OnDoubleTapped(e); }
    private sealed class FileListItem : ListViewItem
    {
        private readonly FileListView owner;
        private bool blankPress;
        public FileListItem(FileListView owner)
        {
            this.owner = owner;
        }
        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            owner.ItemPressed?.Invoke(this, e); blankPress = owner.BeginMarquee?.Invoke(e) == true; owner.blankPress = blankPress;
            if (!blankPress) base.OnPointerPressed(e);
        }
        protected override void OnPointerMoved(PointerRoutedEventArgs e)
        { base.OnPointerMoved(e); }
        protected override void OnTapped(TappedRoutedEventArgs e) { if (blankPress) { e.Handled = true; return; } base.OnTapped(e); }
        protected override void OnDoubleTapped(DoubleTappedRoutedEventArgs e) { if (blankPress) { e.Handled = true; return; } base.OnDoubleTapped(e); }
    }
}
