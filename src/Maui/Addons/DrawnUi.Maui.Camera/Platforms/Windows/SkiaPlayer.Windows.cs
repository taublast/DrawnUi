#if WINDOWS

using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.Editing;
using Windows.Storage;
using Windows.Graphics.Imaging;
using Windows.Foundation;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace DrawnUi.Camera;

public partial class SkiaPlayer
{
    private MediaPlayer _mediaPlayer;
    private MediaSource _mediaSource;
    private MediaComposition _composition;
    private bool _videoLoaded;
    private System.Timers.Timer _frameTimer;

    partial void OnSourceChanged(string source)
    {
        if (!string.IsNullOrEmpty(source))
        {
            Task.Run(() => LoadAsync());
        }
    }

    partial void OnIsPlayingChanged(bool isPlaying)
    {
        if (_mediaPlayer != null)
        {
            if (isPlaying)
            {
                Debug.WriteLine($"[SkiaPlayer] Starting playback. Current state: {_mediaPlayer.CurrentState}");
                _mediaPlayer.Play();
                StartFrameExtraction();
                Debug.WriteLine($"[SkiaPlayer] After Play(). State: {_mediaPlayer.CurrentState}");
            }
            else
            {
                Debug.WriteLine($"[SkiaPlayer] Pausing playback. Current state: {_mediaPlayer.CurrentState}");
                _mediaPlayer.Pause();
                StopFrameExtraction();
            }
        }
        else
        {
            Debug.WriteLine($"[SkiaPlayer] MediaPlayer is null in OnIsPlayingChanged");
        }
    }

    partial void OnPositionChanged(TimeSpan position)
    {
        if (_mediaPlayer != null)
        {
            _mediaPlayer.Position = position;
        }
    }

    private void OnMediaOpened(MediaPlayer sender, object args)
    {
        Debug.WriteLine($"[SkiaPlayer] Media opened successfully");
    }

    private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        Debug.WriteLine($"[SkiaPlayer] Media failed: {args.ErrorMessage}");
    }

    private void OnMediaEnded(MediaPlayer sender, object args)
    {
        Debug.WriteLine($"[SkiaPlayer] Media ended");
        IsPlaying = false;
    }

    private async Task<SKImage> ConvertSoftwareBitmapToSKImage(SoftwareBitmap softwareBitmap)
    {
        // Convert SoftwareBitmap to byte array
        var bitmapBuffer = softwareBitmap.LockBuffer(BitmapBufferAccessMode.Read);
        var reference = bitmapBuffer.CreateReference();
        byte[] bytes;
        
        unsafe
        {
            byte* data;
            uint capacity;
            ((IMemoryBufferByteAccess)reference).GetBuffer(out data, out capacity);
            bytes = new byte[capacity];
            System.Runtime.InteropServices.Marshal.Copy((System.IntPtr)data, bytes, 0, (int)capacity);
        }

        // Create SKImage from bytes
        var info = new SKImageInfo(softwareBitmap.PixelWidth, softwareBitmap.PixelHeight, SKColorType.Bgra8888);
        var skImage = SKImage.FromPixelCopy(info, bytes);
        
        return skImage;
    }

    partial void OnVolumeChanged(double volume)
    {
        if (_mediaPlayer != null)
        {
            _mediaPlayer.Volume = volume;
        }
    }

    private async Task LoadVideoPlatformAsync(string source)
    {
        try
        {
            _mediaPlayer = new MediaPlayer();

            // Add event handlers
            _mediaPlayer.MediaOpened += OnMediaOpened;
            _mediaPlayer.MediaFailed += OnMediaFailed;
            _mediaPlayer.MediaEnded += OnMediaEnded;

            if (source.StartsWith("http"))
            {
                // URL source - for URLs, we can't create MediaComposition for frame extraction
                // This is a limitation - URL sources won't have frame extraction
                _composition = null;
            }
            else
            {
                // File source
                var file = await StorageFile.GetFileFromPathAsync(source);
                _mediaSource = MediaSource.CreateFromStorageFile(file);

                // Create MediaComposition for frame extraction
                _composition = new MediaComposition();
                var clip = await MediaClip.CreateFromFileAsync(file);
                _composition.Clips.Add(clip);
            }

            _mediaPlayer.Source = _mediaSource;

            // Wait for media to open
            var tcs = new TaskCompletionSource<bool>();
            void OnOpened(MediaPlayer sender, object args)
            {
                _mediaPlayer.MediaOpened -= OnOpened;
                tcs.SetResult(true);
            }
            _mediaPlayer.MediaOpened += OnOpened;

            await tcs.Task;

            Duration = _mediaPlayer.NaturalDuration;
            _videoLoaded = true;

            Debug.WriteLine($"[SkiaPlayer] Loaded video: duration={Duration.TotalSeconds:F2}s");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SkiaPlayer] Load error: {ex.Message}");
            throw;
        }
    }

    async partial void PlayPlatformAsync()
    {
        if (_mediaPlayer != null)
        {
            _mediaPlayer.Play();
        }
    }

    async partial void PausePlatformAsync()
    {
        if (_mediaPlayer != null)
        {
            _mediaPlayer.Pause();
        }
    }

    async partial void StopPlatformAsync()
    {
        if (_mediaPlayer != null)
        {
            _mediaPlayer.Position = TimeSpan.Zero;
            _mediaPlayer.Pause();
        }
    }

    async partial void SeekPlatformAsync(TimeSpan position)
    {
        if (_mediaPlayer != null)
        {
            _mediaPlayer.Position = position;
        }
    }

    private void StartFrameExtraction()
    {
        if (_frameTimer == null)
        {
            _frameTimer = new System.Timers.Timer(33); // ~30 FPS
            _frameTimer.Elapsed += OnFrameTimerElapsed;
            _frameTimer.Start();
        }
    }

    private void StopFrameExtraction()
    {
        if (_frameTimer != null)
        {
            _frameTimer.Stop();
            _frameTimer.Dispose();
            _frameTimer = null;
        }
    }

    private void OnFrameTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
        if (_mediaPlayer != null && IsPlaying && Display != null && _composition != null)
        {
            Debug.WriteLine($"[SkiaPlayer] Frame timer elapsed. Position: {_mediaPlayer.Position.TotalSeconds:F2}s, State: {_mediaPlayer.CurrentState}");
            
            // Extract actual video frame
            Task.Run(async () =>
            {
                try
                {
                    var imageStream = await _composition.GetThumbnailAsync(_mediaPlayer.Position, 0, 0, Windows.Media.Editing.VideoFramePrecision.NearestFrame);
                    if (imageStream != null)
                    {
                        var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(imageStream);
                        var softwareBitmap = await decoder.GetSoftwareBitmapAsync();
                        var skImage = await ConvertSoftwareBitmapToSKImage(softwareBitmap);
                        if (skImage != null)
                        {
                            // Update display on UI thread
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                Display.SetImageInternal(skImage, false);
                                this.Invalidate();
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SkiaPlayer] Frame extraction error: {ex.Message}");
                }
            });
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        _mediaPlayer?.Dispose();
        _mediaSource?.Dispose();
        _composition = null; // MediaComposition doesn't implement IDisposable
    }
}

#endif
