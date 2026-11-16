using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.UI.ViewManagement;

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
    private readonly AccessibilitySettings accessibility = new();
    private bool highContrast;
    internal void SuppressBackgroundTap() => blankPress = true;
    public FileListView()
    {
        DefaultStyleKey = typeof(ListView); ItemContainerTransitions = new Microsoft.UI.Xaml.Media.Animation.TransitionCollection();
        highContrast = accessibility.HighContrast;
        Loaded += (_, _) => RefreshIndicatorContrast();
        ActualThemeChanged += (_, _) => RefreshIndicatorContrast();
    }
    private void RefreshIndicatorContrast()
    {
        highContrast = accessibility.HighContrast;
        if (ItemsPanelRoot != null) foreach (var row in ItemsPanelRoot.Children.OfType<FileListItem>()) row.ApplySelectionIndicator();
    }
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
        protected override void OnApplyTemplate() { base.OnApplyTemplate(); ApplySelectionIndicator(); }
        internal void ApplySelectionIndicator()
        {
            if (GetTemplateChild("Root") is not ListViewItemPresenter presenter) return;
            // ListView can re-enable its indicator internally, independently of
            // the resource flag. Set its actual brushes as well as the flag.
            presenter.SelectionIndicatorVisualEnabled = owner.highContrast;
            var brush = owner.highContrast ? (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"] : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            presenter.SelectionIndicatorBrush = presenter.SelectionIndicatorPointerOverBrush = presenter.SelectionIndicatorPressedBrush = presenter.SelectionIndicatorDisabledBrush = brush;
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
