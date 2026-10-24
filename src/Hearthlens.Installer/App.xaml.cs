using System;
using System.IO;
using System.Windows;

namespace Hearthlens.Installer;

public partial class App : Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        var uninstall = HasSwitch(e.Args, "--uninstall", "/uninstall", "-uninstall");
        var silent = HasSwitch(e.Args, "/S", "/silent", "--silent", "-silent");
        var target = FolderSwitch(e.Args);

        if (!silent)
        {
            new MainWindow(uninstall, target).Show();
            return;
        }

        // Hearthlens' updater runs the setup with /S while the app closes. There is no window to click, so the
        // exit code is the only report: 0 installed, 1 the old version stays in place.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var exitCode = 1;
        try
        {
            // An update must replace the copy that is already installed, wherever the wizard put it.
            var directory = target ?? InstallEngine.InstalledDirectory() ?? InstallEngine.DefaultDirectory;

            if (uninstall)
            {
                InstallEngine.Uninstall(directory, deleteUserData: false);
            }
            else
            {
                InstallEngine.Install(directory, desktopShortcut: true, startMenuShortcut: true, startWithWindows: false);
            }

            exitCode = 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException)
        {
            _ = ex;
        }

        Shutdown(exitCode);
    }

    private static bool HasSwitch(string[] args, params string[] switches)
    {
        foreach (var arg in args)
        {
            foreach (var name in switches)
            {
                if (arg.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>The folder after <c>/D=</c> (NSIS-style, unquoted) or <c>--dir=</c>; null when the setup should pick one.</summary>
    private static string? FolderSwitch(string[] args)
    {
        foreach (var arg in args)
        {
            foreach (var prefix in new[] { "/D=", "-D=", "--dir=" })
            {
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    var folder = arg[prefix.Length..].Trim().Trim('"');
                    if (folder.Length > 0)
                    {
                        return folder;
                    }
                }
            }
        }

        return null;
    }
}
