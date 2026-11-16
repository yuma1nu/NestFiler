namespace FilerApp.Core;

public sealed class FavoriteNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "新しいフォルダ";
    public string? Path { get; set; }
    public bool IsFileLink { get; set; }
    public bool Expanded { get; set; } = true;
    public List<FavoriteNode> Children { get; set; } = [];
    public bool IsFolder => Path == null;
}

public static class FavoriteTree
{
    public static List<FavoriteNode> GetRoots(SessionState state) => state.FavoriteNodes ??= state.Favorites.Select(group => new FavoriteNode
    {
        Id = group.Id, Name = group.Name, Expanded = group.Expanded,
        Children = group.Items.Select(item => new FavoriteNode { Id = item.Id, Name = item.Name, Path = item.Path }).ToList()
    }).ToList();

    public static IEnumerable<FavoriteNode> Walk(IEnumerable<FavoriteNode> roots)
    {
        foreach (var node in roots) { yield return node; foreach (var child in Walk(node.Children)) yield return child; }
    }
    public static List<FavoriteNode>? FindList(List<FavoriteNode> roots, string id)
    {
        if (roots.Any(x => x.Id == id)) return roots;
        foreach (var node in roots) if (FindList(node.Children, id) is { } found) return found;
        return null;
    }
    public static bool CanMove(List<FavoriteNode> roots, string id, string? parentId)
    {
        var source = Walk(roots).FirstOrDefault(x => x.Id == id);
        if (source == null) return false;
        if (parentId == null) return true;
        var parent = Walk(roots).FirstOrDefault(x => x.Id == parentId);
        return parent is { IsFolder: true } && !Walk([source]).Any(x => x.Id == parentId);
    }
    public static void Move(List<FavoriteNode> roots, string id, string? parentId, string? beforeId = null)
    {
        if (!CanMove(roots, id, parentId)) throw new InvalidOperationException("自分自身や子孫の中には移動できません。");
        var source = FindList(roots, id)!; var item = source.First(x => x.Id == id);
        var destination = parentId == null ? roots : Walk(roots).First(x => x.Id == parentId).Children;
        if (beforeId != null && !destination.Any(x => x.Id == beforeId)) throw new InvalidOperationException("移動先が変更されました。");
        if (beforeId == id) return;
        source.Remove(item);
        destination.Insert(beforeId == null ? destination.Count : destination.FindIndex(x => x.Id == beforeId), item);
    }
}
