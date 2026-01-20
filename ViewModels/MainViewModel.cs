using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using DesktopMusicPlayer.Models;
using DesktopMusicPlayer.Services;
using Microsoft.Win32;
using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using System.Reflection;

namespace DesktopMusicPlayer.ViewModels
{
    public class MainViewModel : ViewModelBase, IDisposable
    {
        private readonly IAudioService _audioService;
        private readonly MusicProviderService _musicProvider;
        private readonly DispatcherTimer _progressTimer;
        private readonly DispatcherTimer _songSearchDebounceTimer;
        private readonly DispatcherTimer _playlistSearchDebounceTimer;

        // Current Song (Now Playing) - only updated when actually playing
        private Song? _currentSong;
        public Song? CurrentSong
        {
            get => _currentSong;
            set
            {
                if (SetProperty(ref _currentSong, value))
                {
                    OnPropertyChanged(nameof(IsCurrentSongLiked));
                    OnPropertyChanged(nameof(UpcomingSongs));
                }
            }
        }

        // Wrapper for CurrentSong.IsLiked for data binding
        public bool IsCurrentSongLiked => CurrentSong?.IsLiked ?? false;

        // Selected Song (UI selection only - single click)
        private Song? _selectedSong;
        public Song? SelectedSong
        {
            get => _selectedSong;
            set => SetProperty(ref _selectedSong, value);
        }

        // Playback State
        private bool _isPlaying;
        public bool IsPlaying
        {
            get => _isPlaying;
            set => SetProperty(ref _isPlaying, value);
        }

        // Progress
        private double _currentProgress;
        private bool _isProgressUpdateFromTimer = false; // Flag to prevent feedback loops

        public double CurrentProgress
        {
            get => _currentProgress;
            set
            {
                if (SetProperty(ref _currentProgress, value))
                {
                    // Always update time display when progress changes
                    CurrentTimeFormatted = TimeSpan.FromSeconds(value).ToString(@"m\:ss");
                    
                    // Logic: Update audio position ONLY if change came from User (UI), not Timer
                    if (!_isProgressUpdateFromTimer && _audioService != null)
                    {
                        // Limit seek frequency/overhead if needed, but for local files invalidating buffer is fast enough
                        // This handles both Dragging AND Click-to-Seek (IsMoveToPointEnabled)
                        _audioService.Position = TimeSpan.FromSeconds(value);
                    }
                }
            }
        }

        private double _totalDuration;
        public double TotalDuration
        {
            get => _totalDuration;
            set => SetProperty(ref _totalDuration, value);
        }

        private string _currentTimeFormatted = "0:00";
        public string CurrentTimeFormatted
        {
            get => _currentTimeFormatted;
            set => SetProperty(ref _currentTimeFormatted, value);
        }

        private string _totalTimeFormatted = "0:00";
        public string TotalTimeFormatted
        {
            get => _totalTimeFormatted;
            set => SetProperty(ref _totalTimeFormatted, value);
        }

        // Volume
        private double _previousVolume = 0.5;
        private bool _isMuted;
        public bool IsMuted
        {
            get => _isMuted;
            set => SetProperty(ref _isMuted, value);
        }

        private double _volume; // Default 0.0, will trigger setter on first load
        public double Volume
        {
            get => _volume;
            set
            {
                if (SetProperty(ref _volume, value))
                {
                    // Logarithmic volume control (safety first)
                    // Input: 0.0 - 1.0 (Slider)
                    // Output: Logarithmic curve (Cubic x^3) * 0.5 (Headroom/Pre-Gain)
                    // This creates a "Normalized" loudness ceiling similar to Spotify (-14 LUFS)
                    var logVolume = (float)Math.Pow(value, 3.0) * 0.5f;
                    _audioService.Volume = logVolume;
                    
                    // Logic: If user drags slider (value > 0), unmute if muted.
                    // If user drags to 0, mute.
                    if (value > 0 && IsMuted)
                    {
                        IsMuted = false;
                    }
                    else if (value == 0 && !IsMuted)
                    {
                        IsMuted = true;
                    }

                    // Save volume state (persisted)
                    SettingsService.SaveVolume(value);
                }
            }
        }

        // Shuffle & Repeat
        private bool _isShuffleEnabled;
        public bool IsShuffleEnabled
        {
            get => _isShuffleEnabled;
            set
            {
                if (SetProperty(ref _isShuffleEnabled, value))
                {
                    OnPropertyChanged(nameof(UpcomingSongs));
                }
            }
        }

        public enum PlaybackRepeatMode
        {
            Off,
            RepeatAll,
            RepeatOne
        }

        private PlaybackRepeatMode _repeatMode = PlaybackRepeatMode.Off;
        public PlaybackRepeatMode RepeatMode
        {
            get => _repeatMode;
            set
            {
                if (SetProperty(ref _repeatMode, value))
                {
                    OnPropertyChanged(nameof(UpcomingSongs));
                }
            }
        }

        // Music Queue
        public ObservableCollection<Song> PlayQueue { get; } = new ObservableCollection<Song>();

        private bool _isQueueVisible;
        public bool IsQueueVisible
        {
            get => _isQueueVisible;
            set => SetProperty(ref _isQueueVisible, value);
        }
        
        // Upcoming songs (prioritize manual queue, then auto-generated)
        public IEnumerable<Song> UpcomingSongs
        {
            get
            {
                // If manual queue has items, show all of them (queue always has priority)
                if (PlayQueue.Count > 0)
                {
                    return PlayQueue;
                }
                
                // If shuffle is enabled, we can't predict the next song
                if (IsShuffleEnabled)
                {
                    return Enumerable.Empty<Song>();
                }
                
                // Otherwise show the next song in playlist order
                if (CurrentSong == null) return Enumerable.Empty<Song>();
                
                // Fallback to full Songs collection if current view is empty
                var displayedSongs = SongsView?.Cast<Song>().ToList() ?? new List<Song>();
                if (displayedSongs.Count == 0)
                {
                    displayedSongs = Songs.ToList();
                }
                if (displayedSongs.Count == 0) return Enumerable.Empty<Song>();
                
                var currentIndex = displayedSongs.IndexOf(CurrentSong);
                
                if (currentIndex < 0) return Enumerable.Empty<Song>();
                
                // Check if we're on the last song
                bool isLastSong = currentIndex == displayedSongs.Count - 1;
                
                // If Repeat is Off and we're on the last song, no upcoming songs
                if (RepeatMode == PlaybackRepeatMode.Off && isLastSong)
                {
                    return Enumerable.Empty<Song>();
                }
                
                // Calculate next index with wrap-around (for RepeatAll or if not at end)
                var nextIndex = (currentIndex + 1) % displayedSongs.Count;
                return new[] { displayedSongs[nextIndex] };
            }
        }

        // Rename Popup
        private bool _isRenamePopupOpen;
        public bool IsRenamePopupOpen
        {
            get => _isRenamePopupOpen;
            set => SetProperty(ref _isRenamePopupOpen, value);
        }

        // Queue Popup
        private bool _isQueuePopupOpen;
        public bool IsQueuePopupOpen
        {
            get => _isQueuePopupOpen;
            set => SetProperty(ref _isQueuePopupOpen, value);
        }

        // Keyboard Shortcuts Popup
        private bool _isShortcutsPopupOpen;
        public bool IsShortcutsPopupOpen
        {
            get => _isShortcutsPopupOpen;
            set => SetProperty(ref _isShortcutsPopupOpen, value);
        }

        // About Popup
        private bool _isAboutPopupOpen;
        public bool IsAboutPopupOpen
        {
            get => _isAboutPopupOpen;
            set => SetProperty(ref _isAboutPopupOpen, value);
        }

        private string _renameText = string.Empty;
        public string RenameText
        {
            get => _renameText;
            set => SetProperty(ref _renameText, value);
        }
        // Search
        private bool _isLibrarySearchVisible;
        public bool IsLibrarySearchVisible
        {
            get => _isLibrarySearchVisible;
            set => SetProperty(ref _isLibrarySearchVisible, value);
        }

