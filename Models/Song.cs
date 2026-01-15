using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using DesktopMusicPlayer.Helpers;
using System.Threading.Tasks;

namespace DesktopMusicPlayer.Models
{
    public class Song : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public string FilePath { get; set; } = string.Empty;
        
        // Cover Art Lazy Loading
        private BitmapImage? _coverArt;
        private bool _isCoverArtLoaded = false;
        private bool _isLoadingCoverArt = false;

        public BitmapImage? CoverArt
        {
            get
            {
                if (!_isCoverArtLoaded && !_isLoadingCoverArt)
                {
                    _isLoadingCoverArt = true;
                    LoadCoverArtAsync();
                }
                return _coverArt;
            }
            set
            {
                _coverArt = value;
                _isCoverArtLoaded = true;
                OnPropertyChanged();
            }
        }

        private async void LoadCoverArtAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    if (string.IsNullOrEmpty(FilePath) || !System.IO.File.Exists(FilePath)) return;

                    var cacheService = Services.CoverArtCacheService.Instance;
                    
                    // Check cache first
                    if (cacheService.HasCachedImage(FilePath))
                    {
                        var cachedImage = cacheService.LoadFromCache(FilePath);
                        if (cachedImage != null)
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                _coverArt = cachedImage;
                                _isCoverArtLoaded = true;
                                _isLoadingCoverArt = false;
                                OnPropertyChanged(nameof(CoverArt));
                                OnPropertyChanged(nameof(HasCoverArt));
                            });
                            return;
                        }
                    }

                    // Not in cache - extract from MP3 tags
                    using var file = TagLib.File.Create(FilePath);
                    if (file.Tag.Pictures.Length > 0)
                    {
                        var data = file.Tag.Pictures[0].Data.Data;
                        
                        // Save to cache for next time
                        cacheService.SaveToCache(FilePath, data);
                        
                        var image = ImageHelper.BytesToImage(data);
                        
                        // Update on UI Thread
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            _coverArt = image;
                            _isCoverArtLoaded = true;
                            _isLoadingCoverArt = false;
                            OnPropertyChanged(nameof(CoverArt));
                            OnPropertyChanged(nameof(HasCoverArt));
                        });
                    }
                    else
                    {
                        _isCoverArtLoaded = true; // No art found, mark as loaded so we don't try again
                        _isLoadingCoverArt = false;
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            OnPropertyChanged(nameof(HasCoverArt));
                        });
                    }
                }
                catch
                {
                    _isCoverArtLoaded = true; // Error, avoid retry loop
                    _isLoadingCoverArt = false;
                }
            });
        }

        public DateTime DateAdded { get; set; } = DateTime.Now;

        // Like/Heart status
        private bool _isLiked;
        public bool IsLiked
        {
            get => _isLiked;
            set
            {
                if (_isLiked != value)
                {
                    _isLiked = value;
                    OnPropertyChanged();
                }
            }
        }

        public string DurationFormatted => Duration.ToString(@"m\:ss");
        public string DateAddedFormatted => DateAdded.ToString("MMM dd, yyyy");
        
        // Check if song has actual cover art
        public bool HasCoverArt => CoverArt != null;

        // Technical audio details (Format, Sample Rate, Bitrate, Channels)
        public string TechnicalDetails { get; set; } = string.Empty;

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
