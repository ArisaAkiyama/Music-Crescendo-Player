using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DesktopMusicPlayer.Models;

namespace DesktopMusicPlayer.Services
{
    /// <summary>
    /// Service for managing playlist persistence using JSON
    /// </summary>
    public class PlaylistService
    {
        private readonly string _playlistsFilePath;
        private readonly JsonSerializerOptions _jsonOptions;

        public PlaylistService()
        {
            // Store playlists.json in AppData folder
            var appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DesktopMusicPlayer");
            
            Directory.CreateDirectory(appDataFolder);
            _playlistsFilePath = Path.Combine(appDataFolder, "playlists.json");

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        /// <summary>
        /// Data transfer object for JSON serialization
        /// </summary>
        private class PlaylistDto
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string IconGlyph { get; set; } = "\uE8D6";
            public bool IsLikedSongs { get; set; }
            public List<string> SongPaths { get; set; } = new();
        }

        /// <summary>
        /// Load playlists from JSON file
        /// </summary>
        public List<Playlist> LoadPlaylists()
        {
            var playlists = new List<Playlist>();

            try
            {
                if (File.Exists(_playlistsFilePath))
                {
                    var json = File.ReadAllText(_playlistsFilePath);
                    var dtos = JsonSerializer.Deserialize<List<PlaylistDto>>(json, _jsonOptions);
                    
                    if (dtos != null)
                    {
                        int id = 1;
                        foreach (var dto in dtos)
                        {
                            var playlist = new Playlist
                            {
                                Id = id++,
                                Name = dto.Name,
                                IconGlyph = dto.IconGlyph,
                                IsLikedSongs = dto.IsLikedSongs
                            };
                            
                            // Store song paths for later resolution
                            foreach (var path in dto.SongPaths)
                            {
                                // Songs will be resolved later when library is loaded
                                playlist.SongPaths.Add(path);
                            }
                            
                            playlists.Add(playlist);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading playlists: {ex.Message}");
            }

            // Ensure "Favorite Tracks" always exists as first playlist
            if (!playlists.Any(p => p.IsLikedSongs))
            {
                playlists.Insert(0, new Playlist
                {
                    Id = 1,
                    Name = "Favorite Tracks",
                    IconGlyph = "\uEB51", // Heart icon
                    IsLikedSongs = true
                });
                
                // Re-number IDs
                for (int i = 0; i < playlists.Count; i++)
                {
                    playlists[i].Id = i + 1;
                }
            }

            return playlists;
        }

        /// <summary>
        /// Save playlists to JSON file
        /// </summary>
        public void SavePlaylists(IEnumerable<Playlist> playlists)
        {
            try
            {
                var dtos = playlists.Select(p => new PlaylistDto
                {
                    Id = Guid.NewGuid(),
                    Name = p.Name,
                    IconGlyph = p.IconGlyph,
                    IsLikedSongs = p.IsLikedSongs,
                    SongPaths = p.Songs.Select(s => s.FilePath).ToList()
                }).ToList();

                var json = JsonSerializer.Serialize(dtos, _jsonOptions);
                File.WriteAllText(_playlistsFilePath, json);
                
                System.Diagnostics.Debug.WriteLine($"Saved {playlists.Count()} playlists to {_playlistsFilePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving playlists: {ex.Message}");
            }
        }

        /// <summary>
        /// Create a new playlist with default name
        /// </summary>
        public Playlist CreatePlaylist(int existingCount)
        {
            return new Playlist
            {
                Id = existingCount + 1,
                Name = $"My Playlist #{existingCount}",
                IconGlyph = "\uE8D6"
            };
        }
    }
}
