using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Hearthlens.Installer;

public partial class MainWindow : Window
{
    private readonly bool _isUninstall;
    private string _targetDirectory;

    public MainWindow(bool isUninstall = false, string? targetDirectory = null)
    {
        InitializeComponent();
        _isUninstall = isUninstall;

        _targetDirectory = targetDirectory ?? InstallEngine.InstalledDirectory() ?? InstallEngine.DefaultDirectory;

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
        try
        {
            await Task.Run(() => InstallEngine.Install(
                _targetDirectory,
                createDesktop,
                createStartMenu,
                autoStart,
                (message, percent) => Dispatcher.Invoke(() => UpdateStatus(message, percent))));
            SetStep(4);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException)
        {
            MessageBox.Show(
                $"Hearthlens could not be installed.\n\n{ex.Message}",
                "Hearthlens Setup",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            UpdateStatus("Installation could not be completed.", 0);
            SetStep(2);
        }
    }

    private void UpdateStatus(string message, int percent)
    {
        TxtStatus.Text = message;
        TxtPercent.Text = $"{percent}%";
        double maxProgressWidth = 420; // width of inner progress container
        ProgressFill.Width = Math.Max(0, (percent / 100.0) * maxProgressWidth);
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

        await Task.Run(() => InstallEngine.Uninstall(_targetDirectory, deleteUserData));

        TxtUninstallStatus.Text = "Hearthlens has been uninstalled.";
        MessageBox.Show("Hearthlens was successfully removed from your computer.", "Hearthlens", MessageBoxButton.OK, MessageBoxImage.Information);
        Close();
    }
}
