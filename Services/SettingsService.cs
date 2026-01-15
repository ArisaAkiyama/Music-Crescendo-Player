using System;
using System.IO;

namespace DesktopMusicPlayer.Services
{
    /// <summary>
    /// Simple settings service to persist app configuration
    /// </summary>
    public static class SettingsService
    {
        private static readonly string SettingsPath;

        static SettingsService()
        {
            string folderPath = "";
            try { folderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); } catch {}
            
            if (string.IsNullOrEmpty(folderPath))
            {
               try { folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Roaming"); } catch {}
            }
            
            if (string.IsNullOrEmpty(folderPath))
            {
               try { folderPath = Path.GetTempPath(); } catch {}
            }
            
            if (string.IsNullOrEmpty(folderPath))
            {
               folderPath = ".";
            }
            
            try 
            {
                string appDir = Path.Combine(folderPath, "CrescendoMusicPlayer");
                if (!Directory.Exists(appDir)) Directory.CreateDirectory(appDir);
                SettingsPath = Path.Combine(appDir, "settings.txt");
            }
            catch
            {
                SettingsPath = "settings.txt"; // Fallback to current directory simple file
            }
        }

        /// <summary>
        /// Get the last used music folder path
        /// </summary>
        public static string? GetLastMusicFolder()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var content = File.ReadAllText(SettingsPath);
                    if (Directory.Exists(content))
                    {
                        return content;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading settings: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Save the music folder path
        /// </summary>
        public static void SaveMusicFolder(string folderPath)
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllText(SettingsPath, folderPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
    }
}
