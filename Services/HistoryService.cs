using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DesktopMusicPlayer.Services
{
    /// <summary>
    /// Service for managing recently played songs history
    /// </summary>
    public class HistoryService
    {
        private readonly string _historyFilePath;
        private readonly JsonSerializerOptions _jsonOptions;
        private const int MaxHistoryItems = 50;

        public HistoryService()
        {
            var appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DesktopMusicPlayer");
            
            Directory.CreateDirectory(appDataFolder);
            _historyFilePath = Path.Combine(appDataFolder, "history.json");

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        /// <summary>
        /// DTO for history entry
        /// </summary>
        private class HistoryEntryDto
        {
            public string FilePath { get; set; } = string.Empty;
            public DateTime PlayedAt { get; set; }
        }

        /// <summary>
        /// Load history from JSON file
        /// Returns list of file paths with their played times
        /// </summary>
        public List<(string FilePath, DateTime PlayedAt)> LoadHistory()
        {
            var history = new List<(string, DateTime)>();

            try
            {
                if (File.Exists(_historyFilePath))
                {
                    var json = File.ReadAllText(_historyFilePath);
                    var entries = JsonSerializer.Deserialize<List<HistoryEntryDto>>(json, _jsonOptions);
                    
                    if (entries != null)
                    {
                        history = entries
                            .Where(e => !string.IsNullOrEmpty(e.FilePath))
                            .Select(e => (e.FilePath, e.PlayedAt))
                            .ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading history: {ex.Message}");
            }

            return history;
        }

        /// <summary>
        /// Save history to JSON file
        /// </summary>
        public void SaveHistory(IEnumerable<(string FilePath, DateTime PlayedAt)> history)
        {
            try
            {
                var entries = history.Select(h => new HistoryEntryDto
                {
                    FilePath = h.FilePath,
                    PlayedAt = h.PlayedAt
                }).ToList();

                var json = JsonSerializer.Serialize(entries, _jsonOptions);
                File.WriteAllText(_historyFilePath, json);
                
                System.Diagnostics.Debug.WriteLine($"Saved {entries.Count} history entries");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving history: {ex.Message}");
            }
        }

        /// <summary>
        /// Add song to history (move to top if exists, cap at 50 items)
        /// Returns updated history list
        /// </summary>
        public List<(string FilePath, DateTime PlayedAt)> AddToHistory(
            List<(string FilePath, DateTime PlayedAt)> currentHistory, 
            string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return currentHistory;

            // Remove existing entry if present
            currentHistory.RemoveAll(h => h.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));

            // Insert at top with current timestamp
            currentHistory.Insert(0, (filePath, DateTime.Now));

            // Cap at max items
            while (currentHistory.Count > MaxHistoryItems)
            {
                currentHistory.RemoveAt(currentHistory.Count - 1);
            }

            // Save to file
            SaveHistory(currentHistory);

            return currentHistory;
        }
    }
}
