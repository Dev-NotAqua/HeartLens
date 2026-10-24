using System.Windows;

namespace Hearthlens.Installer;

public partial class App : Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        bool isUninstall = false;
        foreach (var arg in e.Args)
        {
            if (arg.Equals("--uninstall", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("/uninstall", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-uninstall", StringComparison.OrdinalIgnoreCase))
            {
                isUninstall = true;
                break;
            }
        }

        var mainWindow = new MainWindow(isUninstall);
        mainWindow.Show();
    }
}
