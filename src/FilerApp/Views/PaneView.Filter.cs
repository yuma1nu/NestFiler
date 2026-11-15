using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace FilerApp.Views;
public sealed partial class PaneView
{
    private bool syncingFilter;
    private readonly DispatcherTimer filterTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private void InitializeFilter()
    {
        filterTimer.Tick += async (_, _) =>
        {
            filterTimer.Stop(); var tab = Current; var query = ListFilter.Text;
            if (tab is not { IsSearch: false }) return;
            await WithStableFocusAsync(() => tab.SetFilterAsync(query)); StateChanged?.Invoke();
        };
    }
    private void UpdateFilterControls()
    {
        syncingFilter = true; filterTimer.Stop();
        ListFilter.Text = Current?.Filter ?? "";
        FilterBar.Visibility = Current is { IsSearch: false, Filter.Length: > 0 } ? Visibility.Visible : Visibility.Collapsed;
        syncingFilter = false;
    }
    public void FocusFilter()
    {
        if (Current?.IsSearch == true) return;
        Activated?.Invoke(this); FilterBar.Visibility = Visibility.Visible; ListFilter.Focus(FocusState.Programmatic); ListFilter.SelectAll();
    }
    private void FilterChanged(object sender, TextChangedEventArgs e) { if (!syncingFilter) { filterTimer.Stop(); filterTimer.Start(); } }
    private async void ClearFilter(object sender, RoutedEventArgs e)
    {
        filterTimer.Stop(); if (Current is { } tab) await WithStableFocusAsync(() => tab.SetFilterAsync(""));
        UpdateFilterControls(); FocusFiles(); StateChanged?.Invoke();
    }
    private void FilterKey(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape) { e.Handled = true; ClearFilter(sender, new()); }
        if (e.Key == VirtualKey.Enter) { e.Handled = true; FocusFiles(); }
    }
}
