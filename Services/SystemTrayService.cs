using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Reflection;
using DesktopMusicPlayer.ViewModels;

namespace DesktopMusicPlayer.Services;

/// <summary>
/// Manages system tray icon and context menu for background playback.
/// </summary>
public class SystemTrayService : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private readonly MainViewModel _viewModel;
    private readonly Action _showMainWindow;
    private readonly Action _exitApplication;
    
    public SystemTrayService(MainViewModel viewModel, Action showMainWindow, Action exitApplication)
    {
        _viewModel = viewModel;
        _showMainWindow = showMainWindow;
        _exitApplication = exitApplication;
        
        InitializeTrayIcon();
    }
    
    private void InitializeTrayIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Text = "Crescendo Music Player",
            Visible = false
        };
        
        // Load icon from embedded resource
        try
        {
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "ocean-wave-blue.ico");
            if (File.Exists(iconPath))
            {
                _notifyIcon.Icon = new Icon(iconPath);
            }
            else
            {
                // Fallback to application icon
                _notifyIcon.Icon = SystemIcons.Application;
            }
        }
        catch
        {
            _notifyIcon.Icon = SystemIcons.Application;
        }
        
        // Double-click to show window
        _notifyIcon.DoubleClick += (s, e) =>
        {
            Show(false);
            _showMainWindow();
        };
        
        // Create context menu
        CreateContextMenu();
    }
    
    private void CreateContextMenu()
    {
        if (_notifyIcon == null) return;
        
        var contextMenu = new ContextMenuStrip();
        
        // Show Window
        var showItem = new ToolStripMenuItem("Show Crescendo");
        showItem.Font = new Font(showItem.Font, FontStyle.Bold);
        showItem.Click += (s, e) =>
        {
            Show(false);
            _showMainWindow();
        };
        contextMenu.Items.Add(showItem);
        
        contextMenu.Items.Add(new ToolStripSeparator());
        
        // Play/Pause
        var playPauseItem = new ToolStripMenuItem("Play/Pause");
        playPauseItem.Click += (s, e) => _viewModel.PlayPauseCommand.Execute(null);
        contextMenu.Items.Add(playPauseItem);
        
        // Previous
        var prevItem = new ToolStripMenuItem("Previous");
        prevItem.Click += (s, e) => _viewModel.PreviousCommand.Execute(null);
        contextMenu.Items.Add(prevItem);
        
        // Next
        var nextItem = new ToolStripMenuItem("Next");
        nextItem.Click += (s, e) => _viewModel.NextCommand.Execute(null);
        contextMenu.Items.Add(nextItem);
        
        contextMenu.Items.Add(new ToolStripSeparator());
        
        // Exit
        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (s, e) =>
        {
            Show(false);
            _exitApplication();
        };
        contextMenu.Items.Add(exitItem);
        
        _notifyIcon.ContextMenuStrip = contextMenu;
    }
    
    /// <summary>
    /// Shows or hides the system tray icon.
    /// </summary>
    public void Show(bool visible)
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = visible;
        }
    }
    
    /// <summary>
    /// Shows a balloon notification.
    /// </summary>
    public void ShowNotification(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
    {
        _notifyIcon?.ShowBalloonTip(2000, title, text, icon);
    }
    
    public void Dispose()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }
}
