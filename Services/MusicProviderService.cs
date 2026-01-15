using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DesktopMusicPlayer.Helpers;
using DesktopMusicPlayer.Models;

namespace DesktopMusicPlayer.Services
{
    public class MusicProviderService
    {
        private readonly string[] _supportedExtensions = { ".mp3", ".wav", ".flac", ".m4a", ".wma", ".aac" };

        /// <summary>
        /// Scan a folder recursively for music files and extract metadata
        /// </summary>
        /// <summary>
        /// Scan a folder recursively for music files and extract metadata asynchronously
        /// </summary>
        public async Task<List<Song>> ScanFolderAsync(string folderPath)
        {
            return await Task.Run(() =>
            {
                var songs = new List<Song>();

                if (!Directory.Exists(folderPath))
                {
                    System.Diagnostics.Debug.WriteLine($"Folder not found: {folderPath}");
                    return songs;
                }

                try
                {
                    var musicFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                        .Where(f => _supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                        .ToList();

                    int id = 1;
                    foreach (var filePath in musicFiles)
                    {
                        var song = ExtractMetadata(filePath, id++);
                        if (song != null)
                        {
                            songs.Add(song);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error scanning folder: {ex.Message}");
                }

                return songs;
            });
        }

        /// <summary>
        /// Get a single song from file path (for drag & drop)
        /// </summary>
        public Song? GetSongFromFile(string filePath)
        {
            return ExtractMetadata(filePath, 0);
        }

        /// <summary>
        /// Extract metadata from a single music file using TagLib#
        /// </summary>
        private Song? ExtractMetadata(string filePath, int id)
        {
            try
            {
                using var file = TagLib.File.Create(filePath);
                
                // Get basic metadata
                var title = !string.IsNullOrWhiteSpace(file.Tag.Title) 
                    ? file.Tag.Title 
                    : Path.GetFileNameWithoutExtension(filePath);
                    
                var artist = !string.IsNullOrWhiteSpace(file.Tag.FirstPerformer) 
                    ? file.Tag.FirstPerformer 
                    : (!string.IsNullOrWhiteSpace(file.Tag.JoinedPerformers) 
                        ? file.Tag.JoinedPerformers 
                        : (!string.IsNullOrWhiteSpace(file.Tag.FirstAlbumArtist) 
                            ? file.Tag.FirstAlbumArtist 
                            : "Unknown Artist"));
                    
                var album = !string.IsNullOrWhiteSpace(file.Tag.Album) 
                    ? file.Tag.Album 
                    : "Unknown Album";

                var duration = file.Properties.Duration;

                // Extract technical audio details
                string technicalDetails = BuildTechnicalDetails(filePath, file.Properties);

                return new Song
                {
                    Id = id,
                    Title = title,
                    Artist = artist,
                    Album = album,
                    Duration = duration,
                    FilePath = filePath,
                    // CoverArt = ImageHelper.BytesToImage(coverArtData), // Lazy loaded in Song model
                    DateAdded = File.GetCreationTime(filePath),
                    TechnicalDetails = technicalDetails
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading metadata for {filePath}: {ex.Message}");
                
                // Return basic song info even if metadata fails
                return new Song
                {
                    Id = id,
                    Title = Path.GetFileNameWithoutExtension(filePath),
                    Artist = "Unknown Artist",
                    Album = "Unknown Album",
                    Duration = TimeSpan.Zero,
                    FilePath = filePath,
                    DateAdded = File.GetCreationTime(filePath)
                };
            }
        }

        /// <summary>
        /// Build technical details string from TagLib# audio properties
        /// </summary>
        private string BuildTechnicalDetails(string filePath, TagLib.Properties properties)
        {
            try
            {
                // Format: Get file extension and convert to uppercase
                string format = Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant();

                // Sample Rate: Convert Hz to kHz (e.g., 44100 -> 44.1)
                double sampleRateKHz = properties.AudioSampleRate / 1000.0;
                string sampleRate = sampleRateKHz.ToString("0.#");

                // Bitrate: Already in kbps
                int bitrate = properties.AudioBitrate;

                // Channels: 1 = Mono, 2+ = Stereo
                string channels = properties.AudioChannels switch
                {
                    1 => "Mono",
                    _ => "Stereo"
                };

                return $"{format}, {sampleRate} kHz, {bitrate} kbps, {channels}";
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
