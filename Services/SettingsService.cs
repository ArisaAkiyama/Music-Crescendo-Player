using System;
using System.IO;
using System.Collections.Generic;

namespace DesktopMusicPlayer.Services
{
    /// <summary>
    /// Simple settings service to persist app configuration
    /// </summary>
    public static class SettingsService
    {
        private static readonly string SettingsPath;
        private static readonly Dictionary<string, string> _settings = new();

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
                
                LoadSettings();
            }
            catch
            {
                SettingsPath = "settings.txt"; // Fallback
            }
        }

        private static void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var lines = File.ReadAllLines(SettingsPath);
                    foreach (var line in lines)
                    {
                        var parts = line.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            _settings[parts[0].Trim()] = parts[1].Trim();
                        }
                        else if (!string.IsNullOrWhiteSpace(line) && !_settings.ContainsKey("MusicFolder"))
                        {
                            // Backwards compatibility: raw path implies MusicFolder
                            _settings["MusicFolder"] = line.Trim();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            }
        }

        private static void SaveSettings()
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                var lines = new List<string>();
                foreach (var kvp in _settings)
                {
                    lines.Add($"{kvp.Key}={kvp.Value}");
                }
                
                File.WriteAllLines(SettingsPath, lines);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        public static string? GetLastMusicFolder()
        {
            return _settings.TryGetValue("MusicFolder", out var val) ? val : null;
        }

        public static void SaveMusicFolder(string folderPath)
        {
            _settings["MusicFolder"] = folderPath;
            SaveSettings();
        }

        public static double GetVolume()
        {
            if (_settings.TryGetValue("Volume", out var val) && double.TryParse(val, out double volume))
            {
                return Math.Clamp(volume, 0.0, 1.0);
            }
            return 0.5; // Default volume 50%
        }

        public static void SaveVolume(double volume)
        {
            _settings["Volume"] = volume.ToString("F2");
            SaveSettings();
        }

        /// <summary>
        /// Get list of folders to watch for auto-sync.
        /// </summary>
        public static List<string> GetWatchedFolders()
        {
            if (_settings.TryGetValue("WatchedFolders", out var val) && !string.IsNullOrWhiteSpace(val))
            {
                return new List<string>(val.Split('|', StringSplitOptions.RemoveEmptyEntries));
            }
            return new List<string>();
        }

        /// <summary>
        /// Save list of folders to watch for auto-sync.
        /// </summary>
        public static void SaveWatchedFolders(IEnumerable<string> folders)
        {
            _settings["WatchedFolders"] = string.Join("|", folders);
            SaveSettings();
        }

        /// <summary>
        /// Add a folder to watch list.
        /// </summary>
        public static void AddWatchedFolder(string folderPath)
        {
            var folders = GetWatchedFolders();
            if (!folders.Contains(folderPath, StringComparer.OrdinalIgnoreCase))
            {
                folders.Add(folderPath);
                SaveWatchedFolders(folders);
            }
        }

        /// <summary>
        /// Remove a folder from watch list.
        /// </summary>
        public static void RemoveWatchedFolder(string folderPath)
        {
            var folders = GetWatchedFolders();
            folders.RemoveAll(f => f.Equals(folderPath, StringComparison.OrdinalIgnoreCase));
            SaveWatchedFolders(folders);
        }
    }
}
