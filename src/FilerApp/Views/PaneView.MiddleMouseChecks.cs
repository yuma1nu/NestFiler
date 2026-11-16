#if DEBUG
namespace FilerApp.Views;
public sealed partial class PaneView
{
    internal void DebugCloseBackgroundTab() => CloseModel(Models.First(x => x != Current));
    internal async Task DebugCheckMiddleCloseAsync(List<string> results)
    {
        var selected = Current; var count = Models.Count;
        var background = Models.First(x => x != selected); var index = Models.IndexOf(background); var path = background.Path;
        CloseModel(background); CloseModel(background); // A duplicate native request must be harmless.
        if (Models.Count != count - 1 || Current != selected) throw new InvalidOperationException("Close request removed the selected tab or closed twice");
        results.Add("PASS background tab close keeps selected tab and ignores duplicate requests");
        await RestoreClosedTabAsync();
        if (Models.Count != count || Models.IndexOf(Current!) != index || Current!.Path != path) throw new InvalidOperationException("Closed tab did not restore at its original index/path");
        results.Add("PASS closed-tab restoration retains original index and path");
        while (Models.Count > 1) CloseModel(Models.First(x => x != Current));
        var last = Current; int historyCount = closedTabs.Count;
        CloseModel(last!);
        if (Models.Count != 1 || Current != last || closedTabs.Count != historyCount) throw new InvalidOperationException("Last tab was closed or added to restore history");
        results.Add("PASS closing last tab keeps pane usable and does not create false restore history");
    }
}
#endif
