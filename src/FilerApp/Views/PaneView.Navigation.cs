using System.ComponentModel;
using FilerApp.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
namespace FilerApp.Views;
public sealed partial class PaneView
{
    private TabViewModel? observedTab;
    public event Action<PaneView, string>? SearchRequested;
    public void SetLabel(string name) => PaneLabel.Text = name;
    private void InitializeBreadcrumbs()
    {
        PathBar.SizeChanged += (_, _) => BreadcrumbScroller.MaxWidth = Math.Max(0, PathBar.ActualWidth - 24);
    }
    private void ObserveTabPath()
    {
        if (observedTab != null) observedTab.PropertyChanged -= TabPathChanged;
        observedTab = Current;
        if (observedTab != null) observedTab.PropertyChanged += TabPathChanged;
        EndPathEdit(); RebuildBreadcrumbs();
    }
    private void TabPathChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TabViewModel.Path)) { EndPathEdit(); RebuildBreadcrumbs(); ConfigureWatcher(); }
        if (e.PropertyName is nameof(TabViewModel.Search) or nameof(TabViewModel.IsSearch) or nameof(TabViewModel.Busy)) UpdateSearchControls();
    }
    private void RebuildBreadcrumbs()
    {
        BreadcrumbItems.Children.Clear();
        if (Current?.Path is not { Length: > 0 } path) return;
        Address.Text = path;
        try
        {
            foreach (var crumb in PathBreadcrumbs.Build(path))
            {
                var button = new Button { Content = crumb.Name, Style = (Style)Application.Current.Resources["FlatRowButton"], HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 0, MaxWidth = 220 };
                ToolTipService.SetToolTip(button, crumb.Path);
                button.Click += async (_, _) => await NavigateAsync(crumb.Path);
                BreadcrumbItems.Children.Add(button);
                BreadcrumbItems.Children.Add(new FontIcon { Glyph = "\uE76C", FontSize = 9, Width = 14, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false });
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or System.IO.IOException)
        { BreadcrumbItems.Children.Add(new TextBlock { Text = path, VerticalAlignment = VerticalAlignment.Center }); }
        DispatcherQueue.TryEnqueue(() => BreadcrumbScroller.ChangeView(double.MaxValue, null, null, true));
    }
    private void PathBarPressed(object sender, PointerRoutedEventArgs e)
    {
        // Buttons own breadcrumb navigation. All remaining space edits the path.
        var source = e.OriginalSource as DependencyObject;
        while (source != null && source != PathBar)
        { if (source is Button) return; source = VisualTreeHelper.GetParent(source); }
        BeginPathEdit(); e.Handled = true;
    }
    private void BeginPathEdit()
    {
        Activated?.Invoke(this);
        Address.Text = Current?.Path ?? "";
        PathBar.Visibility = Visibility.Collapsed; Address.Visibility = Visibility.Visible;
        Address.Focus(FocusState.Programmatic); Address.SelectAll();
        DispatcherQueue.TryEnqueue(() => { if (Address.Visibility == Visibility.Visible) Address.SelectAll(); });
    }
    private void EndPathEdit() { Address.Visibility = Visibility.Collapsed; PathBar.Visibility = Visibility.Visible; }
    private void AddressLostFocus(object sender, RoutedEventArgs e) => EndPathEdit();
    private async void AddressKey(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        { var target = Address.Text.Trim().Trim('"'); e.Handled = true; await NavigateAsync(target); EndPathEdit(); }
        else if (e.Key == VirtualKey.Escape) { e.Handled = true; EndPathEdit(); FocusFiles(); }
    }
    private void SubmitPaneSearch(object sender, RoutedEventArgs e) { Activated?.Invoke(this); if (Current?.IsSearch == true) _ = RunPaneSearchAsync(Current, Current.Search! with { Query = PaneSearch.Text }); else SearchRequested?.Invoke(this, PaneSearch.Text); }
    private void PaneSearchKey(object sender, KeyRoutedEventArgs e)
    { if (e.Key == VirtualKey.Enter) { e.Handled = true; SubmitPaneSearch(sender, e); } }
}
