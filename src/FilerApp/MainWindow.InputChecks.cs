#if DEBUG
namespace FilerApp;
public sealed partial class MainWindow
{
    private async Task RunInputChecksAsync()
    {
        var results = new List<string>();
        try
        {
            await LeftPane.DebugMeasureSelectionAsync(Services.AppLog.DataDirectory, results);
            string folder = Path.Combine(Services.AppLog.DataDirectory, "input-fixture");
            var other = Directory.CreateDirectory(Path.Combine(Services.AppLog.DataDirectory, "mouse-history")).FullName;
            await LeftPane.NavigateAsync(other); await NavigateMouseHistoryAsync(LeftPane, false);
            if (active != LeftPane || LeftPane.Current!.Path != folder) throw new InvalidOperationException("Mouse back route did not navigate pane A");
            await NavigateMouseHistoryAsync(LeftPane, true);
            if (active != LeftPane || LeftPane.Current!.Path != other) throw new InvalidOperationException("Mouse forward route did not navigate pane A");
            results.Add("PASS mouse history command routes back/forward through pane A");
            var originalA = LeftPane.Current;
            await RightPane.NavigateAsync(folder); await RightPane.NavigateAsync(other);
            await NavigateMouseHistoryAsync(RightPane, false);
            if (active != RightPane || RightPane.Current!.Path != folder || LeftPane.Current != originalA) throw new InvalidOperationException("Mouse history route affected the other pane");
            results.Add("PASS mouse history route uses pane B independently of pane A");
            await LeftPane.NavigateAsync(folder); SetActive(LeftPane); LeftPane.FocusFiles();
        }
        catch (Exception ex) { results.Add("FAIL " + ex); }
        finally
        {
            await File.WriteAllLinesAsync(Path.Combine(Services.AppLog.DataDirectory, "input-check.txt"), results);
            if (Environment.GetEnvironmentVariable("FILERAPP_INPUT_HOLD") != "1") { closing = true; Close(); }
        }
    }
}
#endif
