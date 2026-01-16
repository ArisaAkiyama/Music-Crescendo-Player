using System;
using System.Threading;
using NAudio.Wave;

namespace DesktopMusicPlayer.Services
{
    public class AudioService : IAudioService
    {
        private WaveOutEvent? _waveOut;
        private WaveStream? _audioFile; // Changed from AudioFileReader to generic WaveStream
        private NAudio.Wave.SampleProviders.VolumeSampleProvider? _volumeProvider; 
        private bool _disposed;
        private readonly object _lockObject = new();
        private float _currentVolume = 0.05f; // Safety Default: Start QUIET (5%) to prevent loud bursts

        private bool _isManualStop = false;

        public event EventHandler<bool>? PlaybackStopped;

        public TimeSpan Position
        {
            get => _audioFile?.CurrentTime ?? TimeSpan.Zero;
            set
            {
                if (_audioFile != null)
                {
                    // Ensure we don't seek past end
                    if (value > _audioFile.TotalTime) value = _audioFile.TotalTime;
                    _audioFile.CurrentTime = value;
                }
            }
        }

        public TimeSpan Duration => _audioFile?.TotalTime ?? TimeSpan.Zero;

        public float Volume
        {
            get => _currentVolume;
            set
            {
                _currentVolume = Math.Clamp(value, 0f, 1f);
                // Update the volume provider if active
                if (_volumeProvider != null)
                {
                    _volumeProvider.Volume = _currentVolume;
                }
                // Fallback for AudioFileReader if used directly (though we wrap it now)
                else if (_audioFile is AudioFileReader afr)
                {
                    afr.Volume = _currentVolume;
                }
            }
        }

        public bool IsPlaying => _waveOut?.PlaybackState == PlaybackState.Playing;

        public void LoadFile(string filePath)
        {
            lock (_lockObject)
            {
                _isManualStop = true; // Loading new file counts as manual stop of previous
                
                // Cancel previous fade tasks
                _fadeCancellationTokenSource?.Cancel();
                _fadeCancellationTokenSource?.Dispose();
                _fadeCancellationTokenSource = null;
                
                SmoothStop();

                try
                {
                    // Use MediaFoundationReader for better compatibility (fixes VBR MP3 duration issues)
                    // It relies on Windows codecs which are robust
                    _audioFile = new MediaFoundationReader(filePath);
                    
                    // Convert to ISampleProvider to use VolumeSampleProvider
                    var sampleProvider = _audioFile.ToSampleProvider();
                    
                    // Add Volume Control
                    _volumeProvider = new NAudio.Wave.SampleProviders.VolumeSampleProvider(sampleProvider)
                    {
                        Volume = _currentVolume
                    };
                    
                    _waveOut = new WaveOutEvent
                    {
                        DesiredLatency = 100 
                    };
                    
                    // Initialize with the volume provider
                    _waveOut.Init(_volumeProvider);
                    _waveOut.PlaybackStopped += OnPlaybackStopped;
                    
                    // Reset flag after successful init
                    _isManualStop = false; 
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading audio file: {ex.Message}");
                    // Fallback to AudioFileReader if MediaFoundation fails
                    try 
                    {
                        CleanupAudio();
                        _audioFile = new AudioFileReader(filePath);
                        ((AudioFileReader)_audioFile).Volume = _currentVolume;
                        _waveOut = new WaveOutEvent { DesiredLatency = 100 };
                        _waveOut.Init(_audioFile);
                        _waveOut.PlaybackStopped += OnPlaybackStopped;
                        _volumeProvider = null; // AudioFileReader handles volume internally
                        _isManualStop = false;
                    }
                    catch (Exception fallbackEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error loading fallback: {fallbackEx.Message}");
                        CleanupAudio();
                    }
                }
            }
        }

        private CancellationTokenSource? _fadeCancellationTokenSource;

        public void Play()
        {
            if (_waveOut == null || _audioFile == null) return;
            
            _isManualStop = false; // Reset flag when starting playback
            
            if (_waveOut.PlaybackState == PlaybackState.Stopped)
            {
                // If at end, restart from beginning
                // Use a small threshold for floating point time comparison
                if (_audioFile.Position >= _audioFile.Length - 1000) 
                {
                    _audioFile.Position = 0;
                }
                
                // Cancel any existing fade operations
                _fadeCancellationTokenSource?.Cancel();
                _fadeCancellationTokenSource = new CancellationTokenSource();
                var token = _fadeCancellationTokenSource.Token;
                
                // Start with volume 0 for smooth fade-in
                float targetVolume = _currentVolume;
                Volume = 0; // Use property to update provider
                
                _waveOut.Play();
                
                // Quick fade-in on background thread
                System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        const int steps = 10;
                        for (int i = 1; i <= steps; i++)
                        {
                             if (token.IsCancellationRequested) return;

                             // Check if disposed
                            if (_audioFile == null) break;
                            
                            // Safe volume update
                            float newVol = targetVolume * (i / (float)steps);
                            
                            lock (_lockObject)
                            {
                                if (_volumeProvider != null) _volumeProvider.Volume = newVol;
                                else if (_audioFile is AudioFileReader afr) afr.Volume = newVol;
                            }
                            
                            await System.Threading.Tasks.Task.Delay(10, token);
                        }
                        
                        // Ensure final volume is set
                        if (!token.IsCancellationRequested && _audioFile != null) 
                        {
                            lock (_lockObject)
                            {
                                Volume = targetVolume;
                            }
                        }
                    }
                    catch (Exception) { /* Ignore disposal/cancellation errors */ }
                }, token);
            }
            else
            {
                _waveOut.Play();
            }
        }

        public void Pause()
        {
            _isManualStop = true;
            _fadeCancellationTokenSource?.Cancel();
            _waveOut?.Pause();
        }

        public void Stop()
        {
            _isManualStop = true;
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
            
            // Cancel any active fades
            _fadeCancellationTokenSource?.Cancel();
            
            if (_waveOut.PlaybackState == PlaybackState.Playing)
            {
                try
                {
                    // Longer volume fade to zero (~60ms)
                    float originalVolume = _currentVolume;
                    const int fadeSteps = 20;
                    for (int i = fadeSteps; i >= 0; i--)
                    {
                        float fadeVol = originalVolume * (i / (float)fadeSteps);
                        
                        // Update provider directly to avoid changing persistent _currentVolume
                        if (_volumeProvider != null)
                        {
                            _volumeProvider.Volume = fadeVol;
                        }
                        else if (_audioFile is AudioFileReader afr)
                        {
                            afr.Volume = fadeVol;
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
            // If it wasn't a manual stop, treat it as reaching the end
            // This handles truncated files where CurrentTime < TotalTime but playback stopped naturally
            bool reachedEnd = !_isManualStop;
            
            // Also explicitly check for exceptions
            if (e.Exception != null)
            {
                System.Diagnostics.Debug.WriteLine($"Playback stopped due to error: {e.Exception.Message}");
                // If error, we might not want to repeat endlessly, but for now let's treat it as stop
                reachedEnd = false; 
            }
            
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
