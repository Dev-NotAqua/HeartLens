using System.Text.RegularExpressions;
using Hearthlens.Core;
using Xunit;

namespace Hearthlens.Tests;

public sealed class ThemePaletteTests
{
    [Fact]
    public void NightSwapsPaperAndInkAndKeepsAlpha()
    {
        Assert.Equal(0xFF101A16u, ThemePalette.Night(0xFFEFE6D4u));
        Assert.Equal(0xFFEEE5D3u, ThemePalette.Night(0xFF1C1D17u));
        Assert.Equal(0x66D67B66u, ThemePalette.Night(0x668A3B2Eu));
    }

    [Theory]
    [InlineData(0xFF3D7BE0u)] // mana gem blue
    [InlineData(0xFFF3EAD9u)] // nav text is bone in both editions
    [InlineData(0x00000000u)]
    public void UnlistedColoursStayAsTheyAre(uint argb)
    {
        Assert.Equal(argb, ThemePalette.Night(argb));
    }

    [Fact]
    public void EveryThemeColourHasANightTwin()
    {
        var theme = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Hearthlens.App", "Themes", "Theme.xaml"));
        var unchanged = new HashSet<uint> { 0x3D7BE0, 0xF3EAD9, 0x000000, 0xFFFFFF };
        var missing = Regex.Matches(theme, "#([0-9A-Fa-f]{8}|[0-9A-Fa-f]{6})\\b")
            .Select(m => Convert.ToUInt32(m.Groups[1].Value, 16) & 0xFFFFFF)
            .Distinct()
            .Where(rgb => !unchanged.Contains(rgb) && !ThemePalette.DayColours.Contains(rgb))
            .Select(rgb => rgb.ToString("X6"))
            .ToList();
        Assert.True(missing.Count == 0, "Day colours without a night twin: " + string.Join(", ", missing));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Hearthlens.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}