#if DEBUG
using FilerApp.Core;

namespace FilerApp;
public sealed partial class MainWindow
{
    // A small isolated workspace for real pointer input checks. Never replaces
    // the user's normal favorites, history or tab state.
    private async Task PrepareMiddleMouseChecksAsync()
    {
        var root = Path.Combine(Services.AppLog.DataDirectory, "middle-fixture");
        var origin = Directory.CreateDirectory(Path.Combine(root, "ORIGIN-A")).FullName;
        var right = Directory.CreateDirectory(Path.Combine(root, "ORIGIN-B")).FullName;
        var favorite = Directory.CreateDirectory(Path.Combine(root, "MIDDLE-FOLDER")).FullName;
        var file = Path.Combine(favorite, "select-me.txt"); await File.WriteAllTextAsync(file, "middle click fixture");
        state.FavoriteNodes = [new() { Name = "整理用", Children = [new() { Name = "中クリック用フォルダ", Path = favorite }, new() { Name = "中クリック用ファイル", Path = file, IsFileLink = true }] }];
        await LeftPane.NavigateAsync(origin); await LeftPane.NavigateAsync(right, true);
        await RightPane.NavigateAsync(right);
        state.Recent = [new(favorite, true, DateTimeOffset.Now), new(file, false, DateTimeOffset.Now)];
        RenderFavorites(); RenderRecent(); SetActive(LeftPane); LeftPane.FocusFiles();
        Status.Text = "隔離した中クリック操作の確認用プロファイル";
        if (Environment.GetEnvironmentVariable("FILERAPP_MIDDLE_MOUSE_AUTO_CHECK") == "1") await RunMiddleMouseChecksAsync(favorite, file);
    }
    private async Task RunMiddleMouseChecksAsync(string folder, string file)
    {
        var results = new List<string>();
        void Check(bool success, string message) { if (!success) throw new InvalidOperationException(message); results.Add("PASS " + message); }
        try
        {
            var aCount = LeftPane.Models.Count; var bCount = RightPane.Models.Count;
            await OpenPathInNewTabAsync(folder, true);
            Check(LeftPane.Models.Count == aCount + 1 && LeftPane.Current!.Path == folder && RightPane.Models.Count == bCount && active == LeftPane, "favorite folder callback opens one new tab in A only");
            SetActive(RightPane); RightPane.FocusFiles();
            aCount = LeftPane.Models.Count;
            await OpenPathInNewTabAsync(folder, true);
            Check(RightPane.Models.Count == bCount + 1 && RightPane.Current!.Path == folder && LeftPane.Models.Count == aCount && active == RightPane, "favorite folder callback opens one new tab in active B only");
            bCount = RightPane.Models.Count;
            await OpenPathInNewTabAsync(file, false);
            Check(RightPane.Models.Count == bCount + 1 && RightPane.Current!.Path == folder && RightPane.SelectedPaths.Contains(file), "file favorite callback opens its containing folder in a new tab and selects the file");
            bCount = RightPane.Models.Count;
            var recent = state.Recent.First(x => x.Directory && x.Path == folder);
            await OpenPathInNewTabAsync(recent.Path, recent.Directory);
            Check(RightPane.Models.Count == bCount + 1 && RightPane.Current!.Path == folder, "recent-folder callback uses the same new-tab behavior");
            SetActive(LeftPane); LeftPane.FocusFiles();
            bCount = RightPane.Models.Count; var selectedB = RightPane.Current;
            RightPane.DebugCloseBackgroundTab();
            Check(RightPane.Models.Count == bCount - 1 && RightPane.Current == selectedB && active == LeftPane, "closing a background tab in B preserves its selected tab and active A");
            await LeftPane.DebugCheckMiddleCloseAsync(results);
        }
        catch (Exception ex) { results.Add("FAIL " + ex); }
        finally
        {
            await File.WriteAllLinesAsync(Path.Combine(Services.AppLog.DataDirectory, "middle-mouse-check.txt"), results);
            closing = true; Close();
        }
    }
}
#endif
