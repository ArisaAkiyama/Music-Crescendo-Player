using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DesktopMusicPlayer.Models
{
    public class Playlist : INotifyPropertyChanged
    {
        public int Id { get; set; }
        
        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public string IconGlyph { get; set; } = "\uE8D6"; // Default music icon
        
        // Special playlist type
        public bool IsLikedSongs { get; set; }
        
        // Smart Playlist fields
        public bool IsSmart { get; set; }
        public string? SmartCriteria { get; set; }
        
        // Song file paths for JSON serialization (legacy support)
        public List<string> SongPaths { get; set; } = new();
        
        // Songs in this playlist (resolved from paths)
        private ObservableCollection<Song> _songs = new();
        public ObservableCollection<Song> Songs
        {
            get => _songs;
        }

        // Computed property for UI binding - updates when Songs collection changes
        public string SongCountText => Songs.Count == 1 ? "1 Song" : $"{Songs.Count} Songs";

        public Playlist()
        {
            // Subscribe to collection changes to update SongCountText
            _songs.CollectionChanged += (s, e) => OnPropertyChanged(nameof(SongCountText));
        }

        // INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
