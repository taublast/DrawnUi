using System.Diagnostics;
using Android.Content;
using Android.Media;
using DrawnUi.Camera.Platforms.Android;

namespace DrawnUi.Camera;

public partial class SkiaCamera
{
    /// <summary>
    /// Pre-allocated shared buffer for pre-recording.
    /// Allocated once when EnablePreRecording=true, reused across recording sessions.
    /// Eliminates ~27MB allocation lag spike when pressing record.
    /// </summary>
    private PrerecordingEncodedBuffer _sharedPreRecordingBuffer;

    // ── 2-encoder overlap fields (pre-recording → live transition) ─────────────
    // When pre-recording transitions to live, we create a FRESH live encoder while
    // the old pre-rec encoder flushes its buffer to disk in the background.
    // A fresh codec always produces an IDR as its first output → zero keyframe wait.

    /// <summary>Background task that stops the old pre-rec encoder and writes its buffer to disk.</summary>
    private Task _preRecFlushTask;

    /// <summary>Temp output path for the live encoder (muxed with pre-rec file at stop time).</summary>
    private string _liveRecordingFilePath;

    /// <summary>Desired final output path. Set before 2-encoder handoff; used at mux time.</summary>
    private string _pendingFinalOutputPath;

    /// <summary>Whether the GPU camera path (SurfaceTexture/EGL) was active for the current encoder.</summary>
    private bool _usingGpuCameraPath;

    /// <summary>Raw (pre-rotation) camera frame dimensions used to initialize the GPU path.</summary>
    private int _cameraRawWidth, _cameraRawHeight;
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Android implementation: Pre-allocates the shared buffer for pre-recording.
    /// Called once when EnablePreRecording is set to true.
    /// </summary>
    partial void EnsurePreRecordingBufferPreAllocated()
    {
        if (_sharedPreRecordingBuffer == null)
        {
            _sharedPreRecordingBuffer = new PrerecordingEncodedBuffer(PreRecordDuration);
            System.Diagnostics.Debug.WriteLine($"[SkiaCameraAndroid] Pre-allocated shared buffer for pre-recording ({PreRecordDuration.TotalSeconds}s)");
        }
    }

    public virtual void SetZoom(double value)
    {
        // Hardware zoom not supported on Android currently, using manual scaling
        TextureScale = value;

        //in theory nativecontrol should set TextureScale regarding on the amount it was able to set using hardware
        //so the remaining zoom comes from scaling the output texture (preview)
        NativeControl.SetZoom((float)value);

        //temporary hack - preview is our texture
        Display.ZoomX = TextureScale;
        Display.ZoomY = TextureScale;

        Zoomed?.Invoke(this, value);
    }

    private bool orderedRestart = false;
    
    /// <summary>
    /// Updates preview format to match current capture format aspect ratio.
    /// Android implementation: Restarts camera session to apply new format selection.
    /// </summary>
    protected virtual void UpdatePreviewFormatForAspectRatio()
    {
        if (NativeControl is NativeCamera androidCamera)
        {
            if (orderedRestart)
                return;
            
            System.Diagnostics.Debug.WriteLine("[SkiaCameraAndroid] Updating preview format for aspect ratio match");

            orderedRestart = true;
                
            // Android's ChooseOptimalSize() automatically matches aspect ratios during setup
            // We need to restart the camera session to apply the new capture format
            Task.Run(async () =>
            {
                try
                {
                    // Stop current session
                    androidCamera.Stop();

                    // Small delay to ensure cleanup
                    await Task.Delay(100);

                    // Restart with new format - this will trigger ChooseOptimalSize()
                    // with the new capture format as aspect ratio target
                    androidCamera.Start();

                    System.Diagnostics.Debug.WriteLine(
                        "[SkiaCameraAndroid] Camera session restarted for format change");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[SkiaCameraAndroid] Error updating preview format: {ex.Message}");
                }
                finally
                {
                    orderedRestart = false;
                }
            });
        }
    }

