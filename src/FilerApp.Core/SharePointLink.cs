namespace FilerApp.Core;

public enum OfficeApplication { Word, Excel, PowerPoint }

// No HTTP requests: copied sharing tokens stay in the local shortcut, and opaque
// links are never assumed to reveal the document's name or actual permissions.
public sealed record SharePointLink
{
    public Uri Document { get; }
    public OfficeApplication? Application { get; }
    public string? SuggestedName { get; }
    private SharePointLink(Uri document, OfficeApplication? application, string? name)
        => (Document, Application, SuggestedName) = (document, application, name);

    public static SharePointLink Parse(string text)
    {
        text = text.Trim();
        if (text.Length is 0 or > 16384 || text.Any(char.IsControl) ||
            !Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            uri.UserInfo.Length != 0 || !IsSharePointHost(uri.IdnHost))
            throw new ArgumentException("SharePointのHTTPS共有リンクを1件コピーしてください。");

        string? name = null;
        foreach (var pair in uri.Query.TrimStart('?').Split('&'))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length != 2) continue;
            var key = Uri.UnescapeDataString(parts[0]);
            if (key.Equals("file", StringComparison.OrdinalIgnoreCase))
                name = Uri.UnescapeDataString(parts[1].Replace('+', ' '));
        }
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var marker = segments.FirstOrDefault(x => x.Length == 3 && x[0] == ':' && x[2] == ':')?.ToLowerInvariant();
        OfficeApplication? hint = marker switch { ":w:" => OfficeApplication.Word, ":x:" => OfficeApplication.Excel, ":p:" => OfficeApplication.PowerPoint, _ => null };
        if (marker != null && hint == null)
            throw new ArgumentException("デスクトップアプリ用リンクはWord・Excel・PowerPointのファイルに対応しています。フォルダや他の形式には対応していません。");
        name ??= segments.Length > 0 ? Uri.UnescapeDataString(segments[^1]) : null;
        var extension = Path.GetExtension(name ?? "");
        var fromName = OfficeDocuments.ApplicationForExtension(extension);
        if (fromName != null && hint != null && fromName != hint)
            throw new ArgumentException("リンクの種類とファイルの拡張子が一致しません。ファイルの共有リンクをコピーし直してください。");
        // An opaque /s/.../token is not a filename. Known non-Office files must
        // not silently get a browser shortcut or be labelled as an Office file.
        if (fromName == null)
        {
            if (extension.Length > 0 && !extension.Equals(".aspx", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("このファイル形式のデスクトップ起動には対応していません。Word・Excel・PowerPointの共有リンクを指定してください。");
            name = null;
        }
        return new(uri, fromName ?? hint, name == null ? null : OfficeDocuments.SafeSuggestedName(name));
    }

    private static bool IsSharePointHost(string host) => new[] { ".sharepoint.com", ".sharepoint.us", ".sharepoint.cn", ".sharepoint.de", ".sharepoint-mil.us" }
        .Any(suffix => host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && host.Length > suffix.Length);

    public string DesktopUri(OfficeApplication application)
    {
        if (Application is { } known && known != application) throw new ArgumentException("リンクに対応するOfficeアプリを選択してください。");
        return OfficeDocuments.Scheme(application) + ":ofe|u|" + Document.AbsoluteUri.Replace("|", "%7C");
    }
}

public static class OfficeDocuments
{
    public static OfficeApplication? ApplicationForExtension(string extension) => extension.ToLowerInvariant() switch
    {
        ".doc" or ".docx" or ".docm" or ".dot" or ".dotx" or ".dotm" or ".rtf" => OfficeApplication.Word,
        ".xls" or ".xlsx" or ".xlsm" or ".xlsb" or ".xlt" or ".xltx" or ".xltm" => OfficeApplication.Excel,
        ".ppt" or ".pptx" or ".pptm" or ".pps" or ".ppsx" or ".ppsm" or ".pot" or ".potx" or ".potm" => OfficeApplication.PowerPoint,
        _ => null
    };
    public static string Scheme(OfficeApplication application) => application switch
    { OfficeApplication.Word => "ms-word", OfficeApplication.Excel => "ms-excel", OfficeApplication.PowerPoint => "ms-powerpoint", _ => throw new ArgumentOutOfRangeException(nameof(application)) };
    public static string Extension(OfficeApplication application) => application switch
    { OfficeApplication.Word => ".docx", OfficeApplication.Excel => ".xlsx", OfficeApplication.PowerPoint => ".pptx", _ => throw new ArgumentOutOfRangeException(nameof(application)) };

    // Office type icons can be obtained from the filename alone. No shortcut
    // content, network target, or per-row Shell lookup is needed during scrolling.
    public static string IconExtension(string path)
    {
        var extension = Path.GetExtension(path);
        if (extension.Equals(".url", StringComparison.OrdinalIgnoreCase))
        {
            var inner = Path.GetExtension(Path.GetFileNameWithoutExtension(path));
            if (ApplicationForExtension(inner) != null) return inner;
        }
        return extension;
    }

    internal static string SafeSuggestedName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c).ToArray()).Trim().TrimEnd('.');
    }

    public static string ShortcutName(string input, OfficeApplication application)
    {
        var name = input.Trim();
        if (name.EndsWith(".url", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        if (name.Length == 0 || name.Length > 160 || name is "." or ".." || name.EndsWith('.') ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Any(char.IsControl))
            throw new ArgumentException("名前は160文字以内で、ファイル名に使用できる文字を入力してください。");
        var stem = name.Split('.')[0].TrimEnd(' ');
        if (new[] { "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$", "COM¹", "COM²", "COM³", "LPT¹", "LPT²", "LPT³" }.Contains(stem, StringComparer.OrdinalIgnoreCase) ||
            stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && stem[3] is >= '1' and <= '9')
            throw new ArgumentException("Windowsの予約名は使用できません。");
        var extension = Path.GetExtension(name);
        if (extension.Length == 0) name += Extension(application);
        else if (ApplicationForExtension(extension) != application)
            throw new ArgumentException("名前の拡張子を選択したOfficeアプリに合わせてください。");
        return name + ".url";
    }
}
