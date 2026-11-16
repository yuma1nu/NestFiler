namespace FilerApp;
public sealed partial class MainWindow
{
    private async Task OpenPathInNewTabAsync(string path, bool directory)
    {
        var pane = active;
        if (directory) await pane.NavigateAsync(path, true);
        else await pane.NavigateAndSelectAsync(Path.GetDirectoryName(path) ?? throw new IOException("格納フォルダを取得できません。"), path, true);
    }
}
