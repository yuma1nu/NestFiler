using Microsoft.UI.Xaml;
namespace FilerApp;
public partial class App : Application
{
    private Window? window;
    private Services.SingleInstance? instance;
    public App() { InitializeComponent(); UnhandledException += (_, e) => Services.AppLog.Error(e.Message + " | " + e.Exception.ToString()); AppDomain.CurrentDomain.UnhandledException += (_, e) => Services.AppLog.Error(e.ExceptionObject.ToString() ?? "Fatal error"); }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        instance = new(Services.AppLog.DataDirectory);
        if (!instance.IsPrimary) { instance.Dispose(); instance = null; Exit(); return; }
        window = new MainWindow();
        instance.Listen(() => window.DispatcherQueue.TryEnqueue(() => Services.SingleInstance.Activate(window)));
        window.Closed += (_, _) => { instance?.Dispose(); instance = null; };
        window.Activate();
    }
}


