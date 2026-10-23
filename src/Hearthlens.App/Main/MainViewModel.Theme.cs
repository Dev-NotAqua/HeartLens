namespace Hearthlens.App.Main;

public sealed partial class MainViewModel
{
    /// <summary>Raised when the edition changes; the host rebuilds the windows in the new colours.</summary>
    public event Action? ThemeChanged;

    /// <summary>Deep green paper with bone ink for the app and the in-game overlay.</summary>
    public bool NightEdition
    {
        get => Settings.DarkTheme;
        set
        {
            if (Settings.DarkTheme == value)
            {
                return;
            }

            Settings.DarkTheme = value;
            Raise();
            ThemeChanged?.Invoke();
        }
    }

    /// <summary>Your most recent hero, shown as faded art behind the page titles.</summary>
    public string? BannerHero => Recent.FirstOrDefault(g => g.HeroCardId.Length > 0)?.HeroCardId;
}