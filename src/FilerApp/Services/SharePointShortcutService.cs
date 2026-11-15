using System.Runtime.InteropServices;
using System.Text;
using FilerApp.Core;
using Microsoft.Win32;

namespace FilerApp.Services;

public static class SharePointShortcutService
{
    public static Task<string> CreateAsync(string folder, SharePointLink link, OfficeApplication application, string name, CancellationToken token = default)
    {
        var filename = OfficeDocuments.ShortcutName(name, application);
        var url = link.DesktopUri(application);
        return Task.Run(async () =>
        {
            token.ThrowIfCancellationRequested();
            folder = Path.GetFullPath(folder);
            if (!Directory.Exists(folder)) throw new IOException("貼り付け先フォルダが存在しないか、アクセスできません。");
            var content = "[InternetShortcut]\r\nURL=" + url + "\r\n";
            if (GetIcon(OfficeDocuments.IconExtension(filename)) is { } icon)
                content += "IconFile=" + icon.Path + "\r\nIconIndex=" + icon.Index + "\r\n";
            var temporary = Path.Combine(folder, ".nest-shortcut-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                // Publish only the completed file. Both staging and final names
                // are created without overwrite, including a concurrent paste.
                await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
                {
                    var bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(content)).ToArray();
                    await stream.WriteAsync(bytes, token); await stream.FlushAsync(token);
                }
                var stem = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(filename));
                var extension = Path.GetExtension(Path.GetFileNameWithoutExtension(filename));
                for (int attempt = 1; attempt <= 10000; attempt++)
                {
                    token.ThrowIfCancellationRequested();
                    var target = Path.Combine(folder, attempt == 1 ? filename : $"{stem} ({attempt}){extension}.url");
                    try { File.Move(temporary, target, false); return target; }
                    catch (IOException) when (File.Exists(target) || Directory.Exists(target)) { }
                }
                throw new IOException("同名のショートカットが多すぎるため作成できませんでした。");
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }, token);
    }

    public static bool IsRegistered(OfficeApplication application)
    {
        try { using var key = Registry.ClassesRoot.OpenSubKey(OfficeDocuments.Scheme(application)); return key?.GetValue("URL Protocol") != null; }
        catch { return false; }
    }
    private static (string Path, int Index)? GetIcon(string extension)
    {
        try
        {
            var text = new StringBuilder(4096); uint length = (uint)text.Capacity;
            if (AssocQueryStringW(0, 15 /* ASSOCSTR_DEFAULTICON */, extension, null, text, ref length) != 0) return null;
            var value = text.ToString(); var comma = value.LastIndexOf(',');
            if (comma < 0 || !int.TryParse(value[(comma + 1)..], out var index)) return null;
            var path = Environment.ExpandEnvironmentVariables(value[..comma].Trim().Trim('"'));
            if (path.Any(char.IsControl) || !Path.IsPathFullyQualified(path) || path.StartsWith(@"\\") || !File.Exists(path)) return null;
            return (path, index);
        }
        catch { return null; }
    }
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int AssocQueryStringW(uint flags, uint str, string assoc, string? extra, StringBuilder output, ref uint length);
}
