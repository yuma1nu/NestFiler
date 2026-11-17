using FilerApp.Core;

namespace FilerApp.Services;

public enum CollisionChoice { KeepBoth, Replace, Skip, Cancel }
public sealed record FileCollision(string Source, string Destination, bool SourceDirectory, bool DestinationDirectory);
public sealed record CollisionResolution(CollisionChoice Choice, bool ApplyToAll = false);

internal static class CollisionNames
{
    internal static bool SameFolder(string source, string destination) => string.Equals(
        Path.TrimEndingDirectorySeparator(Path.GetDirectoryName(Path.GetFullPath(source)) ?? ""),
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination)), StringComparison.OrdinalIgnoreCase);

    internal static string Available(string folder, string source, bool directory, CancellationToken token)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(source));
        var extension = directory ? "" : Path.GetExtension(name);
        if (name.StartsWith('.') && name.IndexOf('.', 1) < 0) extension = "";
        // Keep the Office extension immediately before .url so the file retains
        // its PowerPoint/Word/Excel association icon after duplication.
        if (extension.Equals(".url", StringComparison.OrdinalIgnoreCase))
        {
            var inner = Path.GetExtension(Path.GetFileNameWithoutExtension(name));
            if (OfficeDocuments.ApplicationForExtension(inner) != null) extension = inner + extension;
        }
        var stem = name[..(name.Length - extension.Length)];
        for (int i = 1; i <= 100000; i++)
        {
            token.ThrowIfCancellationRequested();
            var candidate = $"{stem} ({i}){extension}";
            var path = Path.Combine(folder, candidate);
            if (!File.Exists(path) && !Directory.Exists(path)) return candidate;
        }
        throw new IOException("同名の項目が多すぎるため、両方残す名前を作成できません。");
    }
}
