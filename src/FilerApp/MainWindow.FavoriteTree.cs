using FilerApp.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace FilerApp;
public sealed partial class MainWindow
{
    private sealed record FavoriteDestination(string Label, FavoriteNode? Folder);
    private List<FavoriteNode> FavoriteRoots => FavoriteTree.GetRoots(state);
    private FavoriteNode? selectedFavoriteFolder;
    private async Task CreateFavoriteFolderAsync(FavoriteNode? parent)
    {
        if (await AskAsync("仮想フォルダ名", "新しいフォルダ") is not { } name) return;
        (parent?.Children ?? FavoriteRoots).Add(new() { Name = name });
        if (parent != null) parent.Expanded = true;
        RenderFavorites(); ScheduleSave();
    }
    private async Task AddCurrentFavoriteAsync()
    {
        if (active.Current is not { } tab || string.IsNullOrEmpty(tab.Path)) return;
        var choices = new List<FavoriteDestination> { new("お気に入り（最上位）", null) };
        void AddChoices(IEnumerable<FavoriteNode> nodes, string prefix)
        {
            foreach (var folder in nodes.Where(x => x.IsFolder))
            { var label = prefix + folder.Name; choices.Add(new(label, folder)); AddChoices(folder.Children, label + " / "); }
        }
        AddChoices(FavoriteRoots, "");
        var destination = new ComboBox { ItemsSource = choices, DisplayMemberPath = "Label", SelectedIndex = Math.Max(0, choices.FindIndex(x => x.Folder == selectedFavoriteFolder)), HorizontalAlignment = HorizontalAlignment.Stretch };
        var name = new TextBox { Text = tab.Title };
        var path = tab.Path; // Capture before displaying the dialog.
        var panel = new StackPanel { Spacing = 8 }; panel.Children.Add(name); panel.Children.Add(destination);
        var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, Title = "お気に入りに登録", Content = panel, PrimaryButtonText = "登録", CloseButtonText = "キャンセル" };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary || string.IsNullOrWhiteSpace(name.Text)) return;
        var parent = ((FavoriteDestination)destination.SelectedItem).Folder;
        (parent?.Children ?? FavoriteRoots).Add(new() { Name = name.Text.Trim(), Path = path });
        if (parent != null) parent.Expanded = true;
        RenderFavorites(); ScheduleSave();
    }
    private void RenderFavorites()
    {
        FavoriteList.Children.Clear();
        void Render(List<FavoriteNode> nodes, FavoriteNode? parent, StackPanel host, int depth)
        {
            foreach (var node in nodes)
            {
                var children = new StackPanel { Spacing = 0, Visibility = node.Expanded ? Visibility.Visible : Visibility.Collapsed };
                var row = SidebarRow(node.Name, "\uE8B7", node.IsFolder);
                row.AllowDrop = true;
                row.Margin = new Thickness(depth * 14, 0, 0, 0);
                if (node.IsFolder)
                {
                    var content = (Grid)row.Content;
                    content.Children.RemoveAt(0);
                    content.Children.Insert(0, new Views.FileTypeIcon { IconSize = 16, Entry = new("", node.Name, true, 0, DateTime.MinValue) });
                    if (depth == 0) row.Style = (Style)Application.Current.Resources["FavoriteSectionRow"];
                    content.ColumnDefinitions.Insert(0, new() { Width = new GridLength(10) });
                    Grid.SetColumn((FrameworkElement)content.Children[0], 1); Grid.SetColumn((FrameworkElement)content.Children[1], 2);
                    var chevron = new FontIcon { Glyph = node.Expanded ? "\uE70D" : "\uE76C", FontSize = 9, VerticalAlignment = VerticalAlignment.Center };
                    content.Children.Add(chevron);
                    children.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, (_, _) => chevron.Glyph = children.Visibility == Visibility.Visible ? "\uE70D" : "\uE76C");
                    row.Click += (_, _) =>
                    {
                        selectedFavoriteFolder = node; node.Expanded = !node.Expanded;
                        children.Visibility = node.Expanded ? Visibility.Visible : Visibility.Collapsed;
                        chevron.Glyph = node.Expanded ? "\uE70D" : "\uE76C"; ScheduleSave();
                    };
                }
                else
                {
                    if (node.IsFileLink)
                    {
                        var content = (Grid)row.Content; content.Children.RemoveAt(0);
                        content.Children.Insert(0, new Views.FileTypeIcon { IconSize = 16, Entry = new(node.Path!, node.Name, false, 0, DateTime.MinValue) });
                    }
                    row.Click += async (_, _) => { if (node.IsFileLink) await active.OpenFileAsync(node.Path!); else await active.NavigateAsync(node.Path!); };
                    Views.MiddleClick.Attach(row, () => OpenPathInNewTabAsync(node.Path!, !node.IsFileLink), ShowError);
                }
                ToolTipService.SetToolTip(row, node.IsFolder ? $"{node.Name}（整理用の仮想フォルダ）\n中央にドロップ: 中へ移動 / 上下端: 前後へ移動" : node.Path + (node.IsFileLink ? "\n中クリック: 格納フォルダを新しいタブで開く" : "\n中クリック: 新しいタブで開く"));
                var menu = new MenuFlyout();
                if (node.IsFolder) menu.Items.Add(Menu("この中に仮想フォルダを作成", async (_, _) => await CreateFavoriteFolderAsync(node)));
                else menu.Items.Add(Menu(node.IsFileLink ? "格納フォルダを新しいタブで開く" : "新しいタブで開く", async (_, _) => await OpenPathInNewTabAsync(node.Path!, !node.IsFileLink)));
                menu.Items.Add(Menu("名前を変更", async (_, _) => { if (await AskAsync("表示名", node.Name) is { } name) { node.Name = name; RenderFavorites(); ScheduleSave(); } }));
                menu.Items.Add(Menu("最上位へ移動", (_, _) => { FavoriteTree.Move(FavoriteRoots, node.Id, null); RenderFavorites(); ScheduleSave(); }));
                menu.Items.Add(Menu(node.IsFolder ? "仮想フォルダと配下の登録を解除" : "登録解除", async (_, _) =>
                {
                    if (node.IsFolder && node.Children.Count > 0)
                    {
                        var confirm = new ContentDialog { XamlRoot = Root.XamlRoot, Title = "お気に入りから登録解除", Content = $"「{node.Name}」と配下の登録を解除します。実ファイル・実フォルダは変更しません。", PrimaryButtonText = "登録解除", CloseButtonText = "キャンセル" };
                        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
                    }
                    FavoriteTree.FindList(FavoriteRoots, node.Id)?.Remove(node);
                    if (selectedFavoriteFolder != null && FavoriteTree.Walk([node]).Contains(selectedFavoriteFolder)) selectedFavoriteFolder = null;
                    RenderFavorites(); ScheduleSave();
                }));
                row.ContextFlyout = menu;
                var marker = new Border { Child = row, BorderThickness = new Thickness(0), MinHeight = 26 };
                AttachTreeDrag(row, node); AttachTreeDrop(marker, node, parent, nodes, children);
                host.Children.Add(marker);
                if (node.IsFolder) { host.Children.Add(children); Render(node.Children, node, children, depth + 1); }
            }
        }
        Render(FavoriteRoots, null, FavoriteList, 0);
    }
}


