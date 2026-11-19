#if DEBUG
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FilerApp.Views;
public sealed partial class PaneView
{
    // Invoke the actual template buttons, rather than the model callbacks: a
    // retemplate can silently lose TabViewItem's native CloseButton connection.
    internal async Task DebugCheckNavigationButtonsAsync(List<string> results)
    {
        static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i); yield return child;
                foreach (var nested in Descendants(child)) yield return nested;
            }
        }
        static void Invoke(Button button)
            => ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();

        Tabs.UpdateLayout();
        var selected = Current!; var count = Models.Count;
        var background = Models.First(x => x != selected); var index = Models.IndexOf(background);
        var tab = Descendants(Tabs).OfType<TabViewItem>().Single(x => x.Tag == background);
        Invoke(Descendants(tab).OfType<Button>().Single(x => x.Name == "CloseButton"));
        await Task.Delay(100);
        if (Models.Count != count - 1 || Models.Contains(background) || Current != selected)
            throw new InvalidOperationException("Pill tab close button lost its native close request.");
        await RestoreClosedTabAsync();
        if (Models.Count != count || Models.IndexOf(Current!) != index || Current!.Path != background.Path)
            throw new InvalidOperationException("Closed pill tab did not restore its path/index.");
        Tabs.SelectedItem = selected;
        results.Add("PASS native pill-tab close button preserves selected background tab, and closed-tab restoration retains path/index.");

        var path = selected.Path;
        FocusSearch(); PaneSearch.Text = "README"; Invoke(SearchSubmitButton);
        await Task.Delay(100);
        var search = Current;
        if (search?.IsSearch != true || search.Search?.Query != "README")
            throw new InvalidOperationException("Embedded search button did not open the requested search tab.");
        if (search.SearchWork is { } work) await work.WaitAsync(TimeSpan.FromSeconds(10));
        if (!search.Entries.Any(x => x.Path == Path.Combine(path, "README.md")) || !Models.Contains(selected))
            throw new InvalidOperationException("Embedded search button lost results or replaced the original tab.");
        CloseCurrent(); Tabs.SelectedItem = selected; PaneSearch.Text = ""; FocusFiles();
        results.Add("PASS embedded search button opens a search tab with real filename results and preserves the original folder tab.");
    }
}
#endif
