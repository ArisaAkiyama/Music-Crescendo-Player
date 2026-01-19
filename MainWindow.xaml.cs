using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using DesktopMusicPlayer.Services;
using DesktopMusicPlayer.ViewModels;
using Application = System.Windows.Application;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using DragEventArgs = System.Windows.DragEventArgs;
using Button = System.Windows.Controls.Button;
using Point = System.Windows.Point;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using TextBox = System.Windows.Controls.TextBox;

namespace DesktopMusicPlayer;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MediaControlService _mediaControlService;
    private SmtcService? _smtcService;
    private SystemTrayService? _systemTrayService;
    
    public MainWindow()
    {
        InitializeComponent();
        
        // Subscribe to theme switching event for animation
        if (Application.Current is App app)
        {
            app.ThemeChanging += OnThemeChanging;
        }
        
        // Handle maximize to respect taskbar
        MaxHeight = SystemParameters.MaximizedPrimaryScreenHeight;
        MaxWidth = SystemParameters.MaximizedPrimaryScreenWidth;
        
        // Register global keyboard shortcuts
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        
        // Initialize media control service for Bluetooth headset / multimedia keyboard support
        _mediaControlService = new MediaControlService();
        
        // Wire up ViewModel actions after DataContext is set
        Loaded += (s, e) =>
        {
            if (DataContext is MainViewModel vm)
            {
                // Initialize media controls after window is loaded (handle is available)
                InitializeMediaControls(vm);
                
                vm.SelectAllSongs = () => SongsDataGrid.SelectAll();
                vm.DeselectAllSongs = () => SongsDataGrid.UnselectAll();
                vm.FocusSearchBox = () =>
                {
                    NavbarSearchBox.Focus();
                    Keyboard.Focus(NavbarSearchBox);
                };
                vm.ScrollToCurrentAction = () =>
                {
                    if (vm.CurrentSong != null)
                    {
                        SongsDataGrid.ScrollIntoView(vm.CurrentSong);
                    }
                };
                vm.ToggleFullScreen = () =>
                {
                    if (WindowState == WindowState.Maximized && WindowStyle == WindowStyle.None)
                    {
                        // Exit full screen
                        WindowStyle = WindowStyle.None;
                        WindowState = WindowState.Normal;
                        ResizeMode = ResizeMode.CanResize;
                    }
                    else
                    {
                        // Enter full screen
                        WindowStyle = WindowStyle.None;
                        WindowState = WindowState.Maximized;
                        ResizeMode = ResizeMode.NoResize;
                    }
                };

                
                // Start async data loading (non-blocking)
                _ = vm.InitializeDataAsync();
                
                // Initialize system tray for background playback
                _systemTrayService = new SystemTrayService(
                    vm,
                    ShowFromTray,
                    ExitApplication
                );
            }
        };
        

        
        // Cleanup on window close
        Closed += (s, e) =>
        {
            _mediaControlService.Dispose();
            _smtcService?.Dispose();
            _systemTrayService?.Dispose();
            
            // Dispose ViewModel to stop audio
            if (DataContext is MainViewModel vm)
            {
                vm.Dispose();
            }
        };
    }
    
    /// <summary>
    /// Initialize media control service and subscribe to media key events.
    /// </summary>
    private void InitializeMediaControls(MainViewModel viewModel)
    {
        try
        {
            _mediaControlService.Initialize(this);
            
            // Subscribe to media key events (for keyboard shortcuts when window is focused)
            _mediaControlService.PlayPausePressed += (s, e) => viewModel.PlayPauseCommand.Execute(null);
            _mediaControlService.PlayPressed += (s, e) => viewModel.PlayCommand.Execute(null);
            _mediaControlService.PausePressed += (s, e) => viewModel.PauseCommand.Execute(null);
            _mediaControlService.StopPressed += (s, e) => viewModel.PauseCommand.Execute(null);
            _mediaControlService.NextPressed += (s, e) => viewModel.NextCommand.Execute(null);
            _mediaControlService.PreviousPressed += (s, e) => viewModel.PreviousCommand.Execute(null);
            
            _mediaControlService.MutePressed += (s, e) => 
            {
                 // Toggle mute: if volume > 0, set to 0; otherwise restore to 0.5
                 viewModel.Volume = viewModel.Volume > 0 ? 0 : 0.5;
            };
            
            _mediaControlService.VolumeUpPressed += (s, e) => 
            {
                 viewModel.Volume = Math.Min(1.0, viewModel.Volume + 0.05);
            };
            
            _mediaControlService.VolumeDownPressed += (s, e) => 
            {
                 viewModel.Volume = Math.Max(0.0, viewModel.Volume - 0.05);
            };
            
            // Initialize SMTC for Bluetooth headset support (works in background)
            _smtcService = new SmtcService();
            _smtcService.Initialize();
            
            // Wire up SMTC events
            _smtcService.PlayPressed += (s, e) => 
            {
                viewModel.PlayCommand.Execute(null);
                _smtcService.SetPlaybackStatus(true);
            };
            _smtcService.PausePressed += (s, e) => 
            {
                viewModel.PauseCommand.Execute(null);
                _smtcService.SetPlaybackStatus(false);
            };
            _smtcService.NextPressed += (s, e) => viewModel.NextCommand.Execute(null);
            _smtcService.PreviousPressed += (s, e) => viewModel.PreviousCommand.Execute(null);
            _smtcService.StopPressed += (s, e) => 
            {
                viewModel.PauseCommand.Execute(null);
                _smtcService.SetPlaybackStatus(false);
            };
            
            // Subscribe to ViewModel property changes to update SMTC
            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.CurrentSong))
                {
                    if (viewModel.CurrentSong != null)
                    {
                        _smtcService.UpdateMetadata(
                            viewModel.CurrentSong.Title,
                            viewModel.CurrentSong.Artist,
                            viewModel.CurrentSong.Album);
                    }
                    else
                    {
                        _smtcService.ClearMetadata();
                    }
                }
                else if (e.PropertyName == nameof(MainViewModel.IsPlaying))
                {
                    _smtcService.SetPlaybackStatus(viewModel.IsPlaying);
                }
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize media controls: {ex.Message}");
        }
    }

    private void OnThemeChanging()
    {
        try
        {
            // Lightweight Transition: Fade Solid Color
            // 1. Capture current background color to smooth the transition
            if (FindResource("AppBackgroundBrush") is SolidColorBrush currentBg)
            {
                ThemeTransitionOverlay.Background = currentBg;
            }
            else
            {
                // Fallback if brush isn't solid or found
                ThemeTransitionOverlay.Background = new SolidColorBrush(Colors.Black);
            }

            // 2. Make overlay visible (covering the screen with old background color)
            ThemeTransitionOverlay.Opacity = 1;
            ThemeTransitionOverlay.Visibility = Visibility.Visible;

            // 3. Animate Fade Out (revealing New Theme underneath)
            var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(300))
            {
                BeginTime = TimeSpan.FromMilliseconds(50), // Slight delay to let theme switch happen
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            
            fadeOut.Completed += (s, e) => 
            {
                ThemeTransitionOverlay.Visibility = Visibility.Collapsed;
                ThemeTransitionOverlay.Background = null; // Clear brush
            };

            ThemeTransitionOverlay.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Theme animation error: {ex}");
             ThemeTransitionOverlay.Opacity = 0;
             ThemeTransitionOverlay.Visibility = Visibility.Collapsed;
        }
    }



    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Get the currently focused element
        var focusedElement = Keyboard.FocusedElement;
        
        // Check if focus is on a TextBox (e.g., search input)
        bool isTextBoxFocused = focusedElement is TextBox;
        
        // Get ViewModel
        if (DataContext is not MainViewModel viewModel) return;

        switch (e.Key)
        {
            case Key.Space:
                // Only handle Space if NOT in a TextBox
                if (!isTextBoxFocused)
                {
                    viewModel.PlayPauseCommand.Execute(null);
                    e.Handled = true;
                }
                break;
                
            case Key.Left:
                if (!isTextBoxFocused)
                {
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        // Ctrl+Left = Previous song
                        viewModel.PreviousCommand.Execute(null);
                    }
                    else
                    {
                        // Left = Rewind 5 seconds
                        viewModel.SeekRewindCommand.Execute(null);
                    }
                    e.Handled = true;
                }
                break;
                
            case Key.Right:
                if (!isTextBoxFocused)
                {
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        // Ctrl+Right = Next song
                        viewModel.NextCommand.Execute(null);
                    }
                    else
                    {
                        // Right = Forward 5 seconds
                        viewModel.SeekForwardCommand.Execute(null);
                    }
                    e.Handled = true;
                }
                break;
                
            case Key.M:
                // Mute toggle (bonus shortcut)
                if (!isTextBoxFocused && Keyboard.Modifiers == ModifierKeys.None)
                {
                    // Toggle mute by setting volume to 0 or restoring
                    if (viewModel.Volume > 0)
                    {
                        viewModel.Volume = 0;
                    }
                    else
                    {
                        viewModel.Volume = 0.5;
                    }
                    e.Handled = true;
                }
                break;
                
            case Key.Up:
                if (!isTextBoxFocused)
                {
                    viewModel.Volume = Math.Min(1.0, viewModel.Volume + 0.05);
                    e.Handled = true;
                }
                break;

            case Key.Down:
                if (!isTextBoxFocused)
                {
                    viewModel.Volume = Math.Max(0.0, viewModel.Volume - 0.05);
                    e.Handled = true;
                }
                break;

            case Key.F:
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    viewModel.ToggleLibrarySearchCommand.Execute(null);
                    // Explicitly focus search box if it's becoming visible
                    // (Note: The command toggles visibility, FocusSearchBox action in VM handles focus if wired up, 
                    // but here we might need to rely on the VM's property change triggering UI behavior or call Focus explicitly)
                    // The FocusSearchBox action is wired in MainWindow constructor, so we should check if we need to call it.
                    // The command in VM toggles the bool.
                    // Let's call the action directly if needed, or rely on the bound command.
                    // Actually, let's just execute the command. 
                    if (viewModel.IsLibrarySearchVisible)
                    {
                         // If we just opened it, focus.
                         viewModel.FocusSearchBox?.Invoke();
                    }
                    e.Handled = true;
                }
                break;
                
            case Key.F11:
                // Full screen toggle
                viewModel.ToggleFullScreenCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void SongsDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        // Set the row header to the index + 1
        // This is O(1) and much faster than the previous O(N) converter
        e.Row.Header = (e.Row.GetIndex() + 1).ToString();
    }

    // Double-click on song to play it
    private void SongsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        
        // Get the clicked row
        if (sender is DataGrid dataGrid && dataGrid.SelectedItem is Models.Song song)
        {
            viewModel.PlaySongCommand.Execute(song);
        }
    }

    private void MenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.ContextMenu != null)
        {
            // Set PlacementTarget and DataContext for ContextMenu bindings
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.DataContext = DataContext;
            button.ContextMenu.IsOpen = true;
        }
    }

    // Drag window by title bar
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            MaximizeButton_Click(sender, e);
        }
        else
        {
            if (WindowState == WindowState.Maximized)
            {
                // Allow dragging from maximized state
                var point = e.GetPosition(this);
                WindowState = WindowState.Normal;
                Left = point.X - (Width / 2);
                Top = point.Y - 20;
            }
            DragMove();
        }
    }



    // Slider drag handlers for proper seek handling
    private bool _wasPlayingBeforeDrag = false;
    
    private void Slider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.IsDragging = true;
            
            // Remember if music was playing and pause it
            _wasPlayingBeforeDrag = viewModel.IsPlaying;
            if (_wasPlayingBeforeDrag)
            {
                viewModel.PauseCommand.Execute(null);
            }
        }
    }

    private void Slider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.IsDragging = false;
            // Explicitly seek to the final position
            viewModel.SeekEndCommand.Execute(null);
            
            if (_wasPlayingBeforeDrag)
            {
                viewModel.PlayCommand.Execute(null);
            }
        }
    }

    private void SongsDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                // Use our custom sort logic in ViewModel
                viewModel.SortByCommand.Execute(e.Column.SortMemberPath);
                
                // Prevent the DataGrid from performing its own default sort
                e.Handled = true;
            }
        }


    
    // Helper: Find a visual child of a specific type
    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result)
                return result;
            
            var descendant = FindVisualChild<T>(child);
            if (descendant != null)
                return descendant;
        }
        return null;
    }

    // Drag & Drop handlers for MP3 files
    private void SongList_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.AddSongs(files);
            }
        }
    }

    private void SongList_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            // Check if any file is MP3
            bool hasMP3 = files.Any(f => f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase));
            e.Effects = hasMP3 ? DragDropEffects.Copy : DragDropEffects.None;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void SongList_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            bool hasMP3 = files.Any(f => f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase));
            e.Effects = hasMP3 ? DragDropEffects.Copy : DragDropEffects.None;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }
    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        SystemCommands.MinimizeWindow(this);
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
            SystemCommands.RestoreWindow(this);
        else
            SystemCommands.MaximizeWindow(this);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        SystemCommands.CloseWindow(this);
    }

    // Mini Player Interaction
    private MiniPlayerWindow? _activeMiniPlayer;

    private void SwitchToMiniPlayer_Click(object sender, RoutedEventArgs e)
    {
        _activeMiniPlayer = new MiniPlayerWindow();
        _activeMiniPlayer.DataContext = this.DataContext;
        
        // Handle Mini Player Explicit Close (Expand Button)
        _activeMiniPlayer.Closed += (s, args) =>
        {
            _activeMiniPlayer = null;
            // Native Restore Animation
            this.Show(); // Ensure it's not hidden
            this.WindowState = WindowState.Normal;
            this.Activate();
            this.Opacity = 1.0; // Ensure visible
        };

        // Calculate Position & Show
        _activeMiniPlayer.WindowStartupLocation = WindowStartupLocation.Manual;
        _activeMiniPlayer.Top = 0;
        // Estimate Left position based on MinWidth (380) to start roughly in center
        _activeMiniPlayer.Left = (SystemParameters.PrimaryScreenWidth - 380) / 2;
        _activeMiniPlayer.Show();

        // Native Minimize Animation
        this.WindowState = WindowState.Minimized;
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        // If user minimizes Main Window, show tray icon but keep taskbar icon visible
        if (this.WindowState == WindowState.Minimized)
        {
            _systemTrayService?.Show(true);
            // Don't hide the window - keep it in taskbar
        }
        
        // If user restores Main Window from Taskbar, Close Mini Player
        if (this.WindowState == WindowState.Normal)
        {
            _systemTrayService?.Show(false);
            if (_activeMiniPlayer != null)
            {
                _activeMiniPlayer.Close();
            }
        }
    }
    
    /// <summary>
    /// Minimizes the window to system tray.
    /// </summary>
    private void MinimizeToTray()
    {
        _systemTrayService?.Show(true);
        this.Hide();
    }
    
    /// <summary>
    /// Shows the main window from system tray.
    /// </summary>
    private void ShowFromTray()
    {
        _systemTrayService?.Show(false);
        this.Show();
        this.WindowState = WindowState.Normal;
        this.Activate();
    }
    
    /// <summary>
    /// Exits the application completely.
    /// </summary>
    private void ExitApplication()
    {
        _systemTrayService?.Dispose();
        Application.Current.Shutdown();
    }
}
