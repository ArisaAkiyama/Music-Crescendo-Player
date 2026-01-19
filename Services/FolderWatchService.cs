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
/// Service that monitors a folder for new/deleted music files and syncs with library.
/// </summary>
public class FolderWatchService : IDisposable
{
    private FileSystemWatcher? _watcher;
    private readonly string _watchPath;
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
    
    public string WatchPath => _watchPath;
    public bool IsWatching => _watcher?.EnableRaisingEvents ?? false;

    public FolderWatchService(string? folderPath = null)
    {
        if (folderPath != null)
        {
            _watchPath = folderPath;
        }
        else
        {
            // Intelligent folder detection for "experiment here" support
            // 1. Try "Music" folder in application directory
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var localMusic = Path.Combine(appDir, "Music");
            
            // 2. Try "Music" folder in project root (dev environment)
            // Walk up from bin\Debug\net8.0-windows... -> Project Root
            var projectMusic = Path.GetFullPath(Path.Combine(appDir, @"..\..\..\Music"));

            if (Directory.Exists(localMusic))
            {
                _watchPath = localMusic;
                Debug.WriteLine($"FolderWatchService: Using local Music folder: {_watchPath}");
            }
            else if (Directory.Exists(projectMusic))
            {
                _watchPath = projectMusic;
                Debug.WriteLine($"FolderWatchService: Using project Music folder: {_watchPath}");
            }
            else
            {
                _watchPath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
                Debug.WriteLine($"FolderWatchService: Using System Music folder: {_watchPath}");
            }
        }
        
        if (!Directory.Exists(_watchPath))
        {
            Debug.WriteLine($"FolderWatchService: Path does not exist: {_watchPath}");
        }
    }

    /// <summary>
    /// Start watching the folder for changes.
    /// </summary>
    public void StartWatching()
    {
        if (_watcher != null) return;
        
        if (!Directory.Exists(_watchPath))
        {
            Debug.WriteLine($"Cannot watch non-existent folder: {_watchPath}");
            return;
        }

        try
        {
            _watcher = new FileSystemWatcher(_watchPath)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            // Set filters for music files
            _watcher.Created += OnFileCreated;
            _watcher.Deleted += OnFileDeleted;
            _watcher.Renamed += OnFileRenamed;
            
            Debug.WriteLine($"FolderWatchService: Started watching {_watchPath}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"FolderWatchService: Failed to start watching: {ex.Message}");
        }
    }

    /// <summary>
    /// Stop watching the folder.
    /// </summary>
    public void StopWatching()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
            Debug.WriteLine("FolderWatchService: Stopped watching");
        }
    }

    /// <summary>
    /// Scan the folder and return all music files (for startup sync).
    /// </summary>
    public IEnumerable<string> ScanFolder()
    {
        if (!Directory.Exists(_watchPath))
        {
            yield break;
        }

        foreach (var ext in _supportedExtensions)
        {
            foreach (var file in Directory.EnumerateFiles(_watchPath, $"*{ext}", SearchOption.AllDirectories))
            {
                yield return file;
            }
        }
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        if (!IsMusicFile(e.FullPath)) return;
        
        // Debounce to avoid multiple events for same file (copy operations trigger multiple events)
        DebouncedAction(e.FullPath, () =>
        {
            // Verify file still exists and is accessible
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
        // Handle renamed TO a music file
        if (IsMusicFile(e.FullPath) && !IsMusicFile(e.OldFullPath))
        {
            // File was renamed from non-music to music (e.g., .mp3.tmp to .mp3)
            OnFileCreated(sender, e);
            return;
        }
        
        // Handle renamed FROM a music file
        if (!IsMusicFile(e.FullPath) && IsMusicFile(e.OldFullPath))
        {
            // File was renamed from music to non-music
            OnFileDeleted(sender, new FileSystemEventArgs(WatcherChangeTypes.Deleted, 
                Path.GetDirectoryName(e.OldFullPath) ?? "", Path.GetFileName(e.OldFullPath)));
            return;
        }
        
        // Both are music files - it's a rename
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
