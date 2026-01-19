using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;

namespace DesktopMusicPlayer.Services
{
    /// <summary>
    /// Service for caching cover art images to disk to improve startup performance.
    /// </summary>
    public class CoverArtCacheService
    {
        private static readonly Lazy<CoverArtCacheService> _instance = 
            new Lazy<CoverArtCacheService>(() => new CoverArtCacheService());
        
        public static CoverArtCacheService Instance => _instance.Value;
        
        private readonly string _cacheFolder;
        
        private CoverArtCacheService()
        {
            _cacheFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DesktopMusicPlayer",
                "CoverCache");
            
            // Ensure cache folder exists
            Directory.CreateDirectory(_cacheFolder);
        }
        
        /// <summary>
        /// Gets the cache file path for a given song file path.
        /// Uses MD5 hash of the file path to create a unique filename.
        /// </summary>
        public string GetCachePath(string filePath)
        {
            var hash = ComputeHash(filePath);
            return Path.Combine(_cacheFolder, $"{hash}.jpg");
        }
        
        /// <summary>
        /// Checks if a cached image exists for the given song.
        /// </summary>
        public bool HasCachedImage(string filePath)
        {
            var cachePath = GetCachePath(filePath);
            return File.Exists(cachePath);
        }
        
        /// <summary>
        /// Saves cover art image data to the cache.
        /// Compresses and resizes the image to JPEG 300px to save space.
        /// </summary>
        public void SaveToCache(string filePath, byte[] imageData)
        {
            try
            {
                var cachePath = GetCachePath(filePath);
                
                // If already cached, don't overwrite (assume it's good)
                if (File.Exists(cachePath)) return;

                using (var ms = new MemoryStream(imageData))
                {
                    // decoding
                    var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    var frame = decoder.Frames[0];

                    // Resizing logic
                    double scale = 1.0;
                    if (frame.PixelWidth > 300)
                    {
                        scale = 300.0 / frame.PixelWidth;
                    }

                    var transformedProps = new TransformedBitmap(frame, new System.Windows.Media.ScaleTransform(scale, scale));

                    // Encoding to JPEG
                    var encoder = new JpegBitmapEncoder();
                    encoder.QualityLevel = 75; // Good balance of quality/size
                    encoder.Frames.Add(BitmapFrame.Create(transformedProps));

                    using (var fs = new FileStream(cachePath, FileMode.Create))
                    {
                        encoder.Save(fs);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save cover art to cache: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Loads cover art from the cache if it exists.
        /// </summary>
        public BitmapImage? LoadFromCache(string filePath)
        {
            try
            {
                var cachePath = GetCachePath(filePath);
                if (!File.Exists(cachePath)) return null;
                
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(cachePath, UriKind.Absolute);
                image.DecodePixelWidth = 300; // Optimize memory usage
                image.EndInit();
                image.Freeze(); // Make thread-safe
                
                return image;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load cover art from cache: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Clears all cached cover art images.
        /// </summary>
        public void ClearCache()
        {
            try
            {
                foreach (var file in Directory.GetFiles(_cacheFolder, "*.jpg"))
                {
                    File.Delete(file);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to clear cover art cache: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Gets the total size of the cache in bytes.
        /// </summary>
        public long GetCacheSize()
        {
            long size = 0;
            try
            {
                foreach (var file in Directory.GetFiles(_cacheFolder, "*.jpg"))
                {
                    size += new FileInfo(file).Length;
                }
            }
            catch { }
            return size;
        }
        
        private static string ComputeHash(string input)
        {
            using var md5 = MD5.Create();
            var bytes = Encoding.UTF8.GetBytes(input.ToLowerInvariant());
            var hash = md5.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
