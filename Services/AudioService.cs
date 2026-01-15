using System;
using System.Threading;
using NAudio.Wave;

namespace DesktopMusicPlayer.Services
{
    public class AudioService : IAudioService
    {
        private WaveOutEvent? _waveOut;
        private AudioFileReader? _audioFile;
        private bool _disposed;
        private readonly object _lockObject = new();

        public event EventHandler<bool>? PlaybackStopped;

        public TimeSpan Position
        {
            get => _audioFile?.CurrentTime ?? TimeSpan.Zero;
            set
            {
                if (_audioFile != null)
                {
                    _audioFile.CurrentTime = value;
                }
            }
        }

        public TimeSpan Duration => _audioFile?.TotalTime ?? TimeSpan.Zero;

        public float Volume
        {
            get => _audioFile?.Volume ?? 1f;
            set
            {
                if (_audioFile != null)
                {
                    _audioFile.Volume = Math.Clamp(value, 0f, 1f);
                }
            }
        }

        public bool IsPlaying => _waveOut?.PlaybackState == PlaybackState.Playing;

        public void LoadFile(string filePath)
        {
            lock (_lockObject)
            {
                // Store current volume before cleanup
                float currentVolume = _audioFile?.Volume ?? 1f;
                
                // Smooth fade-out before cleanup to prevent click/pop sounds
                SmoothStop();

                try
                {
                    _audioFile = new AudioFileReader(filePath);
                    
                    // Restore volume to new audio file
                    _audioFile.Volume = currentVolume;
                    
                    _waveOut = new WaveOutEvent
                    {
                        DesiredLatency = 100 // Lower latency for smoother transitions
                    };
                    _waveOut.Init(_audioFile);
                    _waveOut.PlaybackStopped += OnPlaybackStopped;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading audio file: {ex.Message}");
                    CleanupAudio();
                }
            }
        }

        public void Play()
        {
            if (_waveOut == null || _audioFile == null) return;
            
            if (_waveOut.PlaybackState == PlaybackState.Stopped)
            {
                // If at end, restart from beginning
                if (_audioFile.Position >= _audioFile.Length)
                {
                    _audioFile.Position = 0;
                }
                
                // Start with volume 0 for smooth fade-in
                float targetVolume = _audioFile.Volume;
                _audioFile.Volume = 0;
                
                _waveOut.Play();
                
                // Quick fade-in on background thread
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        for (int i = 1; i <= 10; i++)
                        {
                            if (_audioFile != null)
                            {
                                _audioFile.Volume = targetVolume * (i / 10.0f);
                            }
                            Thread.Sleep(3);
                        }
                    }
                    catch { }
                });
            }
            else
            {
                _waveOut.Play();
            }
        }

        public void Pause()
        {
            _waveOut?.Pause();
        }

        public void Stop()
        {
            _waveOut?.Stop();
            if (_audioFile != null)
            {
                _audioFile.Position = 0;
            }
        }

        /// <summary>
        /// Smoothly stop playback with pause and buffer drain to prevent click/pop sounds.
        /// </summary>
        private void SmoothStop()
        {
            if (_waveOut == null || _audioFile == null) 
            {
                CleanupAudio();
                return;
            }
            
            if (_waveOut.PlaybackState == PlaybackState.Playing)
            {
                try
                {
                    // Longer volume fade to zero (~60ms)
                    float originalVolume = _audioFile.Volume;
                    const int fadeSteps = 20;
                    for (int i = fadeSteps; i >= 0; i--)
                    {
                        if (_audioFile != null)
                        {
                            _audioFile.Volume = originalVolume * (i / (float)fadeSteps);
                        }
                        Thread.Sleep(3);
                    }
                    
                    // Pause first (gentler than stop)
                    _waveOut?.Pause();
                    
                    // Wait for audio buffer to fully drain
                    Thread.Sleep(100);
                }
                catch
                {
                    // Ignore errors during fade
                }
            }
            
            // Now clean up
            CleanupAudio();
        }

        private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            // Check if playback ended naturally (reached end of file)
            // Relaxes the check to allow for small buffer differences (within 0.5 seconds of end)
            bool reachedEnd = _audioFile != null && 
                             (_audioFile.TotalTime - _audioFile.CurrentTime).TotalSeconds < 0.5;
            
            PlaybackStopped?.Invoke(this, reachedEnd);
        }

        private void CleanupAudio()
        {
            if (_waveOut != null)
            {
                _waveOut.PlaybackStopped -= OnPlaybackStopped;
                _waveOut.Stop();
                _waveOut.Dispose();
                _waveOut = null;
            }

            if (_audioFile != null)
            {
                _audioFile.Dispose();
                _audioFile = null;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CleanupAudio();
            GC.SuppressFinalize(this);
        }
    }
}
