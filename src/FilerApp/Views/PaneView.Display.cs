using FilerApp.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace FilerApp.Views;
public sealed partial class PaneView
{
    public FileViewMode DisplayMode { get; private set; }
    private bool displayReady, updatingViewButtons;
    private void InitializeDisplay() { displayReady = true; UpdateViewButtons(); }

    private void ViewModeChecked(object sender, RoutedEventArgs e)
    {
        if (!displayReady || updatingViewButtons) return;
        Activated?.Invoke(this);
        if (sender is ToggleButton { Tag: string tag } && Enum.TryParse<FileViewMode>(tag, out var mode)) SetDisplayMode(mode);
    }
    private void ViewModeUnchecked(object sender, RoutedEventArgs e)
    {
        if (!displayReady || updatingViewButtons) return;
        if (sender is ToggleButton { Tag: string tag } && tag == DisplayMode.ToString()) UpdateViewButtons();
    }

    public void SetDisplayMode(FileViewMode mode)
    {
        if (!Enum.IsDefined(mode)) mode = FileViewMode.Details;
        if (DisplayMode != mode)
        {
            CancelRangeSelection(true);
            var hadFileFocus = HasFileFocus();
            var selected = SelectedEntries().ToArray();
            if (hadFileFocus) NavigationRefresh.Focus(FocusState.Programmatic);
            DisplayMode = mode;
            // Keep one ListView and its existing input, context-menu and transfer
            // handlers. Both panels virtualize; only the presentation changes.
            Files.ItemTemplate = (DataTemplate)Resources[mode + "Template"];
            Files.ItemContainerStyle = (Style)Resources[mode == FileViewMode.Details ? "DetailsItemStyle" : "IconItemStyle"];
            Files.ItemsPanel = (ItemsPanelTemplate)Resources[mode + "Panel"];
            ColumnHeaders.Visibility = mode == FileViewMode.Details ? Visibility.Visible : Visibility.Collapsed;
            UpdateColumnView();
            if (selected.Length > 0) Files.ScrollIntoView(selected[0]);
            if (hadFileFocus && isActivePane) FocusFiles();
            StateChanged?.Invoke();
        }
        UpdateViewButtons();
    }
    private void UpdateViewButtons()
    {
        // Keep exactly one selected, including keyboard/automation toggle input.
        updatingViewButtons = true;
        try
        {
            DetailsViewButton.IsChecked = DisplayMode == FileViewMode.Details;
            SmallIconsViewButton.IsChecked = DisplayMode == FileViewMode.SmallIcons;
            LargeIconsViewButton.IsChecked = DisplayMode == FileViewMode.LargeIcons;
        }
        finally { updatingViewButtons = false; }
    }
    public MenuFlyoutSubItem BuildViewModeMenu()
    {
        var menu = new MenuFlyoutSubItem { Text = "表示形式" };
        foreach (var (label, mode) in new[] { ("詳細一覧", FileViewMode.Details), ("小サムネイル", FileViewMode.SmallIcons), ("大サムネイル", FileViewMode.LargeIcons) })
        {
            var item = new ToggleMenuFlyoutItem { Text = label, IsChecked = DisplayMode == mode };
            item.Click += (_, _) => { Activated?.Invoke(this); SetDisplayMode(mode); };
            menu.Items.Add(item);
        }
        if (Current is { IsSearch: false } tab)
        {
            menu.Items.Add(new MenuFlyoutSeparator());
            var filter = new MenuFlyoutItem { Text = "現在の一覧を絞り込み", KeyboardAcceleratorTextOverride = "Ctrl+Shift+F" };
            filter.Click += (_, _) => FocusFilter(); menu.Items.Add(filter);
            foreach (bool system in new[] { false, true })
            {
                var item = new ToggleMenuFlyoutItem { Text = system ? "システム項目を表示" : "隠し項目を表示", IsChecked = system ? tab.ShowSystem : tab.ShowHidden };
                item.Click += async (_, _) => { if (Current != tab) return; await WithStableFocusAsync(() => tab.SetVisibilityAsync(system ? tab.ShowHidden : item.IsChecked, system ? item.IsChecked : tab.ShowSystem)); StateChanged?.Invoke(); };
                menu.Items.Add(item);
            }
        }
        return menu;
    }
}
