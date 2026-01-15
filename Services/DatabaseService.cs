using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using Dapper;

namespace DesktopMusicPlayer.Services;

/// <summary>
/// Database service for SQLite database initialization and connection management.
/// Uses Dapper for data access.
/// </summary>
public class DatabaseService
{
    private static readonly string DatabaseFileName = "music_library.db";
    private static readonly string DatabasePath;
    private static readonly string ConnectionString;

    static DatabaseService()
    {
        string folderPath = "";
        
        try
        {
            // 1. Try LocalApplicationData
            folderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }
        catch { }

        // 2. Fallback to UserProfile if needed
        if (string.IsNullOrEmpty(folderPath))
        {
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(userProfile))
                {
                    folderPath = Path.Combine(userProfile, "AppData", "Local");
                }
            }
            catch { }
        }

        // 3. Fallback to Temp
        if (string.IsNullOrEmpty(folderPath))
        {
            try { folderPath = Path.GetTempPath(); } catch { }
        }

        // 4. Fallback to BaseDirectory (SingleFile friendly)
        if (string.IsNullOrEmpty(folderPath))
        {
             try { folderPath = AppContext.BaseDirectory; } catch { }
        }

        // 5. Nuclear Fallback (Current Directory)
        if (string.IsNullOrEmpty(folderPath))
        {
             folderPath = ".";
        }

        var appDataPath = Path.Combine(folderPath, "CrescendoMusicPlayer");
        
        // Create directory
        try 
        {
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }
        }
        catch 
        {
            // If creation fails, try Temp again as absolute last resort
            try 
            {
                appDataPath = Path.Combine(Path.GetTempPath(), "CrescendoMusicPlayer");
                Directory.CreateDirectory(appDataPath);
            }
            catch { }
        }
        
        DatabasePath = Path.Combine(appDataPath, DatabaseFileName);
        ConnectionString = $"Data Source={DatabasePath};Version=3;";
    }

    /// <summary>
    /// Get a new database connection. Caller is responsible for disposing.
    /// </summary>
    public static IDbConnection GetConnection()
    {
        return new SQLiteConnection(ConnectionString);
    }

    /// <summary>
    /// Initialize the database and create tables if they don't exist.
    /// Call this at application startup.
    /// </summary>
    public static void InitializeDatabase()
    {
        using var connection = GetConnection();
        connection.Open();

        // Create Songs table with indexes
        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS Songs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Artist TEXT NOT NULL,
                Album TEXT,
                FilePath TEXT UNIQUE NOT NULL,
                Duration INTEGER NOT NULL,
                IsLiked INTEGER DEFAULT 0,
                PlayCount INTEGER DEFAULT 0,
                DateAdded TEXT NOT NULL,
                Genre TEXT,
                Year INTEGER,
                TechnicalDetails TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_songs_title ON Songs(Title);
            CREATE INDEX IF NOT EXISTS idx_songs_artist ON Songs(Artist);
            CREATE INDEX IF NOT EXISTS idx_songs_album ON Songs(Album);
            CREATE INDEX IF NOT EXISTS idx_songs_genre ON Songs(Genre);
            CREATE INDEX IF NOT EXISTS idx_songs_filepath ON Songs(FilePath);
            CREATE INDEX IF NOT EXISTS idx_songs_dateadded ON Songs(DateAdded);
            CREATE INDEX IF NOT EXISTS idx_songs_playcount ON Songs(PlayCount);
            CREATE INDEX IF NOT EXISTS idx_songs_isliked ON Songs(IsLiked);
        ");

        // Create Playlists table
        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS Playlists (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                IconGlyph TEXT DEFAULT '0xE8D6',
                IsLikedSongs INTEGER DEFAULT 0,
                IsSmart INTEGER DEFAULT 0,
                SmartCriteria TEXT
            );
        ");

        // Create PlaylistSongs junction table for many-to-many relationship
        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS PlaylistSongs (
                PlaylistId INTEGER NOT NULL,
                SongId INTEGER NOT NULL,
                SortOrder INTEGER DEFAULT 0,
                PRIMARY KEY (PlaylistId, SongId),
                FOREIGN KEY (PlaylistId) REFERENCES Playlists(Id) ON DELETE CASCADE,
                FOREIGN KEY (SongId) REFERENCES Songs(Id) ON DELETE CASCADE
            );
        ");

        // Create default "Liked Songs" playlist if it doesn't exist
        var likedPlaylistExists = connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Playlists WHERE IsLikedSongs = 1"
        );

        if (likedPlaylistExists == 0)
        {
            connection.Execute(@"
                INSERT INTO Playlists (Name, IconGlyph, IsLikedSongs, IsSmart, SmartCriteria)
                VALUES ('Liked Songs', '0xEB51', 1, 1, '{""Type"": ""LikedSongs""}')
            ");
        }
    }

    /// <summary>
    /// Get the database file path for debugging/backup purposes.
    /// </summary>
    public static string GetDatabasePath() => DatabasePath;
}
