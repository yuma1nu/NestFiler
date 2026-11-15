using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FilerApp.Core;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    { if (EqualityComparer<T>.Default.Equals(field, value)) return false; field = value; Changed(name); return true; }
}
public sealed record FileEntry(string Path, string Name, bool IsDirectory, long Size, DateTime Modified)
{
    public FileAttributes Attributes { get; init; }
    public string SearchDetail { get; init; } = "";
    public int SearchLine { get; init; }
    public string SearchText { get; init; } = "";
    public string SearchMatch => SearchLine > 0 ? SearchText : SearchDetail.Length > 0 ? "名前に一致" : "";
    public string SearchLineLabel => SearchLine > 0 ? SearchLine.ToString() : "";
    public string DisplayTooltip => SearchDetail.Length > 0 ? Path + "\n" + SearchDetail : Path;
    public string Icon => IsDirectory ? "\uE8B7" : "\uE8A5";
    public string IconExtension => OfficeDocuments.IconExtension(Path);
    public string Kind => IsDirectory ? "フォルダ" : System.IO.Path.GetExtension(Path).Equals(".url", StringComparison.OrdinalIgnoreCase) && OfficeDocuments.ApplicationForExtension(IconExtension) is { } app ? app + " リンク" : System.IO.Path.GetExtension(Path).ToUpperInvariant();
    public string SizeLabel => IsDirectory ? "" : Size < 1024 ? $"{Size:N0} B" : Size < 1048576 ? $"{Size / 1024d:N1} KB" : $"{Size / 1048576d:N1} MB";
    public string ModifiedLabel => Modified.ToString("yyyy/MM/dd HH:mm");
}
public sealed record Favorite(string Id, string Name, string Path);
public sealed class FavoriteGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "お気に入り";
    public bool Expanded { get; set; } = true;
    public List<Favorite> Items { get; set; } = [];
}
public sealed record RecentEntry(string Path, bool Directory, DateTimeOffset Used);
public enum FileViewMode { Details, SmallIcons, LargeIcons }
public sealed class SessionState
{
    public List<TabSnapshot>? LeftTabs { get; set; }
    public List<TabSnapshot>? RightTabs { get; set; }
    public WindowPlacement? Window { get; set; }
    public List<RecentEntry> Recent { get; set; } = [];
    public List<string> Left { get; set; } = [];
    public List<string> Right { get; set; } = [];
    public List<SearchOptions?> LeftSearches { get; set; } = [];
    public List<SearchOptions?> RightSearches { get; set; } = [];
    public int LeftIndex { get; set; }
    public int RightIndex { get; set; }
    public FileViewMode LeftViewMode { get; set; }
    public FileViewMode RightViewMode { get; set; }
    public ColumnLayoutState? LeftColumns { get; set; }
    public ColumnLayoutState? LeftSearchColumns { get; set; }
    public ColumnLayoutState? RightColumns { get; set; }
    public ColumnLayoutState? RightSearchColumns { get; set; }
    public bool ActiveRight { get; set; }
    public bool PreviewVisible { get; set; }
    public string PreviewEncoding { get; set; } = "UTF-8";
    public bool Dual { get; set; } = true;
    public bool LeftSidebarVisible { get; set; } = true;
    public bool RightSidebarVisible { get; set; } = true;
    public double SidebarWidth { get; set; } = 230;
    public double RightSidebarWidth { get; set; } = 260;
    public double LeftSidebarRatio { get; set; } = 2;
    public double RightSidebarRatio { get; set; } = 1;
    public double PaneRatio { get; set; } = 1;
    public List<FavoriteGroup> Favorites { get; set; } = [];
    public List<FavoriteNode>? FavoriteNodes { get; set; }
}
public sealed record WindowPlacement(int X, int Y, int Width, int Height, bool Maximized);


