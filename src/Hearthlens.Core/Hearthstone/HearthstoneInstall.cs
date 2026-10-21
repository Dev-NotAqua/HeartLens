using System.Diagnostics;
using Microsoft.Win32;

namespace Hearthlens.Core.Hearthstone;

/// <summary>Locates the Hearthstone installation, its log folders and the client locale.</summary>
public static class HearthstoneInstall
{
    private static readonly string[] Fallbacks =
    [
        @"C:\Program Files (x86)\Hearthstone",
        @"C:\Program Files\Hearthstone",
        @"D:\Hearthstone",
        @"D:\Games\Hearthstone",
        @"E:\Hearthstone",
    ];

    public static string LocalAppDataDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Blizzard", "Hearthstone");

    public static string LogConfigPath => Path.Combine(LocalAppDataDir, "log.config");

    public static bool IsRunning()
    {
        var processes = Process.GetProcessesByName("Hearthstone");
        foreach (var p in processes) p.Dispose();
        return processes.Length > 0;
    }

    public static string? FindInstallDirectory(string? configured = null)
    {
        if (IsInstall(configured))
        {
            return configured;
        }

        try
        {
            foreach (var process in Process.GetProcessesByName("Hearthstone"))
            {
                using (process)
                {
                    var dir = Path.GetDirectoryName(process.MainModule?.FileName);
                    if (IsInstall(dir))
                    {
                        return dir;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }

        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            foreach (var key in new[]
                     {
                         @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Hearthstone",
                         @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Hearthstone",
                     })
            {
                using var reg = hive.OpenSubKey(key);
                if (reg?.GetValue("InstallLocation") is string location && IsInstall(location))
                {
                    return location;
                }
            }
        }

        return Fallbacks.FirstOrDefault(IsInstall);
    }

    /// <summary>Newest Hearthstone_yyyy_MM_dd_HH_mm_ss session folder.</summary>
    public static string? LatestLogSession(string installDir)
    {
        var logs = Path.Combine(installDir, "Logs");
        if (!Directory.Exists(logs))
        {
            return null;
        }

        return Directory.EnumerateDirectories(logs, "Hearthstone_*")
            .OrderByDescending(d => Path.GetFileName(d), StringComparer.Ordinal)
            .FirstOrDefault();
    }

    public static IEnumerable<string> AllLogSessions(string installDir)
    {
        var logs = Path.Combine(installDir, "Logs");
        return Directory.Exists(logs)
            ? Directory.EnumerateDirectories(logs, "Hearthstone_*").OrderBy(d => Path.GetFileName(d), StringComparer.Ordinal)
            : Enumerable.Empty<string>();
    }

    /// <summary>Session start time encoded in the folder name.</summary>
    public static DateTime? SessionStart(string sessionDir)
    {
        var name = Path.GetFileName(sessionDir);
        return DateTime.TryParseExact(name, "'Hearthstone_'yyyy_MM_dd_HH_mm_ss", null,
            System.Globalization.DateTimeStyles.None, out var start)
            ? start
            : null;
    }

    /// <summary>Picks the installed text locale, preferring a non-English one when present.</summary>
    public static string DetectLocale(string? installDir)
    {
        var strings = installDir is null ? null : Path.Combine(installDir, "Strings");
        if (strings is null || !Directory.Exists(strings))
        {
            return "enUS";
        }

        var locales = Directory.EnumerateDirectories(strings).Select(Path.GetFileName).OfType<string>()
            .Where(n => n.Length == 4).ToList();
        return locales.FirstOrDefault(l => l != "enUS") ?? "enUS";
    }

    private static bool IsInstall(string? dir) =>
        !string.IsNullOrWhiteSpace(dir) && File.Exists(Path.Combine(dir, "Hearthstone.exe"));
}

/// <summary>Ensures the client writes the log sections the tracker reads.</summary>
public static class LogConfig
{
    private static readonly (string Section, bool Verbose)[] Required =
    [
        ("Power", true),
        ("LoadingScreen", false),
        ("Decks", false),
        ("Arena", false),
        ("FullScreenFX", false),
        ("Achievements", false),
    ];

    public static IReadOnlyList<string> MissingSections()
    {
        var sections = Read();
        return Required.Where(r => !sections.TryGetValue(r.Section, out var values) || !IsSatisfied(values, r.Verbose))
            .Select(r => r.Section).ToList();
    }

    /// <summary>Adds or fixes required sections; other sections are preserved. Returns true if the file changed.</summary>
    public static bool Ensure()
    {
        var sections = Read();
        var changed = false;
        foreach (var (section, verbose) in Required)
        {
            if (sections.TryGetValue(section, out var values) && IsSatisfied(values, verbose))
            {
                continue;
            }

            sections[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["LogLevel"] = "1",
                ["FilePrinting"] = "True",
                ["ConsolePrinting"] = "False",
                ["ScreenPrinting"] = "False",
                ["Verbose"] = verbose ? "True" : "False",
            };
            changed = true;
        }

        if (!changed)
        {
            return false;
        }

        Directory.CreateDirectory(HearthstoneInstall.LocalAppDataDir);
        using var writer = new StreamWriter(HearthstoneInstall.LogConfigPath, append: false);
        foreach (var (name, values) in sections)
        {
            writer.WriteLine($"[{name}]");
            foreach (var (key, value) in values)
            {
                writer.WriteLine($"{key}={value}");
            }
        }

        return true;
    }

    private static bool IsSatisfied(Dictionary<string, string> values, bool verbose) =>
        values.TryGetValue("FilePrinting", out var printing) && printing.Equals("True", StringComparison.OrdinalIgnoreCase)
        && values.TryGetValue("LogLevel", out var level) && level == "1"
        && (!verbose || (values.TryGetValue("Verbose", out var v) && v.Equals("True", StringComparison.OrdinalIgnoreCase)));

    private static Dictionary<string, Dictionary<string, string>> Read()
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(HearthstoneInstall.LogConfigPath))
        {
            return result;
        }

        Dictionary<string, string>? current = null;
        foreach (var raw in File.ReadAllLines(HearthstoneInstall.LogConfigPath))
        {
            var line = raw.Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                result[line[1..^1]] = current;
            }
            else if (current is not null && line.IndexOf('=') is > 0 and var eq)
            {
                current[line[..eq].Trim()] = line[(eq + 1)..].Trim();
            }
        }

        return result;
    }
}
