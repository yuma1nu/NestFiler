using FilerApp.Core;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace FilerApp;
public sealed partial class MainWindow
{
    private void RestoreWindowPlacement()
    {
        if (state.Window is not { } saved) return;
        var bounds = new RectInt32(saved.X, saved.Y, Math.Clamp(saved.Width, 800, 10000), Math.Clamp(saved.Height, 400, 10000));
        var screen = DisplayArea.GetFromRect(bounds, DisplayAreaFallback.Nearest).WorkArea;
        bounds.Width = Math.Min(bounds.Width, screen.Width); bounds.Height = Math.Min(bounds.Height, screen.Height);
        bounds.X = Math.Clamp(bounds.X, screen.X, screen.X + screen.Width - bounds.Width);
        bounds.Y = Math.Clamp(bounds.Y, screen.Y, screen.Y + screen.Height - bounds.Height);
        AppWindow.MoveAndResize(bounds);
        if (saved.Maximized && AppWindow.Presenter is OverlappedPresenter presenter) presenter.Maximize();
    }
    private void CaptureWindowPlacement()
    {
        if (AppWindow.Presenter is not OverlappedPresenter presenter || presenter.State == OverlappedPresenterState.Minimized) return;
        if (presenter.State == OverlappedPresenterState.Maximized)
        { if (state.Window != null) state.Window = state.Window with { Maximized = true }; return; }
        var point = AppWindow.Position; var size = AppWindow.Size;
        state.Window = new WindowPlacement(point.X, point.Y, size.Width, size.Height, false);
    }
}
