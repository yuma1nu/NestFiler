#if DEBUG
using FilerApp.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;

namespace FilerApp;
public sealed partial class MainWindow
{
    private async Task RunSharePointChecksAsync()
    {
        var results = new List<string>();
        var root = Services.AppLog.DataDirectory;
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); results.Add("PASS " + message); }
        const string link = "https://tenant.sharepoint.com/:p:/r/sites/lab/Documents/Report.pptx?web=1&e=fixture";
        try
        {
            var a = Directory.CreateDirectory(Path.Combine(root, "A")).FullName;
            var b = Directory.CreateDirectory(Path.Combine(root, "B")).FullName;
            await LeftPane.NavigateAsync(a); await RightPane.NavigateAsync(b);
            SetActive(LeftPane); LeftPane.FocusFiles();
            var text = new DataPackage(); text.SetText(link);
            Check(ListingCommand(VirtualKey.V, true, false) == "paste" && ListingCommand(VirtualKey.Insert, false, true) == "paste", "A listing routes Ctrl+V and Shift+Insert to paste");
            await PasteIntoPaneAsync(active, active.Current!.Path, text.GetView());
            var shortcut = Path.Combine(a, "Report.pptx.url");
            Check(File.Exists(shortcut) && !Directory.GetFiles(b).Any() && active == LeftPane, "text SharePoint paste creates an Office shortcut only in A");
            Check((await File.ReadAllTextAsync(shortcut)).Contains("URL=ms-powerpoint:ofe|u|" + link), "shortcut target is PowerPoint desktop URI, not browser HTTPS");
            Check(LeftPane.Current!.Entries.Any(x => x.Path == shortcut && x.IconExtension == ".pptx" && x.Kind == "PowerPoint リンク"), "A listing refreshes with PowerPoint shortcut type and icon association");

            foreach (var pane in new[] { LeftPane, RightPane })
            {
                SetActive(pane); pane.FocusAddress(); await Task.Delay(40);
                var address = FocusManager.GetFocusedElement(Root.XamlRoot) as TextBox;
                var count = Directory.GetFiles(a).Length + Directory.GetFiles(b).Length;
                Check(address != null && !CanPasteIntoListing() && ListingCommand(VirtualKey.V, true, false) == null && ListingCommand(VirtualKey.Insert, false, true) == null, (pane == LeftPane ? "A" : "B") + " address editor leaves Ctrl+V / Shift+Insert to native text paste");
                await PasteIntoPaneAsync(pane, pane.Current!.Path, text.GetView());
                Check(count == Directory.GetFiles(a).Length + Directory.GetFiles(b).Length, (pane == LeftPane ? "A" : "B") + " address focus prevents shortcut creation even through the paste handler");
                // This is an isolated profile; exercise the editor's selected text
                // insertion without ever replacing the user's system clipboard.
                address!.SelectedText = link;
                Check(address.Text == link, (pane == LeftPane ? "A" : "B") + " selected address text accepts the link as text");
                pane.FocusFiles();
            }
            SetActive(RightPane); RightPane.FocusFiles();
            var web = new DataPackage(); web.SetWebLink(new Uri("https://tenant.sharepoint.com/sites/lab/Documents/Notes.docx?e=fixture"));
            await PasteIntoPaneAsync(active, b, web.GetView());
            Check(File.Exists(Path.Combine(b, "Notes.docx.url")) && active == RightPane, "WebLink clipboard format creates a Word shortcut in B");

            var opaque = new DataPackage(); opaque.SetText("https://tenant.sharepoint.com/:x:/s/lab/EXopaque?e=fixture");
            debugChooseShortcut = _ => Task.FromResult<(OfficeApplication, string)?>(null);
            await PasteIntoPaneAsync(RightPane, b, opaque.GetView());
            Check(Directory.GetFiles(b).Length == 1 && !operating, "cancelled name dialog creates no shortcut and releases the operation guard");
            debugChooseShortcut = async parsed => { await Task.Yield(); SetActive(LeftPane); LeftPane.FocusFiles(); return (OfficeApplication.Excel, "実験データ"); };
            await PasteIntoPaneAsync(RightPane, b, opaque.GetView());
            Check(File.Exists(Path.Combine(b, "実験データ.xlsx.url")) && !File.Exists(Path.Combine(a, "実験データ.xlsx.url")) && active == LeftPane, "async name selection retains captured B destination and does not steal focus from A");
            debugChooseShortcut = null;

            var file = Path.Combine(root, "ordinary.txt"); await File.WriteAllTextAsync(file, "ordinary clipboard file");
            var mixed = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            mixed.SetStorageItems(new[] { await StorageFile.GetFileFromPathAsync(file) }); mixed.SetText(link);
            SetActive(RightPane); RightPane.FocusFiles();
            await PasteIntoPaneAsync(RightPane, b, mixed.GetView());
            Check(File.Exists(Path.Combine(b, "ordinary.txt")) && !File.Exists(Path.Combine(b, "Report.pptx.url")), "StorageItems take priority over text; existing file copy remains intact");
            await PasteIntoPaneAsync(RightPane, b, text.GetView());
            await PasteIntoPaneAsync(RightPane, b, text.GetView());
            Check(File.Exists(Path.Combine(b, "Report.pptx.url")) && File.Exists(Path.Combine(b, "Report (2).pptx.url")), "repeated paste creates numbered shortcuts without overwrite");
            var countBefore = Directory.GetFiles(b).Length;
            var search = RightPane.OpenSearchTab("Report", new SearchOptions(b, "Report", false, false, false, false, "", "", "UTF-8")); await search.SearchWork!;
            await PasteIntoPaneAsync(RightPane, b, text.GetView());
            Check(Directory.GetFiles(b).Length == countBefore, "search result tabs cannot receive shortcuts");
            await LeftPane.NavigateAsync(a); await RightPane.NavigateAsync(b);
            SetActive(LeftPane); LeftPane.FocusFiles();
            var shellPackage = new DataPackage(); FileTransferDataForCheck(shellPackage, [shortcut, file], false);
            Check(Services.FileTransferData.ReadNativePaths(shellPackage)?.SequenceEqual(new[] { shortcut, file }) == true, "Explorer-compatible CF_HDROP contains physical shortcut and ordinary file paths");
            Check((await Services.FileTransferData.GetPathsAsync(shellPackage.GetView())).SequenceEqual(new[] { shortcut, file }), "DataPackageView reads physical paths without StorageFile shortcut resolution");
            var destination = Directory.CreateDirectory(Path.Combine(root, "shortcut-copy")).FullName;
            await PasteFilesAsync(shellPackage.GetView(), destination);
            var pasted = Path.Combine(destination, Path.GetFileName(shortcut));
            Check(File.Exists(pasted) && File.Exists(shortcut) && File.Exists(Path.Combine(destination, "ordinary.txt")), "clipboard paste copies shortcut and normal file together");
            var movePackage = new DataPackage(); FileTransferDataForCheck(movePackage, [pasted], true);
            var movedFolder = Directory.CreateDirectory(Path.Combine(root, "shortcut-move")).FullName;
            await PasteFilesAsync(movePackage.GetView(), movedFolder);
            var movedPath = Path.Combine(movedFolder, Path.GetFileName(pasted));
            Check(File.Exists(movedPath) && !File.Exists(pasted) && File.Exists(shortcut), "clipboard cut-paste moves shortcut body without changing original target");
            await LeftPane.NavigateAsync(movedFolder); LeftPane.DebugSelectFile(movedPath);
            var deleted = await RunOperationAsync(Services.FileAction.Recycle, LeftPane.SelectedPaths, null);
            Check(deleted.Any(x => x.Result == "成功") && !File.Exists(movedPath) && File.Exists(shortcut), "UI file operation recycles shortcut body and leaves source shortcut intact");
            var favoriteFolder = Directory.CreateDirectory(Path.Combine(root, "favorite-folder")).FullName;
            var favoritesPackage = new DataPackage(); Services.FileTransferData.SetPaths(favoritesPackage, [favoriteFolder, shortcut, file]);
            var initialFavorites = FavoriteRoots.Count;
            await RegisterFavoriteDropAsync(favoritesPackage.GetView(), null, null);
            Check(FavoriteRoots.Count == initialFavorites + 3 && FavoriteRoots.Any(x => x.Path == favoriteFolder && !x.IsFileLink) && FavoriteRoots.Any(x => x.Path == shortcut && x.IsFileLink), "drop registers folders, files and Office shortcuts at favorites root without StorageFile");
            Check(File.Exists(shortcut) && File.Exists(file) && Directory.Exists(favoriteFolder) && favoritesPackage.RequestedOperation == DataPackageOperation.Copy, "favorite registration never moves or deletes physical source items");
            var virtualFolder = new FavoriteNode { Name = "入れ子", Expanded = false };
            var anchor = new FavoriteNode { Name = "既存", Path = a }; virtualFolder.Children.Add(anchor); FavoriteRoots.Add(virtualFolder);
            await RegisterFavoriteDropAsync(favoritesPackage.GetView(), virtualFolder, anchor.Id);
            Check(virtualFolder.Expanded && virtualFolder.Children.Count == 4 && virtualFolder.Children[0].Path == favoriteFolder && virtualFolder.Children[^1] == anchor, "drop into virtual folder respects insertion position and expands it");
            await RegisterFavoriteDropAsync(favoritesPackage.GetView(), virtualFolder, null);
            Check(virtualFolder.Children.Count == 4 && FavoriteRoots.Any(x => x.Path == shortcut), "duplicates in one group are skipped while another group may contain the same path");
            await store.SaveAsync(state); var saved = await store.LoadAsync();
            Check(FavoriteTree.Walk(FavoriteTree.GetRoots(saved)).Any(x => x.Path == shortcut && x.IsFileLink), "file favorite type and nested drop registrations survive SQLite restore");
            var partialDrop = new DataPackage(); Services.FileTransferData.SetPaths(partialDrop, [favoriteFolder, Path.Combine(root, "missing-favorite")]);
            await RegisterFavoriteDropAsync(partialDrop.GetView(), virtualFolder, null);
            Check(virtualFolder.Children.Count == 4 && Status.Text.Contains("登録失敗"), "missing drop item is reported without losing other registrations");
            foreach (var theme in new[] { ElementTheme.Light, ElementTheme.Dark })
            {
                Root.RequestedTheme = theme; RightPane.SetDisplayMode(FileViewMode.LargeIcons); RightPane.FocusFiles();
                DualToggle.IsChecked = true; Microsoft.UI.Xaml.VisualStateManager.GoToState(DualToggle, "Checked", false);
                await Task.Delay(150); Root.UpdateLayout();
                DependencyObject? FindPresenter(DependencyObject node)
                {
                    if (node is ContentPresenter) return node;
                    for (int i = 0; i < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(node); i++)
                        if (FindPresenter(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(node, i)) is { } found) return found;
                    return null;
                }
                var content = FindPresenter(DualToggle) as ContentPresenter;
                var brush = content?.Background as Microsoft.UI.Xaml.Media.SolidColorBrush;
                Check(brush != null && brush.Color.R == brush.Color.G && brush.Color.G == brush.Color.B && brush.Color.A is > 0 and <= 64, theme + " checked toolbar icon uses a translucent neutral background");
                RightPane.DebugAssertQuietSelection();
                results.Add("PASS " + theme + " file selection uses a translucent neutral background without accent indicator");
            }
            Root.RequestedTheme = ElementTheme.Default;
        }
        catch (Exception ex) { results.Add("FAIL " + ex); }
        finally
        {
            debugChooseShortcut = null;
            await File.WriteAllLinesAsync(Path.Combine(root, "sharepoint-check.txt"), results);
            closing = true; Close();
        }
    }
    private static void FileTransferDataForCheck(DataPackage package, string[] paths, bool move) => Services.FileTransferData.SetPaths(package, paths, move);
}
#endif
