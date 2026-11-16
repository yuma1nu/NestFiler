namespace FilerApp.Services;

public sealed record DeletionItem(string Path, bool Permanent);

// Classification runs off the UI thread; UNC paths need no server access.
// Keep exactly the reviewed paths/modes after the confirmation dialog opens.
public sealed class DeletionPlan
{
    public IReadOnlyList<DeletionItem> Items { get; }
    public int PermanentCount => Items.Count(x => x.Permanent);
    private DeletionPlan(DeletionItem[] items) => Items = Array.AsReadOnly(items);
    public static Task<DeletionPlan> PrepareAsync(IEnumerable<string> paths)
    {
        var snapshot = paths.ToArray();
        return Task.Run(() => Create(snapshot, root => new DriveInfo(root).DriveType));
    }
    internal static DeletionPlan Create(IEnumerable<string> paths, Func<string, DriveType> driveType)
    {
        var items = new List<DeletionItem>();
        foreach (var input in paths)
        {
            if (!System.IO.Path.IsPathFullyQualified(input)) throw new ArgumentException("削除対象は絶対パスで指定してください。");
            var full = System.IO.Path.GetFullPath(input);
            var root = System.IO.Path.GetPathRoot(full)!;
            if (string.Equals(System.IO.Path.TrimEndingDirectorySeparator(full), System.IO.Path.TrimEndingDirectorySeparator(root), StringComparison.OrdinalIgnoreCase))
                throw new IOException("ドライブや共有フォルダのルートそのものは削除できません。");
            var probe = full.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase) && !full.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase) ? full[4..] : full;
            bool network = probe.StartsWith(@"\\", StringComparison.Ordinal);
            if (!network)
            {
                var type = driveType(System.IO.Path.GetPathRoot(probe)!);
                if (type == DriveType.Network) network = true;
                else if (type != DriveType.Fixed) throw new IOException("このドライブではごみ箱の利用を確認できないため、削除できません。");
            }
            if (!items.Any(x => string.Equals(x.Path, full, StringComparison.OrdinalIgnoreCase))) items.Add(new(full, network));
        }
        return new(items.ToArray());
    }
}
