using FilerApp.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FilerApp;
public sealed partial class MainWindow
{
    private readonly Dictionary<TreeViewNode, string> places = [];
    private readonly HashSet<TreeViewNode> expandingPlaces = [];
    private int placesVersion;
    private readonly Dictionary<TreeViewNode, CancellationTokenSource> placeLoads = [];
    private void InitializePlaces()
    {
        PlacesTree.Collapsed += (_, args) =>
        {
            if (placeLoads.Remove(args.Node, out var cancellation)) { cancellation.Cancel(); expandingPlaces.Remove(args.Node); if (args.Node.HasUnrealizedChildren) ForgetPlaceChildren(args.Node); }
        };
        Closed += (_, _) => { foreach (var cancellation in placeLoads.Values) cancellation.Cancel(); };
    }
    private async Task LoadPlacesAsync()
    {
        int version = ++placesVersion;
        var roots = await Task.Run(() => DriveInfo.GetDrives().Select(x => x.Name).ToArray());
        if (closing || version != placesVersion) return;
        foreach (var cancellation in placeLoads.Values) cancellation.Cancel();
        PlacesTree.RootNodes.Clear(); places.Clear(); expandingPlaces.Clear(); placeLoads.Clear();
        void Add(string label, string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            var node = new TreeViewNode { Content = label, HasUnrealizedChildren = true }; places[node] = path; PlacesTree.RootNodes.Add(node);
        }
        Add("ホーム", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        Add("デスクトップ", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        Add("ドキュメント", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        foreach (var path in roots) Add(path, path);
    }
    private async void RefreshPlaces(object sender, RoutedEventArgs e) { try { await LoadPlacesAsync(); } catch (Exception ex) { ShowError("場所を取得できません: " + ex.Message); } }
    private async void ExpandPlace(TreeView sender, TreeViewExpandingEventArgs args)
    {
        var node = args.Node;
        if (!node.HasUnrealizedChildren || !places.TryGetValue(node, out var path) || !expandingPlaces.Add(node)) return;
        await PopulatePlaceAsync(node, path);
    }
    private async Task PopulatePlaceAsync(TreeViewNode node, string path)
    {
        using var cancellation = new CancellationTokenSource();
        placeLoads[node] = cancellation;
        var token = cancellation.Token;
        int version = placesVersion;
        try
        {
            var folders = await Task.Run(() => new DirectoryInfo(path).EnumerateDirectories("*", new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint })
                .Take(501).Select(x => (x.Name, x.FullName)).OrderBy(x => x.Name, NaturalNameComparer.Instance).ToArray()).WaitAsync(TimeSpan.FromSeconds(10), token);
            if (closing || version != placesVersion || token.IsCancellationRequested) return;
            for (int i = 0; i < Math.Min(500, folders.Length); i++)
            {
                token.ThrowIfCancellationRequested();
                var folder = folders[i]; var child = new TreeViewNode { Content = folder.Name, HasUnrealizedChildren = true }; places[child] = folder.FullName; node.Children.Add(child);
                // Building hundreds of native TreeViewNodes in one callback
                // blocks clicks, scrolling and folder navigation in other panes.
                if (i % 24 == 23) await Task.Delay(1, token);
            }
            node.HasUnrealizedChildren = false;
            if (folders.Length > 500) Status.Text = "ツリーは先頭500フォルダまで表示しています。全項目は中央の一覧で確認できます。";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ShowError("フォルダツリーを展開できません: " + ex.Message); }
        finally { if (placeLoads.TryGetValue(node, out var current) && ReferenceEquals(current, cancellation)) { placeLoads.Remove(node); expandingPlaces.Remove(node); } }
    }
    private void ForgetPlaceChildren(TreeViewNode node)
    {
        foreach (var child in node.Children) { ForgetPlaceChildren(child); places.Remove(child); }
        if (node.Children.Count > 0) node.Children.Clear();
    }
    private async void OpenPlace(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is TreeViewNode node && places.TryGetValue(node, out var path)) await active.NavigateAsync(path);
    }
}
