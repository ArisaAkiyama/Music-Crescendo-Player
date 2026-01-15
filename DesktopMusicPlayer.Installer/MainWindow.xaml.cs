using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace DesktopMusicPlayer.Installer
{
    public partial class MainWindow : Window
    {
        private const string AppName = "DesktopMusicPlayer";
        private const string ExeName = "DesktopMusicPlayer.exe";

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OptionsToggle_Click(object sender, MouseButtonEventArgs e)
        {
            if (OptionsPanel.Visibility == Visibility.Collapsed)
            {
                OptionsPanel.Visibility = Visibility.Visible;
                OptionsToggle.Text = "Hide Options";
                
                // Set default if empty
                if (string.IsNullOrWhiteSpace(InstallPathTxt.Text))
                {
                    string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    InstallPathTxt.Text = Path.Combine(programFiles, "Crescendo", AppName);
                }
            }
            else
            {
                OptionsPanel.Visibility = Visibility.Collapsed;
                OptionsToggle.Text = "Installation Options";
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Installation Folder"
            };

            if (dialog.ShowDialog() == true)
            {
                InstallPathTxt.Text = Path.Combine(dialog.FolderName, AppName);
            }
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            InstallButton.IsEnabled = false;
            InstallButton.Content = "INSTALLING...";
            
            try
            {
                // Use custom path if options visible and text not empty, else default
                string installDir;
                if (OptionsPanel.Visibility == Visibility.Visible && !string.IsNullOrWhiteSpace(InstallPathTxt.Text))
                {
                    installDir = InstallPathTxt.Text;
                }
                else
                {
                    string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    installDir = Path.Combine(programFiles, "Crescendo", AppName);
                }

                // 0. Kill Running Process
                StatusText.Text = "Stopping running instances...";
                await Task.Run(() =>
                {
                    foreach (var process in System.Diagnostics.Process.GetProcessesByName("DesktopMusicPlayer"))
                    {
                        try { process.Kill(); process.WaitForExit(3000); } catch { }
                    }
                });

                // 1. Clean up Old Installation & User Data (Fresh Install)
                StatusText.Text = "Cleaning up old data...";
                if (Directory.Exists(installDir))
                {
                    try { Directory.Delete(installDir, true); } catch { }
                }

                // Delete AppData (Database & Settings) to ensure fresh start
                try
                {
                    // Roaming
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string userDataDir = Path.Combine(appData, "DesktopMusicPlayer");
                    if (Directory.Exists(userDataDir)) Directory.Delete(userDataDir, true);

                    // Local (Just in case)
                    string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    string localDataDir = Path.Combine(localAppData, "DesktopMusicPlayer");
                    if (Directory.Exists(localDataDir)) Directory.Delete(localDataDir, true);
                }
                catch { /* Ignore if fails, maybe permission issue */ }

                // Create Install Directory
                StatusText.Text = "Creating directories...";
                Directory.CreateDirectory(installDir);
                InstallProgressBar.Value = 10;
                await Task.Delay(500); // Visual delay

                // 2. Extract Embedded Resource
                StatusText.Text = "Extracting files...";
                string zipPath = Path.Combine(Path.GetTempPath(), "App.zip");
                
                // Save resource to temp file
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("DesktopMusicPlayer.Installer.Assets.App.zip"))
                {
                    if (stream == null) throw new Exception("Embedded resource 'App.zip' not found!");
                    
                    using (FileStream fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write))
                    {
                        await stream.CopyToAsync(fileStream);
                    }
                }
                InstallProgressBar.Value = 30;

                // Extract Zip with Overwrite
                await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, installDir, true));
                File.Delete(zipPath); // Cleanup temp
                InstallProgressBar.Value = 60;

                // 3. Create Shortcut
                StatusText.Text = "Creating shortcuts...";
                string exePath = Path.Combine(installDir, ExeName);
                if (File.Exists(exePath))
                {
                    CreateShortcut(exePath);
                }
                
                // 4. Extract Uninstaller
                StatusText.Text = "Registering uninstaller...";
                // Extract Uninstaller
                string uninstallerPath = Path.Combine(installDir, "Uninstaller.exe");
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("DesktopMusicPlayer.Installer.Assets.Uninstaller.exe"))
                {
                    if (stream != null)
                    {
                        using (FileStream fileStream = new FileStream(uninstallerPath, FileMode.Create, FileAccess.Write))
                        {
                            await stream.CopyToAsync(fileStream);
                        }
                    }
                }

                // 5. Register in Registry (Add/Remove Programs)
                try 
                {
                    using (var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64))
                    {
                        using (var key = baseKey.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Crescendo", true))
                        {
                            if (key != null)
                            {
                                key.SetValue("DisplayName", "Crescendo Music Player");
                                key.SetValue("DisplayVersion", "1.0.0");
                                key.SetValue("Publisher", "Crescendo");
                                key.SetValue("UninstallString", $"\"{uninstallerPath}\"");
                                key.SetValue("DisplayIcon", $"\"{exePath}\"");
                                key.SetValue("InstallLocation", installDir);
                                key.SetValue("NoModify", 1);
                                key.SetValue("NoRepair", 1);
                            }
                        }
                    }
                }
                catch { /* Ignore registry errors if any */ }

                InstallProgressBar.Value = 90;
                await Task.Delay(500);

                // Done
                StatusText.Text = "Installation complete!";
                InstallProgressBar.Value = 100;
                InstallButton.Content = "LAUNCH";
                InstallButton.IsEnabled = true;
                
                // Switch click handler to Launch
                InstallButton.Click -= InstallButton_Click;
                InstallButton.Click += (s, args) => 
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exePath) { UseShellExecute = true });
                    Close();
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Installation failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                InstallButton.IsEnabled = true;
                InstallButton.Content = "RETRY";
                StatusText.Text = "Installation failed";
            }
        }

        private void CreateShortcut(string targetPath)
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                string shortcutPath = Path.Combine(desktop, "Crescendo Player.lnk");
                
                // Using PowerShell to create shortcut to avoid COM dependencies (IWshRuntimeLibrary)
                string script = $@"
$WshShell = New-Object -comObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut('{shortcutPath}')
$Shortcut.TargetPath = '{targetPath}'
$Shortcut.WorkingDirectory = '{Path.GetDirectoryName(targetPath)}'
$Shortcut.Description = 'Desktop Music Player'
$Shortcut.IconLocation = '{targetPath}'
$Shortcut.Save()
";
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                System.Diagnostics.Process.Start(psi)?.WaitForExit();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create shortcut: {ex.Message}");
            }
        }
    }
}