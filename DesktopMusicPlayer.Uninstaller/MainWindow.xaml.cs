using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace DesktopMusicPlayer.Uninstaller
{
    public partial class MainWindow : Window
    {
        private const string AppName = "DesktopMusicPlayer";
        private const string RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Crescendo";

        public MainWindow()
        {
            InitializeComponent();
            Hide(); // Run correctly without showing main window
            PerformUninstall();
        }

        private async void PerformUninstall()
        {
            string currentExe = Process.GetCurrentProcess().MainModule.FileName;
            string executingDir = Path.GetDirectoryName(currentExe);

            // Check if running from Temp (The actual uninstallation phase)
            bool isRunningFromTemp = currentExe.Contains(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase);

            if (!isRunningFromTemp)
            {
                // Phase 1: Confirmation and Self-Copy
                var result = MessageBox.Show("Are you sure you want to completely remove Crescendo Music Player and all of its components?", 
                                             "Uninstall Crescendo", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.No)
                {
                    Application.Current.Shutdown();
                    return;
                }

                try
                {
                    string tempExe = Path.Combine(Path.GetTempPath(), "CrescendoUninstaller_Tmp.exe");
                    File.Copy(currentExe, tempExe, true);

                    // Launch temp copy and pass the install directory as argument
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = tempExe,
                        Arguments = $"\"{executingDir}\"", // Pass original path to delete
                        UseShellExecute = true
                    });

                    Application.Current.Shutdown();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to initialize uninstaller: {ex.Message}", "Error");
                    Application.Current.Shutdown();
                }
            }
            else
            {
                // Phase 2: The Cleanup (Running from Temp)
                string[] args = Environment.GetCommandLineArgs();
                string installDir = (args.Length > 1) ? args[1] : null;

                if (string.IsNullOrEmpty(installDir) || !Directory.Exists(installDir))
                {
                    // Fallback: Try to find from Registry if arg missing
                    installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Crescendo", AppName);
                }

                try
                {
                    // Wait for original process to exit
                    await Task.Delay(2000); 

                    // 1. Delete Files
                    if (Directory.Exists(installDir))
                    {
                        Directory.Delete(installDir, true);
                    }
                    
                    // Also delete parent "Crescendo" folder if empty
                    string parentDir = Directory.GetParent(installDir)?.FullName;
                    if (parentDir != null && Directory.Exists(parentDir) && Directory.GetFileSystemEntries(parentDir).Length == 0)
                    {
                        Directory.Delete(parentDir);
                    }

                    // 2. Delete Shortcut (Public Desktop)
                    string publicDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                    string shortcutPath = Path.Combine(publicDesktop, "Crescendo Player.lnk");
                    if (File.Exists(shortcutPath)) File.Delete(shortcutPath);

                    // 3. Remove Registry Key
                    using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                    {
                        using (RegistryKey key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", true))
                        {
                            if (key != null) key.DeleteSubKeyTree("Crescendo", false);
                        }
                    }

                    MessageBox.Show("Crescendo Music Player has been successfully removed.", "Uninstalled", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Uninstall completed with some errors: {ex.Message}\nYou may need to delete the folder manually.", "Warning");
                }
                finally
                {
                    // Self Delete Schedule
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/C timeout /t 3 & del \"{currentExe}\"",
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true
                    });
                    
                    Application.Current.Shutdown();
                }
            }
        }
    }
}