    /// <summary>
    /// Opens a file in the gallery app
    /// </summary>
    /// <param name="imageFilePath">File path or content URI</param>
    public static void OpenFileInGallery(string imageFilePath)
    {
        Intent intent = new Intent();
        intent.SetAction(Intent.ActionView);
        Android.Net.Uri photoUri;

        if (imageFilePath.StartsWith("content://"))
        {
            photoUri = Android.Net.Uri.Parse(imageFilePath);
        }
        else
        {
            var file = new Java.IO.File(imageFilePath);
            if (!file.Exists())
            {
                throw new FileNotFoundException($"File not found: {imageFilePath}");
            }
            photoUri = FileProvider.GetUriForFile(
                Platform.AppContext,
                Platform.AppContext.PackageName + ".provider",
                file);
        }

        intent.SetDataAndType(photoUri, "image/*");
        intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.GrantReadUriPermission);
        Platform.AppContext.StartActivity(intent);
    }

    /// <summary>
    /// Opens a gallery video using the identifier previously returned by MoveVideoToGalleryAsync.
    /// Supports MediaStore content URIs, full file paths, and Android display names returned from scoped storage saves.
    /// </summary>
    /// <param name="galleryIdentifier">Content URI, full path, or MediaStore display name.</param>
    public static async Task PlayVideoDirectly(string galleryIdentifier)
    {
        if (string.IsNullOrWhiteSpace(galleryIdentifier))
            return;

        try
        {
            var videoUri = await ResolveGalleryVideoUriAsync(galleryIdentifier);

            if (videoUri == null)
            {
                Debug.WriteLine($"[SkiaCameraAndroid] Could not resolve gallery video: {galleryIdentifier}");
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var intent = new Intent(Intent.ActionView);
                intent.SetDataAndType(videoUri, "video/*");
                intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.GrantReadUriPermission);
                Platform.AppContext.StartActivity(intent);
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SkiaCameraAndroid] Error opening gallery video '{galleryIdentifier}': {ex.Message}");
        }
    }

    private static async Task<Android.Net.Uri> ResolveGalleryVideoUriAsync(string galleryIdentifier)
    {
        if (galleryIdentifier.StartsWith("content://", StringComparison.OrdinalIgnoreCase))
            return Android.Net.Uri.Parse(galleryIdentifier);

        if (galleryIdentifier.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            var fileUri = Android.Net.Uri.Parse(galleryIdentifier);
            var filePath = fileUri?.Path;
            if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
            {
                return FileProvider.GetUriForFile(
                    Platform.AppContext,
                    Platform.AppContext.PackageName + ".provider",
                    new Java.IO.File(filePath));
            }
        }

        if (Path.IsPathRooted(galleryIdentifier) && File.Exists(galleryIdentifier))
        {
            return FileProvider.GetUriForFile(
                Platform.AppContext,
                Platform.AppContext.PackageName + ".provider",
                new Java.IO.File(galleryIdentifier));
        }

        return await Task.Run(() => FindVideoUriByDisplayName(galleryIdentifier));
    }

    private static Android.Net.Uri FindVideoUriByDisplayName(string displayName)
    {
        var context = Platform.CurrentActivity ?? Android.App.Application.Context;
        var resolver = context.ContentResolver;
        var projection = new[] { "_id" };
        var selection = Android.Provider.MediaStore.Video.Media.InterfaceConsts.DisplayName + " = ?";
        var selectionArgs = new[] { displayName };
        var sortOrder = Android.Provider.MediaStore.Video.Media.InterfaceConsts.DateAdded + " DESC";

        using var cursor = resolver.Query(
            Android.Provider.MediaStore.Video.Media.ExternalContentUri,
            projection,
            selection,
            selectionArgs,
            sortOrder);

        if (cursor?.MoveToFirst() != true)
            return null;

        var contentId = cursor.GetLong(0);
        return ContentUris.WithAppendedId(Android.Provider.MediaStore.Video.Media.ExternalContentUri, contentId);
    }


    public virtual Metadata CreateMetadata()
    {
        return new Metadata()
        {
            Software = "SkiaCamera Android",
            Vendor = $"{Android.OS.Build.Manufacturer}",
            Model = $"{Android.OS.Build.Model}",

            //this will be created inside session
            //Orientation = (int)result.Get(CaptureResult.JpegOrientation),
            //ISO = (int)result.Get(CaptureResult.SensorSensitivity),
            //FocalLength = (float)result.Get(CaptureResult.LensFocalLength)
        };
    }



    protected virtual void CreateNative()
    {
        if (!IsOn || NativeControl != null)
            return;

        DisableOtherCameras();

        NativeControl = new NativeCamera(this);

        NativeControl?.ApplyDeviceOrientation(DeviceRotation);
    }

    protected async Task<List<CameraInfo>> GetAvailableCamerasPlatform(bool refresh)
    {
        var cameras = new List<CameraInfo>();

        try
        {
            var context = Platform.CurrentActivity ?? Android.App.Application.Context;
            var manager = (Android.Hardware.Camera2.CameraManager)context.GetSystemService(Android.Content.Context.CameraService);
            var cameraIds = manager.GetCameraIdList();

            int validIndex = 0;
            for (int i = 0; i < cameraIds.Length; i++)
            {
                var cameraId = cameraIds[i];
                var characteristics = manager.GetCameraCharacteristics(cameraId);

                // Skip cameras without stream configuration (same filter as SetupHardware)
                var map = (Android.Hardware.Camera2.Params.StreamConfigurationMap)characteristics.Get(
                    Android.Hardware.Camera2.CameraCharacteristics.ScalerStreamConfigurationMap);
                if (map == null)
                    continue;

                var facing = (Java.Lang.Integer)characteristics.Get(Android.Hardware.Camera2.CameraCharacteristics.LensFacing);
                var position = CameraPosition.Default;

                if (facing != null)
                {
                    position = facing.IntValue() switch
                    {
                        (int)Android.Hardware.Camera2.LensFacing.Front => CameraPosition.Selfie,
                        (int)Android.Hardware.Camera2.LensFacing.Back => CameraPosition.Default,
                        _ => CameraPosition.Default
                    };
                }

                var flashAvailable = (Java.Lang.Boolean)characteristics.Get(Android.Hardware.Camera2.CameraCharacteristics.FlashInfoAvailable);

                // Check video/photo capability via available output sizes
                var supportsVideo = map.GetOutputSizes(Java.Lang.Class.FromType(typeof(Android.Media.MediaRecorder)))?.Length > 0;
                var supportsPhoto = map.GetOutputSizes((int)Android.Graphics.ImageFormatType.Jpeg)?.Length > 0;

                cameras.Add(new CameraInfo
                {
                    Id = cameraId,
                    Name = $"Camera {cameraId} ({position})",
                    Position = position,
                    Index = validIndex,
                    HasFlash = flashAvailable?.BooleanValue() ?? false,
                    SupportsVideo = supportsVideo,
                    SupportsPhoto = supportsPhoto
                });
                validIndex++;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SkiaCameraAndroid] Error enumerating cameras: {ex.Message}");
        }

        return cameras;
    }

    protected async Task<List<CaptureFormat>> GetAvailableCaptureFormatsPlatform()
    {
        var formats = new List<CaptureFormat>();

        try
        {
            if (NativeControl is NativeCamera native)
            {
                formats = native.StillFormats;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SkiaCameraAndroid] Error getting capture formats: {ex.Message}");
        }

        return formats;
    }

    protected async Task<List<VideoFormat>> GetAvailableVideoFormatsPlatform()
    {
        var formats = new List<VideoFormat>();

        try
        {
            if (NativeControl is NativeCamera native)
            {
                // Get formats from the native camera's predefined formats method
                formats = native.GetPredefinedVideoFormats();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SkiaCameraAndroid] Error getting video formats: {ex.Message}");
        }

        return formats;
    }




    /// <summary>
    /// Call on UI thread only. Called by CheckPermissions.
    /// </summary>
    /// <returns></returns>
    public async Task<bool> RequestPermissions()
    {
        var status = await Permissions
            .CheckStatusAsync<Permissions.Camera>();

        return status == PermissionStatus.Granted;
    }

    /// <summary>
    /// Request gallery/media permissions proactively so Android does not prompt during save.
    /// For Android 10+ (scoped storage) we request read access, for pre-10 we request legacy write.
    /// </summary>
    public async Task<bool> RequestGalleryPermissions()
    {
        // Android 10+ uses MediaStore scoped access; request read media on modern OS (Tiramisu+)
        if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Q)
        {
            var readStatus = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
            if (readStatus != PermissionStatus.Granted)
            {
                readStatus = await Permissions.RequestAsync<Permissions.StorageRead>();
            }

            return readStatus == PermissionStatus.Granted;
        }

        // Legacy external storage write for Android 9 and below
        var writeStatus = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
        if (writeStatus != PermissionStatus.Granted)
        {
            writeStatus = await Permissions.RequestAsync<Permissions.StorageWrite>();
        }

        return writeStatus == PermissionStatus.Granted;
    }

    internal async Task<bool> EnsureMicrophonePermissionAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Microphone>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.Microphone>();
            }

            if (status == PermissionStatus.Granted)
                return true;

            Debug.WriteLine("[SkiaCameraAndroid] Microphone permission denied; recording will be silent.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SkiaCameraAndroid] Error requesting microphone permission: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// Mux pre-recorded and live video files using MediaMuxer.
    /// CRITICAL FIX: Must add ALL tracks from BOTH files BEFORE calling muxer.Start()
    /// Note: Audio is already embedded in video files on Android (handled by encoder).
    /// </summary>
    private async Task<string> MuxVideosInternal(string preRecordedPath, string liveRecordingPath, string outputPath)
    {
        Android.Media.MediaExtractor preExtractor = null;
        Android.Media.MediaExtractor liveExtractor = null;
        Android.Media.MediaMuxer muxer = null;

        try
        {
            System.Diagnostics.Debug.WriteLine($"[MuxVideosAndroid] === Starting Video Muxing ===");
            System.Diagnostics.Debug.WriteLine($"  Pre-recording: {preRecordedPath}");
            System.Diagnostics.Debug.WriteLine($"  Live recording: {liveRecordingPath}");
            System.Diagnostics.Debug.WriteLine($"  Output: {outputPath}");

            // Check file sizes for debugging
            var preFileSize = new System.IO.FileInfo(preRecordedPath).Length;
            var liveFileSize = new System.IO.FileInfo(liveRecordingPath).Length;
            System.Diagnostics.Debug.WriteLine($"  Pre-rec file size: {preFileSize / 1024.0:F2} KB");
            System.Diagnostics.Debug.WriteLine($"  Live file size: {liveFileSize / 1024.0:F2} KB");

            // Create extractors for both files
            preExtractor = new Android.Media.MediaExtractor();
            preExtractor.SetDataSource(preRecordedPath);

            liveExtractor = new Android.Media.MediaExtractor();
            liveExtractor.SetDataSource(liveRecordingPath);

            // Create muxer
            muxer = new Android.Media.MediaMuxer(outputPath, Android.Media.MuxerOutputType.Mpeg4);

            // CRITICAL FIX: Create clean format WITHOUT duration constraints
            var preTrackMap = new Dictionary<int, int>();
            var liveTrackMap = new Dictionary<int, int>();

            System.Diagnostics.Debug.WriteLine($"[MuxVideosAndroid] Pre-recorded file has {preExtractor.TrackCount} tracks");
            System.Diagnostics.Debug.WriteLine($"[MuxVideosAndroid] Live recording file has {liveExtractor.TrackCount} tracks");

            // Add tracks from pre-recorded file first, keyed by MIME type for live dedup.
            var mimeToOutputTrack = new Dictionary<string, int>();
            for (int i = 0; i < preExtractor.TrackCount; i++)
            {
                var sourceFormat = preExtractor.GetTrackFormat(i);
                string mime = sourceFormat.GetString(Android.Media.MediaFormat.KeyMime) ?? "";
                int outTrack = muxer.AddTrack(sourceFormat);
                preTrackMap[i] = outTrack;
                mimeToOutputTrack[mime] = outTrack;
                preExtractor.SelectTrack(i);
                System.Diagnostics.Debug.WriteLine($"[MuxVideosAndroid] Pre-rec track {i} ('{mime}') → output track {outTrack}");
            }

            // Map live tracks to existing output tracks by MIME type.
            // If the live file has a track type absent from the pre-rec file (e.g. audio when
            // pre-rec had no audio), add it as a new output track so it isn't silently dropped.
            for (int i = 0; i < liveExtractor.TrackCount; i++)
            {
                var liveFormat = liveExtractor.GetTrackFormat(i);
                string mime = liveFormat.GetString(Android.Media.MediaFormat.KeyMime) ?? "";
                if (mimeToOutputTrack.TryGetValue(mime, out int existingTrack))
                {
                    liveTrackMap[i] = existingTrack;
                }
                else
                {
                    int outTrack = muxer.AddTrack(liveFormat);
                    liveTrackMap[i] = outTrack;
                    mimeToOutputTrack[mime] = outTrack;
                    System.Diagnostics.Debug.WriteLine($"[MuxVideosAndroid] Live-only track {i} ('{mime}') → new output track {outTrack}");
                }
                liveExtractor.SelectTrack(i);
                System.Diagnostics.Debug.WriteLine($"[MuxVideosAndroid] Live track {i} ('{mime}') → output track {liveTrackMap[i]}");
            }

            // CRITICAL: Seek both extractors to the beginning before reading!
            preExtractor.SeekTo(0, Android.Media.MediaExtractorSeekTo.PreviousSync);
            liveExtractor.SeekTo(0, Android.Media.MediaExtractorSeekTo.PreviousSync);
            System.Diagnostics.Debug.WriteLine($"[MuxVideosAndroid] Seeked both extractors to beginning");

            // Start muxer ONCE after track added
            muxer.Start();
            System.Diagnostics.Debug.WriteLine($"[MuxVideosAndroid] Muxer started - ready to write concatenated samples");

            // Write samples from pre-recorded file (timeOffset = 0)
            var (preFrameCount, preFirstPts, preLastPts) = WriteSamplesToMuxer(muxer, preExtractor, preTrackMap, timeOffsetUs: 0);
            System.Diagnostics.Debug.WriteLine($"[MuxVideosAndroid] PRE-REC: {preFrameCount} frames, timestamps {preFirstPts / 1000.0:F2}ms → {preLastPts / 1000.0:F2}ms");

            // CRITICAL: Use ACTUAL last frame PTS as offset, NOT file metadata duration!
            // This ensures seamless continuation with no gaps
            long liveOffsetUs = preLastPts + 33333;  // Add one frame duration (~33ms @ 30fps) to avoid overlap

            // Write samples from live recording (timeOffset = last pre-rec frame + 1 frame)
            var (liveFrameCount, liveFirstPts, liveLastPts) = WriteSamplesToMuxer(muxer, liveExtractor, liveTrackMap, timeOffsetUs: liveOffsetUs);
            System.Diagnostics.Debug.WriteLine($"[MuxVideosAndroid] LIVE: {liveFrameCount} frames, timestamps {liveFirstPts / 1000.0:F2}ms → {liveLastPts / 1000.0:F2}ms");

            // Stop muxer
            muxer.Stop();
            System.Diagnostics.Debug.WriteLine($"[MuxVideosAndroid] Muxing completed successfully");
            System.Diagnostics.Debug.WriteLine($"[MuxVideosAndroid] Total frames: {preFrameCount + liveFrameCount}");

            return await Task.FromResult(outputPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MuxVideosAndroid] Error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[MuxVideosAndroid] Stack trace: {ex.StackTrace}");
            throw;
        }
        finally
        {
            preExtractor?.Release();
            liveExtractor?.Release();
            muxer?.Release();
        }
    }

    /// <summary>
    /// Write all samples from extractor to muxer with time offset
    /// CRITICAL: Normalizes timestamps to start from 0 before applying offset (prevents gaps!)
    /// </summary>
    private (long frameCount, long firstPtsUs, long lastPtsUs) WriteSamplesToMuxer(Android.Media.MediaMuxer muxer, Android.Media.MediaExtractor extractor, Dictionary<int, int> trackIndexMap, long timeOffsetUs)
    {
        long frameCount = 0;
        long firstFrameTimeUs = -1;  // Track first frame timestamp for normalization
        long firstWrittenPtsUs = -1;
        long lastWrittenPtsUs = -1;
        var sampleData = Java.Nio.ByteBuffer.Allocate(1024 * 1024);
        var sampleInfo = new Android.Media.MediaCodec.BufferInfo();

        try
        {
            while (true)
            {
                sampleData.Clear();
                int trackIndex = extractor.SampleTrackIndex;

                if (trackIndex < 0)
                    break;

                sampleInfo.Offset = 0;
                sampleInfo.Size = extractor.ReadSampleData(sampleData, 0);

                if (sampleInfo.Size <= 0)
                {
                    extractor.Advance();
                    continue;
                }

                // CRITICAL: Capture first frame timestamp for normalization
                if (firstFrameTimeUs == -1)
                    firstFrameTimeUs = extractor.SampleTime;

                // CRITICAL: Normalize timestamp to start from 0, THEN add offset
                long normalizedTimeUs = extractor.SampleTime - firstFrameTimeUs;
                sampleInfo.PresentationTimeUs = normalizedTimeUs + timeOffsetUs;
                sampleInfo.Flags = (Android.Media.MediaCodecBufferFlags)(int)extractor.SampleFlags;

                if (trackIndexMap.ContainsKey(trackIndex))
                {
                    muxer.WriteSampleData(trackIndexMap[trackIndex], sampleData, sampleInfo);
                    frameCount++;

                    // Track first and last written PTS
                    if (firstWrittenPtsUs == -1)
                        firstWrittenPtsUs = sampleInfo.PresentationTimeUs;
                    lastWrittenPtsUs = sampleInfo.PresentationTimeUs;
                }

                extractor.Advance();
            }
        }
        finally
        {
            sampleData?.Dispose();
        }

        return (frameCount, firstWrittenPtsUs, lastWrittenPtsUs);
    }

    // DELETE OLD BROKEN METHOD
    private void ExtractTracksAndWriteToMuxer_OLD_BROKEN_DO_NOT_USE(Android.Media.MediaMuxer muxer, string inputPath, long timeOffsetUs)
    {
        using (var extractor = new Android.Media.MediaExtractor())
        {
            extractor.SetDataSource(inputPath);

            var trackIndexMap = new Dictionary<int, int>();

            for (int i = 0; i < extractor.TrackCount; i++)
            {
                using (var format = extractor.GetTrackFormat(i))
                {
                    int outTrackIndex = muxer.AddTrack(format);
                    trackIndexMap[i] = outTrackIndex;
                    extractor.SelectTrack(i);
                }
            }

            muxer.Start();  // ❌ BUG: This gets called TWICE!

            var sampleData = Java.Nio.ByteBuffer.Allocate(1024 * 1024);
            var sampleInfo = new Android.Media.MediaCodec.BufferInfo();

            while (true)
            {
                sampleData.Clear();
                int trackIndex = extractor.SampleTrackIndex;

                if (trackIndex < 0)
                    break;

                sampleInfo.Offset = 0;
                sampleInfo.Size = extractor.ReadSampleData(sampleData, 0);

                if (sampleInfo.Size <= 0)
                {
                    extractor.Advance();
                    continue;
                }

                sampleInfo.PresentationTimeUs = extractor.SampleTime + timeOffsetUs;
                sampleInfo.Flags = (Android.Media.MediaCodecBufferFlags)(int)extractor.SampleFlags;

                muxer.WriteSampleData(trackIndexMap[trackIndex], sampleData, sampleInfo);
                extractor.Advance();
            }

            sampleData.Dispose();
        }
    }

    private long GetMediaDurationMicroseconds(string filePath)
    {
        using (var retriever = new Android.Media.MediaMetadataRetriever())
        {
            retriever.SetDataSource(filePath);
            string duration = retriever.ExtractMetadata(Android.Media.MetadataKey.Duration);
            if (long.TryParse(duration, out long durationMs))
                return durationMs * 1000; // Convert ms to microseconds
            return 0;
        }
    }

    private void OnAudioSampleAvailable(object sender, AudioSample sample)
    {
        var useSample = OnAudioSampleAvailable(sample);

        WriteAudioSample(useSample);
    }

    public virtual void WriteAudioSample(AudioSample e)
    {
        if (_captureVideoEncoder is AndroidCaptureVideoEncoder droidEnc)
        {
            droidEnc.WriteAudio(e);
        }
    }

    private async Task StopRealtimeVideoProcessingInternal()
    {
        if (_captureVideoEncoder.LiveRecordingDuration < TimeSpan.FromSeconds(1))
        {
            await AbortRealtimeVideoProcessingInternal();
            return;
        }

        // Set busy while processing - prevents user actions during file finalization
        IsBusy = true;

        ICaptureVideoEncoder encoder = null;

        try
        {
             if (_audioCapture != null)
            {
                try
                {
                    await _audioCapture.StopAsync();
                    _audioCapture.SampleAvailable -= OnAudioSampleAvailable;
                    if (_audioCapture is IDisposable disposableAudio)
                    {
                        disposableAudio.Dispose();
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                }
                finally
                {
                    _audioCapture = null;
                }
            }

            // CRITICAL: Stop frame capture timer FIRST before clearing encoder reference
            // This prevents race conditions where CaptureFrame is still executing
            _frameCaptureTimer?.Dispose();
            _frameCaptureTimer = null;

            // Give any in-flight CaptureFrame calls time to complete
            await Task.Delay(50);

            // Detach Android mirror event
            if (_captureVideoEncoder is AndroidCaptureVideoEncoder _droidEncPrev)
            {
                try
                {
                    _droidEncPrev.PreviewAvailable -= _encoderPreviewInvalidateHandler;
                }
                catch
                {
                }

                // Stop GPU camera path if active
                _droidEncPrev.DisposeGpuCameraPath();
            }

            // Revert to normal preview session if GPU path was active
            if (NativeControl is NativeCamera camForGpuCleanup)
            {
                camForGpuCleanup.StopGpuCameraSession();
            }

            // Get local reference to encoder before clearing field to prevent disposal race
            encoder = _captureVideoEncoder;
            _captureVideoEncoder = null;

            // Stop the live encoder and get its result
            CapturedVideo capturedVideo = await encoder?.StopAsync();

            // ── Deferred pre-rec flush path: wait for background flush or serialize buffer now, then mux ──
            if (_preRecFlushTask != null)
            {
                Debug.WriteLine($"[StopRealtimeVideoProcessing] Waiting for pre-rec background flush...");
                try { await _preRecFlushTask; }
                catch (Exception ex) { Debug.WriteLine($"[StopRealtimeVideoProcessing] Pre-rec flush error: {ex.Message}"); }
                _preRecFlushTask = null;
            }
            else if (!string.IsNullOrEmpty(_liveRecordingFilePath) &&
                     encoder is AndroidCaptureVideoEncoder deferredEncoder &&
                     !string.IsNullOrEmpty(_preRecordingFilePath))
            {
                try
                {
                    Debug.WriteLine($"[StopRealtimeVideoProcessing] Serializing pre-record buffer at stop time → {_preRecordingFilePath}");
                    await deferredEncoder.WriteBufferedPreRecordingToFileAsync(_preRecordingFilePath);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[StopRealtimeVideoProcessing] Pre-record stop-time serialization error: {ex.Message}");
                }
            }

            if (!string.IsNullOrEmpty(_liveRecordingFilePath) && !string.IsNullOrEmpty(_pendingFinalOutputPath))
            {
                // Deferred-finalization path: mux pre-record temp file + live temp file → final output.
                string liveFile = capturedVideo?.FilePath ?? _liveRecordingFilePath;
                bool hasPreRec = !string.IsNullOrEmpty(_preRecordingFilePath) &&
                                 File.Exists(_preRecordingFilePath) &&
                                 new FileInfo(_preRecordingFilePath).Length > 0;
                bool hasLive   = File.Exists(liveFile) && new FileInfo(liveFile).Length > 0;

                Debug.WriteLine($"[StopRealtimeVideoProcessing] Final mux inputs: pre-rec={hasPreRec} live={hasLive}");

                if (hasPreRec && hasLive)
                {
                    Debug.WriteLine($"[StopRealtimeVideoProcessing] Muxing: {_preRecordingFilePath} + {liveFile} → {_pendingFinalOutputPath}");
                    string muxedPath = await MuxVideosInternal(_preRecordingFilePath, liveFile, _pendingFinalOutputPath);
                    capturedVideo = new CapturedVideo { FilePath = muxedPath };
                }
                else if (hasLive)
                {
                    Debug.WriteLine($"[StopRealtimeVideoProcessing] Pre-rec flush empty/missing — using live file only");
                    System.IO.File.Move(liveFile, _pendingFinalOutputPath, overwrite: true);
                    capturedVideo = new CapturedVideo { FilePath = _pendingFinalOutputPath };
                }
                else if (hasPreRec)
                {
                    Debug.WriteLine($"[StopRealtimeVideoProcessing] Live file empty — using pre-rec only");
                    System.IO.File.Move(_preRecordingFilePath, _pendingFinalOutputPath, overwrite: true);
                    capturedVideo = new CapturedVideo { FilePath = _pendingFinalOutputPath };
                }

                // Clean up temp live file (pre-rec file is cleaned up below by ClearPreRecordingBuffer)
                try
                {
                    if (liveFile != capturedVideo?.FilePath && File.Exists(liveFile))
                        File.Delete(liveFile);
                }
                catch { }

                _liveRecordingFilePath = null;
                _pendingFinalOutputPath = null;
            }
            else
            {
                // Single-encoder path: live encoder wrote everything to one file
                Debug.WriteLine($"[StopRealtimeVideoProcessing] Single-encoder path: {capturedVideo?.FilePath}");
            }

            ClearPreRecordingBuffer();


            if (capturedVideo != null)
            {
                OnRecordingSuccess(capturedVideo);
            }

            // Update state and notify success
            SetIsRecordingVideo(false);

            IsBusy = false; // Release busy state after successful processing

            // Restart preview audio if still enabled
            if (State == HardwareState.On && (CaptureMode == CaptureModeType.Video && EnableAudioRecording || EnableAudioMonitoring))
            {
                StartPreviewAudioCapture();
            }
        }
        catch (Exception ex)
        {
            // Clean up on error
            _frameCaptureTimer?.Dispose();
            _frameCaptureTimer = null;
            _captureVideoEncoder = null;

            SetIsRecordingVideo(false);
            IsBusy = false; // Release busy state on error

            // Restart preview audio if still enabled
            if (State == HardwareState.On && (CaptureMode == CaptureModeType.Video && EnableAudioRecording || EnableAudioMonitoring))
            {
                StartPreviewAudioCapture();
            }

            RecordingFailed?.Invoke(this, ex);
            throw;
        }
        finally
        {
            // Clean up encoder after StopAsync completes
            encoder?.Dispose();
        }
    }

    private async Task AbortRealtimeVideoProcessingInternal() //OK
    {
        ICaptureVideoEncoder encoder = null;

        try
        {
             if (_audioCapture != null)
            {
                try
                {
                    await _audioCapture.StopAsync();
                    _audioCapture.SampleAvailable -= OnAudioSampleAvailable;
                    if (_audioCapture is IDisposable disposableAudio)
                    {
                        disposableAudio.Dispose();
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                }
                finally
                {
                    _audioCapture = null;
                }
            }

            // CRITICAL: Stop frame capture timer FIRST before clearing encoder reference
            // This prevents race conditions where CaptureFrame is still executing
            _frameCaptureTimer?.Dispose();
            _frameCaptureTimer = null;

            // Give any in-flight CaptureFrame calls time to complete
            await Task.Delay(50);

            // Detach Android mirror event
            if (_captureVideoEncoder is AndroidCaptureVideoEncoder _droidEncPrev)
            {
                try
                {
                    _droidEncPrev.PreviewAvailable -= _encoderPreviewInvalidateHandler;
                }
                catch
                {
                }

                // Stop GPU camera path if active
                _droidEncPrev.DisposeGpuCameraPath();
            }

            // Revert to normal preview session if GPU path was active
            if (NativeControl is NativeCamera camForGpuCleanup)
            {
                camForGpuCleanup.StopGpuCameraSession();
            }


            // Get local reference to encoder before clearing field to prevent disposal race
            encoder = _captureVideoEncoder;
            _captureVideoEncoder = null;

            // Stop encoder
            await encoder?.AbortAsync();

            // Clean up 2-encoder transition resources (if abort happens mid-handoff)
            if (_liveRecordingFilePath != null)
            {
                try { if (File.Exists(_liveRecordingFilePath)) File.Delete(_liveRecordingFilePath); } catch { }
                _liveRecordingFilePath = null;
            }
            _pendingFinalOutputPath = null;
            // Note: _preRecFlushTask runs in background and self-disposes; no need to await on abort

            ClearPreRecordingBuffer();

            SetIsRecordingVideo(false);

            // Restart preview audio if still enabled
            if (State == HardwareState.On && (CaptureMode == CaptureModeType.Video && EnableAudioRecording || EnableAudioMonitoring))
            {
                StartPreviewAudioCapture();
            }
        }
        catch (Exception ex)
        {
            // Clean up on error
            _frameCaptureTimer?.Dispose();
            _frameCaptureTimer = null;
            _captureVideoEncoder = null;

            SetIsRecordingVideo(false);

            // Restart preview audio if still enabled
            if (State == HardwareState.On && (CaptureMode == CaptureModeType.Video && EnableAudioRecording || EnableAudioMonitoring))
            {
                StartPreviewAudioCapture();
            }

            //RecordingFailed?.Invoke(this, ex);
            throw;
        }
        finally
        {
            // Clean up encoder after StopAsync completes
            encoder?.Dispose();
        }
    }

    private async Task StartRealtimeVideoProcessing() //OK
    {
        if (IsBusy)
            return;

        // Stop preview audio - recording will take over with its own audio capture
        StopPreviewAudioCapture();

        // Reset the recording clock immediately — before anything async can yield the thread.
        // Any frame callback that fires after this point (from a new or lingering GPU session)
        // will compute elapsedLocal against this fresh base, showing 0:00 at session start.
        _captureVideoStartTime = DateTime.Now;
        if (_captureVideoEncoder is AndroidCaptureVideoEncoder _prevEncBridge)
            _prevEncBridge.CaptureStartAbsoluteNs = 0; // invalidate old encoder's clock
        _capturePtsBaseTime = null;

        // Create Android encoder (GPU path via MediaCodec Surface + EGL + Skia GL)
        var newEncoder = new AndroidCaptureVideoEncoder();
        _captureVideoEncoder = newEncoder;

        // Set parent reference and pre-recording mode
        _captureVideoEncoder.ParentCamera = this;
        _captureVideoEncoder.IsPreRecordingMode = IsPreRecording;

        // Pass pre-allocated shared buffer to eliminate allocation lag
        if (IsPreRecording && _sharedPreRecordingBuffer != null)
        {
            newEncoder.SharedPreRecordingBuffer = _sharedPreRecordingBuffer;
            Debug.WriteLine($"[StartRealtimeVideoProcessing] Passing pre-allocated shared buffer to encoder");
        }

        Debug.WriteLine($"[StartRealtimeVideoProcessing] Android encoder initialized with IsPreRecordingMode={IsPreRecording}");

        // Preview source: GPU path sets UseRecordingFramesForPreview=true later (single-stream,
        // preview derived from encoder via GlPreviewScaler). Legacy path stays false (ImageReader preview).
        UseRecordingFramesForPreview = false;

        // CRITICAL: Always use final Movies directory path (single-file approach)
        // Buffer stays in memory, so output path doesn't matter during pre-recording phase
        var ctx = Android.App.Application.Context;
        var moviesDir =
            ctx.GetExternalFilesDir(Android.OS.Environment.DirectoryMovies)?.AbsolutePath ??
            ctx.FilesDir?.AbsolutePath ?? ".";
        string outputPath = Path.Combine(moviesDir, $"CaptureVideo_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");

        if (IsPreRecording)
        {
            Debug.WriteLine($"[StartRealtimeVideoProcessing] Android pre-recording (buffer to memory, final output: {outputPath})");
        }
        else
        {
            Debug.WriteLine($"[StartRealtimeVideoProcessing] Android recording to file: {outputPath}");
        }

        // Use camera-reported format if available; else fall back to preview size or 1280x720
        var currentFormat = NativeControl?.GetCurrentVideoFormat();
        var rawWidth =
            currentFormat?.Width > 0 ? currentFormat.Width : (int)(PreviewSize.Width > 0 ? PreviewSize.Width : 1280);
        var rawHeight =
            currentFormat?.Height > 0 ? currentFormat.Height : (int)(PreviewSize.Height > 0 ? PreviewSize.Height : 720);
        var fps = currentFormat?.FrameRate > 0 ? currentFormat.FrameRate : 30;

        // Apply rotation correction to align encoder with preview orientation
        var (width, height) = GetRotationCorrectedDimensions(rawWidth, rawHeight);

        // Diagnostic info
        int prevW = 0, prevH = 0, sensor = -1;
        bool previewRotated = false;
        if (NativeControl is NativeCamera cam)
        {
            prevW = cam.PreviewWidth;
            prevH = cam.PreviewHeight;
            sensor = cam.SensorOrientation;
            previewRotated = (sensor == 90 || sensor == 270);
        }

        System.Diagnostics.Debug.WriteLine(
            $"[CAPTURE-ENCODER] preview={prevW}x{prevH} rotated={previewRotated} sensor={sensor} currentFormat={(currentFormat?.Width ?? 0)}x{(currentFormat?.Height ?? 0)}@{fps} encoderBefore={rawWidth}x{rawHeight} encoderFinal={width}x{height} UseRecordingFramesForPreview={UseRecordingFramesForPreview}");
        _diagEncWidth = width;
        _diagEncHeight = height;
        _diagBitrate = Math.Max((long)width * height * 4, 2_000_000L);
        _cameraRawWidth = rawWidth;
        _cameraRawHeight = rawHeight;
        SetSourceFrameDimensions(width, height);

        // Pass locked rotation to encoder for proper video orientation metadata (Android-specific)
        var audioEnabled = EnableAudioRecording;
        if (audioEnabled)
        {
            audioEnabled = await EnsureMicrophonePermissionAsync();
            if (!audioEnabled)
            {
                Debug.WriteLine("[StartRealtimeVideoProcessing] Microphone permission denied; recording will continue without audio.");
            }
        }

        bool useGpuCameraPath = false;
        if (_captureVideoEncoder is DrawnUi.Camera.AndroidCaptureVideoEncoder androidEncoder)
        {
            await androidEncoder.InitializeAsync(outputPath, width, height, fps, audioEnabled, RecordingLockedRotation);

            // Apply codec selection if set
            if (AudioCodecIndex >= 0)
            {
                var codecs = await GetAvailableAudioCodecsAsync();
                if (AudioCodecIndex < codecs.Count)
                {
                    var codecName = codecs[AudioCodecIndex];
                    androidEncoder.SetAudioCodec(codecName);
                    Debug.WriteLine($"[SkiaCameraAndroid] Selected Audio Codec: {codecName}");
                }
            }

            // Try to initialize GPU camera path for zero-copy frame capture.
            if (GpuCameraFrameProvider.IsSupported())
            {
                bool isFrontCamera = false;
                // Pass raw camera dimensions for SurfaceTexture - camera outputs frames in native orientation
                // Transform matrix from SurfaceTexture handles rotation to match encoder dimensions
                useGpuCameraPath = androidEncoder.InitializeGpuCameraPath(isFrontCamera, rawWidth, rawHeight);
                if (useGpuCameraPath)
                {
                    Debug.WriteLine($"[StartRealtimeVideoProcessing] GPU camera path ENABLED (camera={rawWidth}x{rawHeight})");
                }
                else
                {
                    Debug.WriteLine($"[StartRealtimeVideoProcessing] GPU camera path failed, using legacy SKBitmap path");
                }
            }
            else
            {
                Debug.WriteLine("[StartRealtimeVideoProcessing] GPU camera path not supported, using legacy SKBitmap path");
            }
        }
        else
        {
            await _captureVideoEncoder.InitializeAsync(outputPath, width, height, fps, audioEnabled);
        }

        if (audioEnabled)
        {
            try
            {
                // Create audio capture if not exists
                if (_audioCapture == null)
                {
                    _audioCapture = CreateAudioCapturePlatform();
                    _audioCapture.SampleAvailable += OnAudioSampleAvailable;
                }

                if (IsPreRecording)
                {
                    _audioBuffer = new CircularAudioBuffer(PreRecordDuration);
                    if (_captureVideoEncoder is AndroidCaptureVideoEncoder enc)
                    {
                        enc.SetAudioBuffer(_audioBuffer);
                    }
                }
                else
                {
                    _audioBuffer = null;
                    if (_captureVideoEncoder is AndroidCaptureVideoEncoder enc)
                    {
                        enc.SetAudioBuffer(null);
                    }
                }

                await _audioCapture.StartAsync(AudioSampleRate, AudioChannels, AudioBitDepth, AudioDeviceIndex);
                Debug.WriteLine("[StartRealtimeVideoProcessing] Audio capture started");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StartRealtimeVideoProcessing] Failed to start audio: {ex.Message}");
            }
        }
        else
        {
            _audioBuffer = null;
            if (_captureVideoEncoder is AndroidCaptureVideoEncoder enc)
            {
                enc.SetAudioBuffer(null);
            }
        }

        // CRITICAL: In pre-recording mode, do NOT call StartAsync during initialization
        // Pre-recording mode should just buffer frames in memory without starting file writing
        // StartAsync will be called later when transitioning to live recording
        if (!IsPreRecording)
        {
            await _captureVideoEncoder.StartAsync();
            Debug.WriteLine($"[StartRealtimeVideoProcessing] StartAsync called for live/normal recording");
        }
        else
        {
            Debug.WriteLine($"[StartRealtimeVideoProcessing] Skipping StartAsync - pre-recording mode will buffer frames in memory");
        }

        // Track which frame-delivery path is active (GPU or legacy CPU), needed for 2-encoder handoff
        _usingGpuCameraPath = useGpuCameraPath;

        // Drop the first camera frame to avoid occasional corrupted first frame from the camera/RenderScript pipeline
        _androidWarmupDropRemaining = 1;

        // Stamp the new encoder's audio/video clock bridge (CaptureStartAbsoluteNs must be set
        // as late as possible — after all initialization — for accurate CLOCK_MONOTONIC alignment).
        if (_captureVideoEncoder is AndroidCaptureVideoEncoder droidEncBridge)
            droidEncBridge.CaptureStartAbsoluteNs = Super.GetCurrentTimeNanos();

        // Diagnostics
        _diagStartTime = DateTime.Now;
        _diagDroppedFrames = 0;
        _diagSubmittedFrames = 0;
        _diagLastSubmitMs = 0;
        ResetRecordingFps();
        _targetFps = fps;
        if (NativeControl is NativeCamera diagSnapCam)
            _diagRawFrameCountAtStart = diagSnapCam.RawFrameCount;

        // Event-driven capture on Android: drive encoder from camera preview callback
        int diagCounter = 0;

        if (NativeControl is NativeCamera androidCam)
        {
            // GPU CAMERA PATH: Use SurfaceTexture for zero-copy frame capture
            if (useGpuCameraPath && _captureVideoEncoder is AndroidCaptureVideoEncoder gpuEncoder && gpuEncoder.GpuFrameProvider != null)
            {
                Debug.WriteLine($"[StartRealtimeVideoProcessing] Setting up GPU camera session");

                // Get the GPU surface and create camera session
                var gpuSurface = gpuEncoder.GpuFrameProvider.GetCameraOutputSurface();
                if (gpuSurface != null)
                {
                    // Start the GPU frame provider
                    gpuEncoder.GpuFrameProvider.Start();

                    // Create camera session with GPU surface and target FPS
                    androidCam.CreateGpuCameraSession(gpuSurface, fps);

                    // Set up SurfaceTexture frame callback
                    // CRITICAL: OnFrameAvailable fires on arbitrary Android thread, NOT EGL context thread!
                    // We must NOT call UpdateTexImage here - only signal and let encoder thread process.
                    // ASYNC DECOUPLING: This callback just signals, returns immediately to camera.
                    // Heavy processing happens on dedicated GpuEncodingThread.
                    gpuEncoder.GpuFrameProvider.Renderer.OnFrameAvailable += (sender, surfaceTexture) =>
                    {
                        // Track camera input FPS (count every frame camera delivers)
                        CalculateCameraInputFps();

                        if ((!IsPreRecording && !IsRecording) || _captureVideoEncoder is not AndroidCaptureVideoEncoder droidEnc)
                        {
                            return;
                        }

                        // Calculate timestamp
                        var elapsedLocal = DateTime.Now - _captureVideoStartTime;
                        if (elapsedLocal.Ticks < 0)
                            elapsedLocal = TimeSpan.Zero;

                        // JUST SIGNAL - no await, no heavy work, no frame gate needed
                        // Background thread handles serialization via single-slot pattern
                        droidEnc.SignalGpuFrame(
                            elapsedLocal,
                            FrameProcessor,
                            VideoDiagnosticsOn,
                            DrawDiagnostics
                        );

                    };

                    // Subscribe to GPU frame processed event for FPS tracking
                    // This fires on the background encoding thread after each frame is successfully processed
                    gpuEncoder.OnGpuFrameProcessed += () =>
                    {
                        System.Threading.Interlocked.Increment(ref _diagSubmittedFrames);
                        CalculateRecordingFps();
                    };

                    bool useDualStreamPreview = AndroidCameraExperimentalFlags.UseLegacyDualStreamPreviewDuringRecording;

                    // GPU path normally mirrors preview from the encoder. The experimental dual-stream
                    // mode keeps preview on the ImageReader path for recording comparisons.
                    UseRecordingFramesForPreview = !useDualStreamPreview;

                    if (!useDualStreamPreview && MirrorRecordingToPreview && _captureVideoEncoder is AndroidCaptureVideoEncoder _droidEncPrev)
                    {
                        _encoderPreviewInvalidateHandler = (s, e) =>
                        {
                            try
                            {
                                SafeAction(() => UpdatePreview());
                            }
                            catch
                            {
                            }
                        };
                        _droidEncPrev.PreviewAvailable += _encoderPreviewInvalidateHandler;
                    }

                    Debug.WriteLine(
                        useDualStreamPreview
                            ? "[StartRealtimeVideoProcessing] GPU camera session created (dual-stream test mode, UseRecordingFramesForPreview=false)"
                            : "[StartRealtimeVideoProcessing] GPU camera session created (single-stream, UseRecordingFramesForPreview=true)");
                }
                else
                {
                    Debug.WriteLine($"[StartRealtimeVideoProcessing] GPU surface is null, falling back to legacy path");
                    useGpuCameraPath = false;
                }
            }

            // LEGACY PATH: Use preview callback with SKBitmap
            if (!useGpuCameraPath)
            {
                _androidPreviewHandler = async (captured) =>
                {
                    try
                    {
                        // Track camera input FPS (count every frame camera delivers)
                        CalculateCameraInputFps();

                        // CRITICAL: Process frames during BOTH pre-recording AND live recording
                        // If encoder is null/disposed during transition, this check will catch it and return gracefully
                        if ((!IsPreRecording && !IsRecording) || _captureVideoEncoder is not AndroidCaptureVideoEncoder droidEnc)
                            return;

                        // Warmup: drop the first frame to avoid occasional corrupted first frame artifacts
                        if (System.Threading.Volatile.Read(ref _androidWarmupDropRemaining) > 0)
                        {
                            System.Threading.Interlocked.Decrement(ref _androidWarmupDropRemaining);
                            System.Threading.Interlocked.Increment(ref _diagDroppedFrames);
                            return;
                        }

                        // Ensure single-frame processing
                        if (System.Threading.Interlocked.CompareExchange(ref _androidFrameGate, 1, 0) != 0)
                        {
                            System.Threading.Interlocked.Increment(ref _diagDroppedFrames);
                            return;
                        }

                        // CRITICAL FIX: Use wall clock time from recording start, not camera monotonic timestamps
                        // captured.Time is a monotonic timestamp that continues from camera session start
                        // If camera ran in preview mode before recording, captured.Time is already at high value
                        // We need PTS relative to when recording STARTED (wall clock), not first frame arrival
                        var elapsedLocal = DateTime.Now - _captureVideoStartTime;
                        if (elapsedLocal.Ticks < 0)
                            elapsedLocal = TimeSpan.Zero;

                        try
                        {
                            // Fire ML hook — raw SKImage before BeginFrame / FrameProcessor overlays
                            var rawImg = captured?.Image;
                            if (rawImg != null)
                                OnRawFrameAcquired(rawImg, DeviceRotation);

                            using (droidEnc.BeginFrame(elapsedLocal, out var canvas, out var info))
                            {
                                var img = captured?.Image;
                                if (img == null)
                                    return;

                                var rects = GetAspectFillRects(img.Width, img.Height, info.Width, info.Height);

                                // Apply 180° flip for selfie camera in landscape (sensor is opposite orientation)
                                var isSelfieInLandscape = false && (RecordingLockedRotation == 90 || RecordingLockedRotation == 270);
                                if (isSelfieInLandscape)
                                {
                                    canvas.Save();
                                    canvas.Scale(-1, -1, info.Width / 2f, info.Height / 2f);
                                }

                                canvas.DrawImage(img, rects.src, rects.dst);

                                if (isSelfieInLandscape)
                                {
                                    canvas.Restore();
                                }

                                if (FrameProcessor != null || VideoDiagnosticsOn)
                                {
                                    var rotation = GetActiveRecordingRotation();
                                    var needsCheckpoint = FrameProcessor != null || (VideoDiagnosticsOn && rotation != 0);
                                    var checkpoint = 0;

                                    if (needsCheckpoint)
                                    {
                                        // Keep one isolated scope for callback and diagnostics, while avoiding work
                                        // for diagnostics-only frames when no rotation transform is needed.
                                        checkpoint = canvas.Save();
                                        ApplyCanvasRotation(canvas, info.Width, info.Height, rotation);
                                    }

                                    if (FrameProcessor != null)
                                    {
                                        var (frameWidth, frameHeight) = GetRotatedDimensions(info.Width, info.Height, rotation);
                                        var frame = new DrawableFrame
                                        {
                                            Width = frameWidth,
                                            Height = frameHeight,
                                            Canvas = canvas,
                                            Time = elapsedLocal,
                                            Scale = 1f
                                        };
                                        FrameProcessor.Invoke(frame);
                                    }

                                    if (VideoDiagnosticsOn)
                                        DrawDiagnostics(canvas, info.Width, info.Height);

                                    if (needsCheckpoint)
                                        canvas.RestoreToCount(checkpoint);
                                }
                            }

                            var __sw = System.Diagnostics.Stopwatch.StartNew();
                            await droidEnc.SubmitFrameAsync();
                            __sw.Stop();
                            _diagLastSubmitMs = __sw.Elapsed.TotalMilliseconds;
                            System.Threading.Interlocked.Increment(ref _diagSubmittedFrames);
                            CalculateRecordingFps();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[CaptureFrame/Event] Error: {ex.Message}");
                        }
                        finally
                        {
                            System.Threading.Volatile.Write(ref _androidFrameGate, 0);
                        }
                    }
                    catch
                    {
                    }
                };

                androidCam.PreviewCaptureSuccess = _androidPreviewHandler;
            }
        }

        // Progress reporting
        _captureVideoEncoder.ProgressReported += (sender, duration) =>
        {
            OnRecordingProgress(duration);
        };

    }

    /// <summary>
    /// Transitions from pre-recording to live recording using the 2-encoder overlap pattern.
    ///
    /// The key insight: a FRESH MediaCodec/muxer session always produces an IDR as its very first
    /// encoded output. By creating a new live encoder instead of reusing the pre-rec encoder, we
    /// guarantee an IDR on the first live frame with zero keyframe wait (~0 ms gap).
    ///
    /// Old pre-rec encoder: redirected to write its buffer to _preRecordingFilePath in background.
    /// New live encoder:    initialized with a temp live path (_liveRecordingFilePath).
    /// At stop time: MuxVideosInternal concatenates both files into the final output.
    /// </summary>
    private async Task TransitionFromPreRecToLiveRecording()
    {
        Debug.WriteLine("[TransitionFromPreRecToLiveRecording] Starting 2-encoder handoff...");

        var oldEncoder = _captureVideoEncoder as AndroidCaptureVideoEncoder;
        if (oldEncoder == null)
        {
            Debug.WriteLine("[TransitionFromPreRecToLiveRecording] ERROR: No old encoder - falling back to StartAsync");
            await _captureVideoEncoder.StartAsync();
            return;
        }

        // ── Save final output path and redirect old encoder to pre-rec temp file ──
        _pendingFinalOutputPath = oldEncoder.OutputPath;

        if (!string.IsNullOrEmpty(_preRecordingFilePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_preRecordingFilePath)!);
            if (File.Exists(_preRecordingFilePath))
            {
                try { File.Delete(_preRecordingFilePath); } catch { }
            }
            oldEncoder.OverrideOutputPath(_preRecordingFilePath);
            Debug.WriteLine($"[TransitionFromPreRecToLiveRecording] Old encoder redirected → {_preRecordingFilePath}");
        }

        // ── Compute live temp output path for new encoder ──
        var ctx = Android.App.Application.Context;
        var cacheDir = ctx.CacheDir?.AbsolutePath ?? FileSystem.CacheDirectory;
        _liveRecordingFilePath = Path.Combine(cacheDir, $"live_rec_{Guid.NewGuid()}.mp4");

        // ── Create and initialize new live encoder ──
        var newEncoder = new AndroidCaptureVideoEncoder();
        newEncoder.ParentCamera = this;
        newEncoder.IsPreRecordingMode = false;  // live mode → StartAsync creates muxer directly

        var audioEnabled = EnableAudioRecording;
        await newEncoder.InitializeAsync(_liveRecordingFilePath, _diagEncWidth, _diagEncHeight, _targetFps, audioEnabled, RecordingLockedRotation);

        // Audio: live encoder accepts samples directly (no circular pre-rec buffer)
        _audioBuffer = null;
        newEncoder.SetAudioBuffer(null);

        // ── Wire frame pipeline to new encoder ──
        if (_usingGpuCameraPath && NativeControl is NativeCamera androidCam)
        {
            // GPU path: camera surface physically bound to old encoder's EGL context.
            // We must stop the old GPU session, init a new EGL context on the new encoder,
            // then create a new GPU camera session targeting the new encoder's surface.
            bool isFrontCamera = false;
            bool newGpuOk = newEncoder.InitializeGpuCameraPath(isFrontCamera, _cameraRawWidth, _cameraRawHeight);
            var newGpuSurface = newGpuOk ? newEncoder.GpuFrameProvider?.GetCameraOutputSurface() : null;

            if (newGpuOk && newGpuSurface != null)
            {
                // Subscribe frame-available callback for new encoder BEFORE swapping camera session
                newEncoder.GpuFrameProvider.Renderer.OnFrameAvailable += (sender, surfaceTexture) =>
                {
                    CalculateCameraInputFps();
                    if ((!IsPreRecording && !IsRecording) || _captureVideoEncoder is not AndroidCaptureVideoEncoder droidEnc)
                        return;
                    var elapsed = DateTime.Now - _captureVideoStartTime;
                    if (elapsed.Ticks < 0) elapsed = TimeSpan.Zero;
                    droidEnc.SignalGpuFrame(elapsed, FrameProcessor, VideoDiagnosticsOn, DrawDiagnostics);
                };

                newEncoder.OnGpuFrameProcessed += () =>
                {
                    System.Threading.Interlocked.Increment(ref _diagSubmittedFrames);
                    CalculateRecordingFps();
                };

                if (MirrorRecordingToPreview)
                {
                    _encoderPreviewInvalidateHandler = (s, e) =>
                    {
                        try { SafeAction(() => UpdatePreview()); }
                        catch { }
                    };
                    newEncoder.PreviewAvailable += _encoderPreviewInvalidateHandler;
                }

                // Start new encoder's muxer (fast – no pre-rec buffer to flush)
                await newEncoder.StartAsync();

                // Swap _captureVideoEncoder so frame/audio callbacks route to new encoder
                _captureVideoEncoder = newEncoder;
                _usingGpuCameraPath = true;

                // Disconnect camera from old encoder, reconnect to new encoder's surface
                androidCam.StopGpuCameraSession();
                newEncoder.GpuFrameProvider.Start();
                androidCam.CreateGpuCameraSession(newGpuSurface, _targetFps);

                Debug.WriteLine("[TransitionFromPreRecToLiveRecording] GPU path: camera re-routed to new encoder");
            }
            else
            {
                // GPU init failed on new encoder – fall back to legacy path
                Debug.WriteLine("[TransitionFromPreRecToLiveRecording] GPU init failed on new encoder, switching to legacy path");
                newEncoder.DisposeGpuCameraPath();
                androidCam.StopGpuCameraSession();
                _usingGpuCameraPath = false;

                // Start new encoder on legacy path
                _captureVideoEncoder = newEncoder;
                await newEncoder.StartAsync();
            }
        }
        else
        {
            // Legacy CPU path: _captureVideoEncoder field is read by the frame callback each call.
            // Start the muxer BEFORE swapping so audio samples don't get dropped during the handoff window
            // (WriteAudio returns early while !_isRecording && !IsPreRecordingMode).
            // StartAsync on a fresh encoder (no pre-rec buffer) is very fast (< 10ms).
            await newEncoder.StartAsync();

            // Now swap: next frame callback call routes video+audio to new encoder.
            // Since no frames have been submitted to newEncoder yet, the codec still produces
            // an IDR as its very first output – no _waitForFirstLiveKeyframe needed.
            _captureVideoEncoder = newEncoder;
            _androidWarmupDropRemaining = 1;  // drop first frame to avoid warmup artifacts

            Debug.WriteLine("[TransitionFromPreRecToLiveRecording] Legacy path: new encoder active");
        }

        // Do NOT reset _captureVideoStartTime — it keeps counting from when pre-recording started.
        // This ensures frame.Time / DrawableFrame.Time shows accumulated time (e.g. 3s pre-rec → 3s at transition start).
        // CaptureStartAbsoluteNs is updated for the new encoder's audio/video clock alignment only.
        if (_captureVideoEncoder is AndroidCaptureVideoEncoder droidEncBridge)
            droidEncBridge.CaptureStartAbsoluteNs = Super.GetCurrentTimeNanos();
        _capturePtsBaseTime = null;

        // Progress reporting for new encoder
        _captureVideoEncoder.ProgressReported += (sender, duration) =>
        {
            OnRecordingProgress(duration);
        };

        // ── Flush old pre-rec encoder's buffered segment to disk in background ──
        // FlushPreRecBufferAsync now drains any already-submitted tail frames before writing the
        // temp file. That preserves continuity at the seam without blocking the live encoder start.
        _preRecFlushTask = Task.Run(async () =>
        {
            try
            {
                // Give the old encoder a brief head start so the first tail outputs are available
                // when the drain loop begins on slower devices.
                await Task.Delay(150);
                Debug.WriteLine("[TransitionFromPreRecToLiveRecording] BkTask: flushing pre-rec buffer with tail drain...");
                await oldEncoder.FlushPreRecBufferAsync();
                Debug.WriteLine($"[TransitionFromPreRecToLiveRecording] BkTask: buffer written to {_preRecordingFilePath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TransitionFromPreRecToLiveRecording] BkTask: error flushing buffer: {ex.Message}");
            }
            finally
            {
                try { oldEncoder.Dispose(); }
                catch { }
            }
        });

        Debug.WriteLine("[TransitionFromPreRecToLiveRecording] Handoff complete – old encoder flushing in background");
    }

    /// <summary>
    /// Start video recording. Run this in background thread!
    /// Locks the device rotation for the entire recording session.
    /// Uses either native video recording or capture video flow depending on UseRealtimeVideoProcessing setting.
    /// 
    /// State machine logic:
    /// - If EnablePreRecording && !IsPreRecording: Start memory-only recording (pre-recording phase)
    /// - If IsPreRecording && !IsRecording: Prepend buffer and start file recording (normal phase)
    /// - Otherwise: Start normal file recording
    /// </summary>
    /// <returns>Async task</returns>
    public async Task StartVideoRecording() //OK
    {
        if (IsBusy)
        {
            Debug.WriteLine($"[StartVideoRecording] IsBusy cannot start");
            return;
        }

        // Handle audio-only recording (EnableVideoRecording=false)
        if (!EnableVideoRecording)
        {
            if (!EnableAudioRecording)
                throw new InvalidOperationException("EnableAudioRecording must be true when EnableVideoRecording is false");
            await StartAudioOnlyRecording();
            return;
        }

        Debug.WriteLine($"[StartVideoRecording] IsMainThread {MainThread.IsMainThread}, IsPreRecording={IsPreRecording}, IsRecording={IsRecording}");

        try
        {
            // State 1 -> State 2: If pre-recording enabled and not yet in pre-recording phase, start memory-only recording
            if (EnablePreRecording && !IsPreRecording && !IsRecording)
            {
                Debug.WriteLine("[StartVideoRecording] Transitioning to IsPreRecording (memory-only recording)");
                SetIsPreRecording(true);
                InitializePreRecordingBuffer();

                // Lock the current device rotation for the entire recording session
                RecordingLockedRotation = DeviceRotation;
                Debug.WriteLine($"[StartVideoRecording] Locked rotation at {RecordingLockedRotation}°");

                // Start recording in memory-only mode
                if (UseRealtimeVideoProcessing && FrameProcessor != null)
                {
                    await StartRealtimeVideoProcessing();
                }
                else
                {
                    await StartNativeVideoRecording();
                }
            }
            // State 2 -> State 3: Transition from pre-recording to live recording
            else if (IsPreRecording && !IsRecording)
            {
                bool useTwoEncoderTransition = AndroidCameraExperimentalFlags.IsTwoEncoderPrerecordTransitionEnabled();
                Debug.WriteLine(
                    useTwoEncoderTransition
                        ? "[StartVideoRecording] Transitioning from IsPreRecording to IsRecording (2-encoder handoff)"
                        : "[StartVideoRecording] Transitioning from IsPreRecording to IsRecording (single-encoder deferred-flush transition)");

                // Change states before switching the encoder mode so frame callbacks continue to accept
                // frames throughout the transition window.
                SetIsPreRecording(false);
                SetIsRecordingVideo(true);
                RecordingLockedRotation = DeviceRotation;
                Debug.WriteLine($"[StartVideoRecording] Locked rotation at {RecordingLockedRotation}°");

                if (_captureVideoEncoder != null)
                {
                    if (useTwoEncoderTransition)
                    {
                        // Experimental path: creates a fresh live encoder while the old pre-rec encoder
                        // flushes in the background. This is faster on some devices but can create a seam
                        // when the GPU camera session takes too long to rebind.
                        await TransitionFromPreRecToLiveRecording();
                        Debug.WriteLine("[StartVideoRecording] 2-encoder handoff complete");
                    }
                    else
                    {
                        // Default Android path: keep the same warmed encoder and current GPU camera session.
                        // Start writing only live frames to a temp file now, and defer serializing the
                        // pre-record buffer until stop time to avoid the transition spike.
                        _pendingFinalOutputPath = (_captureVideoEncoder as AndroidCaptureVideoEncoder)?.OutputPath;
                        var ctx = Android.App.Application.Context;
                        var cacheDir = ctx.CacheDir?.AbsolutePath ?? FileSystem.CacheDirectory;
                        _liveRecordingFilePath = Path.Combine(cacheDir, $"live_rec_{Guid.NewGuid()}.mp4");

                        if (_captureVideoEncoder is AndroidCaptureVideoEncoder androidEncoder)
                        {
                            await androidEncoder.StartLiveRecordingOnlyAsync(_liveRecordingFilePath);
                            Debug.WriteLine("[StartVideoRecording] Single-encoder deferred-flush transition complete");
                        }
                        else
                        {
                            await _captureVideoEncoder.StartAsync();
                            Debug.WriteLine("[StartVideoRecording] Single-encoder fallback transition complete");
                        }
                    }
                }
                else
                {
                    Debug.WriteLine("[StartVideoRecording] ERROR: No encoder found for transition!");
                }

            }
            // Normal recording (no pre-recording)
            else if (!IsRecording)
            {
                Debug.WriteLine("[StartVideoRecording] Starting normal recording (no pre-recording)");
                SetIsRecordingVideo(true);

                // Lock the current device rotation for the entire recording session
                RecordingLockedRotation = DeviceRotation;
                Debug.WriteLine($"[StartVideoRecording] Locked rotation at {RecordingLockedRotation}°");

                if (UseRealtimeVideoProcessing && FrameProcessor != null)
                {
                    await StartRealtimeVideoProcessing();
                }
                else
                {
                    await StartNativeVideoRecording();
                }
            }
        }
        catch (Exception ex)
        {
            SetIsRecordingVideo(false);
            SetIsPreRecording(false);
            IsBusy = false;
            RecordingLockedRotation = -1; // Reset on error
            ClearPreRecordingBuffer();
            RecordingFailed?.Invoke(this, ex);
            throw;
        }

        IsBusy = false;
    }

    protected IAudioCapture CreateAudioCapturePlatform()
    {
        return new AudioCaptureAndroid();
    }

    protected async Task<List<string>> GetAvailableAudioDevicesPlatform()
    {
        var devices = new List<string>();
        // Android creates AudioRecord with AudioSource.Mic usually. 
        // Enumerating devices requires API 23+ (Marshmallow).
        if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.M)
        {
             var audioManager = (AudioManager)Android.App.Application.Context.GetSystemService(Context.AudioService);
             var inputs = audioManager.GetDevices(GetDevicesTargets.Inputs);
             foreach(var device in inputs)
             {
                 devices.Add($"{device.ProductName} ({device.Type})");
             }
        }
        else
        {
            devices.Add("Default Microphone");
        }
        return devices;
    }

    protected async Task<List<string>> GetAvailableAudioCodecsPlatform()
    {
        return await Task.Run(() => 
        {
            var codecs = new List<string>();
            try
            {
                // Using MediaCodecList to find encoders
                var list = new MediaCodecList(MediaCodecListKind.RegularCodecs);
                var formats = list.GetCodecInfos();
                foreach(var info in formats)
                {
                    if (info.IsEncoder) 
                    {
                         foreach(var type in info.GetSupportedTypes())
                         {
                             if (type.StartsWith("audio/"))
                             {
                                 codecs.Add($"{info.Name} ({type})");
                             }
                         }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SkiaCameraAndroid] Error listing codecs: {ex}");
            }
            return codecs.Distinct().ToList();
        });
    }

    #region Preview Audio Capture

    private bool _resumePreviewAudioAfterNativeRecording;

    internal void OnAndroidNativeRecordingWillStart()
    {
        _resumePreviewAudioAfterNativeRecording = _previewAudioCapture != null;

        if (_resumePreviewAudioAfterNativeRecording)
        {
            StopPreviewAudioCapture();
        }
    }

    internal void OnAndroidNativeRecordingDidEnd()
    {
        if (!_resumePreviewAudioAfterNativeRecording)
            return;

        _resumePreviewAudioAfterNativeRecording = false;

        if (State == HardwareState.On && ((CaptureMode == CaptureModeType.Video && EnableAudioRecording) || EnableAudioMonitoring))
        {
            StartPreviewAudioCapture();
        }
    }

    private void OnPreviewAudioSampleAvailable(object sender, AudioSample sample)
    {
        OnPreviewAudioSampleAvailable(sample);
    }

    private SemaphoreSlim _audioSemaphore = new(1, 1);

    public override void OnDisposing()
    {
        _audioSemaphore?.Dispose();
        _audioSemaphore = null;
    }


    partial void StartPreviewAudioCapture()
    {
        // Start audio capture if either recording audio or audio monitoring is enabled
        if (_previewAudioCapture != null || (!EnableAudioRecording && !EnableAudioMonitoring))
            return;

        Task.Run(async () =>
        {
            if (!await _audioSemaphore.WaitAsync(1)) // Skip if busy processing
                return;

            try
            {
                StopPreviewAudioCapture();

                _previewAudioCapture = new AudioCaptureAndroid();
                _previewAudioCapture.SampleAvailable += OnPreviewAudioSampleAvailable;
                var started = await _previewAudioCapture.StartAsync(AudioSampleRate, AudioChannels, AudioBitDepth, AudioDeviceIndex);
                if (started)
                {
                    Debug.WriteLine($"[SkiaCamera.Android] Preview audio capture started: {_previewAudioCapture.SampleRate}Hz, {_previewAudioCapture.Channels}ch");
                }
                else
                {
                    Debug.WriteLine("[SkiaCamera.Android] Preview audio capture failed to start");
                    _previewAudioCapture.SampleAvailable -= OnPreviewAudioSampleAvailable;
                    _previewAudioCapture.Dispose();
                    _previewAudioCapture = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SkiaCamera.Android] Preview audio capture error: {ex.Message}");
            }
            finally
            {
                _audioSemaphore?.Release();
            }
        });
    }

    partial void StopPreviewAudioCapture()
    {
        if (_previewAudioCapture == null)
            return;

        try
        {
            _previewAudioCapture.SampleAvailable -= OnPreviewAudioSampleAvailable;
            var kill = _previewAudioCapture;
            _ = kill.StopAsync().ContinueWith(_ =>
            {
                kill.Dispose();
            });

            _previewAudioCapture = null;
            Debug.WriteLine("[SkiaCamera.Android] Preview audio capture stopped");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SkiaCamera.Android] Error stopping preview audio: {ex.Message}");
        }
    }

    #endregion

    #region AUDIO-ONLY RECORDING

    private IAudioCapture _audioOnlyCapture;

    private partial void CreateAudioOnlyEncoder(out IAudioOnlyEncoder encoder)
    {
        encoder = new AudioOnlyEncoderAndroid();
    }

    private partial void StartAudioOnlyCapture(int sampleRate, int channels, out Task task)
    {
        var tcs = new TaskCompletionSource<bool>();
        task = tcs.Task;

        Task.Run(async () =>
        {
            try
            {
                // Stop preview audio capture first
                StopPreviewAudioCapture();

                _audioOnlyCapture = new AudioCaptureAndroid();
                _audioOnlyCapture.SampleAvailable += OnAudioOnlySampleAvailable;
                var started = await _audioOnlyCapture.StartAsync(sampleRate, channels, AudioBitDepth, AudioDeviceIndex);
                if (started)
                {
                    Debug.WriteLine($"[SkiaCamera.Android] Audio-only capture started: {_audioOnlyCapture.SampleRate}Hz, {_audioOnlyCapture.Channels}ch");
                }
                else
                {
                    Debug.WriteLine("[SkiaCamera.Android] Audio-only capture failed to start");
                    _audioOnlyCapture.SampleAvailable -= OnAudioOnlySampleAvailable;
                    _audioOnlyCapture.Dispose();
                    _audioOnlyCapture = null;
                }
                tcs.TrySetResult(started);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SkiaCamera.Android] Audio-only capture error: {ex.Message}");
                tcs.TrySetException(ex);
            }
        });
    }

    private partial void StopAudioOnlyCapture(out Task task)
    {
        var tcs = new TaskCompletionSource<bool>();
        task = tcs.Task;

        if (_audioOnlyCapture == null)
        {
            tcs.TrySetResult(true);
            return;
        }

        Task.Run(async () =>
        {
            try
            {
                _audioOnlyCapture.SampleAvailable -= OnAudioOnlySampleAvailable;
                await _audioOnlyCapture.StopAsync();
                _audioOnlyCapture.Dispose();
                _audioOnlyCapture = null;
                Debug.WriteLine("[SkiaCamera.Android] Audio-only capture stopped");
                tcs.TrySetResult(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SkiaCamera.Android] Error stopping audio-only capture: {ex.Message}");
                tcs.TrySetException(ex);
            }
        });
    }

    #endregion

    #region ML Frame Helper

    protected partial bool TryGetMLFrame(SKImage rawImage, int targetWidth, int targetHeight, byte[] outputBuffer)
    {
        // GPU path: rawImage is null, raw frame is in GL FBO 0 — delegate to encoder's GPU scaler.
        // MUST be called synchronously from OnRawFrameAcquired while EGL context is current.
        if (rawImage == null)
        {
            if (_captureVideoEncoder is AndroidCaptureVideoEncoder droidEnc)
                return droidEnc.TryScaleRawFrameForML(targetWidth, targetHeight, outputBuffer);
            return false;
        }

        // Legacy path: rawImage is a CPU-raster SKImage — scale via CPU Skia surface.
        int required = targetWidth * targetHeight * 4;
        if (outputBuffer == null || outputBuffer.Length < required)
            return false;

        var info = new SKImageInfo(targetWidth, targetHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        if (surface == null)
            return false;

        using var paint = new SKPaint { FilterQuality = SKFilterQuality.Low };
        var dst = new SKRect(0, 0, targetWidth, targetHeight);
        surface.Canvas.DrawImage(rawImage, dst, paint);
        surface.Canvas.Flush();

        using var snapshot = surface.Snapshot();
        if (snapshot == null)
            return false;

        var handle = System.Runtime.InteropServices.GCHandle.Alloc(outputBuffer, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            return snapshot.ReadPixels(info, handle.AddrOfPinnedObject(), targetWidth * 4, 0, 0);
        }
        finally
        {
            handle.Free();
        }
    }

    #endregion
}
