#if DEBUG
using FilerApp.Views;
namespace FilerApp;
public sealed partial class MainWindow
{
    // Opt-in integration checks in an isolated debug profile. No existing user
    // folders are edited; this exercises the same OpenAsync path as double-click.
    private async Task RunPaneFocusChecksAsync()
    {
        if (Environment.GetEnvironmentVariable("FILERAPP_FOCUS_CHECK") != "1" ||
            Environment.GetEnvironmentVariable("FILERAPP_PROFILE_DIR") == null) return;
        var results = new List<string>();
        var root = Path.Combine(Services.AppLog.DataDirectory, "focus-fixture");
        try
        {
            await Task.Delay(200);
            LeftPane.DebugFocusTrace = results.Add; RightPane.DebugFocusTrace = results.Add;
            Directory.CreateDirectory(Path.Combine(root, "empty"));
            Directory.CreateDirectory(Path.Combine(root, "populated"));
            await File.WriteAllTextAsync(Path.Combine(root, "populated", "sample.txt"), "focus check");
            foreach (var pane in new[] { LeftPane, RightPane })
            foreach (var folder in new[] { "empty", "populated" })
            {
                await pane.NavigateAsync(root);
                SetActive(pane);
                await pane.DebugOpenFolderAsync(Path.Combine(root, folder));
                await Task.Delay(100);
                if (active != pane || !pane.DebugContainsFocus()) throw new InvalidOperationException($"{(pane == LeftPane ? "A" : "B")} / {folder}: active={(active == LeftPane ? "A" : "B")}, focus={Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(Root.XamlRoot)?.GetType().Name}, name={(Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(Root.XamlRoot) as Microsoft.UI.Xaml.FrameworkElement)?.Name}");
                results.Add($"PASS {(pane == LeftPane ? "A" : "B")} / {folder}: navigation preserved active pane and keyboard focus inside it");
            }
            SetActive(LeftPane); LeftPane.FocusFiles();
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = LeftPane.DebugWaitWithStableFocusAsync(completion.Task);
            SetActive(RightPane); RightPane.FocusFiles(); completion.SetResult();
            await pending; await Task.Delay(100);
            if (active != RightPane || !RightPane.HasFileFocus()) throw new InvalidOperationException("Navigation completion stole focus back from B");
            results.Add("PASS intentional switch to B is preserved while A operation completes");
            await LeftPane.NavigateAsync(root, true);
            var restoredIndex = LeftPane.SelectedIndex;
            await LeftPane.NavigateAsync(Path.Combine(root, "populated"));
            await LeftPane.Current!.SortAsync("Size", true);
            var tabCount = LeftPane.Models.Count;
            LeftPane.CloseCurrent();
            if (LeftPane.Models.Count != tabCount - 1) throw new InvalidOperationException("Tab was not closed");
            await LeftPane.RestoreClosedTabAsync();
            if (LeftPane.Models.Count != tabCount || LeftPane.SelectedIndex != restoredIndex || LeftPane.Current!.Path != Path.Combine(root, "populated") || !LeftPane.Current.CanBack || LeftPane.Current.SortColumn != "Size" || !LeftPane.Current.SortDescending || active != LeftPane || !LeftPane.ContainsFocus()) throw new InvalidOperationException("Tab restoration lost state or pane focus");
            results.Add("PASS close and restore tab preserves position, path, navigation, sort and pane focus");
            var selectedTab = LeftPane.Current;
            LeftPane.CycleTab(1); LeftPane.CycleTab(-1);
            if (LeftPane.Current != selectedTab || active != LeftPane) throw new InvalidOperationException("Tab cycling failed");
            LeftPane.SelectTab(1); LeftPane.SelectTab(9);
            if (LeftPane.SelectedIndex != LeftPane.Models.Count - 1) throw new InvalidOperationException("Numbered tab selection failed");
            results.Add("PASS next, previous and numbered tab selection");
            var file = Path.Combine(root, "populated", "sample.txt");
            var second = Path.Combine(root, "populated", "日本語.csv");
            await File.WriteAllTextAsync(second, "x,y\n1,2");
            var shellFolder = Path.GetDirectoryName(file)!;
            var counts = new[] { Services.ShellContextMenu.Probe(shellFolder, [file]), Services.ShellContextMenu.Probe(shellFolder, [file, second]), Services.ShellContextMenu.Probe(root, [shellFolder]), Services.ShellContextMenu.Probe(shellFolder, []) };
            if (counts.Any(x => x <= 0)) throw new InvalidOperationException("Shell menu was empty");
            results.Add("PASS Windows Shell menus generated for file, multi-selection, folder and background: " + string.Join(",", counts));
            var owner = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var dismiss = Task.Run(async () => { await Task.Delay(500); CancelCheckMenu(owner, 0x1f, 0, 0); });
            Services.ShellContextMenu.Show(owner, shellFolder, [file], true);
            await dismiss;
            results.Add("PASS native Shell popup displayed and dismissed programmatically (no command invoked)");
            var viewFolder = Directory.CreateDirectory(Path.Combine(root, "view-modes")).FullName;
            await Task.Run(() => { for (int i = 0; i < 300; i++) File.WriteAllText(Path.Combine(viewFolder, $"sample-{i:D3}.txt"), "view check"); });
            await LeftPane.NavigateAsync(viewFolder);
            LeftPane.SelectAllFiles(); LeftPane.FocusFiles();
            var selection = LeftPane.SelectedPaths.Order().ToArray();
            var entryOrder = LeftPane.Current!.Entries.Select(x => x.Path).ToArray();
            RightPane.SetDisplayMode(FilerApp.Core.FileViewMode.LargeIcons);
            foreach (var mode in new[] { FilerApp.Core.FileViewMode.SmallIcons, FilerApp.Core.FileViewMode.LargeIcons, FilerApp.Core.FileViewMode.Details })
            {
                LeftPane.DebugToggleView(mode);
                await LeftPane.DebugWaitForViewAsync(mode);
                var realized = LeftPane.DebugAssertViewLayout(mode);
                LeftPane.DebugToggleView(mode); // Selected mode must stay on.
                LeftPane.DebugAssertViewLayout(mode);
                if (!LeftPane.SelectedPaths.Order().SequenceEqual(selection) || !LeftPane.Current.Entries.Select(x => x.Path).SequenceEqual(entryOrder) || active != LeftPane || !LeftPane.ContainsFocus() || RightPane.DisplayMode != FilerApp.Core.FileViewMode.LargeIcons) throw new InvalidOperationException("Changing view lost selection/order, affected B or changed focus");
                results.Add($"PASS {mode}: exclusive toggle, correct tile/icon geometry, selection/order/focus retained, B independent; {realized}/300 containers realized");
            }
            foreach (var mode in new[] { FilerApp.Core.FileViewMode.Details, FilerApp.Core.FileViewMode.SmallIcons, FilerApp.Core.FileViewMode.LargeIcons })
            {
                await LeftPane.DebugCheckRangeAsync(mode);
                if (active != LeftPane) throw new InvalidOperationException("Range selection changed active pane");
                results.Add("PASS " + mode + ": marquee geometry, replace/add/toggle/shrink, cancel, auto-scroll/offscreen selection, focus and file-drag hit areas");
            }
            await LeftPane.DebugCheckDateTextAsync();
            results.Add("PASS date-only age coloring in light and dark themes");
            SetActive(LeftPane);
            LeftPane.DebugAssertPaneSurface(true, Root.Background); RightPane.DebugAssertPaneSurface(false, Root.Background);
            SetActive(RightPane);
            LeftPane.DebugAssertPaneSurface(false, Root.Background); RightPane.DebugAssertPaneSurface(true, Root.Background);
            SetActive(LeftPane);
            results.Add("PASS active pane uses original surface, inactive pane uses page background, no accent border");
            await LeftPane.NavigateAsync(Path.Combine(root, "empty"));
            LeftPane.SetDisplayMode(FilerApp.Core.FileViewMode.SmallIcons);
            LeftPane.DebugAssertViewLayout(FilerApp.Core.FileViewMode.SmallIcons);
            LeftPane.SetDisplayMode(FilerApp.Core.FileViewMode.LargeIcons);
            LeftPane.DebugAssertViewLayout(FilerApp.Core.FileViewMode.LargeIcons);
            LeftPane.SetDisplayMode((FilerApp.Core.FileViewMode)999);
            LeftPane.DebugAssertViewLayout(FilerApp.Core.FileViewMode.Details);
            if (active != LeftPane || !LeftPane.ContainsFocus()) throw new InvalidOperationException("Empty folder view change lost pane focus");
            results.Add("PASS empty-folder view changes and invalid saved mode fallback");
            await RunSearchThumbnailChecksAsync(root, results);
            await RunColumnPreviewChecksAsync(root, results);
            LeftPane.DebugFocusTrace = null; RightPane.DebugFocusTrace = null;
            await LeftPane.DebugCheckFoundationAsync(root, results);
            await RunTransferChecksAsync(root, results);
        }
        catch (Exception ex) { results.Add("FAIL " + ex); }
        finally
        {
            await File.WriteAllLinesAsync(Path.Combine(Services.AppLog.DataDirectory, "focus-check.txt"), results);
            closing = true; Close();
        }
    }
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "PostMessageW")]
    private static extern bool CancelCheckMenu(nint window, uint message, nuint wp, nint lp);
}
#endif

