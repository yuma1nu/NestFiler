using FilerApp.Core;
using FilerApp.Services;
using FilerApp.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;

namespace FilerApp;
public sealed partial class MainWindow
{
    // The keyboard route leaves TextBox/RichEditBox paste to the native editor.
    // Check again here so a delayed/context-menu invocation cannot create a file
    // while the address bar is being edited.
    private bool CanPasteIntoListing() => !IsEditing(FocusManager.GetFocusedElement(Root.XamlRoot) as DependencyObject);

    private async Task PasteIntoPaneAsync(PaneView pane, string folder, DataPackageView data)
    {
        if (!CanPasteIntoListing() || operating || pane.Current?.IsSearch == true) return;
        if (data.Contains(StandardDataFormats.StorageItems)) { await PasteFilesAsync(data, folder); return; }
        string? text = data.Contains(StandardDataFormats.WebLink) ? (await data.GetWebLinkAsync()).AbsoluteUri :
            data.Contains(StandardDataFormats.Text) ? await data.GetTextAsync() : null;
        if (text == null) { Status.Text = "コピーしたファイル、またはSharePointの共有リンクを貼り付けできます。"; return; }
        if (operating) return;
        var link = SharePointLink.Parse(text);
        // Keep the captured destination even if A/B, folder or tab changes while
        // awaiting clipboard data/dialog/IO. Do not change focus on completion.
        operating = true;
        try
        {
            var choice = await ChooseShortcutAsync(link);
            if (choice == null) { Status.Text = "ショートカットの作成をキャンセルしました。"; return; }
            operationCancellation = new();
            Status.Text = "SharePointショートカットを作成中…";
            var result = await SharePointShortcutService.CreateAsync(folder, link, choice.Value.Application, choice.Value.Name, operationCancellation.Token);
            operationResults = [new(result, "成功")];
            await Task.WhenAll(LeftPane.RefreshAfterOperationAsync(), RightPane.RefreshAfterOperationAsync());
            var installed = await Task.Run(() => SharePointShortcutService.IsRegistered(choice.Value.Application));
            Status.Text = $"{Path.GetFileName(result)} を作成しました。" + (installed ? $"開くと{choice.Value.Application}デスクトップアプリが起動します。" : $"開くには{choice.Value.Application}デスクトップアプリのインストールが必要です。");
        }
        catch (OperationCanceledException) { Status.Text = "ショートカットの作成をキャンセルしました。"; }
        catch (Exception ex) { operationResults = [new(folder, "失敗: " + ex.Message)]; throw; }
        finally { operating = false; operationCancellation?.Dispose(); operationCancellation = null; }
    }

#if DEBUG
    private Func<SharePointLink, Task<(OfficeApplication Application, string Name)?>>? debugChooseShortcut;
#endif
    private async Task<(OfficeApplication Application, string Name)?> ChooseShortcutAsync(SharePointLink link)
    {
#if DEBUG
        if (debugChooseShortcut != null) return await debugChooseShortcut(link);
#endif
        if (link.Application is { } known && link.SuggestedName is { } name) return (known, name);
        var input = new TextBox { Header = "ショートカット名", Text = link.SuggestedName ?? "", PlaceholderText = "例: 実験報告.pptx", MinWidth = 320, MaxLength = 160 };
        var applications = Enum.GetValues<OfficeApplication>();
        var select = new ComboBox { Header = "デスクトップアプリ", ItemsSource = applications, SelectedIndex = link.Application is { } app ? Array.IndexOf(applications, app) : -1, IsEnabled = link.Application == null, HorizontalAlignment = HorizontalAlignment.Stretch, PlaceholderText = "アプリを選択" };
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new TextBlock { Text = "共有リンクだけでは名前が分からないため、入力してください。ファイルのダウンロードは行いません。", TextWrapping = TextWrapping.Wrap, MaxWidth = 400 });
        content.Children.Add(input); content.Children.Add(select); content.Children.Add(error);
        var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, Title = "SharePointショートカット", Content = content, PrimaryButtonText = "作成", CloseButtonText = "キャンセル", DefaultButton = ContentDialogButton.Primary };
        dialog.Opened += (_, _) => { input.Focus(FocusState.Programmatic); input.SelectAll(); };
        dialog.PrimaryButtonClick += (_, args) =>
        {
            try
            {
                if (select.SelectedItem is not OfficeApplication chosen) throw new ArgumentException("デスクトップアプリを選択してください。");
                OfficeDocuments.ShortcutName(input.Text, chosen);
            }
            catch (ArgumentException ex) { error.Text = ex.Message; args.Cancel = true; }
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary ? ((OfficeApplication)select.SelectedItem, input.Text) : null;
    }
}
