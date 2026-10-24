using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace Hearthlens.Installer;

public partial class MainWindow : Window
{
    private const string Version = "0.12.1";
    private readonly bool _isUninstall;
    private string _targetDirectory;

    public MainWindow(bool isUninstall = false)
    {
        InitializeComponent();
        _isUninstall = isUninstall;

        _targetDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "Hearthlens");

        TxtInstallPath.Text = _targetDirectory;
        UpdateDiskSpace();

        if (_isUninstall)
        {
            ShowUninstallPage();
        }
        else
        {
            SetStep(1);
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void SetStep(int step)
    {
        PageWelcome.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
        PagePreferences.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
        PageInstalling.Visibility = step == 3 ? Visibility.Visible : Visibility.Collapsed;
        PageFinished.Visibility = step == 4 ? Visibility.Visible : Visibility.Collapsed;
        PageUninstall.Visibility = Visibility.Collapsed;

        UpdateSpineStep(1, DotStep1, TxtStep1, step);
        UpdateSpineStep(2, DotStep2, TxtStep2, step);
        UpdateSpineStep(3, DotStep3, TxtStep3, step);
        UpdateSpineStep(4, DotStep4, TxtStep4, step);
    }

    private void UpdateSpineStep(int index, System.Windows.Controls.Border dot, System.Windows.Controls.TextBlock txt, int currentStep)
    {
        var marigold = (SolidColorBrush)FindResource("Marigold");
        var verdant = (SolidColorBrush)FindResource("Verdant");
        var inactiveBrush = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));

