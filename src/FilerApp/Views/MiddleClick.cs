using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;

namespace FilerApp.Views;

// Act on release over the original row. Middle-button dragging or leaving the
// row cancels the gesture; left clicks, context menus and left drags are untouched.
internal static class MiddleClick
{
    public static void Attach(FrameworkElement row, Func<Task> action, Action<string> error)
    {
        uint? pointer = null; Point start = default;
        bool Inside(Point point) => point.X >= 0 && point.Y >= 0 && point.X < row.ActualWidth && point.Y < row.ActualHeight;
        row.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, args) =>
        {
            var point = args.GetCurrentPoint(row);
            if (point.Properties.PointerUpdateKind != PointerUpdateKind.MiddleButtonPressed || point.Properties.IsLeftButtonPressed || point.Properties.IsRightButtonPressed) return;
            pointer = args.Pointer.PointerId; start = point.Position; args.Handled = true;
        }), true);
        row.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler((_, args) =>
        {
            if (pointer != args.Pointer.PointerId) return;
            var point = args.GetCurrentPoint(row);
            if (!point.Properties.IsMiddleButtonPressed || !Inside(point.Position) || Math.Abs(point.Position.X - start.X) > 6 || Math.Abs(point.Position.Y - start.Y) > 6) pointer = null;
        }), true);
        row.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(async (_, args) =>
        {
            var point = args.GetCurrentPoint(row);
            if (point.Properties.PointerUpdateKind != PointerUpdateKind.MiddleButtonReleased || pointer != args.Pointer.PointerId) return;
            pointer = null; args.Handled = true;
            if (!Inside(point.Position) || Math.Abs(point.Position.X - start.X) > 6 || Math.Abs(point.Position.Y - start.Y) > 6) return;
            try { await action(); } catch (Exception ex) { error(ex.Message); }
        }), true);
        row.PointerExited += (_, args) => { if (!Inside(args.GetCurrentPoint(row).Position)) pointer = null; };
        row.PointerCanceled += (_, _) => pointer = null;
        row.PointerCaptureLost += (_, _) => pointer = null;
        row.Unloaded += (_, _) => pointer = null;
    }
}
