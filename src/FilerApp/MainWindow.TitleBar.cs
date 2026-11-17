using Microsoft.UI.Windowing;
using Microsoft.UI.System;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace FilerApp;
public sealed partial class MainWindow
{
    private ThemeSettings titleBarThemeSettings = null!;

    private void InitializeTitleBarTheme()
    {
        // ThemeSettings binds to this desktop window; the UWP accessibility
        // change event requires a CoreWindow that unpackaged WinUI does not have.
        titleBarThemeSettings = ThemeSettings.CreateForWindowId(AppWindow.Id);
        titleBarThemeSettings.Changed += TitleBarHighContrastChanged;
        Closed += (_, _) => titleBarThemeSettings.Changed -= TitleBarHighContrastChanged;
        UpdateTitleBarTheme();
    }

    private void TitleBarHighContrastChanged(ThemeSettings sender, object args)
        => DispatcherQueue.TryEnqueue(UpdateTitleBarTheme);

    private void UpdateTitleBarTheme()
    {
        if (!AppWindowTitleBar.IsCustomizationSupported()) return;

        var bar = AppWindow.TitleBar;
        if (titleBarThemeSettings.HighContrast)
        {
            // Let Windows supply accessible caption and button colors.
            bar.BackgroundColor = bar.InactiveBackgroundColor = null;
            bar.ForegroundColor = bar.InactiveForegroundColor = null;
            bar.ButtonBackgroundColor = bar.ButtonInactiveBackgroundColor = null;
            bar.ButtonForegroundColor = bar.ButtonInactiveForegroundColor = null;
            bar.ButtonHoverBackgroundColor = bar.ButtonPressedBackgroundColor = null;
            bar.ButtonHoverForegroundColor = bar.ButtonPressedForegroundColor = null;
            return;
        }

        // Read theme-bound brushes from the content root so the native caption
        // follows the actual window theme, including runtime theme changes.
        var background = ((SolidColorBrush)Root.Background).Color;
        Color BrushColor(string key)
        {
            var color = ((SolidColorBrush)Root.Resources[key]).Color;
            // Native caption colors ignore alpha. Composite theme text opacity
            // explicitly so the inactive caption remains appropriately subdued.
            byte Blend(byte channel, byte backdrop) => (byte)((channel * color.A + backdrop * (255 - color.A) + 127) / 255);
            return Color.FromArgb(255, Blend(color.R, background.R), Blend(color.G, background.G), Blend(color.B, background.B));
        }
        var foreground = BrushColor("TitleBarTextBrush");
        var inactiveForeground = BrushColor("TitleBarInactiveTextBrush");

        bar.BackgroundColor = bar.InactiveBackgroundColor = background;
        bar.ForegroundColor = foreground;
        bar.InactiveForegroundColor = inactiveForeground;
        bar.ButtonBackgroundColor = bar.ButtonInactiveBackgroundColor = background;
        bar.ButtonForegroundColor = foreground;
        bar.ButtonInactiveForegroundColor = inactiveForeground;
        bar.ButtonHoverBackgroundColor = BrushColor("TitleBarHoverBrush");
        bar.ButtonPressedBackgroundColor = BrushColor("TitleBarPressedBrush");
        bar.ButtonHoverForegroundColor = bar.ButtonPressedForegroundColor = foreground;
    }
}
