using FilerApp.Services;
using FilerApp.Views;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
namespace FilerApp;
public sealed partial class MainWindow
{
    private bool shellMenuOpen, fileMenuOpen;
    private async void ShowContext(PaneView pane, Point point, bool native)
    {
        SetActive(pane);
        if (native) { await ShowShellContextAsync(pane); return; }
        var menu = new MenuFlyout(); bool standardRequested = false;
        menu.Opening += (_, _) => fileMenuOpen = true;
        var count = pane.SelectedPaths.Length;
        void Add(string label, string command, string shortcut = "", bool enabled = true)
        {
            var item = new MenuFlyoutItem { Text = label, KeyboardAcceleratorTextOverride = shortcut, IsEnabled = enabled };
            item.Click += async (_, _) => { SetActive(pane); await CommandAsync(command); }; menu.Items.Add(item);
        }
        if (count > 0)
        {
            var open = new MenuFlyoutItem { Text = "開く", KeyboardAcceleratorTextOverride = "Enter", IsEnabled = count == 1 };
            open.Click += async (_, _) => await pane.OpenAsync(); menu.Items.Add(open);
            Add("コピー", "copy", "Ctrl+C"); Add("切り取り", "cut", "Ctrl+X");
        }
        Add("貼り付け", "paste", "Ctrl+V", pane.Current?.IsSearch != true);
        if (count > 0)
        {
            Add("パスをコピー", "copyPath", "Ctrl+Shift+C"); menu.Items.Add(new MenuFlyoutSeparator());
            Add("反対ペインへコピー", "copyOther"); Add("反対ペインへ移動", "moveOther");
            Add("名前を変更", "rename", "F2", count == 1); Add("削除…", "recycle", "Delete");
            if (pane.Current?.IsSearch == true) Add("格納フォルダを新しいタブで開く", "locate", "", count == 1);
        }
        else { Add("パスをコピー", "copyPath", "Ctrl+Shift+C"); Add("新規フォルダ", "mkdir", "Ctrl+Shift+N", pane.Current?.IsSearch != true); Add("更新", "refresh", "F5"); }
        if (pane.BuildSearchOptionsMenu() is { } options) { menu.Items.Add(new MenuFlyoutSeparator()); menu.Items.Add(options); }
        menu.Items.Add(pane.BuildViewModeMenu());
        if (operationResults.Count > 0) Add("ファイル操作の結果", "results");
        if (operating) Add("ファイル操作を中断", "cancel");
        menu.Items.Add(new MenuFlyoutSeparator());
        Add("現在のフォルダをお気に入りに登録", "favorite", "Ctrl+D");
        var shell = new MenuFlyoutItem { Text = "Windows標準メニューを表示…", KeyboardAcceleratorTextOverride = "Shift+右クリック" };
        shell.IsEnabled = CanShowShellContext(pane);
        shell.Click += (_, _) => standardRequested = true; menu.Items.Add(shell);
        menu.Closed += async (_, _) => { fileMenuOpen = false; if (standardRequested) { await Task.Yield(); await ShowShellContextAsync(pane); } };
        pane.ShowFileContext(menu, point);
    }
    private async Task ShowShellContextAsync(PaneView pane)
    {
        if (shellMenuOpen || operating || pane.Current is not { } tab) return;
        if (!CanShowShellContext(pane)) { Status.Text = "Windows標準メニューは同じ格納フォルダ内の項目を選択して開いてください。"; return; }
        shellMenuOpen = true;
        try
        {
            var paths = pane.SelectedPaths;
            var folder = paths.Length > 0 ? Path.GetDirectoryName(paths[0])! : tab.Path;
            if (ShellContextMenu.Show(WinRT.Interop.WindowNative.GetWindowHandle(this), folder, paths, true))
                await System.Threading.Tasks.Task.WhenAll(LeftPane.RefreshAfterOperationAsync(), RightPane.RefreshAfterOperationAsync());
        }
        catch (Exception ex) { ShowError($"Windowsメニューを開けません: {ex.Message}"); }
        finally { shellMenuOpen = false; }
    }
    private static bool CanShowShellContext(PaneView pane)
    {
        var paths = pane.SelectedPaths;
        return paths.Length == 0 ? pane.Current?.IsSearch != true : paths.Select(Path.GetDirectoryName).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1;
    }
}
