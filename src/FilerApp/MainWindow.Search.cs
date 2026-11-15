using FilerApp.Views;
using Microsoft.UI.Xaml;
namespace FilerApp;
public sealed partial class MainWindow
{
    private void ShowPaneSearch(PaneView pane, string text)
    {
        SetActive(pane);
        if (pane.Current?.IsSearch == true && string.IsNullOrWhiteSpace(text)) pane.FocusSearch();
        else pane.OpenSearchTab(text);
    }
    private void OpenSearchTab(object sender, RoutedEventArgs e) => ShowPaneSearch(active, "");
    private void InitializeSearch()
    {
        LeftPane.SearchRequested += ShowPaneSearch;
        RightPane.SearchRequested += ShowPaneSearch;
    }
}
