using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Dapper;
using DesktopMusicPlayer.Models;

namespace DesktopMusicPlayer.Services;

/// <summary>
/// Repository for Playlist operations with Smart Playlist support.
/// </summary>
public class PlaylistRepository
{
    private readonly SongRepository _songRepository = new();

    /// <summary>
    /// Get all playlists.
    /// </summary>
    public IEnumerable<Playlist> GetAllPlaylists()
    {
        using var connection = DatabaseService.GetConnection();
        var dtos = connection.Query<PlaylistDto>("SELECT * FROM Playlists ORDER BY Id");
        return dtos.Select(MapToPlaylist);
    }

    /// <summary>
    /// Get a playlist by ID.
    /// </summary>
    public Playlist? GetPlaylistById(int id)
    {
        using var connection = DatabaseService.GetConnection();
        var dto = connection.QueryFirstOrDefault<PlaylistDto>(
            "SELECT * FROM Playlists WHERE Id = @Id", new { Id = id });
        return dto != null ? MapToPlaylist(dto) : null;
    }

    /// <summary>
    /// Get the Liked Songs playlist.
    /// </summary>
    public Playlist? GetLikedSongsPlaylist()
    {
        using var connection = DatabaseService.GetConnection();
        var dto = connection.QueryFirstOrDefault<PlaylistDto>(
            "SELECT * FROM Playlists WHERE IsLikedSongs = 1");
        return dto != null ? MapToPlaylist(dto) : null;
    }

    /// <summary>
    /// Add a new playlist.
    /// </summary>
    public int AddPlaylist(Playlist playlist)
    {
        using var connection = DatabaseService.GetConnection();
        var id = connection.ExecuteScalar<int>(@"
            INSERT INTO Playlists (Name, IconGlyph, IsLikedSongs, IsSmart, SmartCriteria)
            VALUES (@Name, @IconGlyph, @IsLikedSongs, @IsSmart, @SmartCriteria);
            SELECT last_insert_rowid();
        ", new
        {
            playlist.Name,
            playlist.IconGlyph,
            IsLikedSongs = playlist.IsLikedSongs ? 1 : 0,
            IsSmart = playlist.IsSmart ? 1 : 0,
            playlist.SmartCriteria
        });

        playlist.Id = id;
        return id;
    }

    /// <summary>
    /// Update a playlist.
    /// </summary>
    public void UpdatePlaylist(Playlist playlist)
    {
        using var connection = DatabaseService.GetConnection();
        connection.Execute(@"
            UPDATE Playlists 
            SET Name = @Name, IconGlyph = @IconGlyph, IsSmart = @IsSmart, SmartCriteria = @SmartCriteria
            WHERE Id = @Id
        ", new
        {
            playlist.Id,
            playlist.Name,
            playlist.IconGlyph,
            IsSmart = playlist.IsSmart ? 1 : 0,
            playlist.SmartCriteria
        });
    }

    /// <summary>
    /// Delete a playlist.
    /// </summary>
    public void DeletePlaylist(int playlistId)
    {
        using var connection = DatabaseService.GetConnection();
        // Also deletes from PlaylistSongs due to CASCADE
        connection.Execute("DELETE FROM Playlists WHERE Id = @Id", new { Id = playlistId });
    }

