#if DEBUG
using System.Diagnostics;
using FilerApp.Core;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace FilerApp.Views;
public sealed partial class PaneView
{
    internal async Task DebugExerciseOutputAsync(string folder, FileViewMode mode, string scenario, List<string> results, Func<Task> favorite, Action<string> phase)
    {
        var clock = Stopwatch.StartNew();
        SetDisplayMode(mode); await NavigateAsync(folder);
        await Current!.SetFilterAsync(""); await Current.SetVisibilityAsync(true, true);
        await Task.Delay(200); Files.UpdateLayout();
        results.Add($"MEASURE {scenario} open+layout={clock.Elapsed.TotalMilliseconds:F1}ms entries={Current.Entries.Count}");
        if (Current.Entries.Count < 300) throw new InvalidOperationException("Actual output listing incomplete");
        var steps = new List<double>();
        phase("scroll");
        // Wheel-like small steps, then large jumps; include directory/file boundary.
        for (int i = 0; i < 100; i++)
        {
            clock.Restart();
            double position = i < 60 ? Math.Min(columnScroll!.ScrollableHeight, i * 112d) : columnScroll!.ScrollableHeight * (i % 11) / 10d;
            columnScroll.ChangeView(null, position, null, true);
            if (i % 10 == 0 && Environment.GetEnvironmentVariable("FILERAPP_OUTPUT_NO_SELECTION") != "1")
            {
                int index = Math.Min(Current.Entries.Count - 1, i * 4);
                Files.DeselectRange(new ItemIndexRange(0, (uint)Files.Items.Count));
                Files.SelectRange(new ItemIndexRange(Math.Max(0, index - 50), (uint)Math.Min(51, index + 1)));
            }
            await Task.Delay(20); steps.Add(clock.Elapsed.TotalMilliseconds);
            if (i % 20 == 0) await File.AppendAllTextAsync(Path.Combine(Services.AppLog.DataDirectory, "output-progress.txt"), $"{DateTimeOffset.Now:O} {scenario} step={i} elapsed={steps[^1]:F1}ms realized={Files.ItemsPanelRoot.Children.Count}\n");
        }
        var ordered = steps.Order().ToArray();
        results.Add($"MEASURE {scenario} scroll/selection step p95={ordered[95]:F1}ms max={ordered[^1]:F1}ms realized={Files.ItemsPanelRoot.Children.Count}/{Current.Entries.Count}");
        if (mode == FileViewMode.Details && Files.ItemsPanelRoot.Children.Count >= Current.Entries.Count) throw new InvalidOperationException("Output list lost virtualization");
        phase("favorite");
        await favorite();
    }
}
#endif
