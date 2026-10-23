using Avalonia;
using Avalonia.Styling;

namespace Hearthlens.UI.Infrastructure;

/// <summary>Switches the shared app between the printed day edition and the night edition.</summary>
public static class ThemeManager
{
    public static void Apply(bool dark)
    {
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        }
    }
}