        if (index == currentStep)
        {
            dot.Background = marigold;
            ((System.Windows.Controls.TextBlock)dot.Child).Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0x1A, 0x16));
            txt.Foreground = new SolidColorBrush(Color.FromRgb(0xF8, 0xF1, 0xE3));
            txt.FontWeight = FontWeights.SemiBold;
        }
        else if (index < currentStep)
        {
            dot.Background = verdant;
            ((System.Windows.Controls.TextBlock)dot.Child).Foreground = new SolidColorBrush(Color.FromRgb(0xF8, 0xF1, 0xE3));
            txt.Foreground = new SolidColorBrush(Color.FromRgb(0x7F, 0xBE, 0xA8));
            txt.FontWeight = FontWeights.Normal;
        }
        else
        {
            dot.Background = inactiveBrush;
            ((System.Windows.Controls.TextBlock)dot.Child).Foreground = new SolidColorBrush(Color.FromRgb(0xA9, 0xA2, 0x8F));
            txt.Foreground = new SolidColorBrush(Color.FromRgb(0xA9, 0xA2, 0x8F));
            txt.FontWeight = FontWeights.Normal;
        }
    }

    private void ShowUninstallPage()
    {
        PageWelcome.Visibility = Visibility.Collapsed;
        PagePreferences.Visibility = Visibility.Collapsed;
        PageInstalling.Visibility = Visibility.Collapsed;
        PageFinished.Visibility = Visibility.Collapsed;
        PageUninstall.Visibility = Visibility.Visible;

        NavStep1.Visibility = Visibility.Collapsed;
        NavStep2.Visibility = Visibility.Collapsed;
        NavStep3.Visibility = Visibility.Collapsed;
        NavStep4.Visibility = Visibility.Collapsed;
    }

    private void BtnToPreferences_Click(object sender, RoutedEventArgs e)
    {
        SetStep(2);
    }

    private void BtnBackToWelcome_Click(object sender, RoutedEventArgs e)
    {
        SetStep(1);
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Hearthlens installation directory",
            InitialDirectory = Directory.Exists(TxtInstallPath.Text) ? TxtInstallPath.Text : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        };

        if (dialog.ShowDialog() == true)
        {
            _targetDirectory = dialog.FolderName;
            TxtInstallPath.Text = _targetDirectory;
            UpdateDiskSpace();
        }
    }

    private void UpdateDiskSpace()
    {
        try
        {
            string root = Path.GetPathRoot(_targetDirectory) ?? "C:\\";
            var drive = new DriveInfo(root);
            double freeGb = Math.Round(drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0), 1);
            TxtDiskSpace.Text = $"Space required: ~180 MB • Available on {drive.Name.TrimEnd('\\')}: {freeGb} GB";
        }
        catch
        {
            TxtDiskSpace.Text = "Space required: ~180 MB";
        }
    }

    private async void BtnStartInstall_Click(object sender, RoutedEventArgs e)
    {
        _targetDirectory = TxtInstallPath.Text.Trim();
        if (string.IsNullOrWhiteSpace(_targetDirectory))
        {
            MessageBox.Show("Please specify a valid installation folder.", "Hearthlens Setup", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool createDesktop = ChkDesktop.IsChecked == true;
        bool createStartMenu = ChkStartMenu.IsChecked == true;
        bool autoStart = ChkAutoStart.IsChecked == true;

        SetStep(3);
        await Task.Run(() => PerformInstall(_targetDirectory, createDesktop, createStartMenu, autoStart));
        SetStep(4);
    }

    private void PerformInstall(string targetDir, bool createDesktop, bool createStartMenu, bool autoStart)
    {
        // 1. Close any running Hearthlens instances
        try
        {
            UpdateStatus("Closing any running Hearthlens instances...", 5);
            foreach (var proc in Process.GetProcessesByName("Hearthlens"))
            {
                try
                {
                    proc.Kill();
                    proc.WaitForExit(1500);
                }
                catch { }
            }
        }
        catch { }

        Directory.CreateDirectory(targetDir);

        // 2. Locate embedded app payload
        Stream? zipStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Hearthlens.Payload.app.zip");

        // Fallback for development if zip is beside or in payload folder
        if (zipStream == null)
        {
            string localZip = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "payload", "app.zip");
            if (File.Exists(localZip))
            {
                zipStream = File.OpenRead(localZip);
            }
            else
            {
                string tempZip = Path.Combine(Path.GetTempPath(), "hearthlens-app.zip");
                if (File.Exists(tempZip))
                {
                    zipStream = File.OpenRead(tempZip);
                }
            }
        }

        if (zipStream == null)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show("Installation package payload (app.zip) could not be located.", "Setup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            });
            return;
        }

        using (zipStream)
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
        {
            int total = archive.Entries.Count;
            int count = 0;

            foreach (var entry in archive.Entries)
            {
                count++;
                int percent = 10 + (int)((count / (double)total) * 75);

                if (string.IsNullOrEmpty(entry.Name))
                {
                    // Directory entry
                    Directory.CreateDirectory(Path.Combine(targetDir, entry.FullName));
                    continue;
                }

                string destPath = Path.Combine(targetDir, entry.FullName);
                string? destDir = Path.GetDirectoryName(destPath);
                if (destDir != null)
                {
                    Directory.CreateDirectory(destDir);
                }

                UpdateStatus($"Extracting: {entry.Name}", percent);
                entry.ExtractToFile(destPath, overwrite: true);
            }
        }

        // 3. Write uninstaller copy
        UpdateStatus("Creating uninstaller...", 88);
        string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
        string uninstallerExe = Path.Combine(targetDir, "Uninstall Hearthlens.exe");
        if (File.Exists(currentExe) && !currentExe.Equals(uninstallerExe, StringComparison.OrdinalIgnoreCase))
        {
            try { File.Copy(currentExe, uninstallerExe, overwrite: true); } catch { }
        }

        // 4. Create Shortcuts
        UpdateStatus("Registering shortcuts...", 92);
        string mainExe = Path.Combine(targetDir, "Hearthlens.exe");

        if (createDesktop)
        {
            string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string desktopLnk = Path.Combine(desktopDir, "Hearthlens.lnk");
            CreateShortcut(desktopLnk, mainExe, "Hearthlens — Hearthstone Companion", mainExe + ",0");
        }

        if (createStartMenu)
        {
            string startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs));
            Directory.CreateDirectory(startMenuDir);
            string startMenuLnk = Path.Combine(startMenuDir, "Hearthlens.lnk");
            CreateShortcut(startMenuLnk, mainExe, "Hearthlens — Hearthstone Companion", mainExe + ",0");
        }

        // 5. Autostart Registry
        if (autoStart)
        {
            try
            {
                using var runKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                runKey.SetValue("Hearthlens", $"\"{mainExe}\"");
            }
            catch { }
        }

        // 6. Windows Apps & Features Uninstall Registration
        UpdateStatus("Finalizing setup...", 98);
        try
        {
            using var uninstallKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Hearthlens");
            uninstallKey.SetValue("DisplayName", "Hearthlens");
            uninstallKey.SetValue("DisplayVersion", Version);
            uninstallKey.SetValue("Publisher", "Hearthlens");
            uninstallKey.SetValue("DisplayIcon", $"{mainExe},0");
            uninstallKey.SetValue("InstallLocation", targetDir);
            uninstallKey.SetValue("UninstallString", $"\"{uninstallerExe}\" --uninstall");
            uninstallKey.SetValue("QuietUninstallString", $"\"{uninstallerExe}\" --uninstall");
            uninstallKey.SetValue("NoModify", 1, RegistryValueKind.DWord);
            uninstallKey.SetValue("NoRepair", 1, RegistryValueKind.DWord);

            long totalBytes = 0;
            if (Directory.Exists(targetDir))
            {
                foreach (var f in Directory.GetFiles(targetDir, "*.*", SearchOption.AllDirectories))
                {
                    totalBytes += new FileInfo(f).Length;
                }
            }
            uninstallKey.SetValue("EstimatedSize", (int)(totalBytes / 1024), RegistryValueKind.DWord);
        }
        catch { }

        UpdateStatus("Complete!", 100);
        System.Threading.Thread.Sleep(400);
    }

    private void UpdateStatus(string message, int percent)
    {
        Dispatcher.Invoke(() =>
        {
            TxtStatus.Text = message;
            TxtPercent.Text = $"{percent}%";
            double maxProgressWidth = 420; // width of inner progress container
            ProgressFill.Width = Math.Max(0, (percent / 100.0) * maxProgressWidth);
        });
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string description, string iconPath)
    {
        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType != null)
            {
                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = targetPath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
                shortcut.Description = description;
                shortcut.IconLocation = iconPath;
                shortcut.Save();
            }
        }
        catch { }
    }

    private void BtnFinish_Click(object sender, RoutedEventArgs e)
    {
        if (ChkLaunchNow.IsChecked == true)
        {
            string mainExe = Path.Combine(_targetDirectory, "Hearthlens.exe");
            if (File.Exists(mainExe))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = mainExe,
                        WorkingDirectory = _targetDirectory,
                        UseShellExecute = true
                    });
                }
                catch { }
            }
        }

        Close();
    }

    private async void BtnDoUninstall_Click(object sender, RoutedEventArgs e)
    {
        BtnDoUninstall.IsEnabled = false;
        TxtUninstallStatus.Text = "Removing Hearthlens...";

        bool deleteUserData = ChkDeleteUserData.IsChecked == true;

        await Task.Run(() =>
        {
            // 1. Close Hearthlens
            try
            {
                foreach (var proc in Process.GetProcessesByName("Hearthlens"))
                {
                    try { proc.Kill(); proc.WaitForExit(1000); } catch { }
                }
            }
            catch { }

            // 2. Remove shortcuts
            try
            {
                string desktopLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Hearthlens.lnk");
                if (File.Exists(desktopLnk)) File.Delete(desktopLnk);

                string startMenuLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Hearthlens.lnk");
                if (File.Exists(startMenuLnk)) File.Delete(startMenuLnk);
            }
            catch { }

            // 3. Remove registry entries
            try
            {
                using var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
                runKey?.DeleteValue("Hearthlens", throwOnMissingValue: false);

                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Hearthlens", throwOnMissingSubKey: false);
            }
            catch { }

            // 4. Optionally delete user data
            if (deleteUserData)
            {
                try
                {
                    string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Hearthlens");
                    if (Directory.Exists(appDataDir))
                    {
                        Directory.Delete(appDataDir, recursive: true);
                    }
                }
                catch { }
            }

            // 5. Delete application directory (except self, schedule self delete)
            string appDir = _targetDirectory;
            if (Directory.Exists(appDir))
            {
                try
                {
                    foreach (var file in Directory.GetFiles(appDir, "*.*", SearchOption.AllDirectories))
                    {
                        if (file.EndsWith("Uninstall Hearthlens.exe", StringComparison.OrdinalIgnoreCase)) continue;
                        try { File.Delete(file); } catch { }
                    }
                }
                catch { }
            }
        });

        TxtUninstallStatus.Text = "Hearthlens has been uninstalled.";
        MessageBox.Show("Hearthlens was successfully removed from your computer.", "Hearthlens", MessageBoxButton.OK, MessageBoxImage.Information);
        Close();
    }
}
