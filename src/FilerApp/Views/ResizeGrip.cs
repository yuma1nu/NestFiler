using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
namespace FilerApp.Views;

public sealed class ResizeGrip : UserControl
{
    private readonly Thumb grip = new();
    public event DragDeltaEventHandler DragDelta { add => grip.DragDelta += value; remove => grip.DragDelta -= value; }
    public event DragStartedEventHandler DragStarted { add => grip.DragStarted += value; remove => grip.DragStarted -= value; }
    public event DragCompletedEventHandler DragCompleted { add => grip.DragCompleted += value; remove => grip.DragCompleted -= value; }
    public bool Horizontal
    {
        get => Height == 5;
        set
        {
            Width = value ? double.NaN : 5; Height = value ? 5 : double.NaN;
            ProtectedCursor = InputSystemCursor.Create(value ? InputSystemCursorShape.SizeNorthSouth : InputSystemCursorShape.SizeWestEast);
            var line = value ? "Height=\"1\" VerticalAlignment=\"Center\"" : "Width=\"1\" HorizontalAlignment=\"Center\"";
            grip.Template = (ControlTemplate)XamlReader.Load($"<ControlTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"><Grid Background=\"Transparent\"><Border {line} Background=\"{{ThemeResource DividerStrokeColorDefaultBrush}}\"/></Grid></ControlTemplate>");
        }
    }
    public ResizeGrip() { Content = grip; Horizontal = false; IsTabStop = false; grip.IsTabStop = false; }
}


