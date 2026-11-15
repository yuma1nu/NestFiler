using FilerApp.Core;
using FilerApp.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace FilerApp.Views;
public sealed partial class QuickPreviewDialog
{
    private void RenderTextPreview(PreviewResult result)
    {
        if (result.Text == null) return;
        TextPreview.Visibility = Visibility.Visible;
        if (result.Format == ".csv")
        {
            try
            {
                var grid = new Grid { ColumnSpacing = 12, RowSpacing = 4 };
                var rows = TextFiles.ParseCsv(result.Text);
                var columns = rows.Count == 0 ? 0 : rows.Max(x => x.Length);
                for (var c = 0; c < columns; c++) grid.ColumnDefinitions.Add(new() { Width = new GridLength(160) });
                for (var r = 0; r < rows.Count; r++)
                {
                    grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
                    for (var c = 0; c < rows[r].Length; c++)
                    {
                        var cell = new TextBlock { Text = rows[r][c], TextTrimming = TextTrimming.CharacterEllipsis, IsTextSelectionEnabled = true };
                        ToolTipService.SetToolTip(cell, rows[r][c]); Grid.SetRow(cell, r); Grid.SetColumn(cell, c); grid.Children.Add(cell);
                    }
                }
                TextPreview.Content = grid; PreviewInfo.Text += "\n表表示: 最大100行×30列、セル最大2,000文字"; return;
            }
            catch (Exception ex) { PreviewInfo.Text += $"\nCSV表を作成できません（切り詰め含む）: {ex.Message}"; }
        }
        if (result.Format is ".md" or ".markdown")
        {
            var stack = new StackPanel { Spacing = 5 };
            foreach (var line in result.Text.Split('\n').Take(500))
            {
                var heading = line.StartsWith("# ") ? 1 : line.StartsWith("## ") ? 2 : line.StartsWith("### ") ? 3 : 0;
                stack.Children.Add(new TextBlock { Text = heading == 0 ? line : line[(heading + 1)..], FontSize = heading == 1 ? 24 : heading == 2 ? 20 : heading == 3 ? 17 : 13, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true });
            }
            TextPreview.Content = stack; PreviewInfo.Text += "\nMarkdown簡易表示（見出し対応、最大500行）"; return;
        }
        var text = result.Text.Length > 32000 ? result.Text[..32000] : result.Text;
        TextPreview.Content = new TextBlock { Text = text, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Mono, Consolas"), FontSize = 13, IsTextSelectionEnabled = true, TextWrapping = TextWrapping.Wrap };
        if (text.Length < result.Text.Length) PreviewInfo.Text += "\n表示は先頭32,000文字まで";
    }
}
