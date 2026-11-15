using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
namespace FilerApp;
public sealed partial class MainWindow
{
    private void RestoreLayout()
    {
        LeftPane.RestoreColumns(state.LeftColumns, state.LeftSearchColumns); RightPane.RestoreColumns(state.RightColumns, state.RightSearchColumns);
        LeftPane.SetDisplayMode(state.LeftViewMode); RightPane.SetDisplayMode(state.RightViewMode);
        var total = Workspace.ActualWidth > 0 ? Workspace.ActualWidth : 1430;
        var centerMinimum = state.Dual ? 645 : 360;
        var history = Math.Clamp(state.RightSidebarWidth, 180, Math.Max(180, total - centerMinimum - 150));
        SidebarColumn.Width = new(Math.Clamp(state.SidebarWidth, 140, Math.Max(140, total - centerMinimum - history - 10)));
        HistoryColumn.Width = new(history);
        LeftColumn.Width = new(Math.Clamp(state.PaneRatio, 0.01, 100), GridUnitType.Star);
        FavoritesRow.Height = new(Math.Clamp(state.LeftSidebarRatio, 0.001, 1000), GridUnitType.Star);
        RecentFilesRow.Height = new(Math.Clamp(state.RightSidebarRatio, 0.001, 1000), GridUnitType.Star);
    }
    private void ChoosePaneLayout(object sender, RoutedEventArgs e)
    {
        // These two buttons are choices, not independent toggles.
        DualToggle.IsChecked = (sender as ToggleButton) == DualToggle;
        ApplyDual(); ApplySidebarVisibility(); ScheduleSave();
    }
    private void ApplyDual()
    {
        bool dual = DualToggle.IsChecked == true;
        SinglePaneToggle.IsChecked = !dual;
        CenterColumn.MinWidth = dual ? 645 : 360;
        if (!dual && active == RightPane) { SetActive(LeftPane); if (ready) LeftPane.FocusFiles(); }
        RightPane.Visibility = PaneDivider.Visibility = dual ? Visibility.Visible : Visibility.Collapsed;
        RightColumn.MinWidth = dual ? 320 : 0;
        RightColumn.Width = dual ? new(1, GridUnitType.Star) : new(0);
        DividerColumn.Width = new(dual ? 5 : 0);
    }
    private void ToggleSidebar(object sender, RoutedEventArgs e)
    {
        bool isLeft = (sender as ToggleButton) == LeftSidebarToggle;
        if (isLeft && LeftSidebarToggle.IsChecked != true && SidebarColumn.ActualWidth > 0) state.SidebarWidth = SidebarColumn.ActualWidth;
        if (!isLeft && RightSidebarToggle.IsChecked != true && HistoryColumn.ActualWidth > 0) state.RightSidebarWidth = HistoryColumn.ActualWidth;
        var hidden = isLeft ? LeftSidebar : RightSidebar;
        if ((isLeft ? LeftSidebarToggle.IsChecked : RightSidebarToggle.IsChecked) != true)
        {
            for (var node = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(Root.XamlRoot) as DependencyObject; node != null; node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(node))
                if (node == hidden) { active.FocusFiles(); break; }
        }
        ApplySidebarVisibility(); ScheduleSave();
    }
    private void ApplySidebarVisibility()
    {
        bool left = LeftSidebarToggle.IsChecked == true, right = RightSidebarToggle.IsChecked == true;
        LeftSidebar.Visibility = SidebarDivider.Visibility = left ? Visibility.Visible : Visibility.Collapsed;
        RightSidebar.Visibility = HistoryDivider.Visibility = right ? Visibility.Visible : Visibility.Collapsed;
        SidebarColumn.MinWidth = left ? 140 : 0; HistoryColumn.MinWidth = right ? 180 : 0;
        double total = Workspace.ActualWidth > 0 ? Workspace.ActualWidth : 1430;
        double rightWidth = right ? Math.Clamp(state.RightSidebarWidth, 180, Math.Max(180, total - CenterColumn.MinWidth - (left ? 140 : 0) - 10)) : 0;
        double leftWidth = left ? Math.Clamp(state.SidebarWidth, 140, Math.Max(140, total - CenterColumn.MinWidth - rightWidth - 10)) : 0;
        SidebarColumn.Width = new(leftWidth); HistoryColumn.Width = new(rightWidth);
        SidebarDividerColumn.Width = new(left ? 5 : 0); HistoryDividerColumn.Width = new(right ? 5 : 0);
    }
    private void ResizeSidebar(object sender, DragDeltaEventArgs e)
    {
        var maximum = Math.Max(140, Workspace.ActualWidth - HistoryColumn.ActualWidth - CenterColumn.MinWidth - 10);
        SidebarColumn.Width = new(Math.Clamp(SidebarColumn.ActualWidth + e.HorizontalChange, 140, maximum)); ScheduleSave();
    }
    private void ResizeHistory(object sender, DragDeltaEventArgs e)
    {
        var maximum = Math.Max(180, Workspace.ActualWidth - SidebarColumn.ActualWidth - CenterColumn.MinWidth - 10);
        HistoryColumn.Width = new(Math.Clamp(HistoryColumn.ActualWidth - e.HorizontalChange, 180, maximum)); ScheduleSave();
    }
    private void ResizePanes(object sender, DragDeltaEventArgs e)
    {
        var total = Panes.ActualWidth - 5; if (total <= 640) return;
        var left = Math.Clamp(LeftColumn.ActualWidth + e.HorizontalChange, 320, total - 320);
        state.PaneRatio = left / (total - left); LeftColumn.Width = new(state.PaneRatio, GridUnitType.Star); ScheduleSave();
    }
    private static void ResizeRows(RowDefinition first, RowDefinition last, double delta, double minimumFirst, double minimumLast)
    {
        var total = first.ActualHeight + last.ActualHeight; if (total <= minimumFirst + minimumLast) return;
        var top = Math.Clamp(first.ActualHeight + delta, minimumFirst, total - minimumLast);
        first.Height = new(top / (total - top), GridUnitType.Star); last.Height = new(1, GridUnitType.Star);
    }
    private void ResizeLeftSections(object sender, DragDeltaEventArgs e) { ResizeRows(FavoritesRow, LeftEmptyRow, e.VerticalChange, 80, 60); ScheduleSave(); }
    private void ResizeRightSections(object sender, DragDeltaEventArgs e) { ResizeRows(RecentFilesRow, RecentFoldersRow, e.VerticalChange, 80, 80); ScheduleSave(); }
}

