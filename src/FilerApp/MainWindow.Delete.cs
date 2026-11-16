using FilerApp.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FilerApp;
public sealed partial class MainWindow
{
    private bool deletePromptOpen;
#if DEBUG
    private Func<DeletionPlan, DeletionPlan>? debugDeletionPlan;
    private Func<DeletionPlan, Task<bool>>? debugDeletionConfirmation;
#endif
    private async Task DeleteSelectedAsync(string[] paths)
    {
        if (deletePromptOpen || paths.Length == 0) return;
        deletePromptOpen = true;
        try
        {
            var plan = await DeletionPlan.PrepareAsync(paths);
#if DEBUG
            if (debugDeletionPlan != null) plan = debugDeletionPlan(plan);
#endif
            if (closing || operating) return;
            bool approved;
#if DEBUG
            if (debugDeletionConfirmation != null) approved = await debugDeletionConfirmation(plan);
            else
#endif
                approved = await CreateDeletionDialog(plan).ShowAsync() == ContentDialogResult.Primary;
            if (approved)
                await RunOperationAsync(FileAction.Recycle, plan.Items.Select(x => x.Path).ToArray(), null, plan);
        }
        finally { deletePromptOpen = false; }
    }
    private ContentDialog CreateDeletionDialog(DeletionPlan plan)
    {
        bool permanent = plan.PermanentCount > 0;
        var content = new StackPanel { Spacing = 12, MinWidth = 340, MaxWidth = 620 };
        content.Children.Add(new TextBlock
        {
            Text = permanent ? $"サーバー上の{plan.PermanentCount}項目を完全削除します。PCのごみ箱には入りません。このアプリから元に戻すことはできません。" +
                (plan.PermanentCount < plan.Items.Count ? $"\nローカルの{plan.Items.Count - plan.PermanentCount}項目はごみ箱へ移動します。" : "") : "ごみ箱へ移動します。ごみ箱を利用できない場合は削除を中止します。",
            TextWrapping = TextWrapping.Wrap
        });
        var paths = new ListView { ItemsSource = plan.Items.Select(x => (x.Permanent ? "完全削除: " : "ごみ箱: ") + x.Path).ToArray(), SelectionMode = ListViewSelectionMode.None, MaxHeight = 280 };
        paths.ContainerContentChanging += (_, args) => { if (!args.InRecycleQueue) ToolTipService.SetToolTip(args.ItemContainer, args.Item?.ToString()); };
        content.Children.Add(paths);
        return new ContentDialog
        {
            XamlRoot = Root.XamlRoot,
            Title = permanent ? "完全削除の確認" : $"{plan.Items.Count}項目をごみ箱へ移動",
            Content = content, PrimaryButtonText = permanent ? plan.PermanentCount == plan.Items.Count ? "完全削除" : "完全削除を含めて実行" : "ごみ箱へ移動", CloseButtonText = "キャンセル",
            DefaultButton = ContentDialogButton.Close
        };
    }
}