        private string _librarySearchText = string.Empty;
        public string LibrarySearchText
        {
            get => _librarySearchText;
            set 
            {
                if (SetProperty(ref _librarySearchText, value))
                {
                    // Debounce: restart timer on each keystroke (playlists only)
                    _playlistSearchDebounceTimer.Stop();
                    _playlistSearchDebounceTimer.Start();
                }
            }
        }
        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    // Debounce: restart timer on each keystroke (songs only)
                    _songSearchDebounceTimer.Stop();
                    _songSearchDebounceTimer.Start();
                }
            }
        }

        // Current folder path
        private string _currentFolderPath = string.Empty;
        public string CurrentFolderPath
        {
            get => _currentFolderPath;
            set => SetProperty(ref _currentFolderPath, value);
        }

        // Dragging state for slider - public for binding
        private bool _isDragging;
        public bool IsDragging
        {
            get => _isDragging;
            set => SetProperty(ref _isDragging, value);
        }

        // Loading state for async initialization
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        // Collections
        public ObservableCollection<Song> Songs { get; } = new();
        public ObservableCollection<Playlist> Playlists { get; } = new();
        
        // Filtered view for playlists
        private ICollectionView? _playlistsView;
        public ICollectionView? PlaylistsView
        {
            get => _playlistsView;
            set => SetProperty(ref _playlistsView, value);
        }
        
        // Filtered view for search
        private ICollectionView? _songsView;
        public ICollectionView? SongsView 
        { 
            get => _songsView;
            private set => SetProperty(ref _songsView, value);
        }

        // Selected playlist for filtering
        private Playlist? _selectedPlaylist;
        public Playlist? SelectedPlaylist
        {
            get => _selectedPlaylist;
            set
            {
                if (SetProperty(ref _selectedPlaylist, value))
                {
                    UpdateContentView();
                    OnPropertyChanged(nameof(IsFavoritesEmpty));
                    OnPropertyChanged(nameof(IsFolderEmpty));
                }
            }
        }

        // Computed property for "No Favorites" state
        public bool IsFavoritesEmpty => SelectedPlaylist?.IsLikedSongs == true && (SongsView == null || SongsView.IsEmpty);

        // Computed property for "No Tracks" state (Folders/Playlists)
        public bool IsFolderEmpty => SelectedPlaylist != null && !SelectedPlaylist.IsLikedSongs && (SongsView == null || SongsView.IsEmpty);

        // Content header title
        private string _contentTitle = "All Tracks";
        public string ContentTitle
        {
            get => _contentTitle;
            set => SetProperty(ref _contentTitle, value);
        }

        // Song count display
        private string _songCountText = "0 songs";
        public string SongCountText
        {
            get => _songCountText;
            set => SetProperty(ref _songCountText, value);
        }

        // Actions for code-behind
        public Action? SelectAllSongs { get; set; }
        public Action? DeselectAllSongs { get; set; }
        public Action? FocusSearchBox { get; set; }

        // Commands
        public ICommand PlayCommand { get; }
        public ICommand SortByCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand PlayPauseCommand { get; }
        public ICommand ToggleMuteCommand { get; }
        public ICommand NextCommand { get; }
        public ICommand PreviousCommand { get; }
        public ICommand ShuffleCommand { get; }
        public ICommand RepeatCommand { get; }
        public ICommand SeekStartCommand { get; }
        public ICommand SeekEndCommand { get; }
        public ICommand PlaySongCommand { get; }
        public ICommand OpenFolderCommand { get; }
        public ICommand ToggleLikeCommand { get; }
        public ICommand RemoveFromLibraryCommand { get; }
        public ICommand AddToPlaylistCommand { get; }
        public ICommand HomeCommand { get; }
        public ICommand AddPlaylistCommand { get; }
        public ICommand AddFolderCommand { get; }
        public ICommand AddFilesCommand { get; }
        public ICommand SeekForwardCommand { get; }
        public ICommand SeekRewindCommand { get; }
        public ICommand DeletePlaylistCommand { get; }
        public ICommand RenamePlaylistCommand { get; }
        public ICommand ConfirmRenameCommand { get; }
        public ICommand CancelRenameCommand { get; }
        public ICommand AddToQueueCommand { get; }
        public ICommand RemoveFromQueueCommand { get; }
        public ICommand ToggleQueueCommand { get; }
        public ICommand ToggleQueuePopupCommand { get; }
        public ICommand CloseQueuePopupCommand { get; }
        public ICommand ClearQueueCommand { get; }
        public ICommand SortCommand { get; }
        public ICommand ShowRecentlyPlayedCommand { get; }
        public ICommand ClearHistoryCommand { get; }

        public ICommand AddSongToPlaylistCommand { get; }
        public ICommand CloseShortcutsPopupCommand { get; }
        public ICommand CloseAboutPopupCommand { get; }
        public ICommand ExitApplicationCommand { get; }
        public ICommand SettingsCommand { get; }
        public ICommand ScrollToCurrentCommand { get; }
        public ICommand ToggleLibrarySearchCommand { get; }
        public ICommand CloseLibrarySearchCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand DeselectAllCommand { get; }
        public ICommand FocusSearchCommand { get; }
        public ICommand ToggleSidebarCommand { get; }
        public ICommand ToggleFullScreenCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand VolumeUpCommand { get; }
        public ICommand VolumeDownCommand { get; }
        public ICommand SetRepeatOffCommand { get; }
        public ICommand SetRepeatAllCommand { get; }
        public ICommand SetRepeatOneCommand { get; }
        public ICommand KeyboardShortcutsCommand { get; }
        public ICommand AboutCommand { get; }
        public ICommand ToggleThemeCommand { get; }
        public ICommand ToggleMyTracksCommand { get; }
        
        private bool _isMyTracksExpanded = true;
        public bool IsMyTracksExpanded
        {
            get => _isMyTracksExpanded;
            set => SetProperty(ref _isMyTracksExpanded, value);
        }

        // Watched Folders for auto-sync
        public ObservableCollection<string> WatchedFolders { get; } = new();
        public ICommand AddWatchedFolderCommand { get; }
        public ICommand RemoveWatchedFolderCommand { get; }

        // Settings Popup
        private bool _isSettingsPopupOpen;
        public bool IsSettingsPopupOpen
        {
            get => _isSettingsPopupOpen;
            set => SetProperty(ref _isSettingsPopupOpen, value);
        }

        // Auto-Update Properties
        private bool _isUpdateAvailable;
        public bool IsUpdateAvailable
        {
            get => _isUpdateAvailable;
            set => SetProperty(ref _isUpdateAvailable, value);
        }

        private string _updateVersion = "";
        public string UpdateVersion
        {
            get => _updateVersion;
            set => SetProperty(ref _updateVersion, value);
        }
        
        private string _updateReleaseNotes = "";
        public string UpdateReleaseNotes
        {
            get => _updateReleaseNotes;
            set => SetProperty(ref _updateReleaseNotes, value);
        }
        
        private string _updateDownloadUrl = "";

        private double _updateDownloadProgress;
        public double UpdateDownloadProgress
        {
            get => _updateDownloadProgress;
            set => SetProperty(ref _updateDownloadProgress, value);
        }

        private bool _isUpdating;
        public bool IsUpdating
        {
            get => _isUpdating;
            set => SetProperty(ref _isUpdating, value);
        }

        private bool _isCheckUpdatesPopupOpen;
        public bool IsCheckUpdatesPopupOpen
        {
            get => _isCheckUpdatesPopupOpen;
            set => SetProperty(ref _isCheckUpdatesPopupOpen, value);
        }

        private bool _isCheckingForUpdates;
        public bool IsCheckingForUpdates
        {
            get => _isCheckingForUpdates;
            set => SetProperty(ref _isCheckingForUpdates, value);
        }

        public ICommand CloseCheckUpdatesPopupCommand { get; }
        
        public ICommand CheckForUpdatesCommand { get; }
        public ICommand PerformUpdateCommand { get; }






        // Actions for code-behind
        public Action? ToggleFullScreen { get; set; }
        public Action? ScrollToCurrentAction { get; set; }

        // Sidebar visibility
        private bool _isSidebarVisible = true;
        public bool IsSidebarVisible
        {
            get => _isSidebarVisible;
            set => SetProperty(ref _isSidebarVisible, value);
        }

        // Current sort state
        private string _currentSortProperty = "DateAdded";
        private bool _sortAscending = false; // Default descending for DateAdded

        // Playlist service for persistence (legacy JSON)
        private readonly PlaylistService _playlistService;
        private readonly HistoryService _historyService;
        private List<(string FilePath, DateTime PlayedAt)> _historyData = new();
        
        // SQLite repositories
        private readonly SongRepository _songRepository;
        private readonly PlaylistRepository _playlistRepository;
        
        // Folder Watch Service for auto-sync
        private FolderWatchService? _folderWatchService;
        
        // Update Service
        private readonly UpdateService _updateService;

        // Recently Played Songs
        public ObservableCollection<Song> RecentlyPlayedSongs { get; } = new ObservableCollection<Song>();

        public string CurrentVersion { get; private set; } = "1.0.0"; // Fallback
        public string AppBuildDate { get; private set; } = "Unknown"; // Build date

        public MainViewModel()
        {
            // Get current version (Semantic Versioning)
            try 
            {
                var assembly = System.Reflection.Assembly.GetEntryAssembly();
                if (assembly != null)
                {
                    var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                    
                    if (!string.IsNullOrEmpty(informationalVersion))
                    {
                        // Remove build metadata (part after +)
                        var version = informationalVersion.Split('+')[0];
                        CurrentVersion = $"v{version}";
                    }
                    else
                    {
                        // Fallback to standard version
                        var version = assembly.GetName().Version;
                        if (version != null)
                        {
                            CurrentVersion = $"v{version.Major}.{version.Minor}.{version.Build}";
                        }
                    }
                }
            }
            catch { /* Ignore version read error */ }
            
            // Get build date from assembly file
            try
            {
                var assemblyPath = System.Reflection.Assembly.GetEntryAssembly()?.Location;
                if (!string.IsNullOrEmpty(assemblyPath) && System.IO.File.Exists(assemblyPath))
                {
                    var buildDate = System.IO.File.GetLastWriteTime(assemblyPath);
                    AppBuildDate = $"Build {buildDate:yyyy.MM.dd}";
                }
            }
            catch { /* Ignore build date error */ }

            // Set default visibility
            IsQueueVisible = true;
            
            // Initialize services
            _audioService = new AudioService();
            _musicProvider = new MusicProviderService();
            
            // Set default visibility
            IsQueueVisible = true;
            
            // Load saved volume (or default 0.5) using Setter to apply Cubic Logic
            Volume = SettingsService.GetVolume();
            _playlistService = new PlaylistService();
            
            // Initialize SQLite repositories
            _songRepository = new SongRepository();
            _playlistRepository = new PlaylistRepository();
            _historyService = new HistoryService();
            _updateService = new UpdateService();
            _toastService = new ToastNotificationService();
            
            // Subscribe to playback stopped event
            _audioService.PlaybackStopped += OnPlaybackStopped;

            // Initialize progress timer
            _progressTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _progressTimer.Tick += OnProgressTimerTick;
            
            // Initialize SONG search debounce timer (300ms delay) - navbar search
            _songSearchDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _songSearchDebounceTimer.Tick += (s, e) =>
            {
                _songSearchDebounceTimer.Stop();
                SongsView?.Refresh(); // Only refresh songs, NOT playlists
            };
            
            // Initialize PLAYLIST search debounce timer (300ms delay) - sidebar library search
            _playlistSearchDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _playlistSearchDebounceTimer.Tick += (s, e) =>
            {
                _playlistSearchDebounceTimer.Stop();
                PlaylistsView?.Refresh(); // Only refresh playlists, NOT songs
            };

            // Initialize Commands
            PlayCommand = new RelayCommand(_ => Play());
            PauseCommand = new RelayCommand(_ => Pause());
            PlayPauseCommand = new RelayCommand(_ => PlayPause());
            ToggleMuteCommand = new RelayCommand(_ => 
            {
                if (IsMuted)
                {
                    // Unmute: Restore previous volume
                    IsMuted = false;
                    Volume = _previousVolume > 0 ? _previousVolume : 0.5;
                }
                else
                {
                    // Mute: Save current volume and set to 0
                    if (Volume > 0) _previousVolume = Volume;
                    IsMuted = true;
                    Volume = 0;
                }
            });
            NextCommand = new RelayCommand(_ => Next());
            PreviousCommand = new RelayCommand(_ => Previous());
            
            // Queue Commands
            RemoveFromQueueCommand = new RelayCommand(param => RemoveFromQueue(param as Models.Song));
            ShuffleCommand = new RelayCommand(_ => IsShuffleEnabled = !IsShuffleEnabled);
            RepeatCommand = new RelayCommand(_ => 
            {
                // Cycle: Off -> RepeatAll -> RepeatOne -> Off
                switch (RepeatMode)
                {
                    case PlaybackRepeatMode.Off:
                        RepeatMode = PlaybackRepeatMode.RepeatAll;
                        break;
                    case PlaybackRepeatMode.RepeatAll:
                        RepeatMode = PlaybackRepeatMode.RepeatOne;
                        break;
                    case PlaybackRepeatMode.RepeatOne:
                        RepeatMode = PlaybackRepeatMode.Off;
                        break;
                }
            });
            SeekStartCommand = new RelayCommand(_ => IsDragging = true);
            SeekEndCommand = new RelayCommand(_ => EndSeek());
            PlaySongCommand = new RelayCommand(song => PlaySelectedSong(song as Song));
            OpenFolderCommand = new RelayCommand(_ => OpenFolder());
            ToggleLikeCommand = new RelayCommand(song => ToggleLike(song as Song));
            RemoveFromLibraryCommand = new RelayCommand(song => RemoveFromLibrary(song as Song));
            AddToPlaylistCommand = new RelayCommand(song => AddToPlaylist(song as Song));
            KeyboardShortcutsCommand = new RelayCommand(_ => ShowKeyboardShortcuts());
            AboutCommand = new RelayCommand(_ => ShowAbout());
            ToggleThemeCommand = new RelayCommand(_ => ToggleTheme());
            ToggleMyTracksCommand = new RelayCommand(_ => IsMyTracksExpanded = !IsMyTracksExpanded);
            HomeCommand = new RelayCommand(_ => 
            { 
                 SelectedPlaylist = null;
                 SearchText = string.Empty;
                 LibrarySearchText = string.Empty;
                 ContentTitle = "All Tracks";
                 // Ensure 'All Songs' filter applies
                 if (SongsView is ICollectionView view)
                 {
                     view.Filter = FilterSongs;
                     view.Refresh();
                 }
            });

            // Menu Commands
            ExitApplicationCommand = new RelayCommand(_ => Application.Current.Shutdown());
            SettingsCommand = new RelayCommand(_ => MessageBox.Show("Settings are coming soon!", "Settings", MessageBoxButton.OK, MessageBoxImage.Information));
            ScrollToCurrentCommand = new RelayCommand(_ => { /* Logic handled in code-behind */ });
            SelectAllCommand = new RelayCommand(_ => SelectAllSongs?.Invoke());
            DeselectAllCommand = new RelayCommand(_ => DeselectAllSongs?.Invoke());
            FocusSearchCommand = new RelayCommand(_ => 
            {
                IsLibrarySearchVisible = true;
                FocusSearchBox?.Invoke();
            });
            ToggleSidebarCommand = new RelayCommand(_ => IsSidebarVisible = !IsSidebarVisible);
            ToggleFullScreenCommand = new RelayCommand(_ => ToggleFullScreen?.Invoke());
            StopCommand = new RelayCommand(_ => Stop());
            VolumeUpCommand = new RelayCommand(_ => Volume = Math.Min(1.0, Volume + 0.1));
            VolumeDownCommand = new RelayCommand(_ => Volume = Math.Max(0.0, Volume - 0.1));
            SetRepeatOffCommand = new RelayCommand(_ => RepeatMode = PlaybackRepeatMode.Off);
            SetRepeatAllCommand = new RelayCommand(_ => RepeatMode = PlaybackRepeatMode.RepeatAll);
            SetRepeatOneCommand = new RelayCommand(_ => RepeatMode = PlaybackRepeatMode.RepeatOne);
            KeyboardShortcutsCommand = new RelayCommand(_ => IsShortcutsPopupOpen = !IsShortcutsPopupOpen);
            AboutCommand = new RelayCommand(_ => IsAboutPopupOpen = !IsAboutPopupOpen);
            AddPlaylistCommand = new RelayCommand(_ => AddNewPlaylist());
            AddFolderCommand = new RelayCommand(_ => OpenFolder()); // Opens folder picker dialog
            AddFilesCommand = new RelayCommand(_ => AddMp3Files()); // Opens file picker dialog
            SeekForwardCommand = new RelayCommand(_ => SeekForward());
            SeekRewindCommand = new RelayCommand(_ => SeekRewind());
            DeletePlaylistCommand = new RelayCommand(param => DeleteSelectedPlaylist(param));
            RenamePlaylistCommand = new RelayCommand(param => OpenRenamePopup(param));
            ConfirmRenameCommand = new RelayCommand(_ => ConfirmRename());
            CancelRenameCommand = new RelayCommand(_ => CancelRename());
            SortByCommand = new RelayCommand(param => SortBy(param as string));
            AddToQueueCommand = new RelayCommand(song => AddToQueue(song as Song));
            RemoveFromQueueCommand = new RelayCommand(song => RemoveFromQueue(song as Song));
            ToggleQueueCommand = new RelayCommand(_ => IsQueueVisible = !IsQueueVisible);
            ToggleQueuePopupCommand = new RelayCommand(_ => IsQueuePopupOpen = !IsQueuePopupOpen);
            CloseQueuePopupCommand = new RelayCommand(_ => IsQueuePopupOpen = false);
            ClearQueueCommand = new RelayCommand(_ => 
            {
                PlayQueue.Clear();
                OnPropertyChanged(nameof(UpcomingSongs));
            });
            SortCommand = new RelayCommand(property => SortBy(property as string));
            ShowRecentlyPlayedCommand = new RelayCommand(_ => ShowRecentlyPlayed());
            ClearHistoryCommand = new RelayCommand(_ => ClearHistory());
            AddSongToPlaylistCommand = new RelayCommand(param => AddSongToPlaylist(param));
            CloseShortcutsPopupCommand = new RelayCommand(_ => IsShortcutsPopupOpen = false);
            CloseAboutPopupCommand = new RelayCommand(_ => IsAboutPopupOpen = false);

            ExitApplicationCommand = new RelayCommand(_ => Application.Current.Shutdown());
            ScrollToCurrentCommand = new RelayCommand(_ => ScrollToCurrentAction?.Invoke());
            ToggleLibrarySearchCommand = new RelayCommand(_ => IsLibrarySearchVisible = !IsLibrarySearchVisible);
            CloseLibrarySearchCommand = new RelayCommand(_ => { IsLibrarySearchVisible = false; LibrarySearchText = ""; });
            ToggleMyTracksCommand = new RelayCommand(_ => IsMyTracksExpanded = !IsMyTracksExpanded);
            
            // Watched Folders Commands
            AddWatchedFolderCommand = new RelayCommand(_ => AddWatchedFolder());
            RemoveWatchedFolderCommand = new RelayCommand(folder => RemoveWatchedFolder(folder as string));
            SettingsCommand = new RelayCommand(_ => IsSettingsPopupOpen = !IsSettingsPopupOpen);
            
            // Auto-Update Commands
            CheckForUpdatesCommand = new RelayCommand(async _ => await CheckForUpdatesAsync(isManual: true));
            PerformUpdateCommand = new RelayCommand(async _ => await PerformUpdateAsync());
            CloseCheckUpdatesPopupCommand = new RelayCommand(_ => { IsCheckUpdatesPopupOpen = false; IsCheckingForUpdates = false; });

            // Initialize collection view for filtering (empty at this point)
            InitializeCollectionView();

            // Apply default theme (Gradient) on startup
            ApplyThemeChange();

            // Note: Data loading is deferred to InitializeDataAsync() 
            // which should be called from MainWindow.Loaded event
        }

        /// <summary>
        /// Asynchronously initializes data. Call this from MainWindow.Loaded event.
        /// </summary>
        public async Task InitializeDataAsync()
        {
            if (IsLoading) return; // Prevent double initialization
            
            IsLoading = true;
            
            try
            {
                // Phase 0: Cleanup orphan songs (Zombie songs from previous bugs)
                await Task.Run(() => 
                {
                    int deleted = _songRepository.DeleteOrphanSongs();
                    if (deleted > 0) System.Diagnostics.Debug.WriteLine($"Cleaned up {deleted} orphan songs.");
                });

            // Phase 0.5: Rename existing 'Liked Songs' to 'Favorite Tracks' (Migration)
                await Task.Run(() =>
                {
                    var likedPlaylist = _playlistRepository.GetLikedSongsPlaylist();
                    if (likedPlaylist != null && likedPlaylist.Name == "Liked Songs")
                    {
                        likedPlaylist.Name = "Favorite Tracks";
                        _playlistRepository.UpdatePlaylist(likedPlaylist);
                        System.Diagnostics.Debug.WriteLine("Migrated 'Liked Songs' to 'Favorite Tracks'");
                    }
                });

                // Phase 1: Load playlists metadata from DB (fast, can be on background)
                var playlistsFromDb = await Task.Run(() => _playlistRepository.GetAllPlaylists().ToList());

                // Phase 1.5: Migrate from JSON if DB is empty
                if (!playlistsFromDb.Any())
                {
                    await Task.Run(() =>
                    {
                        try 
                        {
                            var legacyPlaylists = _playlistService.LoadPlaylists(); // JSON load
                            if (legacyPlaylists.Any())
                            {
                                System.Diagnostics.Debug.WriteLine($"Found {legacyPlaylists.Count} legacy playlists. Migrating to SQLite...");
                                foreach (var playlist in legacyPlaylists)
                                {
                                     // Add playlist to DB
                                     _playlistRepository.AddPlaylist(playlist);
                                     
                                     // Migrate songs
                                     foreach (var songPath in playlist.SongPaths)
                                     {
                                         if (!System.IO.File.Exists(songPath)) continue;
                                         
                                         // Check if song exists in DB by path
                                         var existingSong = _songRepository.GetSongByFilePath(songPath);
                                         if (existingSong == null)
                                         {
                                              // Load metadata from file
                                              var song = _musicProvider.GetSongFromFile(songPath);
                                              if (song != null)
                                              {
                                                  // Restore IsLiked if this is Favorite Tracks
                                                  if (playlist.IsLikedSongs) song.IsLiked = true;
                                                  
                                                  _songRepository.AddSong(song);
                                                  existingSong = song;
                                              }
                                         }
                                         else if (playlist.IsLikedSongs)
                                         {
                                             // Ensure IsLiked is true for existing song
                                             if (!existingSong.IsLiked)
                                             {
                                                 existingSong.IsLiked = true;
                                                 _songRepository.UpdateLikedStatus(existingSong.Id, true);
                                             }
                                         }
                                         
                                         // Link song to playlist (unless Smart)
                                         if (existingSong != null && !playlist.IsSmart)
                                         {
                                            _playlistRepository.AddSongToPlaylist(playlist.Id, existingSong.Id);
                                         }
                                     }
                                }
                                System.Diagnostics.Debug.WriteLine("Migration complete.");
                            }
                        }
                        catch (Exception ex)
                        {
                             System.Diagnostics.Debug.WriteLine("Migration failed: " + ex.Message);
                        }
                    });
                    
                    // Re-fetch from DB after migration
                    playlistsFromDb = await Task.Run(() => _playlistRepository.GetAllPlaylists().ToList());
                }

                // OPTIMIZATION: Load all songs first to ensure shared instances (Flyweight Pattern)
                // This prevents memory duplication and ensures 'Like' status is consistent across playlists
                var allSongs = await Task.Run(() => _songRepository.GetAllSongs().OrderByDescending(s => s.DateAdded).ToList());
                var songLookup = allSongs.ToDictionary(s => s.Id);

                // Prepare playlists in background with shared song instances
                foreach (var playlist in playlistsFromDb)
                {
                    // Get song associations (IDs/basic info) for this playlist
                    // We re-fetch to resolve smart playlist criteria dynamically
                    var playlistRawSongs = await Task.Run(() => _playlistRepository.GetSongsForPlaylist(playlist).ToList());
                    
                    foreach (var rawSong in playlistRawSongs)
                    {
                        // Use the shared instance from main library if available
                        if (songLookup.TryGetValue(rawSong.Id, out var sharedSong))
                        {
                             playlist.Songs.Add(sharedSong);
                        }
                        else
                        {
                            // Fallback: This song exists in playlist query but wasn't in GetAllSongs?
                            // Should be rare, but we add it to lookup to maintain consistency
                            songLookup[rawSong.Id] = rawSong;
                            playlist.Songs.Add(rawSong);
                            
                            // Also need to ensure it ends up in the main list
                            allSongs.Add(rawSong);
                        }
                    }
                }

                // Batch Update UI (Single Dispatcher Invoke)
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // Clear existing (assuming full reload)
                    Songs.Clear();
                    Playlists.Clear();

                    // Add all songs to Library
                    foreach (var song in allSongs)
                    {
                        Songs.Add(song);
                    }

                    // Add playlists
                    foreach (var playlist in playlistsFromDb)
                    {
                        Playlists.Add(playlist);
                    }
                });
                
                // Phase 4: Load history
                await Task.Run(() =>
                {
                    _historyData = _historyService.LoadHistory();
                });
                
                // Refresh on UI thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    RefreshRecentlyPlayedSongs();
                    SongsView?.Refresh();
                    PlaylistsView?.Refresh();
                    UpdateSongCount();
                    
                    // Force re-apply volume setting to ensure AudioService is synced
                    // This fixes the "Loud Start" bug by triggering the Cubic + Headroom logic again
                    var savedVol = _volume;
                    Volume = savedVol;
                    
                    // DEBUG: Check if songs are loaded
                    // MessageBox.Show($"Debug: Loaded {Songs.Count} songs from database.\nPlaylists: {Playlists.Count}\nDB Path: {DesktopMusicPlayer.Services.DatabaseService.GetDatabasePath()}", "Startup Debug");
                    
                    // Check if a file was passed via command-line (file association)
                    if (!string.IsNullOrEmpty(App.StartupFilePath))
                    {
                        PlayFileFromCommandLine(App.StartupFilePath);
                    }
                    
                    // Initialize folder watch for Music folder auto-sync
                    InitializeFolderWatch();

                    // Check for updates silently
                    _ = CheckForUpdatesAsync();
                });
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Plays a file that was passed via command-line (file association).
        /// Adds the file to the library if it doesn't exist, then plays it.
        /// </summary>
        private void PlayFileFromCommandLine(string filePath)
        {
            try
            {
                // Check if file already exists in library
                var existingSong = Songs.FirstOrDefault(s => 
                    s.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
                
                if (existingSong != null)
                {
                    // Already in library - just play it
                    CurrentSong = existingSong;
                    Play();
                    System.Diagnostics.Debug.WriteLine($"Playing existing song: {existingSong.Title}");
                }
                else
                {
                    // Not in library - add it first using MusicProviderService
                    var musicProvider = new MusicProviderService();
                    var newSong = musicProvider.GetSongFromFile(filePath);
                    if (newSong != null)
                    {
                        // Add to database
                        _songRepository.AddSong(newSong);
                        
                        // Add to local collection
                        Songs.Insert(0, newSong);
                        
                        // Add to "My Songs" playlist if it exists
                        var mySongsPlaylist = Playlists.FirstOrDefault(p => p.Name == "My Songs");
                        if (mySongsPlaylist != null)
                        {
                            mySongsPlaylist.Songs.Insert(0, newSong);
                            _playlistRepository.AddSongToPlaylist(mySongsPlaylist.Id, newSong.Id);
                        }
                        
                        // Refresh views
                        SongsView?.Refresh();
                        UpdateSongCount();
                        
                        // Play the new song
                        CurrentSong = newSong;
                        Play();
                        System.Diagnostics.Debug.WriteLine($"Added and playing new song: {newSong.Title}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to play file from command-line: {ex.Message}");
            }
        }

        /// <summary>
        /// Initialize folder watch service and sync Music folder at startup.
        /// </summary>
        private void InitializeFolderWatch()
        {
            try
            {
                _folderWatchService = new FolderWatchService();
                
                // Subscribe to file events
                _folderWatchService.FileAdded += OnFolderFileAdded;
                _folderWatchService.FileDeleted += OnFolderFileDeleted;
                _folderWatchService.FileRenamed += OnFolderFileRenamed;
                
                // Update watched folders collection for UI
                WatchedFolders.Clear();
                foreach (var path in _folderWatchService.WatchPaths)
                {
                    WatchedFolders.Add(path);
                }
                
                // Perform startup sync - add any new files from all watched folders
                var existingPaths = Songs.Select(s => s.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var newFilesCount = 0;
                
                foreach (var filePath in _folderWatchService.ScanFolder())
                {
                    if (!existingPaths.Contains(filePath))
                    {
                        AddSongFromFile(filePath, false); // silent add
                        newFilesCount++;
                    }
                }
                
                if (newFilesCount > 0)
                {
                    SongsView?.Refresh();
                    UpdateSongCount();
                    System.Diagnostics.Debug.WriteLine($"Startup sync: Added {newFilesCount} new songs from watched folders");
                }
                
                // Auto-Organize existing songs into playlists recursively
                // This ensures that even if songs were already in DB, we create/update their playlists
                foreach (var song in Songs.ToList()) // ToList to avoid modification issues if any
                {
                    AssignSongToAutoPlaylist(song);
                }
                PlaylistsView?.Refresh();
                
                // Start watching for changes
                _folderWatchService.StartWatching();
                System.Diagnostics.Debug.WriteLine($"Folder watch started: {string.Join(", ", _folderWatchService.WatchPaths)}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize folder watch: {ex.Message}");
            }
        }

        /// <summary>
        /// Add a new folder to watch for auto-sync.
        /// </summary>
        private void AddWatchedFolder()
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select folder to watch for music files",
                ShowNewFolderButton = false
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var folderPath = dialog.SelectedPath;
                if (!WatchedFolders.Contains(folderPath))
                {
                    _folderWatchService?.AddFolder(folderPath);
                    WatchedFolders.Add(folderPath);
                    
                    // Sync files from new folder
                    var existingPaths = Songs.Select(s => s.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    foreach (var ext in new[] { ".mp3", ".m4a", ".wav", ".flac", ".wma", ".aac", ".ogg" })
                    {
                        foreach (var file in Directory.EnumerateFiles(folderPath, $"*{ext}", SearchOption.AllDirectories))
                        {
                            if (!existingPaths.Contains(file))
                            {
                                AddSongFromFile(file, false);
                            }
                        }
                    }
                    
                    SongsView?.Refresh();
                    UpdateSongCount();
                }
            }
        }

        /// <summary>
        /// Remove a folder from watch list.
        /// </summary>
        private void RemoveWatchedFolder(string? folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return;
            
            _folderWatchService?.RemoveFolder(folderPath);
            WatchedFolders.Remove(folderPath);
        }

        private ToastNotificationService _toastService;
        
        // ... (existing constructor)
        
        private async Task CheckForUpdatesAsync(bool isManual = false)
        {
            if (isManual)
            {
                IsCheckUpdatesPopupOpen = true;
                IsCheckingForUpdates = true;
                // Min delay to show spinner
                await Task.Delay(1000);
            }

            // Reset state
            IsUpdateAvailable = false;
            
            try 
            {
                var info = await _updateService.CheckForUpdatesAsync();
                
                if (isManual) IsCheckingForUpdates = false;

                if (info != null)
                {
                    UpdateVersion = info.Version;
                    UpdateReleaseNotes = info.ReleaseNotes;
                    _updateDownloadUrl = info.DownloadUrl;
                    IsUpdateAvailable = true;
                    
                    // Show Toast if auto-check
                    if (!isManual)
                    {
                        _toastService?.ShowUpdateToast(info.Version, () => 
                        {
                            IsCheckUpdatesPopupOpen = true;
                            // Optionally switch to Settings tab or verify popup visibility logic
                        });
                    }
                }
            }
            catch (Exception)
            {
                if (isManual) IsCheckingForUpdates = false;
                // On error, we just leave IsUpdateAvailable as false -> "Up to date" or maybe add Error state later.
                // For now, failure to check implies no update available.
            }
        }

        private async Task PerformUpdateAsync()
        {
            if (IsUpdating || string.IsNullOrEmpty(_updateDownloadUrl)) return;

            IsUpdating = true;
            UpdateDownloadProgress = 0;

            try
            {
                var progress = new Progress<double>(p => UpdateDownloadProgress = p);
                await _updateService.DownloadInstallerAsync(_updateDownloadUrl, progress);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Update failed: {ex.Message}");
                MessageBox.Show($"Update failed: {ex.Message}", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
                IsUpdating = false;
            }
        }

        /// <summary>
        /// Add a song from file path to the library.
        /// </summary>
        private Song? AddSongFromFile(string filePath, bool playAfterAdd = false)
        {
            try
            {
                // Check if already exists
                var existing = Songs.FirstOrDefault(s => 
                    s.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
                if (existing != null) return existing;
                
                // Create song from file
                var newSong = _musicProvider.GetSongFromFile(filePath);
                if (newSong == null) return null;
                
                // Add to database
                _songRepository.AddSong(newSong);
                
                // Add to local collection
                Songs.Insert(0, newSong);
                
                // Auto-Import Folders as Playlists
                AssignSongToAutoPlaylist(newSong);
                
                if (playAfterAdd)
                {
                    CurrentSong = newSong;
                    Play();
                }
                
                return newSong;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to add song from file: {ex.Message}");
                return null;
            }
        }

        private void OnFolderFileAdded(object? sender, string filePath)
        {
            System.Diagnostics.Debug.WriteLine($"Auto-add from folder: {filePath}");
            var song = AddSongFromFile(filePath);
            if (song != null)
            {
                SongsView?.Refresh();
                UpdateSongCount();
            }
        }
        
        /// <summary>
        /// Automatically adds a song to a playlist based on its folder name.
        /// </summary>
        private void AssignSongToAutoPlaylist(Song song)
        {
            var folderName = GetTopLevelFolderName(song.FilePath, WatchedFolders);
                
            if (!string.IsNullOrEmpty(folderName))
            {
                // Find or Create Playlist
                var playlist = Playlists.FirstOrDefault(p => p.Name.Equals(folderName, StringComparison.OrdinalIgnoreCase));
                
                if (playlist == null)
                {
                    // Create New Playlist
                    int newId = Playlists.Any() ? Playlists.Max(p => p.Id) + 1 : 1;
                    playlist = new Playlist
                    {
                        Id = newId,
                        Name = folderName,
                        IconGlyph = "\uE8D6", // Folder Icon
                        IsLikedSongs = false
                    };
                    
                    Playlists.Add(playlist);
                    _playlistRepository.AddPlaylist(playlist);
                    System.Diagnostics.Debug.WriteLine($"Created new playlist from folder: {folderName}");
                }
                
                // Add song to playlist if not already there
                if (!playlist.Songs.Any(s => s.FilePath == song.FilePath))
                {
                    playlist.Songs.Add(song); // Add to end
                    _playlistRepository.AddSongToPlaylist(playlist.Id, song.Id);
                    System.Diagnostics.Debug.WriteLine($"Auto-added to playlist: {playlist.Name}");
                }
            }
            
            // Legacy/Manual "My Songs" matching
            var pathSegments = song.FilePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var manualPlaylists = Playlists.Where(p => 
                p.Name.Equals("My Songs", StringComparison.OrdinalIgnoreCase) 
            ).ToList();

            foreach (var playlist in manualPlaylists)
            {
                if (!playlist.Songs.Any(s => s.FilePath == song.FilePath))
                {
                    playlist.Songs.Insert(0, song);
                    _playlistRepository.AddSongToPlaylist(playlist.Id, song.Id);
                }
            }
        }

        /// <summary>
        /// Gets the name of the top-level subfolder relative to the watched root.
        /// Returns null if the file is directly in the watched root.
        /// </summary>
        private string? GetTopLevelFolderName(string filePath, IEnumerable<string> watchedFolders)
        {
            foreach (var root in watchedFolders)
            {
                if (filePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                     // Get relative path: e.g. "ClariS\Song.mp3" or "Song.mp3"
                     var relative = Path.GetRelativePath(root, filePath);
                     var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                     
                     // If parts > 1, the first part is the folder name (e.g. "ClariS")
                     // If parts == 1, it's just the file name (e.g. "Song.mp3") -> return null
                     if (parts.Length > 1) 
                     {
                         return parts[0]; 
                     }
                }
            }
            return null;
        }

        private void OnFolderFileDeleted(object? sender, string filePath)
        {
            System.Diagnostics.Debug.WriteLine($"Auto-remove from folder: {filePath}");
            var song = Songs.FirstOrDefault(s => 
                s.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            
            if (song != null)
            {
                // Stop if currently playing
                if (CurrentSong == song)
                {
                    Pause();
                    CurrentSong = null;
                }
                
                // Remove from playlists
                foreach (var playlist in Playlists)
                {
                    if (playlist.Songs.Remove(song))
                    {
                        _playlistRepository.RemoveSongFromPlaylist(playlist.Id, song.Id);
                    }
                }
                
                // Remove from library
                Songs.Remove(song);
                _songRepository.DeleteSong(song.Id);
                
                SongsView?.Refresh();
                UpdateSongCount();
            }
        }

        private void OnFolderFileRenamed(object? sender, (string OldPath, string NewPath) e)
        {
            System.Diagnostics.Debug.WriteLine($"Auto-rename: {e.OldPath} -> {e.NewPath}");
            var song = Songs.FirstOrDefault(s => 
                s.FilePath.Equals(e.OldPath, StringComparison.OrdinalIgnoreCase));
            
            if (song != null)
            {
                // Update file path
                song.FilePath = e.NewPath;
                _songRepository.UpdateSong(song);
                
                // Re-read metadata if title was based on filename
                var newMetadata = _musicProvider.GetSongFromFile(e.NewPath);
                if (newMetadata != null && song.Title == Path.GetFileNameWithoutExtension(e.OldPath))
                {
                    song.Title = newMetadata.Title;
                    song.Artist = newMetadata.Artist;
                    song.Album = newMetadata.Album;
                    _songRepository.UpdateSong(song);
                }
                
                SongsView?.Refresh();
            }
        }

        private void InitializeCollectionView()
        {
            SongsView = CollectionViewSource.GetDefaultView(Songs);
            SongsView.Filter = FilterSongs;
            
            PlaylistsView = CollectionViewSource.GetDefaultView(Playlists);
            PlaylistsView.Filter = FilterPlaylists;
            
            // Set default sort: DateAdded descending (newest first)
            ApplySort("DateAdded", ListSortDirection.Descending);
        }

        private void SortBy(string? propertyName)
        {
            if (string.IsNullOrEmpty(propertyName) || SongsView == null) return;

            // Toggle direction if same property, otherwise default ascending (except DateAdded)
            if (_currentSortProperty == propertyName)
            {
                _sortAscending = !_sortAscending;
            }
            else
            {
                _currentSortProperty = propertyName;
                // DateAdded defaults to descending (newest first), others ascending
                _sortAscending = propertyName != "DateAdded";
            }

            var direction = _sortAscending ? ListSortDirection.Ascending : ListSortDirection.Descending;
            ApplySort(propertyName, direction);
        }

        private void ApplySort(string propertyName, ListSortDirection direction)
        {
            if (SongsView == null) return;
            
            using (SongsView.DeferRefresh())
            {
                SongsView.SortDescriptions.Clear();
                SongsView.SortDescriptions.Add(new SortDescription(propertyName, direction));
            }
            
            // Update Next in Queue to reflect new sort order
            OnPropertyChanged(nameof(UpcomingSongs));
        }

        private bool FilterSongs(object obj)
        {
            if (obj is not Song song) return false;

            // First check playlist filter
            bool passesPlaylistFilter = true;
            if (SelectedPlaylist != null)
            {
                if (SelectedPlaylist.IsLikedSongs)
                {
                    passesPlaylistFilter = song.IsLiked;
                }
                else
                {
                    // Use FilePath comparison to avoid Reference Equality issues
                    passesPlaylistFilter = SelectedPlaylist.Songs.Any(s => s.FilePath.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase));
                }
            }

            if (!passesPlaylistFilter) return false;

            // Then check search filter
            if (string.IsNullOrWhiteSpace(SearchText)) return true;
            
            return song.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || song.Artist.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || song.Album.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        }

        private bool FilterPlaylists(object item)
        {
            if (string.IsNullOrEmpty(LibrarySearchText)) return true;
            
            if (item is Playlist playlist)
            {
                // Simple case-insensitive search
                return playlist.Name != null && 
                       playlist.Name.Contains(LibrarySearchText, StringComparison.OrdinalIgnoreCase);
            }
            
            return false;
        }





        private void LoadHistoryData()
        {
            _historyData = _historyService.LoadHistory();
            RefreshRecentlyPlayedSongs();
        }

        private void RefreshRecentlyPlayedSongs()
        {
            RecentlyPlayedSongs.Clear();
            
            // History is capped at 50 items (see HistoryService.MaxHistoryItems)
            foreach (var entry in _historyData)
            {
                // Find the original song in the library
                var song = Songs.FirstOrDefault(s => 
                    s.FilePath.Equals(entry.FilePath, StringComparison.OrdinalIgnoreCase));
                
                if (song != null)
                {
                    // Reuse the original Song object directly
                    // Note: DateAdded in "Recently Played" view shows play time from _historyData
                    // but we add the original song to avoid memory duplication
                    if (!RecentlyPlayedSongs.Contains(song))
                    {
                        RecentlyPlayedSongs.Add(song);
                    }
                }
            }
        }

        private void ShowRecentlyPlayed()
        {
            SelectedPlaylist = null;
            ContentTitle = "Recently Played";
            
            // Update song count
            SongCountText = $"{RecentlyPlayedSongs.Count} songs";
            
            // Show recently played in main view
            SongsView = CollectionViewSource.GetDefaultView(RecentlyPlayedSongs);
            SongsView.Filter = null; // No filter for recently played
            
            // Sort by DateAdded descending (most recent first)
            ApplySort("DateAdded", ListSortDirection.Descending);
        }

        private void AddToHistory(Song song)
        {
            if (song == null || string.IsNullOrEmpty(song.FilePath)) return;
            
            _historyData = _historyService.AddToHistory(_historyData, song.FilePath);
            RefreshRecentlyPlayedSongs();
        }

        private void ClearHistory()
        {
            _historyData.Clear();
            _historyService.SaveHistory(_historyData);
            RecentlyPlayedSongs.Clear();
            
            // Refresh view if currently showing Recently Played
            if (ContentTitle == "Recently Played")
            {
                SongCountText = "0 songs";
                SongsView?.Refresh();
            }
        }

        private void SavePlaylists()
        {
            // Save playlists to both SQLite and JSON (for backwards compatibility)
            foreach (var playlist in Playlists)
            {
                if (playlist.Id > 0)
                {
                    _playlistRepository.UpdatePlaylist(playlist);
                }
                else
                {
                    _playlistRepository.AddPlaylist(playlist);
                }
            }
            
            // Also save to JSON as backup
            _playlistService.SavePlaylists(Playlists);
        }
        
        /// <summary>
        /// Save a song to the SQLite database and optionally to a playlist.
        /// </summary>
        private void SaveSongToDatabase(Song song, Playlist? playlist = null)
        {
            // Check if song already exists in database
            if (!_songRepository.SongExists(song.FilePath))
            {
                _songRepository.AddSong(song);
            }
            
            // Add to playlist if specified
            if (playlist != null && song.Id > 0)
            {
                _playlistRepository.AddSongToPlaylist(playlist.Id, song.Id);
            }
        }

        private void AddNewPlaylist()
        {
            // Show folder browser dialog to select music folder
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select a folder containing MP3 files",
                ShowNewFolderButton = false,
                UseDescriptionForTitle = true
            };
            
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var folderPath = dialog.SelectedPath;
                var folderName = System.IO.Path.GetFileName(folderPath);
                
                // Scan for MP3 files in the folder
                var mp3Files = System.IO.Directory.GetFiles(folderPath, "*.mp3", System.IO.SearchOption.AllDirectories);
                
                if (mp3Files.Length == 0)
                {
                    System.Windows.MessageBox.Show("No MP3 files found in the selected folder.", "No Music Found", 
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    return;
                }
                
                try
                {
                    // Create new playlist with folder name
                    var newPlaylist = new Playlist
                    {
                        Name = folderName,
                        IconGlyph = "\uE8D6"
                    };
                    
                    // Save playlist to database first to get the ID
                    var playlistId = _playlistRepository.AddPlaylist(newPlaylist);
                    newPlaylist.Id = playlistId;

                    // Add songs to playlist
                    foreach (var filePath in mp3Files)
                    {
                        try 
                        {
                            // Check if song already exists in library
                            var existingSong = Songs.FirstOrDefault(s => s.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
                            
                            if (existingSong != null)
                            {
                                // Use existing song
                                newPlaylist.Songs.Add(existingSong);
                                newPlaylist.SongPaths.Add(filePath);
                                
                                // Add to junction table
                                if (existingSong.Id > 0)
                                {
                                    _playlistRepository.AddSongToPlaylist(newPlaylist.Id, existingSong.Id);
                                }
                            }
                            else
                            {
                                // Create new song from file
                                var song = _musicProvider.GetSongFromFile(filePath);
                                if (song != null)
                                {
                                    // Save song to database first
                                    var songId = _songRepository.AddSong(song);
                                    song.Id = songId;
                                    
                                    Songs.Add(song);
                                    newPlaylist.Songs.Add(song);
                                    newPlaylist.SongPaths.Add(filePath);
                                    
                                    // Add to junction table
                                    _playlistRepository.AddSongToPlaylist(newPlaylist.Id, song.Id);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error adding song {filePath}: {ex.Message}");
                        }
                    }
                    
                    // Add playlist to UI and save JSON backup
                    Playlists.Add(newPlaylist);
                    _playlistService.SavePlaylists(Playlists);
                    
                    // Select the new playlist
                    SelectedPlaylist = newPlaylist;
                    
                    MessageBox.Show($"Successfully added playlist '{folderName}' with {newPlaylist.Songs.Count} songs.\nSaved to DB.", "Success");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving to database: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void AddMp3Files()
        {
            // Show file picker dialog for MP3 files
            using var dialog = new System.Windows.Forms.OpenFileDialog
            {
                Title = "Select MP3 Files",
                Filter = "MP3 Files (*.mp3)|*.mp3",
                Multiselect = true
            };
            
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var filePaths = dialog.FileNames;
                
                if (filePaths.Length == 0) return;
                
                // Get or create a "My Songs" playlist for loose files
                var mySongsPlaylist = Playlists.FirstOrDefault(p => p.Name == "My Songs" && !p.IsLikedSongs);
                
                if (mySongsPlaylist == null)
                {
                    mySongsPlaylist = new Playlist
                    {
                        Name = "My Songs",
                        IconGlyph = "\uE8D6"
                    };
                    // Save playlist to database first to get proper ID
                    _playlistRepository.AddPlaylist(mySongsPlaylist);
                    Playlists.Add(mySongsPlaylist);
                }
                
                int addedCount = 0;
                foreach (var filePath in filePaths)
                {
                    // Check if song already exists in library
                    var existingSong = Songs.FirstOrDefault(s => s.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
                    
                    if (existingSong == null)
                    {
                        try
                        {
                            // Add new song
                            var song = _musicProvider.GetSongFromFile(filePath);
                            if (song != null)
                            {
                                // Save song to database first to get proper ID
                                _songRepository.AddSong(song);
                                
                                Songs.Add(song);
                                mySongsPlaylist.Songs.Add(song);
                                mySongsPlaylist.SongPaths.Add(filePath);
                                
                                // Add to junction table for playlist
                                if (mySongsPlaylist.Id > 0 && song.Id > 0)
                                {
                                    _playlistRepository.AddSongToPlaylist(mySongsPlaylist.Id, song.Id);
                                }
                                addedCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to import file '{filePath}': {ex.Message}");
                        }
                    }
                    else if (!mySongsPlaylist.Songs.Contains(existingSong))
                    {
                        // Song exists, just add to playlist if not already there
                        mySongsPlaylist.Songs.Add(existingSong);
                        mySongsPlaylist.SongPaths.Add(filePath);
                        
                        // Add to junction table
                        if (mySongsPlaylist.Id > 0 && existingSong.Id > 0)
                        {
                            _playlistRepository.AddSongToPlaylist(mySongsPlaylist.Id, existingSong.Id);
                        }
                        addedCount++;
                    }
                }
                
                SavePlaylists();
                
                // Select the playlist
                SelectedPlaylist = mySongsPlaylist;
                
                System.Diagnostics.Debug.WriteLine($"Added {addedCount} MP3 files to 'My Songs'");
            }
        }

        private void DeleteSelectedPlaylist(object? parameter = null)
        {
            // Get playlist from parameter or fall back to SelectedPlaylist
            var playlistToDelete = parameter as Playlist ?? SelectedPlaylist;
            
            if (playlistToDelete == null) return;
            
            // Protect Liked Songs from deletion
            if (playlistToDelete.IsLikedSongs)
            {
                System.Diagnostics.Debug.WriteLine("Cannot delete Liked Songs playlist");
                return;
            }
            
            // Get file paths of songs to remove (orphan songs)
            var songPathsToRemove = new List<string>();
            foreach (var song in playlistToDelete.Songs)
            {
                // Check if this song is used by any OTHER playlist (excluding Liked Songs)
                bool isUsedElsewhere = Playlists
                    .Where(p => p != playlistToDelete && !p.IsLikedSongs)
                    .Any(p => p.Songs.Any(s => s.FilePath.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase)));
                
                if (!isUsedElsewhere)
                {
                    songPathsToRemove.Add(song.FilePath);
                }
            }
            
            // Remove from collection
            Playlists.Remove(playlistToDelete);
            
            // Delete from SQLite database
            if (playlistToDelete.Id > 0)
            {
                _playlistRepository.DeletePlaylist(playlistToDelete.Id);
            }
            
            // Remove orphan songs from global library and database
            // Use the centralized method to ensure all cleanups (Favorites, Queue, DB, Playback) happen correctly
            var songsToRemove = Songs.Where(s => songPathsToRemove.Contains(s.FilePath)).ToList();
            foreach (var song in songsToRemove)
            {
                RemoveFromLibrary(song); 
            }
            

            
            System.Diagnostics.Debug.WriteLine($"Deleted playlist '{playlistToDelete.Name}'. Removed {songsToRemove.Count} songs from library.");
        }

        private void OpenRenamePopup(object? parameter = null)
        {
            // Get playlist from parameter or fall back to SelectedPlaylist
            var playlistToRename = parameter as Playlist ?? SelectedPlaylist;
            
            if (playlistToRename == null) return;
            
            // Protect Liked Songs from renaming
            if (playlistToRename.IsLikedSongs)
            {
                System.Diagnostics.Debug.WriteLine("Cannot rename Liked Songs playlist");
                return;
            }
            
            // Select the playlist being renamed
            SelectedPlaylist = playlistToRename;
            
            // Set initial text to current name and open popup
            RenameText = playlistToRename.Name;
            IsRenamePopupOpen = true;
        }

        private void ConfirmRename()
        {
            if (SelectedPlaylist == null || string.IsNullOrWhiteSpace(RenameText))
            {
                IsRenamePopupOpen = false;
                return;
            }
            
            // Apply the new name
            SelectedPlaylist.Name = RenameText.Trim();
            ContentTitle = SelectedPlaylist.Name;
            
            // Save to database to persist the rename
            _playlistRepository.UpdatePlaylist(SelectedPlaylist);
            
            // Close popup
            IsRenamePopupOpen = false;
            RenameText = string.Empty;
        }

        private void CancelRename()
        {
            IsRenamePopupOpen = false;
            RenameText = string.Empty;
        }

        private void GoHome()
        {
            SelectedPlaylist = null;
            ContentTitle = "All Tracks";
            
            // Re-initialize SongsView with Songs collection (in case it was changed to RecentlyPlayedSongs)
            SongsView = CollectionViewSource.GetDefaultView(Songs);
            SongsView.Filter = FilterSongs;
            SongsView.Refresh();
            
            UpdateSongCount();
        }

        private void UpdateContentView()
        {
            // Re-initialize SongsView with Songs collection (in case it was changed to RecentlyPlayedSongs)
            SongsView = CollectionViewSource.GetDefaultView(Songs);
            SongsView.Filter = FilterSongs;
            
            if (SelectedPlaylist == null)
            {
                // Show all songs
                ContentTitle = "All Tracks";
            }
            else if (SelectedPlaylist.IsLikedSongs)
            {
                // Show only liked songs
                ContentTitle = "Favorite Tracks";
            }
            else
            {
                // Show playlist songs
                ContentTitle = SelectedPlaylist.Name;
            }
            
            SongsView.Refresh();
            UpdateSongCount();
        }

        private void UpdateSongCount()
        {
            if (SongsView != null)
            {
                var count = SongsView.Cast<Song>().Count();
                SongCountText = count == 1 ? "1 song" : $"{count} songs";
            }
        }



        private async Task LoadSongsFromPathAsync(string folderPath)
        {
            // Clear existing songs
            Songs.Clear();
            CurrentFolderPath = folderPath;

            var songs = await _musicProvider.ScanFolderAsync(folderPath);
            foreach (var song in songs)
            {
                Songs.Add(song);
            }

            // If no songs found, show message
            if (Songs.Count == 0)
            {
                LoadDummyData();
            }
            else
            {
                // Do not auto-select song on startup
                CurrentSong = null;
            }

            // Refresh the view
            SongsView?.Refresh();
        }

        private async void OpenFolder()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Music Folder",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                var folderPath = dialog.FolderName;
                var folderName = Path.GetFileName(folderPath);
                
                // Check if playlist with this folder name already exists
                var existingPlaylist = Playlists.FirstOrDefault(p => 
                    p.Name.Equals(folderName, StringComparison.OrdinalIgnoreCase) && !p.IsLikedSongs);
                
                if (existingPlaylist == null)
                {
                    // Create new playlist with folder name
                    var newPlaylist = new Playlist
                    {
                        Name = folderName,
                        IconGlyph = "\uE8B7" // Folder icon
                    };
                    
                    // Save to database first
                    var playlistId = _playlistRepository.AddPlaylist(newPlaylist);
                    newPlaylist.Id = playlistId;

                    Playlists.Add(newPlaylist);
                    
                    // Load songs and associate with playlist
                    await LoadSongsFromPathToPlaylist(folderPath, newPlaylist);
                    

                    SelectedPlaylist = newPlaylist;
                }
                else
                {
                    // Update existing playlist
                    await LoadSongsFromPathToPlaylist(folderPath, existingPlaylist);

                    SelectedPlaylist = existingPlaylist;
                }
                
                SettingsService.SaveMusicFolder(folderPath);
                
                // Auto-watch the opened folder
                if (_folderWatchService != null && !WatchedFolders.Contains(folderPath))
                {
                    _folderWatchService.AddFolder(folderPath);
                    WatchedFolders.Add(folderPath);
                }
                
                System.Diagnostics.Debug.WriteLine($"Successfully added/updated playlist '{folderName}' from folder.");
            }
        }

        private async Task LoadSongsFromPathToPlaylist(string folderPath, Playlist playlist)
        {
            // Load songs from folder asynchronously
            var songs = await _musicProvider.ScanFolderAsync(folderPath);
            
            // UI updates must happen on UI thread (WPF usually handles ObservableCollection updates from async contexts automatically if binding is set up right, 
            // but for safety with large batch updates, we can just add them directly since we are back on the UI context after await)
            
            foreach (var song in songs)
            {
                // Check if song exists in DB first (by FilePath)
                var existingSongInDb = _songRepository.GetSongByFilePath(song.FilePath);

                // Add to main library if not exists
                if (existingSongInDb == null)
                {
                    // Save to DB
                    var songId = _songRepository.AddSong(song);
                    song.Id = songId;
                    
                    // Add to Memory Collection
                    if (!Songs.Any(s => s.FilePath.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase)))
                    {
                        Songs.Add(song);
                    }
                    existingSongInDb = song;
                }
                else
                {
                     // Ensure logic uses the DB version if available (has correct ID)
                     if (!Songs.Any(s => s.FilePath.Equals(existingSongInDb.FilePath, StringComparison.OrdinalIgnoreCase)))
                     {
                         Songs.Add(existingSongInDb);
                     }
                }
                
                // Add to playlist association
                var finalSong = existingSongInDb; // Use the one with valid ID
                if (finalSong != null) 
                {
                    // Check if memory playlist has it
                     if (!playlist.Songs.Any(s => s.FilePath.Equals(finalSong.FilePath, StringComparison.OrdinalIgnoreCase)))
                     {
                         playlist.Songs.Add(finalSong);
                         playlist.SongPaths.Add(finalSong.FilePath);
                         
                         // Save association to DB
                         if (playlist.Id > 0 && finalSong.Id > 0)
                         {
                             _playlistRepository.AddSongToPlaylist(playlist.Id, finalSong.Id);
                         }
                     }
                }
            }
            
            SongsView?.Refresh();
            UpdateSongCount();
        }

        private void ToggleLike(Song? song = null)
        {
            var targetSong = song ?? CurrentSong;
            if (targetSong == null) return;
            
            targetSong.IsLiked = !targetSong.IsLiked;
            
            // Sync to SQLite immediately
            if (targetSong.Id > 0)
            {
                _songRepository.UpdateLikedStatus(targetSong.Id, targetSong.IsLiked);
            }
            
            // Update Favorite Tracks playlist in memory
            var favoritePlaylist = Playlists.FirstOrDefault(p => p.IsLikedSongs);
            if (favoritePlaylist != null)
            {
                if (targetSong.IsLiked)
                {
                    // Add if not exists
                    if (!favoritePlaylist.Songs.Any(s => s.FilePath.Equals(targetSong.FilePath, StringComparison.OrdinalIgnoreCase)))
                    {
                        favoritePlaylist.Songs.Add(targetSong);
                    }
                }
                else
                {
                    // Remove if exists
                    var existing = favoritePlaylist.Songs.FirstOrDefault(s => s.FilePath.Equals(targetSong.FilePath, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        favoritePlaylist.Songs.Remove(existing);
                    }
                }
            }
            
            // Refresh view if showing
            SongsView?.Refresh();
            
            // Update UI binding for Love button
            if (targetSong == CurrentSong)
            {
                OnPropertyChanged(nameof(IsCurrentSongLiked));
            }
            
            // Notify empty state change
            OnPropertyChanged(nameof(IsFavoritesEmpty));
        }



        private void RemoveFromLibrary(Song? song)
{
    if (song == null) return;
    
    // If removing current song, stop playback and clear current song
    if (CurrentSong == song)
    {
        _audioService.Stop();
        IsPlaying = false;
        _progressTimer.Stop();
        CurrentSong = null;  // Always show "No Song" when currently playing song is removed
    }
    
    // Remove from Songs collection
    Songs.Remove(song);
    
    // Remove from queue if present
    var queueItem = PlayQueue.FirstOrDefault(s => s.FilePath.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase));
    if (queueItem != null)
    {
        PlayQueue.Remove(queueItem);
        OnPropertyChanged(nameof(UpcomingSongs));
    }
    
    // Remove from database (with error handling)
    if (song.Id > 0)
    {
        try
        {
            _songRepository.DeleteSong(song.Id);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to delete song from DB: {ex.Message}");
            // Continue execution to ensure UI is updated even if DB fails
        }
    }
    
    // Remove from all playlists' Songs collections (triggers SongCountText update)
    // We execute this regardless of DB success to ensure UI consistency
    foreach (var playlist in Playlists)
    {
        // Special aggressive check for Favorite Tracks (Smart + LikedSongs)
        // We check this REGARDLESS of song.IsLiked state to ensure consistency
        if (playlist.IsSmart && playlist.IsLikedSongs)
        {
             // Try to find and remove by path
             var favoriteSong = playlist.Songs.FirstOrDefault(s => s.FilePath.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase));
             if (favoriteSong != null)
             {
                 playlist.Songs.Remove(favoriteSong);
             }
             // Optional: If not found, we could force refresh, but removing by path is usually sufficient 
             // and safer against race conditions where the song might not be in DB yet.
        }
        else
        {
            // For regular playlists
            var playlistSong = playlist.Songs.FirstOrDefault(s => s.FilePath.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase));
            if (playlistSong != null)
            {
                playlist.Songs.Remove(playlistSong);
            }
        }
    }
    
    // Re-number songs
    for (int i = 0; i < Songs.Count; i++)
    {
        Songs[i].Id = i + 1;
    }
    
    // Refresh view
    SongsView?.Refresh();
}
        private void AddToPlaylist(Song? song)
        {
            if (song == null) return;
            
            // Legacy method - just mark as liked
            song.IsLiked = true;
            System.Diagnostics.Debug.WriteLine($"Added '{song.Title}' to liked songs");
        }

        /// <summary>
        /// Add a song to a specific playlist (called from context menu)
        /// </summary>
        private void AddSongToPlaylist(object? parameter)
        {
            // Parameter is Tuple<Song, Playlist> from MultiValueConverter
            if (parameter is Tuple<object, object> tuple)
            {
                var song = tuple.Item1 as Song;
                var playlist = tuple.Item2 as Playlist;
                
                if (song == null || playlist == null) return;
                
                // Don't add duplicates
                if (playlist.Songs.Contains(song) || playlist.SongPaths.Contains(song.FilePath))
                {
                    System.Diagnostics.Debug.WriteLine($"'{song.Title}' is already in '{playlist.Name}'");
                    return;
                }
                
                // Add to playlist
                playlist.Songs.Add(song);
                playlist.SongPaths.Add(song.FilePath);
                
                // If adding to Liked Songs, also mark song as liked
                if (playlist.IsLikedSongs)
                {
                    song.IsLiked = true;
                }
                
                // Save changes to JSON

                
                // Refresh view if this playlist is selected
                if (SelectedPlaylist == playlist)
                {
                    SongsView?.Refresh();
                }
                
                System.Diagnostics.Debug.WriteLine($"Added '{song.Title}' to '{playlist.Name}'");
            }
        }

        private void AddToQueue(Song? song)
        {
            if (song == null) return;
            
            // Don't add duplicates
            if (!PlayQueue.Contains(song))
            {
                PlayQueue.Add(song);
                OnPropertyChanged(nameof(UpcomingSongs));
                System.Diagnostics.Debug.WriteLine($"Added '{song.Title}' to queue. Queue size: {PlayQueue.Count}");
            }
        }

        private void RemoveFromQueue(Song? song)
        {
            if (song == null) return;
            
            PlayQueue.Remove(song);
            OnPropertyChanged(nameof(UpcomingSongs));
            System.Diagnostics.Debug.WriteLine($"Removed '{song.Title}' from queue. Queue size: {PlayQueue.Count}");
        }

        private void LoadDummyData()
        {
            Songs.Add(new Song 
            { 
                Id = 1, 
                Title = "Click '+' to add music folder", 
                Artist = "No songs found", 
                Album = "Select a folder with MP3 files", 
                Duration = TimeSpan.Zero 
            });
        }

        // Public method for drag & drop
        public void AddSongs(string[] filePaths)
        {
            if (filePaths == null || filePaths.Length == 0) return;

            int addedCount = 0;
            foreach (var filePath in filePaths)
            {
                // Validate MP3 extension
                if (!filePath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Check if file exists
                if (!File.Exists(filePath))
                    continue;

                // Check if already in library (by file path)
                if (Songs.Any(s => s.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
                    continue;

                try
                {
                    // Use MusicProviderService to read metadata
                    var song = _musicProvider.GetSongFromFile(filePath);
                    if (song != null)
                    {
                        song.Id = Songs.Count + 1;
                        Songs.Add(song);
                        addedCount++;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error adding file {filePath}: {ex.Message}");
                }
            }

            // Refresh view and update count
            SongsView?.Refresh();
            UpdateSongCount();

            System.Diagnostics.Debug.WriteLine($"Added {addedCount} songs via drag & drop");
        }

        private void LoadCurrentSong()
        {
            if (CurrentSong == null || string.IsNullOrEmpty(CurrentSong.FilePath)) return;
            
            if (!File.Exists(CurrentSong.FilePath)) return;

            // Save current playback state before loading new file
            bool wasPlaying = IsPlaying;

            _audioService.LoadFile(CurrentSong.FilePath);
            
            // Sync volume from ViewModel using safe logic
            _audioService.Volume = (float)Math.Pow(_volume, 3.0) * 0.5f;
            
            TotalDuration = _audioService.Duration.TotalSeconds;
    TotalTimeFormatted = _audioService.Duration.ToString(@"m\:ss");
    CurrentProgress = 0;
    CurrentTimeFormatted = "0:00";

    // Auto-fix Duration Metadata:
    // If actual audio duration differs significantly from metadata (e.g. VBR header issue or truncated file),
    // update the database so the UI shows the correct length next time.
    if (TotalDuration > 0 && Math.Abs(TotalDuration - CurrentSong.Duration.TotalSeconds) > 2)
    {
        CurrentSong.Duration = TimeSpan.FromSeconds(TotalDuration);
        // Fire and forget update to avoid blocking UI
        System.Threading.Tasks.Task.Run(() => _songRepository.UpdateSong(CurrentSong));
        System.Diagnostics.Debug.WriteLine($"Auto-corrected duration for '{CurrentSong.Title}' to {CurrentSong.Duration}");
    }

    // Resume playback if was playing before
            if (wasPlaying)
            {
                _audioService.Play();
                IsPlaying = true;
                _progressTimer.Start();
            }
        }

        private void PlaySelectedSong(Song? song)
        {
            if (song == null) return;
            CurrentSong = song;
            LoadCurrentSong();
            Play();
            
            // Add to recently played history
            AddToHistory(song);
        }

        private void Play()
        {
            if (CurrentSong == null) return;
            
            // Load file if not loaded yet
            if (string.IsNullOrEmpty(CurrentSong.FilePath) || !File.Exists(CurrentSong.FilePath))
            {
                MessageBox.Show($"File tidak ditemukan:\n{CurrentSong.FilePath}", 
                    "File Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check if audio file is loaded (Duration > 0 means loaded)
            if (_audioService.Duration.TotalSeconds <= 0)
            {
                // Load the audio file first
                _audioService.LoadFile(CurrentSong.FilePath);
                
                // Check if load was successful
                if (_audioService.Duration.TotalSeconds <= 0)
                {
                    MessageBox.Show($"Gagal memutar file:\n{CurrentSong.Title}\n\nFile mungkin rusak atau format tidak didukung.", 
                        "Playback Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    
                    // Skip to next song if available
                    if (Songs.Count > 1)
                    {
                        Next();
                    }
                    return;
                }
                
                TotalDuration = _audioService.Duration.TotalSeconds;
                TotalTimeFormatted = _audioService.Duration.ToString(@"m\:ss");
                CurrentProgress = 0;
                CurrentTimeFormatted = "0:00";
            }

            _audioService.Play();
            IsPlaying = true;
            _progressTimer.Start();
            
            // Increment play count in database
            if (CurrentSong.Id > 0)
            {
                _songRepository.IncrementPlayCount(CurrentSong.Id);
            }
        }

        private void Pause()
        {
            _audioService.Pause();
            IsPlaying = false;
            _progressTimer.Stop();
        }

        private void Stop()
        {
            _audioService.Stop();
            IsPlaying = false;
            _progressTimer.Stop();
            CurrentProgress = 0;
            CurrentTimeFormatted = "0:00";
        }

        private void PlayPause()
        {
            if (IsPlaying)
            {
                Pause();
            }
            else
            {
                // If no song is loaded, select and play the first song
                if (CurrentSong == null)
                {
                    // Try to get first visible song from SongsView
                    Song? firstSong = null;
                    if (SongsView != null)
                    {
                        foreach (var item in SongsView)
                        {
                            if (item is Song song)
                            {
                                firstSong = song;
                                break;
                            }
                        }
                    }
                    
                    // Fall back to Songs collection
                    firstSong ??= Songs.FirstOrDefault();
                    
                    if (firstSong != null)
                    {
                        PlaySelectedSong(firstSong);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("No songs available to play");
                    }
                }
                else
                {
                    Play();
                }
            }
        }

        private void Next()
        {
            // Check if queue has items - play from queue first
            if (PlayQueue.Count > 0)
            {
                var nextSong = PlayQueue[0];
                PlayQueue.RemoveAt(0);
                OnPropertyChanged(nameof(UpcomingSongs));
                CurrentSong = nextSong;
                LoadCurrentSong();
                
                if (IsPlaying)
                {
                    Play();
                }
                return;
            }
            
            // Normal playback logic - use displayed order (SongsView) for sequential playback
            if (CurrentSong == null) return;
            
            // Get the displayed list (respects sorting)
            // Fallback to full Songs collection if current view is empty (e.g., viewing empty folder/favorites)
            var displayedSongs = SongsView?.Cast<Song>().ToList() ?? new List<Song>();
            if (displayedSongs.Count == 0)
            {
                // Current view is empty but song is playing - fallback to all songs
                displayedSongs = Songs.ToList();
            }
            if (displayedSongs.Count == 0) return;
            
            var currentIndex = displayedSongs.IndexOf(CurrentSong);
            int nextIndex;

            if (IsShuffleEnabled)
            {
                var random = new Random();
                nextIndex = random.Next(displayedSongs.Count);
            }
            else
            {
                nextIndex = (currentIndex + 1) % displayedSongs.Count;
            }

            CurrentSong = displayedSongs[nextIndex];
            LoadCurrentSong();
            
            if (IsPlaying)
            {
                Play();
            }
        }

        private void Previous()
        {
            if (CurrentSong == null) return;
            
            // If more than 3 seconds into song, restart it
            if (_audioService.Position.TotalSeconds > 3)
            {
                _audioService.Position = TimeSpan.Zero;
                CurrentProgress = 0;
                return;
            }

            // Get the displayed list (respects sorting)
            // Fallback to full Songs collection if current view is empty
            var displayedSongs = SongsView?.Cast<Song>().ToList() ?? new List<Song>();
            if (displayedSongs.Count == 0)
            {
                displayedSongs = Songs.ToList();
            }
            if (displayedSongs.Count == 0) return;
            
            var currentIndex = displayedSongs.IndexOf(CurrentSong);
            var prevIndex = currentIndex > 0 ? currentIndex - 1 : displayedSongs.Count - 1;
            CurrentSong = displayedSongs[prevIndex];
            LoadCurrentSong();
            
            if (IsPlaying)
            {
                Play();
            }
        }

        private void EndSeek()
        {
            // Set IsDragging to false first
            IsDragging = false;
            // Seek to the current slider position
            _audioService.Position = TimeSpan.FromSeconds(CurrentProgress);
        }

        private void OnProgressTimerTick(object? sender, EventArgs e)
        {
            // Only update from audio if NOT dragging
            if (!IsDragging && _audioService.IsPlaying)
            {
                try
                {
                    _isProgressUpdateFromTimer = true;
                    CurrentProgress = _audioService.Position.TotalSeconds;
                }
                finally
                {
                    _isProgressUpdateFromTimer = false;
                }
                
                CurrentTimeFormatted = _audioService.Position.ToString(@"m\:ss");
                TotalDuration = _audioService.Duration.TotalSeconds;
                TotalTimeFormatted = _audioService.Duration.ToString(@"m\:ss");
            }
        }

        private void OnPlaybackStopped(object? sender, bool reachedEnd)
        {
            if (reachedEnd)
            {
                // Song finished naturally
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    switch (RepeatMode)
                    {
                        case PlaybackRepeatMode.RepeatOne:
                            // Repeats the same song - properly restart all playback state
                            _audioService.Position = TimeSpan.Zero;
                            _audioService.Play();
                            IsPlaying = true;
                            _progressTimer.Start();
                            System.Diagnostics.Debug.WriteLine("RepeatOne: Restarting song");
                            break;

                        case PlaybackRepeatMode.RepeatAll:
                            // If at the end of playlist,Next() handles wrapping logic if we modify it, 
                            // OR we can explicitly handle it here. 
                            // Let's rely on Next() but ensuring it knows to wrap.
                            // Actually, my Previous logic for Next() already handles wrapping IF it's not shuffle.
                            // But if it's RepeatAll, we WANT to loop back to 0 even if it was going to stop.
                            
                            // Let's look at Next() logic. If I call Next() it should handle it.
                            // BUT, original Next() might stop if it's the last song (wait, I modified it earlier to loop).
                            // Earlier I modified UpcomingSongs to loop. Let's check Next() implementation.
                            Next(); // Helper param to indicate auto-advance? No, Next() takes no params.
                            break;

                        case PlaybackRepeatMode.Off:
                        default:
                            // Verify if we are at the last song.
                            // If we are at the last song AND Repeat is Off, we should STOP.
                            // My previous Next() logic loops. I need to modify Next() or handle it here.
                            
                             if (SongsView != null && CurrentSong != null)
                             {
                                 var songList = SongsView.Cast<Song>().ToList();
                                 var currentIndex = songList.IndexOf(CurrentSong);
                                 if (currentIndex >= songList.Count - 1)
                                 {
                                     // Last song and Repeat Off -> Stop
                                     IsPlaying = false;
                                     _progressTimer.Stop();
                                     _audioService.Stop();
                                     // Reset position to start? Maybe.
                                     return;
                                 }
                             }
                            Next();
                            break;
                    }
                });
            }
            else
            {
                // Stopped manually
                IsPlaying = false;
                _progressTimer.Stop();
            }
        }

        private void SeekForward()
        {
            if (TotalDuration <= 0) return;
            
            var newPosition = CurrentProgress + 5; // +5 seconds
            if (newPosition > TotalDuration)
            {
                newPosition = TotalDuration;
            }
            
            CurrentProgress = newPosition;
            _audioService.Position = TimeSpan.FromSeconds(newPosition);
        }

        private void SeekRewind()
        {
            var newPosition = CurrentProgress - 5; // -5 seconds
            if (newPosition < 0)
            {
                newPosition = 0;
            }
            
            CurrentProgress = newPosition;
            _audioService.Position = TimeSpan.FromSeconds(newPosition);
        }


        public enum ThemeType
        {
            Dark,
            Gradient,
            Light
        }

        private ThemeType _currentTheme = ThemeType.Gradient;
        private bool _isThemeChanging = false;
        public ThemeType CurrentTheme
        {
            get => _currentTheme;
            set => SetProperty(ref _currentTheme, value);
        }

        // Helper boolean for UI binding
        public bool IsDarkTheme => CurrentTheme != ThemeType.Light;


        // ...

        private async void ToggleTheme()
        {
            if (_isThemeChanging) return;
            _isThemeChanging = true;

            try
            {
                var window = Application.Current.MainWindow;
                if (window == null)
                {
                    ApplyThemeChange();
                    return;
                }

                // Smooth opacity transition to prevent visual shock
                const double targetOpacity = 0.5;
                const int animationDurationMs = 150; // Slightly slower for smoother feel
                const int steps = 10;
                double originalOpacity = window.Opacity;
                double stepSize = (originalOpacity - targetOpacity) / steps;
                int stepDelay = animationDurationMs / steps;

                // Fade out
                for (int i = 0; i < steps; i++)
                {
                    window.Opacity -= stepSize;
                    await System.Threading.Tasks.Task.Delay(stepDelay);
                }
                window.Opacity = targetOpacity;

                // Cycle: Gradient -> Dark -> Light -> Gradient
                switch (CurrentTheme)
                {
                    case ThemeType.Gradient:
                        CurrentTheme = ThemeType.Dark;
                        break;
                    case ThemeType.Dark:
                        CurrentTheme = ThemeType.Light;
                        break;
                    case ThemeType.Light:
                        CurrentTheme = ThemeType.Gradient;
                        break;
                }

                // Apply theme change
                ApplyThemeChange();

                // Fade back in
                for (int i = 0; i < steps; i++)
                {
                    window.Opacity += stepSize;
                    await System.Threading.Tasks.Task.Delay(stepDelay);
                }
                window.Opacity = 1.0;
            }
            finally
            {
                _isThemeChanging = false;
            }
        }

        private void ApplyThemeChange()
        {


            Uri themeUri;
            switch (CurrentTheme)
            {
                case ThemeType.Gradient:
                    themeUri = new Uri("Themes/GradientTheme.xaml", UriKind.Relative);
                    break;
                case ThemeType.Light:
                    themeUri = new Uri("Themes/LightTheme.xaml", UriKind.Relative);
                    break;
                case ThemeType.Dark:
                default:
                    themeUri = new Uri("Themes/DarkTheme.xaml", UriKind.Relative);
                    break;
            }
            
            if (Application.Current is App app)
            {
                app.ChangeTheme(themeUri);
            }

            // Notify legacy property just in case
            OnPropertyChanged(nameof(IsDarkTheme));
        }

        private void ShowKeyboardShortcuts()
        {
            MessageBox.Show("Keyboard Shortcuts coming soon!", "Shortcuts", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ShowAbout()
        {
            MessageBox.Show("Crescendo\nVersion 1.0\n\nA beautiful music player created with WPF and .NET 8.", "About Crescendo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // IDisposable implementation
        private bool _disposed = false;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Stop and dispose audio
            _progressTimer?.Stop();
            _audioService?.Dispose();
            _folderWatchService?.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