    /// <summary>
    /// Add a song to a manual playlist.
    /// </summary>
    public void AddSongToPlaylist(int playlistId, int songId)
    {
        using var connection = DatabaseService.GetConnection();
        var maxOrder = connection.ExecuteScalar<int>(
            "SELECT COALESCE(MAX(SortOrder), 0) FROM PlaylistSongs WHERE PlaylistId = @PlaylistId",
            new { PlaylistId = playlistId });

        connection.Execute(@"
            INSERT OR IGNORE INTO PlaylistSongs (PlaylistId, SongId, SortOrder)
            VALUES (@PlaylistId, @SongId, @SortOrder)
        ", new { PlaylistId = playlistId, SongId = songId, SortOrder = maxOrder + 1 });
    }

    /// <summary>
    /// Remove a song from a manual playlist.
    /// </summary>
    public void RemoveSongFromPlaylist(int playlistId, int songId)
    {
        using var connection = DatabaseService.GetConnection();
        connection.Execute(
            "DELETE FROM PlaylistSongs WHERE PlaylistId = @PlaylistId AND SongId = @SongId",
            new { PlaylistId = playlistId, SongId = songId });
    }

    /// <summary>
    /// Get songs for a playlist (handles both manual and Smart Playlists).
    /// </summary>
    public IEnumerable<Song> GetSongsForPlaylist(Playlist playlist)
    {
        if (playlist.IsSmart)
        {
            return GetSmartPlaylistSongs(playlist);
        }
        else
        {
            return GetManualPlaylistSongs(playlist.Id);
        }
    }

    /// <summary>
    /// Get songs for a manual playlist (via junction table).
    /// </summary>
    private IEnumerable<Song> GetManualPlaylistSongs(int playlistId)
    {
        using var connection = DatabaseService.GetConnection();
        var songs = connection.Query<SongDto>(@"
            SELECT s.* FROM Songs s
            INNER JOIN PlaylistSongs ps ON s.Id = ps.SongId
            WHERE ps.PlaylistId = @PlaylistId
            ORDER BY ps.SortOrder
        ", new { PlaylistId = playlistId });

        return songs.Select(MapToSong);
    }

    /// <summary>
    /// Smart Playlist engine - generates dynamic SQL based on criteria.
    /// </summary>
    private IEnumerable<Song> GetSmartPlaylistSongs(Playlist playlist)
    {
        if (string.IsNullOrEmpty(playlist.SmartCriteria))
            return Enumerable.Empty<Song>();

        try
        {
            var criteria = JsonSerializer.Deserialize<SmartPlaylistCriteria>(
                playlist.SmartCriteria,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (criteria == null)
                return Enumerable.Empty<Song>();

            return ExecuteSmartQuery(criteria);
        }
        catch (JsonException)
        {
            return Enumerable.Empty<Song>();
        }
    }

    /// <summary>
    /// Execute Smart Playlist query based on criteria type.
    /// </summary>
    private IEnumerable<Song> ExecuteSmartQuery(SmartPlaylistCriteria criteria)
    {
        using var connection = DatabaseService.GetConnection();
        IEnumerable<SongDto> songs;

        switch (criteria.Type?.ToLowerInvariant())
        {
            case "topplayed":
                var limit = criteria.Limit ?? 50;
                songs = connection.Query<SongDto>(
                    "SELECT * FROM Songs WHERE PlayCount > 0 ORDER BY PlayCount DESC LIMIT @Limit",
                    new { Limit = limit });
                break;

            case "likedsongs":
                songs = connection.Query<SongDto>(
                    "SELECT * FROM Songs WHERE IsLiked = 1 ORDER BY DateAdded DESC");
                break;

            case "recentlyadded":
                var days = criteria.Days ?? 30;
                var cutoffDate = DateTime.Now.AddDays(-days).ToString("o");
                songs = connection.Query<SongDto>(
                    "SELECT * FROM Songs WHERE DateAdded >= @CutoffDate ORDER BY DateAdded DESC",
                    new { CutoffDate = cutoffDate });
                break;

            case "genre":
                if (string.IsNullOrEmpty(criteria.Genre))
                    return Enumerable.Empty<Song>();

                var genreQuery = "SELECT * FROM Songs WHERE Genre = @Genre";
                var parameters = new DynamicParameters();
                parameters.Add("Genre", criteria.Genre);

                if (criteria.MinYear.HasValue)
                {
                    genreQuery += " AND Year >= @MinYear";
                    parameters.Add("MinYear", criteria.MinYear.Value);
                }
                if (criteria.MaxYear.HasValue)
                {
                    genreQuery += " AND Year <= @MaxYear";
                    parameters.Add("MaxYear", criteria.MaxYear.Value);
                }

                genreQuery += " ORDER BY Year DESC, Title";
                songs = connection.Query<SongDto>(genreQuery, parameters);
                break;

            case "artist":
                if (string.IsNullOrEmpty(criteria.Artist))
                    return Enumerable.Empty<Song>();

                songs = connection.Query<SongDto>(
                    "SELECT * FROM Songs WHERE Artist LIKE @Artist ORDER BY Album, Title",
                    new { Artist = $"%{criteria.Artist}%" });
                break;

            case "album":
                if (string.IsNullOrEmpty(criteria.Album))
                    return Enumerable.Empty<Song>();

                songs = connection.Query<SongDto>(
                    "SELECT * FROM Songs WHERE Album LIKE @Album ORDER BY Title",
                    new { Album = $"%{criteria.Album}%" });
                break;

            case "neverplayed":
                songs = connection.Query<SongDto>(
                    "SELECT * FROM Songs WHERE PlayCount = 0 ORDER BY DateAdded DESC");
                break;

            default:
                return Enumerable.Empty<Song>();
        }

        return songs.Select(MapToSong);
    }

    // Smart Playlist Criteria model
    private class SmartPlaylistCriteria
    {
        public string? Type { get; set; }
        public int? Limit { get; set; }
        public int? Days { get; set; }
        public string? Genre { get; set; }
        public string? Artist { get; set; }
        public string? Album { get; set; }
        public int? MinYear { get; set; }
        public int? MaxYear { get; set; }
    }

    // DTOs for database mapping
    private class PlaylistDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? IconGlyph { get; set; }
        public int IsLikedSongs { get; set; }
        public int IsSmart { get; set; }
        public string? SmartCriteria { get; set; }
    }

    private class SongDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string? Album { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public int Duration { get; set; }
        public int IsLiked { get; set; }
        public int PlayCount { get; set; }
        public string DateAdded { get; set; } = string.Empty;
        public string? Genre { get; set; }
        public int? Year { get; set; }
        public string? TechnicalDetails { get; set; }
    }

    private static Playlist MapToPlaylist(PlaylistDto dto)
    {
        return new Playlist
        {
            Id = dto.Id,
            Name = dto.Name,
            IconGlyph = dto.IconGlyph ?? "\uE8D6",
            IsLikedSongs = dto.IsLikedSongs == 1,
            IsSmart = dto.IsSmart == 1,
            SmartCriteria = dto.SmartCriteria
        };
    }

    private static Song MapToSong(SongDto dto)
    {
        return new Song
        {
            Id = dto.Id,
            Title = dto.Title,
            Artist = dto.Artist,
            Album = dto.Album ?? string.Empty,
            FilePath = dto.FilePath,
            Duration = TimeSpan.FromSeconds(dto.Duration),
            IsLiked = dto.IsLiked == 1,
            DateAdded = DateTime.TryParse(dto.DateAdded, out var date) ? date : DateTime.Now,
            TechnicalDetails = dto.TechnicalDetails ?? string.Empty
        };
    }
}
