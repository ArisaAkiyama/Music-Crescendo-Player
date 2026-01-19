using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Windows.Media;
using Windows.Media.Playback;
using Windows.Storage.Streams;

namespace DesktopMusicPlayer.Services;

/// <summary>
/// Service for Windows System Media Transport Controls (SMTC).
/// This enables media controls from Bluetooth devices even when the app is in background.
/// </summary>
public class SmtcService : IDisposable
{
    private SystemMediaTransportControls? _smtc;
    private MediaPlayer? _dummyPlayer;
    private bool _isDisposed;
    
    // Events for media commands
    public event EventHandler? PlayPressed;
    public event EventHandler? PausePressed;
    public event EventHandler? PlayPausePressed;
    public event EventHandler? NextPressed;
    public event EventHandler? PreviousPressed;
    public event EventHandler? StopPressed;

    /// <summary>
    /// Initialize SMTC. Should be called after window is loaded.
    /// </summary>
    public void Initialize()
    {
        try
        {
            // Use a dummy MediaPlayer to get access to SMTC
            // This is the recommended way for desktop apps
            _dummyPlayer = new MediaPlayer();
            _dummyPlayer.CommandManager.IsEnabled = false; // Disable auto-handling
            
            _smtc = _dummyPlayer.SystemMediaTransportControls;
            
            if (_smtc != null)
            {
                // Enable buttons
                _smtc.IsEnabled = true;
                _smtc.IsPlayEnabled = true;
                _smtc.IsPauseEnabled = true;
                _smtc.IsNextEnabled = true;
                _smtc.IsPreviousEnabled = true;
                _smtc.IsStopEnabled = true;
                
                // Subscribe to button pressed events
                _smtc.ButtonPressed += OnButtonPressed;
                
                Debug.WriteLine("SMTC initialized successfully");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to initialize SMTC: {ex.Message}");
        }
    }

    /// <summary>
    /// Update the current playback status in SMTC.
    /// </summary>
    public void SetPlaybackStatus(bool isPlaying)
    {
        if (_smtc == null) return;
        
        try
        {
            _smtc.PlaybackStatus = isPlaying 
                ? MediaPlaybackStatus.Playing 
                : MediaPlaybackStatus.Paused;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to set playback status: {ex.Message}");
        }
    }

    /// <summary>
    /// Update the media metadata displayed in SMTC (e.g., artist, title).
    /// </summary>
    public void UpdateMetadata(string title, string artist, string album)
    {
        if (_smtc == null) return;

        try
        {
            var updater = _smtc.DisplayUpdater;
            updater.Type = MediaPlaybackType.Music;
            updater.MusicProperties.Title = title ?? "Unknown Title";
            updater.MusicProperties.Artist = artist ?? "Unknown Artist";
            updater.MusicProperties.AlbumTitle = album ?? "Unknown Album";
            updater.Update();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to update SMTC metadata: {ex.Message}");
        }
    }

    /// <summary>
    /// Clear metadata when no song is playing.
    /// </summary>
    public void ClearMetadata()
    {
        if (_smtc == null) return;

        try
        {
            _smtc.DisplayUpdater.ClearAll();
            _smtc.DisplayUpdater.Update();
            _smtc.PlaybackStatus = MediaPlaybackStatus.Stopped;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to clear SMTC metadata: {ex.Message}");
        }
    }

    private void OnButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
    {
        // These events come on a background thread, so we need to marshal to UI thread
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Play:
                    PlayPressed?.Invoke(this, EventArgs.Empty);
                    break;
                case SystemMediaTransportControlsButton.Pause:
                    PausePressed?.Invoke(this, EventArgs.Empty);
                    break;
                case SystemMediaTransportControlsButton.Next:
                    NextPressed?.Invoke(this, EventArgs.Empty);
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    PreviousPressed?.Invoke(this, EventArgs.Empty);
                    break;
                case SystemMediaTransportControlsButton.Stop:
                    StopPressed?.Invoke(this, EventArgs.Empty);
                    break;
            }
        });
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        if (_smtc != null)
        {
            _smtc.ButtonPressed -= OnButtonPressed;
            _smtc = null;
        }

        _dummyPlayer?.Dispose();
        _dummyPlayer = null;

        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}
