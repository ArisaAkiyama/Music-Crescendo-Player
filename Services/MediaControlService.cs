using System;
using System.Windows;
using System.Windows.Interop;

namespace DesktopMusicPlayer.Services;

/// <summary>
/// Service to handle media transport controls from Bluetooth headsets and multimedia keyboards.
/// Uses WM_APPCOMMAND Windows messages for reliable media key detection.
/// </summary>
public class MediaControlService : IDisposable
{
    // Windows message constants
    private const int WM_APPCOMMAND = 0x0319;
    
    // Media key command constants (APPCOMMAND_*)
    private const int APPCOMMAND_MEDIA_PLAY_PAUSE = 14;
    private const int APPCOMMAND_MEDIA_PLAY = 46;
    private const int APPCOMMAND_MEDIA_PAUSE = 47;
    private const int APPCOMMAND_MEDIA_STOP = 13;
    private const int APPCOMMAND_MEDIA_NEXTTRACK = 11;
    private const int APPCOMMAND_MEDIA_PREVIOUSTRACK = 12;
    private const int APPCOMMAND_VOLUME_MUTE = 8;
    private const int APPCOMMAND_VOLUME_DOWN = 9;
    private const int APPCOMMAND_VOLUME_UP = 10;

    private HwndSource? _hwndSource;
    private bool _isDisposed;

    // Events for media commands
    public event EventHandler? PlayPausePressed;
    public event EventHandler? PlayPressed;
    public event EventHandler? PausePressed;
    public event EventHandler? StopPressed;
    public event EventHandler? NextPressed;
    public event EventHandler? PreviousPressed;
    public event EventHandler? MutePressed;
    public event EventHandler? VolumeUpPressed;
    public event EventHandler? VolumeDownPressed;

    /// <summary>
    /// Initialize the media control service by hooking into the window's message loop.
    /// Call this after the window is loaded.
    /// </summary>
    /// <param name="window">The main application window</param>
    public void Initialize(Window window)
    {
        if (_hwndSource != null) return;

        var windowHandle = new WindowInteropHelper(window).Handle;
        if (windowHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Window handle is not available. Make sure to call Initialize after the window is loaded.");
        }

        _hwndSource = HwndSource.FromHwnd(windowHandle);
        _hwndSource?.AddHook(WndProc);
    }

    /// <summary>
    /// Windows message handler to intercept media key commands.
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_APPCOMMAND)
        {
            int command = GetAppCommand(lParam);
            bool wasHandled = false;
            
            // Process command and invoke events on UI thread
            switch (command)
            {
                case APPCOMMAND_MEDIA_PLAY_PAUSE:
                    Application.Current.Dispatcher.BeginInvoke(() => PlayPausePressed?.Invoke(this, EventArgs.Empty));
                    wasHandled = true;
                    break;
                case APPCOMMAND_MEDIA_PLAY:
                    Application.Current.Dispatcher.BeginInvoke(() => PlayPressed?.Invoke(this, EventArgs.Empty));
                    wasHandled = true;
                    break;
                case APPCOMMAND_MEDIA_PAUSE:
                    Application.Current.Dispatcher.BeginInvoke(() => PausePressed?.Invoke(this, EventArgs.Empty));
                    wasHandled = true;
                    break;
                case APPCOMMAND_MEDIA_STOP:
                    Application.Current.Dispatcher.BeginInvoke(() => StopPressed?.Invoke(this, EventArgs.Empty));
                    wasHandled = true;
                    break;
                case APPCOMMAND_MEDIA_NEXTTRACK:
                    Application.Current.Dispatcher.BeginInvoke(() => NextPressed?.Invoke(this, EventArgs.Empty));
                    wasHandled = true;
                    break;
                case APPCOMMAND_MEDIA_PREVIOUSTRACK:
                    Application.Current.Dispatcher.BeginInvoke(() => PreviousPressed?.Invoke(this, EventArgs.Empty));
                    wasHandled = true;
                    break;
                case APPCOMMAND_VOLUME_MUTE:
                    Application.Current.Dispatcher.BeginInvoke(() => MutePressed?.Invoke(this, EventArgs.Empty));
                    wasHandled = true;
                    break;
                case APPCOMMAND_VOLUME_UP:
                    Application.Current.Dispatcher.BeginInvoke(() => VolumeUpPressed?.Invoke(this, EventArgs.Empty));
                    wasHandled = true;
                    break;
                case APPCOMMAND_VOLUME_DOWN:
                    Application.Current.Dispatcher.BeginInvoke(() => VolumeDownPressed?.Invoke(this, EventArgs.Empty));
                    wasHandled = true;
                    break;
            }
            
            handled = wasHandled;
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Extract the APPCOMMAND value from lParam.
    /// The command is stored in the high word, bits 8-15.
    /// </summary>
    private static int GetAppCommand(IntPtr lParam)
    {
        // GET_APPCOMMAND_LPARAM macro: (short)(HIWORD(lParam) & ~FAPPCOMMAND_MASK)
        int value = (int)lParam.ToInt64();
        int hiWord = (value >> 16) & 0xFFFF;
        return hiWord & 0x0FFF; // Mask out device info
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        
        _hwndSource?.RemoveHook(WndProc);
        _hwndSource = null;
        _isDisposed = true;
        
        GC.SuppressFinalize(this);
    }
}
