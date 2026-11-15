using System.Text;
using Microsoft.VisualBasic.FileIO;
namespace FilerApp.Core;

public sealed record TextSample(string Text, bool Truncated, string EncodingName);
public static class TextFiles
{
    public static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase) { ".txt", ".md", ".markdown", ".csv", ".tsv", ".log", ".json", ".xml", ".yaml", ".yml", ".ini", ".cfg", ".cs", ".cpp", ".h", ".py", ".js", ".ts", ".sql", ".html", ".css" };
    public static Encoding GetEncoding(string name)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return name == "CP932" ? Encoding.GetEncoding(932, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback) : new UTF8Encoding(false, true);
    }
    public static async Task<TextSample> ReadAsync(string path, string encoding, int limit, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 8192, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var bytes = new byte[limit + 1]; var count = 0;
        while (count < bytes.Length)
        {
            var read = await stream.ReadAsync(bytes.AsMemory(count), token); if (read == 0) break; count += read;
        }
        var truncated = count > limit; count = Math.Min(count, limit);
        if (bytes.AsSpan(0, count).Contains((byte)0)) throw new InvalidDataException("バイナリまたは未対応文字コードです（UTF-8 / CP932のみ対応）。");
        var decoder = GetEncoding(encoding).GetDecoder(); var chars = new char[count + 1];
        decoder.Convert(bytes, 0, count, chars, 0, chars.Length, !truncated, out _, out var used, out _);
        return new(new string(chars, 0, used).TrimStart('\uFEFF'), truncated, encoding);
    }
    public static IReadOnlyList<string[]> ParseCsv(string text, int maxRows = 100, int maxColumns = 30)
    {
        using var parser = new TextFieldParser(new StringReader(text)); parser.SetDelimiters(","); parser.HasFieldsEnclosedInQuotes = true;
        var rows = new List<string[]>();
        while (!parser.EndOfData && rows.Count < maxRows) rows.Add((parser.ReadFields() ?? []).Take(maxColumns).Select(x => x.Length > 2000 ? x[..2000] + "…" : x).ToArray());
        return rows;
    }
}
