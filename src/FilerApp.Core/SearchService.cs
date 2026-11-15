using System.Text.RegularExpressions;
namespace FilerApp.Core;

public sealed record SearchOptions(string Folder, string Query, bool Content, bool Recursive, bool CaseSensitive, bool Regex, string Extensions, string ExcludedFolders, string Encoding, bool IncludeNames = false);
public sealed record SearchHit(string Path, int Line, string Text, bool Error = false)
{
    public FileEntry? Entry { get; init; }
    public string Name => System.IO.Path.GetFileName(Path);
    public string Label => Error ? $"⚠ {Path} — {Text}" : Line > 0 ? $"{Path}:{Line}  {Text}" : Path;
}
public sealed record SearchSummary(int Matches, int Warnings, bool LimitReached);
public sealed class SearchService
{
    public Task<SearchSummary> SearchAsync(SearchOptions options, IProgress<SearchHit> progress, CancellationToken token) => Task.Run(async () =>
    {
        if (string.IsNullOrWhiteSpace(options.Query)) throw new ArgumentException("検索文字列を入力してください。");
        var regex = options.Content && options.Regex ? new Regex(options.Query, options.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)) : null;
        var comparison = options.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var extensions = options.Extensions.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries).Select(x => "." + x.TrimStart('*', '.')).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var excluded = options.ExcludedFolders.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>(); pending.Push(System.IO.Path.GetFullPath(options.Folder));
        var matches = 0; var warnings = 0;
        void Warn(string path, string text) { warnings++; if (warnings <= 100) progress.Report(new(path, 0, text, true)); }
        while (pending.Count > 0)
        {
            token.ThrowIfCancellationRequested(); var folder = pending.Pop();
            IEnumerator<string>? iterator = null;
            try { iterator = Directory.EnumerateFileSystemEntries(folder).GetEnumerator(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Warn(folder, ex.Message); continue; }
            using (iterator)
            while (true)
            {
                token.ThrowIfCancellationRequested(); string path;
                try { if (!iterator.MoveNext()) break; path = iterator.Current; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Warn(folder, ex.Message); break; }
                try
                {
                    var attributes = File.GetAttributes(path);
                    if (attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                    if (attributes.HasFlag(FileAttributes.Directory))
                    {
                        if (excluded.Contains(System.IO.Path.GetFileName(path))) continue;
                        if ((!options.Content || options.IncludeNames) && extensions.Count == 0 && FileNames.Matches(System.IO.Path.GetFileName(path), options.Query, options.CaseSensitive))
                        {
                            var directory = new DirectoryInfo(path);
                            progress.Report(new(path, 0, "") { Entry = new(path, directory.Name, true, 0, directory.LastWriteTime) });
                            if (++matches >= 5000) return new SearchSummary(matches, warnings, true);
                        }
                        if (options.Recursive) pending.Push(path);
                        continue;
                    }
                    var extension = System.IO.Path.GetExtension(path);
                    if (extensions.Count > 0 && !extensions.Contains(extension)) continue;
                    var info = new FileInfo(path);
                    FileEntry? entry = null;
                    FileEntry Metadata() => entry ??= new(path, info.Name, false, info.Length, info.LastWriteTime);
                    if (!options.Content || options.IncludeNames)
                    {
                        if (FileNames.Matches(System.IO.Path.GetFileName(path), options.Query, options.CaseSensitive))
                        {
                            progress.Report(new(path, 0, "") { Entry = Metadata() });
                            if (++matches >= 5000) return new SearchSummary(matches, warnings, true);
                        }
                    }
                    if (options.Content && TextFiles.Extensions.Contains(extension))
                    {
                        if (new FileInfo(path).Length > 4 * 1024 * 1024) { Warn(path, "4 MBを超えるため内容検索を省略"); continue; }
                        var sample = await TextFiles.ReadAsync(path, options.Encoding, 4 * 1024 * 1024, token);
                        if (sample.Truncated) { Warn(path, "読み込み中に4 MBを超えたため省略"); continue; }
                        using var reader = new StringReader(sample.Text); string? line; var number = 0;
                        while ((line = reader.ReadLine()) != null)
                        {
                            token.ThrowIfCancellationRequested(); number++;
                            if (regex?.IsMatch(line) ?? line.Contains(options.Query, comparison))
                            { progress.Report(new(path, number, line.Length > 1000 ? line[..1000] + "…" : line) { Entry = Metadata() }); if (++matches >= 5000) return new SearchSummary(matches, warnings, true); }
                        }
                    }
                    if (matches >= 5000) return new SearchSummary(matches, warnings, true);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.DecoderFallbackException or RegexMatchTimeoutException) { Warn(path, ex.Message); }
            }
        }
        return new SearchSummary(matches, warnings, false);
    }, token);
}

