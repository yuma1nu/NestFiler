using FilerApp.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FilerApp;
public sealed partial class MainWindow
{
#if DEBUG
    private Func<FileCollision, CancellationToken, Task<CollisionResolution>>? debugCollisionResolver;
#endif
    private Task<CollisionResolution> ResolveCollisionAsync(FileCollision collision, CancellationToken token)
    {
#if DEBUG
        if (debugCollisionResolver != null) return debugCollisionResolver(collision, token);
#endif
        var completion = new TaskCompletionSource<CollisionResolution>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!DispatcherQueue.TryEnqueue(async () =>
        {
            if (token.IsCancellationRequested) { completion.TrySetResult(new(CollisionChoice.Cancel)); return; }
            try
            {
                var (dialog, choice, all) = CreateCollisionDialog(collision);
                using var cancellation = token.Register(() => DispatcherQueue.TryEnqueue(dialog.Hide));
                var result = await dialog.ShowAsync();
                completion.TrySetResult(result == ContentDialogResult.Primary && !token.IsCancellationRequested ?
                    new((CollisionChoice)choice.SelectedIndex, all.IsChecked == true) : new(CollisionChoice.Cancel));
            }
            catch (Exception ex) { completion.TrySetException(ex); }
        })) completion.TrySetResult(new(CollisionChoice.Cancel));
        return completion.Task;
    }
    private (ContentDialog Dialog, ComboBox Choice, CheckBox All) CreateCollisionDialog(FileCollision collision)
    {
        var content = new StackPanel { Spacing = 12, MinWidth = 340, MaxWidth = 600 };
        content.Children.Add(new TextBlock { Text = $"同じ名前の項目があります。\n\n転送元: {collision.Source}\n\n配置先: {collision.Destination}", TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true });
        var choice = new ComboBox { Header = "処理方法", HorizontalAlignment = HorizontalAlignment.Stretch };
        choice.Items.Add("両方残す（連番を付ける）");
        choice.Items.Add(collision.SourceDirectory && collision.DestinationDirectory ? "フォルダを統合する（中の同名項目は個別確認）" : "置き換える");
        choice.Items.Add("この項目をスキップ");
        choice.SelectedIndex = 0;
        var all = new CheckBox { Content = "この操作で以降の同名項目にも適用" };
        content.Children.Add(choice); content.Children.Add(all);
        return (new ContentDialog { XamlRoot = Root.XamlRoot, Title = "同名項目の確認", Content = content, PrimaryButtonText = "選択した操作を実行", CloseButtonText = "キャンセル", DefaultButton = ContentDialogButton.Close }, choice, all);
    }
}
