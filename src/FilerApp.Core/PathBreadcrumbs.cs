namespace FilerApp.Core;
public sealed record PathCrumb(string Name, string Path);
public static class PathBreadcrumbs
{
    public static IReadOnlyList<PathCrumb> Build(string path)
    {
        var full = System.IO.Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
        var root = System.IO.Path.GetPathRoot(full)!;
        var result = new List<PathCrumb> { new(root.TrimEnd('\\', '/'), root) };
        var current = root;
        foreach (var part in full[root.Length..].Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries))
        { current = System.IO.Path.Combine(current, part); result.Add(new(part, current)); }
        return result;
    }
}
