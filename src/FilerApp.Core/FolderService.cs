namespace FilerApp.Core;

public interface IFolderService
{
    Task<IReadOnlyList<FileEntry>> ReadAsync(string path, CancellationToken cancellationToken);
}
public sealed class FolderService : IFolderService
{
    public Task<IReadOnlyList<FileEntry>> ReadAsync(string path, CancellationToken cancellationToken) => Task.Run<IReadOnlyList<FileEntry>>(() =>
    {
        var result = new List<FileEntry>();
        foreach (var info in new DirectoryInfo(path).EnumerateFileSystemInfos())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = (info.Attributes & FileAttributes.Directory) != 0;
            result.Add(new(info.FullName, info.Name, directory, info is FileInfo f ? f.Length : 0, info.LastWriteTime) { Attributes = info.Attributes });
        }
        return result.OrderByDescending(x => x.IsDirectory).ThenBy(x => x.Name, NaturalNameComparer.Instance).ToArray();
    }, cancellationToken);
}
