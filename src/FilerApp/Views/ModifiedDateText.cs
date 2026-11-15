using FilerApp.Core;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI.ViewManagement;

namespace FilerApp.Views;
public sealed class ModifiedDateText : Grid
{
    private readonly TextBlock text = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
    private static readonly AccessibilitySettings accessibility = new();
    private static readonly HashSet<ModifiedDateText> visible = [];
    private static readonly DispatcherTimer clock = new() { Interval = TimeSpan.FromSeconds(15) };
    private static bool highContrast = accessibility.HighContrast;
    private FileEntry? renderedEntry;
    private ModificationAge renderedAge;
    private ElementTheme renderedTheme;
    private bool renderedHighContrast;
    public static readonly DependencyProperty EntryProperty = DependencyProperty.Register(nameof(Entry), typeof(FileEntry), typeof(ModifiedDateText), new PropertyMetadata(null, (owner, _) => ((ModifiedDateText)owner).Refresh()));
    public FileEntry? Entry { get => (FileEntry?)GetValue(EntryProperty); set => SetValue(EntryProperty, value); }
    internal TextBlock DateText => text;
    static ModifiedDateText() { clock.Tick += (_, _) => { highContrast = accessibility.HighContrast; foreach (var date in visible.ToArray()) date.Refresh(); }; }
    public ModifiedDateText()
    {
        Children.Add(text);
        ActualThemeChanged += (_, _) => Refresh();
        // HighContrastChanged subscription can fail for an unpackaged desktop
        // process. Poll with the shared clock instead; no per-row event hooks.
        Loaded += (_, _) => { visible.Add(this); clock.Start(); Refresh(); };
        Unloaded += (_, _) => { visible.Remove(this); if (visible.Count == 0) clock.Stop(); };
    }
    internal void Refresh()
    {
        if (Entry is not { } entry) { text.Text = ""; return; }
        var age = ModificationDates.Classify(entry.Modified, DateTime.Now);
        if (ReferenceEquals(renderedEntry, entry) && renderedAge == age && renderedTheme == ActualTheme && renderedHighContrast == highContrast) return;
        renderedEntry = entry; renderedAge = age; renderedTheme = ActualTheme; renderedHighContrast = highContrast;
        var palette = (ResourceDictionary)Application.Current.Resources.ThemeDictionaries[highContrast ? "HighContrast" : ActualTheme == ElementTheme.Light ? "Light" : "Dark"];
        text.Text = age == ModificationAge.Unknown ? "—" : entry.ModifiedLabel;
        text.Foreground = (Brush)palette["Modified" + age + "Brush"];
        text.FontWeight = age == ModificationAge.LastDay ? FontWeights.SemiBold : FontWeights.Normal;
        var explanation = age switch { ModificationAge.LastDay => "24時間以内", ModificationAge.LastWeek => "7日以内", ModificationAge.LastMonth => "30日以内", ModificationAge.LastYear => "1年以内", ModificationAge.Older => "1年以上前", ModificationAge.Future => "未来の更新日時（端末の時刻もご確認ください）", _ => "更新日時不明" };
        ToolTipService.SetToolTip(this, $"更新日時: {text.Text} — {explanation}");
    }
}
