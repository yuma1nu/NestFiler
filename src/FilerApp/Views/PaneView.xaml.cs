using System.Collections.ObjectModel;
using System.Diagnostics;
using FilerApp.Core;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace FilerApp.Views;
public sealed partial class PaneView : UserControl
{
    public ObservableCollection<TabViewModel> Models { get; } = [];
    public TabViewModel? Current => Tabs.SelectedItem as TabViewModel;
    public FileEntry? Selected => Files.SelectedItem as FileEntry;
    public int SelectedIndex => Tabs.SelectedIndex;
    public event Action<PaneView>? Activated;
    public event Action? StateChanged;
    public event Action<FileEntry?>? SelectionUpdated;
    public event Action<string>? Error;
    public event Action<string, bool>? Visited;


    public PaneView()
    {
        InitializeComponent(); InitializeVisualWork(); InitializeColumns(); InitializeDisplay(); InitializePaneSearch(); Tabs.TabItemsSource = Models; InitializeTransfer(); InitializeMarquee(); InitializeBreadcrumbs(); InitializeTabContext(); InitializeFileContext(); InitializeWatching(); InitializeFilter();
        Frame.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(ActivatePane), true);
    }
    public void SetActive(bool active)
    {
        isActivePane = active;
        if (!active) CancelRangeSelection(true);
        ActivePaneSurface.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
    }
    public async Task InitializeAsync(List<string> paths, int index, List<SearchOptions?>? searches = null, List<TabSnapshot>? snapshots = null)
    {
        if (paths.Count == 0) paths = [Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)];
        // Start every independent load before awaiting: a slow share cannot hold up other tabs.
        var loads = paths.Select((path, i) => { var model = new TabViewModel(new FolderService()); Models.Add(model); if (snapshots?.ElementAtOrDefault(i) is { } snapshot) return model.RestoreAsync(snapshot); if (searches?.ElementAtOrDefault(i) is { } options) { model.PrepareSearch(options); if (!string.IsNullOrWhiteSpace(options.Query)) _ = model.RunSearchAsync(options); return Task.CompletedTask; } return model.NavigateAsync(path); }).ToArray();
        Tabs.SelectedIndex = Math.Clamp(index, 0, Models.Count - 1);
        await Task.WhenAll(loads);
        await RestorePositionAsync(); ConfigureWatcher();
    }
    public async Task NavigateAsync(string path, bool newTab = false)
    {
        NoteListScroll();
        if (Current?.IsSearch == true) newTab = true;
        Activated?.Invoke(this);
        await WithStableFocusAsync(async () =>
        {
            if (newTab || Current == null) { var model = new TabViewModel(new FolderService()); Models.Add(model); Tabs.SelectedItem = model; }
            var tab = Current!;
            await tab.NavigateAsync(path);
            if (!tab.Status.StartsWith("開けません")) Visited?.Invoke(tab.Path, true);
            StateChanged?.Invoke();
        });
    }
    public async Task RefreshAsync() { if (Current is { } tab) await WithStableFocusAsync(tab.RefreshAsync); }
    public bool HasFileFocus()
    {
        var focus = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
        while (focus != null) { if (focus == Files) return true; focus = VisualTreeHelper.GetParent(focus); }
        return false;
    }
    public void FocusFiles()
    {
        // Focus a realized row, not the ListView's implicit "next" target.
        Files.UpdateLayout();
        var item = Files.SelectedItem ?? Files.Items.FirstOrDefault();
        if (item != null && Files.ContainerFromItem(item) is Control row)
            row.Focus(FocusState.Programmatic);
        else NavigationRefresh.Focus(FocusState.Programmatic);
    }
    public void FocusAddress() => BeginPathEdit();
    public void SelectRelativeFile(int delta)
    {
        if (Files.Items.Count == 0) return;
        var index = Math.Clamp(Files.SelectedIndex + delta, 0, Files.Items.Count - 1);
        if (index == Files.SelectedIndex) return;
        Files.SelectedItems.Clear(); Files.SelectedIndex = index; Files.ScrollIntoView(Files.SelectedItem);
    }
    private void ActivatePane(object sender, PointerRoutedEventArgs e) => Activated?.Invoke(this);
    private async void TabChanged(object sender, SelectionChangedEventArgs e) { if (observedTab != null && Files.ItemsSource == observedTab.Entries) CapturePosition(observedTab); Frame.DataContext = Current; ObserveTabPath(); UpdateSearchControls(); UpdateFilterControls(); UpdateColumnView(); ColumnsChanged?.Invoke(); ConfigureWatcher(); RecheckFolder(); await RestorePositionAsync(); StateChanged?.Invoke(); SelectionUpdated?.Invoke(null); }
    private async void TabAdd(TabView sender, object args) => await NavigateAsync(Current?.Path ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), true);
    private void TabClose(TabView sender, TabViewTabCloseRequestedEventArgs args) { if (args.Item is TabViewModel model) CloseModel(model); }
    private async void AddTab(object sender, RoutedEventArgs e) => await NavigateAsync(Current?.Path ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), true);
    private void CloseTab(object sender, RoutedEventArgs e) => CloseCurrent();
    private async void Back(object sender, RoutedEventArgs e) => await GoBackAsync();
    private async void Forward(object sender, RoutedEventArgs e) => await GoForwardAsync();
    private async void Up(object sender, RoutedEventArgs e) => await GoUpAsync();
    private async void Refresh(object sender, RoutedEventArgs e) => await RefreshAsync();
    private async void Sort(object sender, RoutedEventArgs e)
    {
        if (suppressColumnSort) return;
        if (Current is not { } tab) return;
        var column = (string)((Button)sender).Tag;
        var pending = WithStableFocusAsync(() => tab.SortAsync(column, tab.SortColumn == column && !tab.SortDescending));
        UpdateSortHeaders(); await pending;
    }
    private void UpdateSortHeaders()
    {
#if DEBUG
        headerUpdateCount++;
#endif
        foreach (var (column, button) in columnButtons)
        {
            var label = FileColumns.Title(column);
            bool selected = (Current?.SortColumn ?? "Name") == column;
            bool descending = Current?.SortDescending == true;
            button.Content = label + (selected ? descending ? " ↓" : " ↑" : "");
            ToolTipService.SetToolTip(button, selected ? $"{label}：{(descending ? "降順" : "昇順")}（クリックで切り替え）" : $"{label}で並べ替え");
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, label + (selected ? descending ? " 降順" : " 昇順" : " 並べ替え"));
        }
    }    private void FileSelection(object sender, SelectionChangedEventArgs e) { if (!updatingMarqueeSelection) QueueSelectionUpdate(); }
    private async void OpenSelected(object sender, DoubleTappedRoutedEventArgs e) => await OpenAsync();
    private async void FileKey(object sender, KeyRoutedEventArgs e) { if (e.Key == VirtualKey.Enter) { e.Handled = true; await OpenAsync(); } }
    private void RecordFolderVisit() { if (Current is { IsSearch: false } tab && !tab.Status.StartsWith("開けません")) Visited?.Invoke(tab.Path, true); }
    public async Task OpenAsync()
    {
        if (Selected is not { } file) return;
        if (file.IsDirectory) { await NavigateAsync(file.Path); return; }
        try { await Task.Run(() => Process.Start(new ProcessStartInfo(file.Path) { UseShellExecute = true })); Visited?.Invoke(file.Path, false); }
        catch (Exception ex) { Error?.Invoke($"開けません: {ex.Message}"); }
    }
}








