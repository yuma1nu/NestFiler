using FilerApp.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace FilerApp;
public sealed partial class MainWindow
{
    private void RecordRecent(string path, bool directory)
    {
        state.Recent.RemoveAll(x => StringComparer.OrdinalIgnoreCase.Equals(x.Path, path));
        state.Recent.Insert(0, new(path, directory, DateTimeOffset.Now));
        state.Recent = state.Recent.Where(x => x.Directory).Take(100).Concat(state.Recent.Where(x => !x.Directory).Take(100)).OrderByDescending(x => x.Used).ToList();
        RenderRecent(); ScheduleSave();
    }
    private void RenderRecent()
    {
        RecentFilesList.Children.Clear(); RecentFoldersList.Children.Clear();
        foreach (var entry in state.Recent.OrderByDescending(x => x.Used))
        {
            var button = SidebarRow(Path.GetFileName(entry.Path.TrimEnd('\\')) is { Length: > 0 } name ? name : entry.Path, entry.Directory ? "\uE8B7" : "\uE8A5");
            ToolTipService.SetToolTip(button, $"{entry.Path}\n{entry.Used.LocalDateTime:g}\n存在・接続状態は開く際に確認");
            button.Click += async (_, _) => await OpenRecentPathAsync(entry.Path, entry.Directory);
            var menu = new MenuFlyout();
            menu.Items.Add(Menu("履歴から削除", (_, _) => { state.Recent.Remove(entry); RenderRecent(); ScheduleSave(); }));
            if (entry.Directory) menu.Items.Add(Menu("お気に入りに登録", async (_, _) => { await active.NavigateAsync(entry.Path); AddFavorite(this, new RoutedEventArgs()); }));
            var content = (Grid)button.Content;
            content.ColumnDefinitions.Add(new() { Width = new GridLength(78) });
            var used = new TextBlock { Text = entry.Used.LocalDateTime.ToString("MM/dd HH:mm"), FontSize = 10, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] };
            Grid.SetColumn(used, 2); content.Children.Add(used);
            button.ContextFlyout = menu; (entry.Directory ? RecentFoldersList : RecentFilesList).Children.Add(button);
        }
    }
    private void ClearRecentFiles(object sender, RoutedEventArgs e) { state.Recent.RemoveAll(x => !x.Directory); RenderRecent(); ScheduleSave(); }
    private void ClearRecentFolders(object sender, RoutedEventArgs e) { state.Recent.RemoveAll(x => x.Directory); RenderRecent(); ScheduleSave(); }
    private async Task OpenRecentPathAsync(string path, bool directory)
    {
        try
        {
            if (directory) await active.NavigateAsync(path);
            else { await Task.Run(() => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true })); RecordRecent(path, false); }
        }
        catch (Exception ex) { ShowError($"履歴・検索結果を開けません: {ex.Message}"); }
    }
}


