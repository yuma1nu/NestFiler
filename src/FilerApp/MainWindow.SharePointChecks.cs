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
        }
        catch (Exception ex) { results.Add("FAIL " + ex); }
        finally
        {
            debugChooseShortcut = null;
            await File.WriteAllLinesAsync(Path.Combine(root, "sharepoint-check.txt"), results);
            closing = true; Close();
        }
    }
}
#endif
