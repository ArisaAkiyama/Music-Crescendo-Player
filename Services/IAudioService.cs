using System;

namespace DesktopMusicPlayer.Services
{
    public interface IAudioService : IDisposable
    {
        /// <summary>
        /// Load an audio file for playback
        /// </summary>
        void LoadFile(string filePath);
        
        /// <summary>
        /// Start or resume playback
        /// </summary>
        void Play();
        
        /// <summary>
        /// Pause playback
        /// </summary>
        void Pause();
        
        /// <summary>
        /// Stop playback and reset position
        /// </summary>
        void Stop();
        
        /// <summary>
        /// Current playback position
        /// </summary>
        TimeSpan Position { get; set; }
        
        /// <summary>
        /// Total duration of the loaded audio
        /// </summary>
        TimeSpan Duration { get; }
        
        /// <summary>
        /// Volume level (0.0 to 1.0)
        /// </summary>
        float Volume { get; set; }
        
        /// <summary>
        /// Whether audio is currently playing
        /// </summary>
        bool IsPlaying { get; }
        
        /// <summary>
        /// Fired when playback stops (end of file or explicit stop)
        /// </summary>
        event EventHandler<bool>? PlaybackStopped;
    }
}
