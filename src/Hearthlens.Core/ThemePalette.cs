namespace Hearthlens.Core;

/// <summary>
/// The two editions of the Windows app's theme. Themes/Theme.xaml holds the day edition; the night edition is the same
/// file with every colour swapped for its night twin below (alpha is kept). Colours not listed stay as they are.
/// </summary>
public static class ThemePalette
{
    private static readonly Dictionary<uint, uint> NightRgb = new()
    {
        [0xEFE6D4] = 0x101A16, // paper
        [0xF8F1E3] = 0x17231E, // raised paper
        [0xE4D6BD] = 0x1E2E27,
        [0xD9C7AA] = 0x273A32,
        [0xD8C8AE] = 0x2C3D35, // hairlines
        [0xB9A98F] = 0x4E5C54,
        [0xF4EAD8] = 0x1A2822, // rows
        [0xFBF5EA] = 0x21362D, // selected sheet
        [0x1C1D17] = 0xEEE5D3, // ink
        [0x615F4D] = 0xA7A08C,
        [0x7F7A68] = 0x8F8977,
        [0x8A3B2E] = 0xD67B66, // oxblood
        [0x6F2D23] = 0xC46A56,
        [0xA54A39] = 0xE58D77,
        [0xF0DED0] = 0x3A241F,
        [0x5E2A20] = 0xF3D4C7,
        [0xE1ECE5] = 0x1F3B32,
        [0x234A3E] = 0xDCEFE5,
        [0xF6EDDD] = 0x151D18, // ink on oxblood
        [0x1A1B15] = 0x151D18, // ink on marigold
        [0x2F6B5A] = 0x7FBEA8, // verdigris
        [0x3E6B7A] = 0x8FB7C4,
        [0xC8891F] = 0xE2A63F, // marigold
        [0x173F35] = 0x0C2A22, // the green spine
        [0x0F2A24] = 0x08120F,
        [0x245746] = 0x1B4437,
        [0xB7C4B7] = 0xA9BCB1,
        [0x6C4B7A] = 0xC3A0D6,
        [0xA56812] = 0xE2A63F,
        [0x8A5E0E] = 0xE2A63F,
        [0x57756C] = 0x9CC7B5,
        [0x6B6654] = 0xB7B09B,
    };

    /// <summary>Every day colour (RGB) that has a night twin.</summary>
    public static IReadOnlyCollection<uint> DayColours => NightRgb.Keys;

    /// <summary>The night twin of an ARGB day colour.</summary>
    public static uint Night(uint argb) =>
        NightRgb.TryGetValue(argb & 0xFFFFFF, out var night) ? (argb & 0xFF000000) | night : argb;
}