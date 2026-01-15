using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using DesktopMusicPlayer.Models;

namespace DesktopMusicPlayer.Services;

/// <summary>
/// Repository for Song CRUD operations using Dapper.
/// </summary>
public class SongRepository
{
    /// <summary>
    /// Get all songs from the database.
    /// </summary>
    public IEnumerable<Song> GetAllSongs()
    {
        using var connection = DatabaseService.GetConnection();
        var songs = connection.Query<SongDto>("SELECT * FROM Songs ORDER BY DateAdded DESC");
        return songs.Select(MapToSong);
    }

    /// <summary>
    /// Get a song by its ID.
    /// </summary>
    public Song? GetSongById(int id)
    {
        using var connection = DatabaseService.GetConnection();
        var dto = connection.QueryFirstOrDefault<SongDto>(
            "SELECT * FROM Songs WHERE Id = @Id", new { Id = id });
        return dto != null ? MapToSong(dto) : null;
    }

    /// <summary>
    /// Get a song by its file path.
    /// </summary>
    public Song? GetSongByFilePath(string filePath)
    {
        using var connection = DatabaseService.GetConnection();
        var dto = connection.QueryFirstOrDefault<SongDto>(
            "SELECT * FROM Songs WHERE FilePath = @FilePath", new { FilePath = filePath });
        return dto != null ? MapToSong(dto) : null;
    }

    /// <summary>
    /// Fast search for songs by keyword (searches Title, Artist, Album).
    /// Uses parameterized query to prevent SQL injection.
    /// </summary>
    public IEnumerable<Song> SearchSongs(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return GetAllSongs();

        using var connection = DatabaseService.GetConnection();
        var searchTerm = $"%{keyword}%";
        var songs = connection.Query<SongDto>(@"
            SELECT * FROM Songs 
            WHERE Title LIKE @k OR Artist LIKE @k OR Album LIKE @k OR Genre LIKE @k
            ORDER BY 
                CASE WHEN Title LIKE @k THEN 0 ELSE 1 END,
                Title
        ", new { k = searchTerm });
        
        return songs.Select(MapToSong);
    }

    /// <summary>
    /// Add a new song to the database.
    /// </summary>
    public int AddSong(Song song)
    {
        using var connection = DatabaseService.GetConnection();
        var id = connection.ExecuteScalar<int>(@"
            INSERT INTO Songs (Title, Artist, Album, FilePath, Duration, IsLiked, PlayCount, DateAdded, Genre, Year, TechnicalDetails)
            VALUES (@Title, @Artist, @Album, @FilePath, @Duration, @IsLiked, @PlayCount, @DateAdded, @Genre, @Year, @TechnicalDetails);
            SELECT last_insert_rowid();
        ", new
        {
            song.Title,
            song.Artist,
            song.Album,
            song.FilePath,
            Duration = (int)song.Duration.TotalSeconds,
            IsLiked = song.IsLiked ? 1 : 0,
            PlayCount = 0,
            DateAdded = song.DateAdded.ToString("o"),
            Genre = (string?)null,
            Year = (int?)null,
            song.TechnicalDetails
        });

        song.Id = id;
        return id;
    }

    /// <summary>
    /// Update an existing song.
    /// </summary>
    public void UpdateSong(Song song)
    {
        using var connection = DatabaseService.GetConnection();
        connection.Execute(@"
            UPDATE Songs 
            SET Title = @Title, Artist = @Artist, Album = @Album, 
                IsLiked = @IsLiked, Genre = @Genre, Year = @Year,
                TechnicalDetails = @TechnicalDetails
            WHERE Id = @Id
        ", new
        {
            song.Id,
            song.Title,
            song.Artist,
            song.Album,
            IsLiked = song.IsLiked ? 1 : 0,
            Genre = (string?)null,
            Year = (int?)null,
            song.TechnicalDetails
        });
    }

    /// <summary>
    /// Update the IsLiked status of a song.
    /// </summary>
    public void UpdateLikedStatus(int songId, bool isLiked)
    {
        using var connection = DatabaseService.GetConnection();
        connection.Execute(
            "UPDATE Songs SET IsLiked = @IsLiked WHERE Id = @Id",
            new { Id = songId, IsLiked = isLiked ? 1 : 0 });
    }

    /// <summary>
    /// Increment the play count for a song.
    /// </summary>
    public void IncrementPlayCount(int songId)
    {
        using var connection = DatabaseService.GetConnection();
        connection.Execute(
            "UPDATE Songs SET PlayCount = PlayCount + 1 WHERE Id = @Id",
            new { Id = songId });
    }

    /// <summary>
    /// Delete a song by ID.
    /// </summary>
    public void DeleteSong(int songId)
    {
        using var connection = DatabaseService.GetConnection();
        connection.Execute("DELETE FROM Songs WHERE Id = @Id", new { Id = songId });
    }

    /// <summary>
    /// Delete a song by file path.
    /// </summary>
    public void DeleteSongByFilePath(string filePath)
    {
        using var connection = DatabaseService.GetConnection();
        connection.Execute("DELETE FROM Songs WHERE FilePath = @FilePath", new { FilePath = filePath });
    }

    /// <summary>
    /// Check if a song exists by file path.
    /// </summary>
    public bool SongExists(string filePath)
    {
        using var connection = DatabaseService.GetConnection();
        return connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Songs WHERE FilePath = @FilePath",
            new { FilePath = filePath }) > 0;
    }

    /// <summary>
    /// Get all liked songs.
    /// </summary>
    public IEnumerable<Song> GetLikedSongs()
    {
        using var connection = DatabaseService.GetConnection();
        var songs = connection.Query<SongDto>(
            "SELECT * FROM Songs WHERE IsLiked = 1 ORDER BY DateAdded DESC");
        return songs.Select(MapToSong);
    }

    /// <summary>
    /// Get top played songs.
    /// </summary>
    public IEnumerable<Song> GetTopPlayedSongs(int limit = 50)
    {
        using var connection = DatabaseService.GetConnection();
        var songs = connection.Query<SongDto>(
            "SELECT * FROM Songs WHERE PlayCount > 0 ORDER BY PlayCount DESC LIMIT @Limit",
            new { Limit = limit });
        return songs.Select(MapToSong);
    }

    /// <summary>
    /// Get recently added songs.
    /// </summary>
    public IEnumerable<Song> GetRecentlyAddedSongs(int days = 30)
    {
        using var connection = DatabaseService.GetConnection();
        var cutoffDate = DateTime.Now.AddDays(-days).ToString("o");
        var songs = connection.Query<SongDto>(
            "SELECT * FROM Songs WHERE DateAdded >= @CutoffDate ORDER BY DateAdded DESC",
            new { CutoffDate = cutoffDate });
        return songs.Select(MapToSong);
    }

    // DTO for database mapping
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

    // Map DTO to Song model
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
