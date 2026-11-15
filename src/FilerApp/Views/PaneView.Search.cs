using FilerApp.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace FilerApp.Views;
public sealed partial class PaneView
{
    private bool searchUiReady, synchronizingSearchUi;
    private void InitializePaneSearch() { searchUiReady = true; UpdateSearchControls(); }
    public TabViewModel OpenSearchTab(string query, SearchOptions? template = null)
    {
        CancelRangeSelection(true); Activated?.Invoke(this);
        if (ContainsFocus()) NavigationRefresh.Focus(FocusState.Programmatic);
        var options = template ?? new SearchOptions(Current?.Path ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), query, false, true, false, false, "", ".git,node_modules,bin,obj", "UTF-8");
        var tab = new TabViewModel(new FolderService()); tab.PrepareSearch(options);
        Models.Add(tab); Tabs.SelectedItem = tab; StateChanged?.Invoke();
        if (!string.IsNullOrWhiteSpace(options.Query)) _ = RunPaneSearchAsync(tab, options);
        else FocusSearch();
        return tab;
    }
    public void FocusSearch() { Activated?.Invoke(this); PaneSearch.Focus(FocusState.Programmatic); PaneSearch.SelectAll(); }
    private async Task RunPaneSearchAsync(TabViewModel tab, SearchOptions options)
    {
        if (Current == tab && isActivePane)
        {
            NavigationRefresh.Focus(FocusState.Programmatic);
            await WithStableFocusAsync(() => tab.RunSearchAsync(options));
        }
        else await tab.RunSearchAsync(options);
        StateChanged?.Invoke();
    }
    private void UpdateSearchControls()
    {
        if (!searchUiReady) return;
        bool searching = Current?.IsSearch == true;
        BackButton.Visibility = ForwardButton.Visibility = UpButton.Visibility = searching ? Visibility.Collapsed : Visibility.Visible;
        SearchModeChoice.Visibility = SearchOptionsButton.Visibility = searching ? Visibility.Visible : Visibility.Collapsed;
        ViewModeButtons.Visibility = searching ? Visibility.Collapsed : Visibility.Visible;
        StopSearchButton.Visibility = searching && Current!.Busy ? Visibility.Visible : Visibility.Collapsed;
        synchronizingSearchUi = true;
        try
        {
            if (Current?.Search is { } options)
            {
                SearchModeChoice.SelectedIndex = !options.Content ? 0 : options.IncludeNames ? 2 : 1;
                if (PaneSearch.FocusState == FocusState.Unfocused) PaneSearch.Text = options.Query;
            }
            else PaneSearch.Text = "";
        }
        finally { synchronizingSearchUi = false; }
    }
    private async void SearchModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!searchUiReady || synchronizingSearchUi || Current?.Search is not { } options) return;
        var tab = Current;
        var next = options with { Content = SearchModeChoice.SelectedIndex != 0, IncludeNames = SearchModeChoice.SelectedIndex == 2, Query = PaneSearch.Text };
        if (string.IsNullOrWhiteSpace(next.Query)) { tab.PrepareSearch(next); StateChanged?.Invoke(); }
        else await RunPaneSearchAsync(tab, next);
    }
    private void StopPaneSearch(object sender, RoutedEventArgs e) => Current?.StopSearch();
    private void ShowSearchOptions(object sender, RoutedEventArgs e)
    {
        var menu = new MenuFlyout();
        if (BuildSearchOptionsMenu() is { } options)
            foreach (var item in options.Items.ToArray()) { options.Items.Remove(item); menu.Items.Add(item); }
        menu.Items.Add(new MenuFlyoutSeparator()); menu.Items.Add(BuildViewModeMenu());
        menu.ShowAt(SearchOptionsButton);
    }
    public MenuFlyoutSubItem? BuildSearchOptionsMenu()
    {
        if (Current is not { Search: { } options } tab) return null;
        var menu = new MenuFlyoutSubItem { Text = "検索条件" };
        async Task Apply(SearchOptions value)
        {
            if (!Models.Contains(tab)) return;
            if (string.IsNullOrWhiteSpace(value.Query)) { tab.PrepareSearch(value); StateChanged?.Invoke(); }
            else await RunPaneSearchAsync(tab, value);
        }
        void Toggle(string label, bool value, Func<bool, SearchOptions> change, bool enabled = true)
        {
            var item = new ToggleMenuFlyoutItem { Text = label, IsChecked = value, IsEnabled = enabled };
            item.Click += async (_, _) => await Apply(change(item.IsChecked)); menu.Items.Add(item);
        }
        Toggle("サブフォルダを含める", options.Recursive, value => options with { Recursive = value });
        Toggle("大文字・小文字を区別", options.CaseSensitive, value => options with { CaseSensitive = value });
        Toggle("内容を正規表現で検索", options.Regex, value => options with { Regex = value }, options.Content);
        var encoding = new MenuFlyoutSubItem { Text = "文字コード: " + options.Encoding };
        foreach (var value in new[] { "UTF-8", "CP932" })
        {
            var item = new ToggleMenuFlyoutItem { Text = value, IsChecked = options.Encoding == value, IsEnabled = options.Content };
            item.Click += async (_, _) => await Apply(options with { Encoding = value }); encoding.Items.Add(item);
        }
        menu.Items.Add(encoding); menu.Items.Add(new MenuFlyoutSeparator());
        void Edit(string label, string title, string value, Func<string, SearchOptions> change)
        {
            var item = new MenuFlyoutItem { Text = label };
            item.Click += async (_, _) =>
            {
                var input = new TextBox { Text = value, MinWidth = 320, MaxLength = 32767 };
                var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = title, Content = input, PrimaryButtonText = "適用", CloseButtonText = "キャンセル", DefaultButton = ContentDialogButton.Primary };
                if (await dialog.ShowAsync() == ContentDialogResult.Primary) await Apply(change(input.Text.Trim().Trim('"')));
            };
            menu.Items.Add(item);
        }
        Edit("対象フォルダ…", "検索対象フォルダ", options.Folder, value => options with { Folder = value });
        Edit("対象の拡張子…", "拡張子（.txt,.csvなど／空欄で全対象）", options.Extensions, value => options with { Extensions = value });
        Edit("除外フォルダ…", "除外するフォルダ名（カンマ区切り）", options.ExcludedFolders, value => options with { ExcludedFolders = value });
        menu.Items.Add(new MenuFlyoutSeparator());
        var repeat = new MenuFlyoutItem { Text = "再検索 (F5)" }; repeat.Click += async (_, _) => await RunPaneSearchAsync(tab, options); menu.Items.Add(repeat);
        var stop = new MenuFlyoutItem { Text = "検索を中断", IsEnabled = tab.Busy }; stop.Click += (_, _) => tab.StopSearch(); menu.Items.Add(stop);
        var warnings = new MenuFlyoutItem { Text = $"検索の警告 ({tab.SearchWarnings.Count})", IsEnabled = tab.SearchWarnings.Count > 0 };
        warnings.Click += async (_, _) => await new ContentDialog { XamlRoot = XamlRoot, Title = "検索の警告", Content = new ScrollViewer { MaxHeight = 420, Content = new TextBlock { Text = string.Join("\n\n", tab.SearchWarnings.Select(x => x.Label)), TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true } }, CloseButtonText = "閉じる" }.ShowAsync();
        menu.Items.Add(warnings); return menu;
    }
    public async Task OpenContainingFolderAsync()
    {
        if (Selected is { } selected && Path.GetDirectoryName(selected.Path) is { } parent) await NavigateAndSelectAsync(parent, selected.Path, true);
    }
    public void StopAllLoads() { StopWatching(); foreach (var tab in Models) tab.Cancel(); }
    public Task RefreshAfterOperationAsync()
    {
        if (Current?.IsSearch == true) { _ = RefreshAsync(); return Task.CompletedTask; }
        return RefreshAsync();
    }
}
