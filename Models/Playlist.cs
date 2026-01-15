using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        public ObservableCollection<Song> Songs { get; } = new();

        // INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
