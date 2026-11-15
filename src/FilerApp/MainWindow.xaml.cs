using FilerApp.Core;
using FilerApp.Services;
using FilerApp.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.System;
using Windows.Storage.Streams;

namespace FilerApp;
public sealed partial class MainWindow : Window
{
    private readonly StateStore store = new(Path.Combine(AppLog.DataDirectory, "state.db"));
    private SessionState state = new();
    private PaneView active = null!;
    private bool ready, closing, stateWritable = true;


    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    public MainWindow()
    {
        InitializeComponent();
        Root.PreviewKeyDown += PreviewSpaceKeyDown;
        Root.PreviewKeyUp += PreviewSpaceKeyUp;
        Root.PreviewKeyDown += RootKey;
        Root.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(MouseNavigationPressed), true);
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1440, 900));
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter) { presenter.PreferredMinimumWidth = 1200; presenter.PreferredMinimumHeight = 600; }
        active = LeftPane; LeftPane.SetLabel("A"); RightPane.SetLabel("B");
        InitializeOperations(); InitializeSearch(); InitializeFavoriteRootDrop(); InitializePlaces();
        Root.SizeChanged += (_, _) => { ApplyDual(); previewDialog?.FitTo(Root.ActualWidth, Root.ActualHeight); };
        Root.ActualThemeChanged += (_, _) => { if (previewDialog != null) previewDialog.RequestedTheme = Root.ActualTheme; };
        foreach (var pane in new[] { LeftPane, RightPane })
        {
            pane.MayAutoRefresh = CanRefreshListing;
            pane.Visited += RecordRecent;
            pane.ContextMenuRequested += ShowContext;
            pane.Activated += SetActive; pane.StateChanged += ScheduleSave; pane.Error += ShowError;
            pane.SelectionUpdated += entry => { if (pane == active) _ = UpdatePreviewAsync(); ScheduleSave(); };
        }
        Activated += (_, args) => { if (ready && args.WindowActivationState != WindowActivationState.Deactivated) { LeftPane.RecheckFolder(); RightPane.RecheckFolder(); } };
        AppWindow.Changed += (_, _) => { if (ready) { CaptureWindowPlacement(); ScheduleSave(); } };
        saveTimer.Tick += async (_, _) => { saveTimer.Stop(); await SaveAsync(); };
        Root.Loaded += Initialize;
        AppWindow.Closing += async (_, e) =>
        {
            if (closing) return;
            if (operating) { e.Cancel = true; Status.Text = "ファイル操作を終了してから閉じてください。"; return; }
            e.Cancel = true; LeftPane.StopAllLoads(); RightPane.StopAllLoads(); previewDialog?.Hide(); saveTimer.Stop(); await SaveAsync(); closing = true; Close();
        };
    }
    private async void Initialize(object sender, RoutedEventArgs e)
    {
        Root.Loaded -= Initialize;
        try { state = await store.LoadAsync(); } catch (Exception ex) { stateWritable = false; ShowError($"設定の読み込みに失敗しました。元の設定を保護するため保存を停止します: {ex.Message}"); }
        if (Environment.GetEnvironmentVariable("FILERAPP_PROFILE_DIR") != null && state.Left.Count == 0) { state.Left = [AppLog.DataDirectory]; state.Right = [AppLog.DataDirectory]; }
        RestoreWindowPlacement(); RestoreLayout();
        DualToggle.IsChecked = state.Dual; LeftSidebarToggle.IsChecked = state.LeftSidebarVisible; RightSidebarToggle.IsChecked = state.RightSidebarVisible;
        ApplyDual(); ApplySidebarVisibility(); RenderFavorites(); RenderRecent(); SetActive(LeftPane);
        await Task.WhenAll(LeftPane.InitializeAsync(state.Left, state.LeftIndex, state.LeftSearches, state.LeftTabs), RightPane.InitializeAsync(state.Right, state.RightIndex, state.RightSearches, state.RightTabs));
        previewEncoding = state.PreviewEncoding == "CP932" ? "CP932" : "UTF-8"; previewVisible = false;
        SetActive(state.ActiveRight && state.Dual ? RightPane : LeftPane);
        ready = true; Status.Text = "";
        _ = LoadPlacesAsync();
        if (!stateWritable) Status.Text = "設定を読み込めなかったため、保存を停止しています。元のstate.dbを保持しています。";
        else if (store.RecoveredBackup) Status.Text = "設定の読み込みに失敗したため、直前の保存内容から復旧しました。";
#if DEBUG
        if (Environment.GetEnvironmentVariable("FILERAPP_SHAREPOINT_CHECK") == "1" && Environment.GetEnvironmentVariable("FILERAPP_PROFILE_DIR") != null) { await RunSharePointChecksAsync(); return; }
        if (Environment.GetEnvironmentVariable("FILERAPP_OUTPUT_LIVE_PROBE") == "1" && Environment.GetEnvironmentVariable("FILERAPP_PROFILE_DIR") != null) StartOutputLiveProbe();
        if (Environment.GetEnvironmentVariable("FILERAPP_OUTPUT_CHECK") == "1" && Environment.GetEnvironmentVariable("FILERAPP_PROFILE_DIR") != null) { await RunOutputChecksAsync(); return; }
        if (Environment.GetEnvironmentVariable("FILERAPP_RESPONSIVENESS_CHECK") == "1" && Environment.GetEnvironmentVariable("FILERAPP_PROFILE_DIR") != null) { await RunResponsivenessChecksAsync(); return; }
        if (Environment.GetEnvironmentVariable("FILERAPP_INPUT_CHECK") == "1" && Environment.GetEnvironmentVariable("FILERAPP_PROFILE_DIR") != null) { await RunInputChecksAsync(); return; }
        await RunPaneFocusChecksAsync();
#endif
    }
    private void SetActive(PaneView pane)
    {
        active = pane; LeftPane.SetActive(pane == LeftPane); RightPane.SetActive(pane == RightPane);
        ActiveLabel.Text = pane == LeftPane ? "Aペイン" : "Bペイン";
        _ = UpdatePreviewAsync();
    }
    private void ShowError(string message) { Status.Text = message; AppLog.Error(message); }
    private void ScheduleSave() { if (!ready) return; saveTimer.Stop(); saveTimer.Start(); }
    private async Task SaveAsync()
    {
        if (!ready || !stateWritable) return;
        LeftPane.CapturePosition(); RightPane.CapturePosition(); CaptureWindowPlacement();
        state.LeftTabs = LeftPane.Models.Select(x => x.Capture()).ToList(); state.RightTabs = RightPane.Models.Select(x => x.Capture()).ToList();
        state.ActiveRight = active == RightPane; state.PreviewVisible = false; state.PreviewEncoding = previewEncoding;
        state.Left = LeftPane.Models.Select(x => x.Path).ToList(); state.Right = RightPane.Models.Select(x => x.Path).ToList();
        state.LeftSearches = LeftPane.Models.Select(x => x.Search).ToList(); state.RightSearches = RightPane.Models.Select(x => x.Search).ToList();
        state.LeftIndex = LeftPane.SelectedIndex; state.RightIndex = RightPane.SelectedIndex;
        state.LeftViewMode = LeftPane.DisplayMode; state.RightViewMode = RightPane.DisplayMode;
        state.LeftColumns = LeftPane.NormalColumns.Capture(); state.LeftSearchColumns = LeftPane.SearchColumns.Capture();
        state.RightColumns = RightPane.NormalColumns.Capture(); state.RightSearchColumns = RightPane.SearchColumns.Capture();
        state.Dual = DualToggle.IsChecked == true;
        state.LeftSidebarVisible = LeftSidebarToggle.IsChecked == true; state.RightSidebarVisible = RightSidebarToggle.IsChecked == true;
        if (state.LeftSidebarVisible) state.SidebarWidth = SidebarColumn.ActualWidth;
        if (state.RightSidebarVisible) state.RightSidebarWidth = HistoryColumn.ActualWidth;
        state.LeftSidebarRatio = FavoritesRow.Height.Value / LeftEmptyRow.Height.Value; state.RightSidebarRatio = RecentFilesRow.Height.Value / RecentFoldersRow.Height.Value;
        try { await store.SaveAsync(state); } catch (StateConflictException ex) { stateWritable = false; ShowError(ex.Message); } catch (Exception ex) { ShowError($"保存に失敗しました: {ex.Message}"); }
    }
    private async Task<string?> AskAsync(string title, string value)
    {
        var input = new TextBox { Text = value, MinWidth = 300, MaxLength = 200 };
        var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, Title = title, Content = input, PrimaryButtonText = "保存", CloseButtonText = "キャンセル", DefaultButton = ContentDialogButton.Primary };
        return await dialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(input.Text) ? input.Text.Trim() : null;
    }
    private async void AddGroup(object sender, RoutedEventArgs e) => await CreateFavoriteFolderAsync(null);
    private async void AddFavorite(object sender, RoutedEventArgs e) => await AddCurrentFavoriteAsync();
    private static MenuFlyoutItem Menu(string text, RoutedEventHandler action) { var item = new MenuFlyoutItem { Text = text }; item.Click += action; return item; }
    private static bool IsEditing(DependencyObject? element)
    { while (element != null) { if (element is TextBox or PasswordBox or RichEditBox) return true; element = VisualTreeHelper.GetParent(element); } return false; }
    private bool CanRefreshListing()
    {
        if (operating || shellMenuOpen || fileMenuOpen || previewDialog != null || LeftPane.IsFileDragActive || RightPane.IsFileDragActive || LeftPane.IsDeferringVisualWork || RightPane.IsDeferringVisualWork) return false;
        var focused = FocusManager.GetFocusedElement(Root.XamlRoot) as DependencyObject;
        if (IsEditing(focused)) return false;
        for (var node = focused; node != null; node = VisualTreeHelper.GetParent(node)) if (node is ContentDialog) return false;
        return true;
    }
}

