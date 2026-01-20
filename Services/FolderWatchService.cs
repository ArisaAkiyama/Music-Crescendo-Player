using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace DesktopMusicPlayer.Services;

/// <summary>
/// Service that monitors multiple folders for new/deleted music files and syncs with library.
/// </summary>
public class FolderWatchService : IDisposable
{
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _watchPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".m4a", ".wav", ".flac", ".wma", ".aac", ".ogg"
    };
    
    // Events for folder changes
    public event EventHandler<string>? FileAdded;
    public event EventHandler<string>? FileDeleted;
    public event EventHandler<(string OldPath, string NewPath)>? FileRenamed;
    
    // Debounce timer to batch rapid changes
    private readonly Dictionary<string, System.Threading.Timer> _debounceTimers = new();
    private readonly object _lockObj = new();
    
    public IReadOnlyCollection<string> WatchPaths => _watchPaths;
    public bool IsWatching => _watchers.Count > 0 && _watchers.Values.Any(w => w.EnableRaisingEvents);

    public FolderWatchService()
    {
        // Load saved watched folders
        var savedFolders = SettingsService.GetWatchedFolders();
        foreach (var folder in savedFolders)
        {
            if (Directory.Exists(folder))
            {
                _watchPaths.Add(folder);
            }
        }
        
        // Always include default Music folder if no custom folders
        if (_watchPaths.Count == 0)
        {
            var defaultMusic = GetDefaultMusicFolder();
            if (!string.IsNullOrEmpty(defaultMusic) && Directory.Exists(defaultMusic))
            {
                _watchPaths.Add(defaultMusic);
            }
        }
    }

    /// <summary>
    /// Get the default Music folder based on environment.
    /// </summary>
    private string GetDefaultMusicFolder()
    {
        // 1. Try "Music" folder in application directory
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var localMusic = Path.Combine(appDir, "Music");
        
        // 2. Try "Music" folder in project root (dev environment)
        var projectMusic = Path.GetFullPath(Path.Combine(appDir, @"..\..\..\Music"));

        if (Directory.Exists(localMusic))
        {
            Debug.WriteLine($"FolderWatchService: Using local Music folder: {localMusic}");
            return localMusic;
        }
        else if (Directory.Exists(projectMusic))
        {
            Debug.WriteLine($"FolderWatchService: Using project Music folder: {projectMusic}");
            return projectMusic;
        }
        else
        {
            var systemMusic = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            Debug.WriteLine($"FolderWatchService: Using System Music folder: {systemMusic}");
            return systemMusic;
        }
    }

    /// <summary>
    /// Add a folder to watch list and start watching it.
    /// </summary>
    public void AddFolder(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            Debug.WriteLine($"FolderWatchService: Cannot add non-existent folder: {folderPath}");
            return;
        }

        if (_watchPaths.Contains(folderPath))
        {
            Debug.WriteLine($"FolderWatchService: Folder already watched: {folderPath}");
            return;
        }

        _watchPaths.Add(folderPath);
        SettingsService.AddWatchedFolder(folderPath);
        
        // Start watching if service is active
        if (_watchers.Count > 0)
        {
            StartWatchingFolder(folderPath);
        }
        
        Debug.WriteLine($"FolderWatchService: Added folder to watch: {folderPath}");
    }

    /// <summary>
    /// Remove a folder from watch list and stop watching it.
    /// </summary>
    public void RemoveFolder(string folderPath)
    {
        if (_watchPaths.Remove(folderPath))
        {
            SettingsService.RemoveWatchedFolder(folderPath);
            
            if (_watchers.TryGetValue(folderPath, out var watcher))
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                _watchers.Remove(folderPath);
            }
            
            Debug.WriteLine($"FolderWatchService: Removed folder from watch: {folderPath}");
        }
    }

    /// <summary>
    /// Start watching all folders for changes.
    /// </summary>
    public void StartWatching()
    {
        foreach (var path in _watchPaths)
        {
            StartWatchingFolder(path);
        }
    }

    private void StartWatchingFolder(string folderPath)
    {
        if (_watchers.ContainsKey(folderPath)) return;
        
        if (!Directory.Exists(folderPath))
        {
            Debug.WriteLine($"Cannot watch non-existent folder: {folderPath}");
            return;
        }

        try
        {
            var watcher = new FileSystemWatcher(folderPath)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            watcher.Created += OnFileCreated;
            watcher.Deleted += OnFileDeleted;
            watcher.Renamed += OnFileRenamed;
            
            _watchers[folderPath] = watcher;
            Debug.WriteLine($"FolderWatchService: Started watching {folderPath}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"FolderWatchService: Failed to start watching {folderPath}: {ex.Message}");
        }
    }

    /// <summary>
    /// Stop watching all folders.
    /// </summary>
    public void StopWatching()
    {
        foreach (var watcher in _watchers.Values)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        _watchers.Clear();
        Debug.WriteLine("FolderWatchService: Stopped watching all folders");
    }

    /// <summary>
    /// Scan all watched folders and return all music files (for startup sync).
    /// </summary>
    public IEnumerable<string> ScanFolder()
    {
        foreach (var path in _watchPaths)
        {
            if (!Directory.Exists(path)) continue;



            IEnumerable<string> files;
            try
            {
                // Optimization: Scan once for all files, then filter by extension in memory
                // This builds the iterator lazily and avoids walking the directory tree multiple times
                files = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
                        .Where(f => _supportedExtensions.Contains(Path.GetExtension(f)));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error scanning {path}: {ex.Message}");
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }
        }
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        if (!IsMusicFile(e.FullPath)) return;
        
        DebouncedAction(e.FullPath, () =>
        {
            if (File.Exists(e.FullPath))
            {
                Debug.WriteLine($"FolderWatchService: File added - {e.FullPath}");
                System.Windows.Application.Current.Dispatcher.BeginInvoke(() => 
                    FileAdded?.Invoke(this, e.FullPath));
            }
        });
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        if (!IsMusicFile(e.FullPath)) return;
        
        Debug.WriteLine($"FolderWatchService: File deleted - {e.FullPath}");
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() => 
            FileDeleted?.Invoke(this, e.FullPath));
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        if (IsMusicFile(e.FullPath) && !IsMusicFile(e.OldFullPath))
        {
            OnFileCreated(sender, e);
            return;
        }
        
        if (!IsMusicFile(e.FullPath) && IsMusicFile(e.OldFullPath))
        {
            OnFileDeleted(sender, new FileSystemEventArgs(WatcherChangeTypes.Deleted, 
                Path.GetDirectoryName(e.OldFullPath) ?? "", Path.GetFileName(e.OldFullPath)));
            return;
        }
        
        if (IsMusicFile(e.FullPath) && IsMusicFile(e.OldFullPath))
        {
            Debug.WriteLine($"FolderWatchService: File renamed - {e.OldFullPath} -> {e.FullPath}");
            System.Windows.Application.Current.Dispatcher.BeginInvoke(() => 
                FileRenamed?.Invoke(this, (e.OldFullPath, e.FullPath)));
        }
    }

    private bool IsMusicFile(string path)
    {
        var ext = Path.GetExtension(path);
        return _supportedExtensions.Contains(ext);
    }

    private void DebouncedAction(string key, Action action, int delayMs = 1500)
    {
        lock (_lockObj)
        {
            if (_debounceTimers.TryGetValue(key, out var existingTimer))
            {
                existingTimer.Dispose();
            }

            var timer = new System.Threading.Timer(_ =>
            {
                lock (_lockObj)
                {
                    _debounceTimers.Remove(key);
                }
                action();
            }, null, delayMs, Timeout.Infinite);

            _debounceTimers[key] = timer;
        }
    }

    public void Dispose()
    {
        StopWatching();
        
        lock (_lockObj)
        {
            foreach (var timer in _debounceTimers.Values)
            {
                timer.Dispose();
            }
            _debounceTimers.Clear();
        }
        
        GC.SuppressFinalize(this);
    }
}
