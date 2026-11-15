using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace FilerApp;
public sealed partial class MainWindow
{
    private static Button SidebarRow(string text, string glyph, bool heading = false)
    {
        var content = new Grid { ColumnSpacing = 6 };
        content.ColumnDefinitions.Add(new() { Width = new GridLength(18) });
        content.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        content.Children.Add(new FontIcon { Glyph = glyph, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        var label = new TextBlock { Text = text, FontSize = heading ? 12 : 13, FontWeight = heading ? FontWeights.SemiBold : FontWeights.Normal, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(label, 1); content.Children.Add(label);
        var row = new Button { Content = content, Style = (Style)Application.Current.Resources["FlatRowButton"] };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(row, text);
        return row;
    }
}
