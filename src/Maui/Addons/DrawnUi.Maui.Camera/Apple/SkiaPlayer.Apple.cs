#if IOS || MACCATALYST

using System.Diagnostics;
using AVFoundation;
using AVFoundation;
using AVKit;
using CoreMedia;
using DrawnUi.Maui.Navigation;
using Foundation;
using Foundation;
using HealthKit;
using Metal;  
using Photos;
using SkiaSharp.Views.Maui.Controls;  
using UIKit;
using AVFoundation;
using CoreMedia;
using Foundation;
using System.Diagnostics;
using System.Threading.Tasks;

namespace DrawnUi.Camera;

public partial class SkiaPlayer
{
    private AVAsset _asset;
    private AVAssetReader _reader;
    private AVAssetReaderTrackOutput _videoOutput;
    private AVAssetReaderTrackOutput _audioOutput;
    private CMTime _currentTime;
    private bool _videoLoaded;

    partial void OnSourceChanged(string source)
    {
        if (!string.IsNullOrEmpty(source))
        {
            Task.Run(() => LoadAsync());
        }
    }

    partial void OnIsPlayingChanged(bool isPlaying)
    {
        // Playback control would be implemented with timers/display link
    }

    partial void OnPositionChanged(TimeSpan position)
    {
        _currentTime = CMTime.FromSeconds(position.TotalSeconds, 1);
    }

    partial void OnVolumeChanged(double volume)
    {
        // Volume control would need audio session implementation
    }

    private async Task LoadVideoPlatformAsync(string source)
    {
        try
        {
            NSUrl url;
            if (source.StartsWith("http"))
            {
                url = NSUrl.FromString(source);
            }
            else
            {
                url = NSUrl.FromFilename(source);
            }

            _asset = AVAsset.FromUrl(url);

            // Load asset properties
            var loadTcs = new TaskCompletionSource<bool>();
            _asset.LoadValuesAsynchronously(new[] { "tracks", "duration" }, () =>
            {
                loadTcs.TrySetResult(true);
            });
            await loadTcs.Task;

            Duration = TimeSpan.FromSeconds(_asset.Duration.Seconds);
            _currentTime = CMTime.Zero;

            // Find video track
            var videoTracks = _asset.TracksWithMediaType(AVMediaTypes.Video.GetConstant());
            if (videoTracks.Length > 0)
            {
                var videoTrack = videoTracks[0];

                // Create reader
                _reader = AVAssetReader.FromAsset(_asset, out NSError error);
                if (error != null)
                    throw new Exception($"AVAssetReader error: {error.Description}");

                // Create video output
                _videoOutput = AVAssetReaderTrackOutput.Create(videoTrack, (AVVideoSettingsUncompressed)null);
                _videoOutput.AlwaysCopiesSampleData = false;
                _reader.AddOutput(_videoOutput);

                // Find audio track
                var audioTracks = _asset.TracksWithMediaType(AVMediaTypes.Audio.GetConstant());
                if (audioTracks.Length > 0)
                {
                    var audioTrack = audioTracks[0];
                    _audioOutput = AVAssetReaderTrackOutput.Create(audioTrack, (AudioSettings)null);
                    _audioOutput.AlwaysCopiesSampleData = false;
                    _reader.AddOutput(_audioOutput);
                }

                _reader.StartReading();
                _videoLoaded = true;
            }

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
        // Would implement playback loop with CADisplayLink or timer
    }

    async partial void PausePlatformAsync()
    {
        // Would stop playback loop
    }

    async partial void StopPlatformAsync()
    {
        _currentTime = CMTime.Zero;
        _reader?.CancelReading();
    }

    async partial void SeekPlatformAsync(TimeSpan position)
    {
        _currentTime = CMTime.FromSeconds(position.TotalSeconds, 1);
        // Would need to recreate reader at new position
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        _reader?.CancelReading();
        _reader?.Dispose();
        _asset?.Dispose();
    }
}


#endif

