namespace FilerApp.Core;

public sealed record FileColumn(string Key, double Width);
public sealed class ColumnLayoutState
{
    public List<FileColumn> Columns { get; set; } = [];
    public bool FillName { get; set; }
}

// One small layout model for each pane and list kind. It contains no UI state.
public sealed class FileColumns
{
    private readonly bool search;
    private readonly List<FileColumn> columns = [];
    public IReadOnlyList<FileColumn> Columns => columns;
    public bool FillName { get; private set; }
    public double TotalWidth => columns.Sum(x => x.Width);
    public event Action? Changed;
    public FileColumns(bool search, ColumnLayoutState? saved = null) { this.search = search; Restore(saved); }
    public static string Title(string key) => key switch { "Name" => "名前", "Modified" => "更新日時", "Kind" => "種類", "Size" => "サイズ", "Match" => "一致内容", "Line" => "行番号", "Path" => "パス", _ => key };
    private List<FileColumn> Defaults() => search ? [new("Name", 180), new("Match", 240), new("Line", 56), new("Path", 260), new("Modified", 132), new("Kind", 64), new("Size", 80)] : [new("Name", 180), new("Modified", 132), new("Kind", 64), new("Size", 80)];
    private static double Minimum(string key) => key switch { "Name" or "Match" => 80, "Path" => 100, "Modified" => 88, "Kind" or "Line" => 40, _ => 52 };
    private static double Width(string key, double width) => double.IsFinite(width) ? Math.Clamp(width, Minimum(key), 2048) : Math.Max(Minimum(key), 120);
    public void Restore(ColumnLayoutState? saved)
    {
        var defaults = Defaults(); var keys = defaults.Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
        columns.Clear();
        foreach (var column in saved?.Columns ?? []) if (keys.Remove(column.Key)) columns.Add(new(column.Key, Width(column.Key, column.Width)));
        columns.AddRange(defaults.Where(x => keys.Contains(x.Key)));
        FillName = saved?.FillName ?? !search; Changed?.Invoke();
    }
    public void Reset() => Restore(null);
    public void Resize(string key, double width)
    {
        int index = columns.FindIndex(x => x.Key == key); if (index < 0) return;
        var wasFilling = FillName; FillName = false; var updated = Width(key, width);
        if (columns[index].Width == updated) { if (wasFilling) Changed?.Invoke(); return; }
        columns[index] = new(key, updated); Changed?.Invoke();
    }
    public void Fit(double viewportWidth)
    {
        if (!FillName || !double.IsFinite(viewportWidth) || viewportWidth <= 0) return;
        int index = columns.FindIndex(x => x.Key == "Name");
        var width = Width("Name", viewportWidth - columns.Where(x => x.Key != "Name").Sum(x => x.Width));
        if (Math.Abs(columns[index].Width - width) < 0.5) return;
        columns[index] = new("Name", width); Changed?.Invoke();
    }
    public void Move(string source, string target, bool before)
    {
        if (source == target) return;
        int from = columns.FindIndex(x => x.Key == source), to = columns.FindIndex(x => x.Key == target);
        if (from < 0 || to < 0) return;
        var moving = columns[from]; columns.RemoveAt(from);
        to = columns.FindIndex(x => x.Key == target); columns.Insert(to + (before ? 0 : 1), moving); Changed?.Invoke();
    }
    public ColumnLayoutState Capture() => new() { Columns = columns.ToList(), FillName = FillName };
}
