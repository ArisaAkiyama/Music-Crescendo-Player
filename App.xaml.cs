using System.Configuration;
using System.Data;
using System.Windows;
using DesktopMusicPlayer.Services;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace DesktopMusicPlayer;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>

    public partial class App : Application
    {
        /// <summary>
        /// File path passed via command-line (when opening MP3 directly)
        /// </summary>
        public static string? StartupFilePath { get; private set; }
        
        protected override void OnStartup(StartupEventArgs e)
        {
            // Capture command-line arguments (for file association)
            if (e.Args.Length > 0)
            {
                var filePath = e.Args[0];
                if (System.IO.File.Exists(filePath) && 
                    filePath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                {
                    StartupFilePath = filePath;
                    System.Diagnostics.Debug.WriteLine($"Startup file: {StartupFilePath}");
                }
            }
            
            // Initialize SQLite database and create tables if needed
            try
            {
                DatabaseService.InitializeDatabase();
                System.Diagnostics.Debug.WriteLine($"Database initialized at: {DatabaseService.GetDatabasePath()}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize database: {ex.Message}", "Database Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Global Exception Handling
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            base.OnStartup(e);
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"Uncaught Exception: {e.Exception.Message}\n\nStackTrace:\n{e.Exception.StackTrace}", "Application Crash", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true; // Prevent crash
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
             if (e.ExceptionObject is Exception ex)
             {
                 MessageBox.Show($"Fatal Error: {ex.Message}\n\n{ex.StackTrace}", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
             }
        }
        
        public event Action? ThemeChanging;

        public void ChangeTheme(Uri themeUri)
        {
            // Notify listeners that theme is about to change (for animations)
            ThemeChanging?.Invoke(); // Triggers capture of current state

            ResourceDictionary newTheme = new ResourceDictionary() { Source = themeUri };

            // Find existing theme dictionary (Dark or Light) and replace it
            // We assume the theme dictionary is the first one, or we can look by Source if we track it.
            // But MergedDictionaries[0] is DarkTheme.xaml based on App.xaml.
            // Let's be safer: clear and re-add Styles (or replace index 0).
            
            // Simpler approach: Dictionary 0 is Theme, Dictionary 1 is Styles. 
            // We just update Dictionary 0.
            if (Resources.MergedDictionaries.Count > 0)
            {
                Resources.MergedDictionaries[0] = newTheme;
            }
            else
            {
                Resources.MergedDictionaries.Add(newTheme);
            }
        }
    }
