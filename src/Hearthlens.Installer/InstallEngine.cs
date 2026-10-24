using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using Microsoft.Win32;

namespace Hearthlens.Installer;

/// <summary>
/// Installs and removes Hearthlens. The setup wizard and the silent run (<c>/S</c>) that Hearthlens' own updater
/// uses both go through here, so an update replaces exactly the files a first install does.
/// </summary>
internal static class InstallEngine
{
    public const string Version = "0.12.3";

    private const string AppExeName = "Hearthlens.exe";
    private const string UninstallerName = "Uninstall Hearthlens.exe";
    private const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Hearthlens";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string PayloadResource = "Hearthlens.Payload.app.zip";

    /// <summary>%LOCALAPPDATA%\Programs\Hearthlens: a per-user folder, so setup never asks for administrator rights.</summary>
    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs",
        "Hearthlens");

    /// <summary>The folder an earlier install recorded, or null when Hearthlens is not installed for this user.</summary>
    public static string? InstalledDirectory()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(UninstallKeyPath);
            return key?.GetValue("InstallLocation") is string location && location.Length > 0 ? location : null;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Copies the published app out of the setup's own payload into <paramref name="targetDirectory"/>, creates the
    /// shortcuts the player asked for and registers the copy in Settings → Apps.
    /// </summary>
    /// <exception cref="InvalidOperationException">The setup carries no app payload, or no install folder was given.</exception>
    public static void Install(
        string targetDirectory,
        bool desktopShortcut,
        bool startMenuShortcut,
        bool startWithWindows,
        Action<string, int>? report = null)
    {
        report ??= static (_, _) => { };

        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            throw new InvalidOperationException("Please specify a valid installation folder.");
        }

        report("Closing any running Hearthlens instances...", 5);
        CloseRunningApp();

        Directory.CreateDirectory(targetDirectory);

        using (var archive = OpenPayload())
        {
            var total = archive.Entries.Count;
            var done = 0;

            foreach (var entry in archive.Entries)
            {
                done++;
                var percent = 10 + (int)(done / (double)total * 75);

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(Path.Combine(targetDirectory, entry.FullName));
                    continue;
                }

                var destination = Path.Combine(targetDirectory, entry.FullName);
                var folder = Path.GetDirectoryName(destination);
                if (folder is not null)
                {
                    Directory.CreateDirectory(folder);
                }

                report($"Extracting: {entry.Name}", percent);
                entry.ExtractToFile(destination, overwrite: true);
            }
        }

        report("Creating uninstaller...", 88);
        WriteUninstaller(targetDirectory);

        report("Registering shortcuts...", 92);
        var mainExe = Path.Combine(targetDirectory, AppExeName);
        if (desktopShortcut)
        {
            CreateShortcut(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Hearthlens.lnk"),
                mainExe,
                mainExe);
        }

        if (startMenuShortcut)
        {
            var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            Directory.CreateDirectory(programs);
            CreateShortcut(Path.Combine(programs, "Hearthlens.lnk"), mainExe, mainExe);
        }

        if (startWithWindows)
        {
            Try(() =>
            {
                using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath);
                runKey.SetValue("Hearthlens", $"\"{mainExe}\"");
            });
        }

        report("Finalizing setup...", 98);
        RegisterUninstallEntry(targetDirectory, mainExe);
        report("Complete!", 100);
    }

    /// <summary>Removes the files, shortcuts and registry entries an install created; match history is kept unless asked.</summary>
    public static void Uninstall(string targetDirectory, bool deleteUserData)
    {
        CloseRunningApp();

        Try(() =>
        {
            foreach (var shortcut in new[]
                     {
                         Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Hearthlens.lnk"),
                         Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Hearthlens.lnk"),
                     })
            {
                if (File.Exists(shortcut))
                {
                    File.Delete(shortcut);
                }
            }
        });

        Try(() =>
        {
            using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            runKey?.DeleteValue("Hearthlens", throwOnMissingValue: false);
            Registry.CurrentUser.DeleteSubKeyTree(UninstallKeyPath, throwOnMissingSubKey: false);
        });

        if (deleteUserData)
        {
            Try(() =>
            {
                var appDataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Hearthlens");
                if (Directory.Exists(appDataDir))
                {
                    Directory.Delete(appDataDir, recursive: true);
                }
            });
        }

        // The running uninstaller lives in this folder, so it is left behind for the OS to clean up.
        Try(() =>
        {
            if (!Directory.Exists(targetDirectory))
            {
                return;
            }

            foreach (var file in Directory.GetFiles(targetDirectory, "*.*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(UninstallerName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Try(() => File.Delete(file));
            }
        });
    }

    private static ZipArchive OpenPayload()
    {
        Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResource);

        // Builds made without an embedded payload can still read one beside the executable.
        if (stream is null)
        {
            var localZip = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "payload", "app.zip");
            if (File.Exists(localZip))
            {
                stream = File.OpenRead(localZip);
            }
            else
            {
                var tempZip = Path.Combine(Path.GetTempPath(), "hearthlens-app.zip");
                if (File.Exists(tempZip))
                {
                    stream = File.OpenRead(tempZip);
                }
            }
        }

        if (stream is null)
        {
            throw new InvalidOperationException("Installation package payload (app.zip) could not be located.");
        }

        try
        {
            return new ZipArchive(stream, ZipArchiveMode.Read);
        }
        catch (InvalidDataException)
        {
            stream.Dispose();
            throw;
        }
    }

    private static void WriteUninstaller(string targetDirectory)
    {
        var currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        var uninstaller = Path.Combine(targetDirectory, UninstallerName);
        if (currentExe.Length == 0 || currentExe.Equals(uninstaller, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Try(() => File.Copy(currentExe, uninstaller, overwrite: true));
    }

    private static void RegisterUninstallEntry(string targetDirectory, string mainExe)
    {
        Try(() =>
        {
            using var key = Registry.CurrentUser.CreateSubKey(UninstallKeyPath);
            key.SetValue("DisplayName", "Hearthlens");
            key.SetValue("DisplayVersion", Version);
            key.SetValue("Publisher", "Hearthlens");
            key.SetValue("DisplayIcon", $"{mainExe},0");
            key.SetValue("InstallLocation", targetDirectory);
            key.SetValue("UninstallString", $"\"{Path.Combine(targetDirectory, UninstallerName)}\" --uninstall");
            key.SetValue("QuietUninstallString", $"\"{Path.Combine(targetDirectory, UninstallerName)}\" --uninstall /S");
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);

            long totalBytes = 0;
            if (Directory.Exists(targetDirectory))
            {
                foreach (var file in Directory.GetFiles(targetDirectory, "*.*", SearchOption.AllDirectories))
                {
                    totalBytes += new FileInfo(file).Length;
                }
            }

            key.SetValue("EstimatedSize", (int)(totalBytes / 1024), RegistryValueKind.DWord);
        });
    }

    private static void CloseRunningApp()
    {
        Try(() =>
        {
            foreach (var process in Process.GetProcessesByName("Hearthlens"))
            {
                Try(() =>
                {
                    process.Kill();
                    process.WaitForExit(1500);
                });
            }
        });
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string iconPath)
    {
        Try(() =>
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return;
            }

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
            shortcut.Description = "Hearthlens — Hearthstone Companion";
            shortcut.IconLocation = iconPath + ",0";
            shortcut.Save();
        });
    }

    /// <summary>Setup is best effort about the desktop and the registry: a locked shortcut never fails an install.</summary>
    private static void Try(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException
                                       or PlatformNotSupportedException or System.ComponentModel.Win32Exception
                                       or InvalidOperationException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            _ = ex;
        }
    }
}
