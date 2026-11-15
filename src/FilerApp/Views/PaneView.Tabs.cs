using FilerApp.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
namespace FilerApp.Views;
public sealed partial class PaneView
{
    private readonly LinkedList<(TabSnapshot Snapshot, int Index)> closedTabs = [];
    public void CloseCurrent() { if (Current is { } tab) CloseModel(tab); }
    private void CloseModel(TabViewModel tab)
    {
        var index = Models.IndexOf(tab); if (index < 0 || Models.Count <= 1) return;
        if (ContainsFocus()) NavigationRefresh.Focus(FocusState.Programmatic);
        CapturePosition(); closedTabs.AddLast((tab.Capture(), index)); if (closedTabs.Count > 20) closedTabs.RemoveFirst();
        var selected = Current; tab.Cancel(); Models.RemoveAt(index);
        if (selected == tab) Tabs.SelectedIndex = Math.Min(index, Models.Count - 1);
        else if (selected != null) Tabs.SelectedItem = selected;
        if (isActivePane) FocusFiles(); StateChanged?.Invoke();
    }
    public async Task RestoreClosedTabAsync()
    {
        if (closedTabs.Last is not { } last) return;
        var (snapshot, index) = last.Value; closedTabs.RemoveLast(); Activated?.Invoke(this);
        await WithStableFocusAsync(async () =>
        {
            var tab = new TabViewModel(new FolderService());
            Models.Insert(Math.Clamp(index, 0, Models.Count), tab); Tabs.SelectedItem = tab;
            await tab.RestoreAsync(snapshot); UpdateSortHeaders(); StateChanged?.Invoke();
        });
    }
    public void CycleTab(int delta)
    {
        if (Models.Count == 0) return;
        Activated?.Invoke(this);
        if (ContainsFocus()) NavigationRefresh.Focus(FocusState.Programmatic);
        Tabs.SelectedIndex = (Tabs.SelectedIndex + delta + Models.Count) % Models.Count;
        FocusFiles();
    }
    public void SelectTab(int number)
    {
        if (Models.Count == 0) return;
        Activated?.Invoke(this);
        if (ContainsFocus()) NavigationRefresh.Focus(FocusState.Programmatic);
        Tabs.SelectedIndex = number == 9 ? Models.Count - 1 : Math.Min(number - 1, Models.Count - 1); FocusFiles();
    }
    public void SelectAllFiles() => Files.SelectAll();
    public async Task GoBackAsync() { if (Current is { } tab) await WithStableFocusAsync(tab.BackAsync); RecordFolderVisit(); StateChanged?.Invoke(); }
    public async Task GoForwardAsync() { if (Current is { } tab) await WithStableFocusAsync(tab.ForwardAsync); RecordFolderVisit(); StateChanged?.Invoke(); }
    public async Task GoUpAsync() { try { if (Current is { } tab && Directory.GetParent(tab.Path) is { } parent) { var previous = tab.Path; await NavigateAndSelectAsync(parent.FullName, previous, tab.IsSearch); } RecordFolderVisit(); StateChanged?.Invoke(); } catch (Exception ex) { Error?.Invoke(ex.Message); } }
    private void InitializeTabContext()
    {
        Tabs.RightTapped += (_, args) =>
        {
            var node = args.OriginalSource as DependencyObject;
            while (node != null && node != Tabs && node is not TabViewItem) node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(node);
            if (node is not TabViewItem item || item.Tag is not TabViewModel tab) return;
            args.Handled = true; Activated?.Invoke(this);
            var menu = new MenuFlyout();
            void Add(string label, RoutedEventHandler action) { var entry = new MenuFlyoutItem { Text = label }; entry.Click += action; menu.Items.Add(entry); }
            Add("タブを閉じる", (_, _) => CloseModel(tab));
            Add("他のタブを閉じる", (_, _) => { foreach (var other in Models.Where(x => x != tab).ToArray()) CloseModel(other); });
            Add("右側のタブを閉じる", (_, _) => { foreach (var other in Models.Skip(Models.IndexOf(tab) + 1).ToArray().Reverse()) CloseModel(other); });
            Add("タブを複製", async (_, _) => { if (tab.Search is { } options) OpenSearchTab(options.Query, options); else await NavigateAsync(tab.Path, true); });
            Add("閉じたタブを復元", async (_, _) => await RestoreClosedTabAsync());
            menu.ShowAt(Tabs, new Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowOptions { Position = args.GetPosition(Tabs) });
        };
    }
}
