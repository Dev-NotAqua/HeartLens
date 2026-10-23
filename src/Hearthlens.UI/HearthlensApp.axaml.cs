using Avalonia;
using Avalonia.Markup.Xaml;

namespace Hearthlens.UI;

/// <summary>Theme and fonts shared by every Hearthlens shell; platform shells derive from it.</summary>
public class HearthlensApp : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
}
