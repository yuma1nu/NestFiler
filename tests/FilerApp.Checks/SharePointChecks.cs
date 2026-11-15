using FilerApp.Core;
using FilerApp.Services;

internal static class SharePointChecks
{
    public static async Task RunAsync(string root, Action<bool, string> check)
    {
        var direct = SharePointLink.Parse("https://tenant.sharepoint.com/sites/lab/Documents/%E5%AE%9F%E9%A8%93.pptx?web=1&e=secret");
        check(direct.Application == OfficeApplication.PowerPoint && direct.SuggestedName == "実験.pptx" && direct.DesktopUri(OfficeApplication.PowerPoint).StartsWith("ms-powerpoint:ofe|u|https://") && direct.DesktopUri(OfficeApplication.PowerPoint).EndsWith("?web=1&e=secret"), "SharePoint日本語名とPowerPointデスクトップURI・共有パラメータ保持");
        var opaque = SharePointLink.Parse("https://tenant.sharepoint.com/:p:/s/Lab/EXopaque?e=secret");
        check(opaque.Application == OfficeApplication.PowerPoint && opaque.SuggestedName == null, "名前を含まない共有リンクは推測せず入力対象にする");
        var word = SharePointLink.Parse("https://tenant-my.sharepoint.com/:w:/r/personal/user/Documents/notes.docx?d=123&csf=1&web=1");
        var excel = SharePointLink.Parse("https://tenant.sharepoint.com/sites/lab/_layouts/15/Doc.aspx?sourcedoc=%7B123%7D&file=Data+2026.xlsm&action=default");
        check(word.Application == OfficeApplication.Word && word.DesktopUri(OfficeApplication.Word).StartsWith("ms-word:ofe|u|") && excel.Application == OfficeApplication.Excel && excel.SuggestedName == "Data 2026.xlsm" && excel.DesktopUri(OfficeApplication.Excel).StartsWith("ms-excel:ofe|u|"), "Word・Excel・Doc.aspxのfileパラメータを解釈");
        var unknown = SharePointLink.Parse("https://tenant.sharepoint.com/s/opaque?e=abc");
        check(unknown.Application == null && unknown.SuggestedName == null, "不透明リンクはアプリも選択対象にする");
        foreach (var bad in new[] { "https://example.com/test.pptx", "https://tenant.sharepoint.com.evil.test/a.pptx", "http://tenant.sharepoint.com/a.pptx", "file:///C:/report.pptx", "https://user:password@tenant.sharepoint.com/a.pptx", "https://tenant.sharepoint.com/a.pptx\r\nIconFile=evil", "https://tenant.sharepoint.com/:f:/s/team/opaque", "https://tenant.sharepoint.com/file.pdf", "https://tenant.sharepoint.com/:p:/r/report.docx" })
        {
            bool rejected = false; try { SharePointLink.Parse(bad); } catch (ArgumentException) { rejected = true; }
            check(rejected, "対象外URL・複数行・形式不一致を拒否: " + (bad.Contains('\r') ? "改行" : new Uri(bad).Scheme + ":" + new Uri(bad).Host + new Uri(bad).AbsolutePath));
        }
        check(SharePointLink.Parse("https://tenant.sharepoint.com/test.pptx?x=a|b").DesktopUri(OfficeApplication.PowerPoint).EndsWith("x=a%7Cb"), "Office URIの区切り文字を共有URL内でエスケープ");
        foreach (var name in new[] { "", "..", "../escape", "report.docx", "CON", "com1.pptx", "LPT².pptx", "bad\nname", "bad.", new string('a', 161) })
        {
            bool rejected = false; try { OfficeDocuments.ShortcutName(name, OfficeApplication.PowerPoint); } catch (ArgumentException) { rejected = true; }
            check(rejected, "不正・予約・形式不一致のショートカット名を拒否");
        }
        check(OfficeDocuments.ShortcutName("報告", OfficeApplication.PowerPoint) == "報告.pptx.url" && OfficeDocuments.ShortcutName("報告.pptm.url", OfficeApplication.PowerPoint) == "報告.pptm.url", "拡張子補完と二重url防止");
        var folder = Directory.CreateDirectory(Path.Combine(root, "sharepoint-shortcuts")).FullName;
        var first = await SharePointShortcutService.CreateAsync(folder, direct, OfficeApplication.PowerPoint, "報告");
        var original = await File.ReadAllBytesAsync(first);
        var content = await File.ReadAllTextAsync(first);
        check(content.StartsWith("[InternetShortcut]\r\nURL=ms-powerpoint:ofe|u|") && content.Contains("e=secret") && original[0] == 0xff && original[1] == 0xfe, "実ファイルはUTF-16のInternetShortcut・共有URLの認証情報を保持");
        check(await InternetShortcutReader.ReadAsync(first) == direct.DesktopUri(OfficeApplication.PowerPoint), "Windows標準InternetShortcutでOffice起動URI・日本語URLを正確に読み取り");
        var parallel = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => SharePointShortcutService.CreateAsync(folder, direct, OfficeApplication.PowerPoint, "報告")));
        check(parallel.Distinct().Count() == 3 && parallel.All(File.Exists) && original.AsEnumerable().SequenceEqual(await File.ReadAllBytesAsync(first)) && Directory.GetFiles(folder).Length == 4, "同時貼り付けの同名衝突でも連番作成・既存内容保持・一時ファイル残留なし");
        var entries = await new FolderService().ReadAsync(folder, default);
        check(entries.All(x => x.IconExtension == ".pptx" && x.Kind == "PowerPoint リンク"), "作成したショートカットを通常列挙でPowerPointリンクと認識");
        check(OfficeDocuments.IconExtension("normal.url") == ".url" && OfficeDocuments.IconExtension("report.pptx.url") == ".pptx" && OfficeDocuments.IconExtension("real.pptx") == ".pptx", "ショートカットのアイコン判定はファイル名だけで完了");
        try { await SharePointShortcutService.CreateAsync(folder, direct, OfficeApplication.PowerPoint, "cancel", new CancellationToken(true)); throw new Exception("キャンセル未検出"); }
        catch (OperationCanceledException) { check(Directory.GetFiles(folder).Length == 4, "作成前キャンセルでファイルを残さない"); }
        try { await SharePointShortcutService.CreateAsync(Path.Combine(root, "unreachable-shortcut-target"), direct, OfficeApplication.PowerPoint, "failure"); throw new Exception("失敗未検出"); }
        catch (IOException) { check(true, "到達不能な貼り付け先は失敗を返す"); }
        try { await SharePointShortcutService.CreateAsync(first, direct, OfficeApplication.PowerPoint, "failure"); throw new Exception("失敗未検出"); }
        catch (IOException) { check(original.AsEnumerable().SequenceEqual(await File.ReadAllBytesAsync(first)), "ファイルを貼り付け先に指定しても元データを変更しない"); }
    }
}
