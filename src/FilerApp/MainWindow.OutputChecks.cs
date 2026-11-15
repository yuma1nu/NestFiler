#if DEBUG
using System.Collections.Concurrent;
using System.Diagnostics;
using FilerApp.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;

namespace FilerApp;
public sealed partial class MainWindow
{
    private void StartOutputLiveProbe()
    {
        var cancellation = new CancellationTokenSource(); Closed += (_, _) => cancellation.Cancel();
        var file = Path.Combine(Services.AppLog.DataDirectory, "live-queue.txt");
        _ = Task.Run(async () =>
        {
            try
            {
                while (!cancellation.IsCancellationRequested)
                {
                    long queued = Stopwatch.GetTimestamp(); var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                    if (!DispatcherQueue.TryEnqueue(() => completion.TrySetResult($"{DateTimeOffset.Now:O} queue={Stopwatch.GetElapsedTime(queued).TotalMilliseconds:F1}ms window={AppWindow.Size.Width}x{AppWindow.Size.Height} A={LeftPane.Current?.Entries.Count}/{LeftPane.DisplayMode} B={RightPane.Current?.Entries.Count}/{RightPane.DisplayMode}"))) break;
                    // Log the timeout on the worker too, even if UI is not dispatching.
                    string line;
                    try { line = await completion.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellation.Token); }
                    catch (TimeoutException) { line = $"{DateTimeOffset.Now:O} UI queue exceeded 2000ms"; }
                    await File.AppendAllTextAsync(file, line + Environment.NewLine, cancellation.Token);
                    await Task.Delay(250, cancellation.Token);
                }
            }
            catch (OperationCanceledException) { }
        });
    }
    private async Task RunOutputChecksAsync()
    {
        var results = new List<string>();
        // Read the actual build output, without adding or altering any of its files.
        var folder = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
        var small = Directory.CreateDirectory(Path.Combine(Services.AppLog.DataDirectory, "favorite-empty")).FullName;
        try
        {
            await File.AppendAllTextAsync(Path.Combine(Services.AppLog.DataDirectory, "output-progress.txt"), $"{DateTimeOffset.Now:O} checks started\n");
            var inventory = await Task.Run(() => new DirectoryInfo(folder).GetFileSystemInfos());
            results.Add($"TARGET {folder}");
            results.Add("SELECTION during scroll=" + (Environment.GetEnvironmentVariable("FILERAPP_OUTPUT_NO_SELECTION") != "1"));
            results.Add($"INVENTORY total={inventory.Length}, files={inventory.Count(x => x is FileInfo)}, directories={inventory.Count(x => x is DirectoryInfo)}");
            results.Add("EXTENSIONS " + string.Join(", ", inventory.GroupBy(x => x.Extension).OrderByDescending(x => x.Count()).Select(x => $"{x.Key}:{x.Count()}")));
            FavoriteRoots.Add(new() { Name = "output-check-favorite", Path = small }); RenderFavorites();
            var button = ((Border)FavoriteList.Children[^1]).Child as Button ?? throw new InvalidOperationException("Missing favorite");
            await RightPane.NavigateAsync(small);
            foreach (var scenario in new[] { "details-cold", "details-two-panes", "small-icons-two-panes", "details-maximized", "small-icons-maximized" })
            {
                var mode = scenario.StartsWith("details") ? FileViewMode.Details : FileViewMode.SmallIcons;
                if (scenario == "details-maximized" && AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter) { presenter.Maximize(); await Task.Delay(300); }
                results.Add($"WINDOW {scenario} {AppWindow.Size.Width}x{AppWindow.Size.Height} scale={Root.XamlRoot.RasterizationScale:F2}");
                await File.AppendAllTextAsync(Path.Combine(Services.AppLog.DataDirectory, "output-progress.txt"), $"{DateTimeOffset.Now:O} {scenario} started\n");
                if (scenario != "details-cold") { await RightPane.NavigateAsync(folder); RightPane.SetDisplayMode(mode); }
                SetActive(LeftPane);
                var queueTimes = new ConcurrentQueue<(string Phase, double Delay)>();
                string phase = "open";
                using var sampling = new CancellationTokenSource();
                var worker = Task.Run(async () =>
                {
                    int pending = 0;
                    try
                    {
                        while (true)
                        {
                            await Task.Delay(25, sampling.Token);
                            if (Interlocked.CompareExchange(ref pending, 1, 0) != 0) continue;
                            long queued = Stopwatch.GetTimestamp(); string queuedPhase = Volatile.Read(ref phase);
                            if (!DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () => { queueTimes.Enqueue((queuedPhase, Stopwatch.GetElapsedTime(queued).TotalMilliseconds)); Interlocked.Exchange(ref pending, 0); })) break;
                        }
                    }
                    catch (OperationCanceledException) { }
                });
                try
                {
                    await LeftPane.DebugExerciseOutputAsync(folder, mode, scenario, results, async () =>
                    {
                        var dispatch = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);
                        var invocation = Task.Run(() =>
                        {
                            long queued = Stopwatch.GetTimestamp();
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                dispatch.TrySetResult(Stopwatch.GetElapsedTime(queued).TotalMilliseconds);
                                ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
                            });
                        });
                        await invocation;
                        var clock = Stopwatch.StartNew();
                        double delay = await dispatch.Task.WaitAsync(TimeSpan.FromSeconds(5));
                        for (int i = 0; i < 250 && (LeftPane.Current?.Path != small || LeftPane.Current.Busy); i++) await Task.Delay(10);
                        if (LeftPane.Current?.Path != small || LeftPane.Current.Busy || LeftPane.Current.Entries.Count != 0) throw new InvalidOperationException("Favorite did not complete navigation");
                        results.Add($"MEASURE {scenario} favorite queue={delay:F1}ms navigation={clock.Elapsed.TotalMilliseconds:F1}ms");
                    }, next => Volatile.Write(ref phase, next));
                }
                finally { sampling.Cancel(); await worker; }
                foreach (var group in queueTimes.GroupBy(x => x.Phase))
                { var values = group.Select(x => x.Delay).Order().ToArray(); results.Add($"MEASURE {scenario} {group.Key} queue p95={values[(int)(values.Length * .95)]:F1}ms max={values[^1]:F1}ms samples={values.Length}"); }
                var samples = queueTimes.Select(x => x.Delay).Order().ToArray();
                if (samples.Length < 10 || samples[^1] > 1000) throw new InvalidOperationException("UI queue stalled during output-folder test");
                results.Add($"MEASURE {scenario} UI queue p95={samples[(int)(samples.Length * .95)]:F1}ms max={samples[^1]:F1}ms samples={samples.Length}");
                results.Add("PASS " + scenario + " scroll/selection/favorite and UI queue complete");
                await File.WriteAllLinesAsync(Path.Combine(Services.AppLog.DataDirectory, "output-check.txt"), results);
            }
            await LeftPane.NavigateAsync(folder); LeftPane.SetDisplayMode(FileViewMode.Details);
            await RightPane.NavigateAsync(folder); RightPane.SetDisplayMode(FileViewMode.Details); SetActive(LeftPane);
            var node = new TreeViewNode { Content = "Debug output", HasUnrealizedChildren = true };
            places[node] = folder; PlacesTree.RootNodes.Add(node);
            var treeClock = Stopwatch.StartNew(); await PopulatePlaceAsync(node, folder); node.IsExpanded = true; await Task.Delay(100);
            results.Add($"MEASURE actual-output tree children={node.Children.Count} elapsed={treeClock.Elapsed.TotalMilliseconds:F1}ms");
            if (node.Children.Count != inventory.Count(x => x is DirectoryInfo && (x.Attributes & (FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint)) == 0)) throw new InvalidOperationException("Output tree children disagree");
            results.Add("PASS actual-output tree populated while both file panes display output");
        }
        catch (Exception ex) { results.Add("FAIL " + ex); }
        finally
        {
            await File.WriteAllLinesAsync(Path.Combine(Services.AppLog.DataDirectory, "output-check.txt"), results);
            if (Environment.GetEnvironmentVariable("FILERAPP_OUTPUT_HOLD") != "1") { closing = true; Close(); }
        }
    }
}
#endif
