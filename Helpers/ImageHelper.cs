using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace DesktopMusicPlayer.Helpers
{
    public static class ImageHelper
    {
        /// <summary>
        /// Convert byte array to BitmapImage for WPF Image binding
        /// </summary>
        public static BitmapImage? BytesToImage(byte[]? data)
        {
            if (data == null || data.Length == 0) 
                return null;

            try
            {
                var image = new BitmapImage();
                using var stream = new MemoryStream(data);
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.DecodePixelWidth = 300; // Optimize memory usage
                image.EndInit();
                image.Freeze(); // Make it thread-safe
                return image;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error converting image: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Create a default placeholder image for songs without cover art
        /// </summary>
        public static BitmapImage CreatePlaceholderImage()
        {
            // Return null - UI will show icon instead
            return null!;
        }
    }
}
