#if DEBUG
using FilerApp.Core;

namespace FilerApp;
public sealed partial class MainWindow
{
    private async Task RunEnterChecksAsync()
    {
        var results = new List<string>();
        var root = Directory.CreateDirectory(Path.Combine(Services.AppLog.DataDirectory, "enter-fixture")).FullName;
        var child = Directory.CreateDirectory(Path.Combine(root, "ENTER-TARGET")).FullName;
        Directory.CreateDirectory(Path.Combine(child, "NESTED"));
        await LeftPane.NavigateAsync(root); await RightPane.NavigateAsync(root);
        LeftPane.DebugSelectFile(child); RightPane.DebugSelectFile(child); SetActive(LeftPane); LeftPane.FocusFiles();
        if (Environment.GetEnvironmentVariable("FILERAPP_ENTER_HOLD") == "1") { Status.Text = "Enter入力の確認用（専用フォルダ）"; return; }
        try
        {
            foreach (var pane in new[] { LeftPane, RightPane })
            foreach (var mode in Enum.GetValues<FileViewMode>())
            {
                await pane.NavigateAsync(root); pane.SetDisplayMode(mode); await Task.Delay(50);
                SetActive(pane); pane.DebugSelectFile(child); pane.FocusFiles();
                if (!CanOpenListingWithEnter(false)) throw new InvalidOperationException("Selected row does not qualify for Enter in " + mode);
                var other = pane == LeftPane ? RightPane : LeftPane; var otherPath = other.Current!.Path;
                await pane.OpenAsync();
                if (pane.Current!.Path != child || active != pane || other.Current!.Path != otherPath) throw new InvalidOperationException("Enter open path did not enter the selected folder independently");
                results.Add($"PASS {(pane == LeftPane ? "A" : "B")} {mode}: focused selected folder uses double-click open path and leaves other pane unchanged");
            }
            foreach (var pane in new[] { LeftPane, RightPane })
            {
                SetActive(pane); pane.FocusAddress();
                if (CanOpenListingWithEnter(false)) throw new InvalidOperationException("Enter would steal address editor submission");
                results.Add($"PASS {(pane == LeftPane ? "A" : "B")} address editing keeps its own Enter behavior");
            }
            await LeftPane.NavigateAsync(root); SetActive(LeftPane); LeftPane.DebugSelectFile(child); LeftPane.FocusFiles();
            if (CanOpenListingWithEnter(true)) throw new InvalidOperationException("Modified Enter would be intercepted");
            results.Add("PASS modified Enter is not treated as plain file open");
            LeftPane.FocusFilter();
            if (CanOpenListingWithEnter(false)) throw new InvalidOperationException("Enter would steal text editing");
            results.Add("PASS filter text editing is excluded even while a folder remains selected");
        }
        catch (Exception ex) { results.Add("FAIL " + ex); }
        finally
        {
            await File.WriteAllLinesAsync(Path.Combine(Services.AppLog.DataDirectory, "enter-check.txt"), results);
            closing = true; Close();
        }
    }
}
#endif
