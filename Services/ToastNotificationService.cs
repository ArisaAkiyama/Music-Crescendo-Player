using System;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace DesktopMusicPlayer.Services
{
    /// <summary>
    /// Service for displaying Windows 10/11 Toast Notifications.
    /// Requires TFM net8.0-windows10.0.17763.0 or higher.
    /// </summary>
    public class ToastNotificationService
    {
        public void ShowUpdateToast(string version, Action? onActivated)
        {
            try 
            {
                var xml = $@"
                <toast>
                    <visual>
                        <binding template='ToastGeneric'>
                            <text>Update Available</text>
                            <text>Version {version} is ready to download.</text>
                            <image placement='appLogoOverride' src='{AppDomain.CurrentDomain.BaseDirectory}Assets\ocean-wave-blue.png'/>
                        </binding>
                    </visual>
                    <actions>
                        <action
                            content='Update Now'
                            arguments='action=update'
                            activationType='foreground'/>
                    </actions>
                </toast>";

                var doc = new XmlDocument();
                doc.LoadXml(xml);

                var toast = new ToastNotification(doc);
                
                // Handle activation (clicking the toast or the button)
                toast.Activated += (s, e) => 
                {
                     // Ensure execution on UI thread
                     System.Windows.Application.Current.Dispatcher.Invoke(() => onActivated?.Invoke());
                };
                
                // "DesktopMusicPlayer" is the AppUserModelID. 
                // Ensure this matches what is set in the Installer or Shortcut.
                // If not set, it might default to the process name or require registration.
                // For a self-contained app with no rigorous registration, usually just the name works if shortcuts are set up correctly.
                try 
                {
                    ToastNotificationManager.CreateToastNotifier("DesktopMusicPlayer").Show(toast);
                }
                catch (Exception ex)
                {
                    // Fallback or just log if toast fails (e.g. detailed permissions issues)
                    System.Diagnostics.Debug.WriteLine($"Toast failed: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                 System.Diagnostics.Debug.WriteLine($"Error preparing toast: {ex.Message}");
            }
        }
    }
}
