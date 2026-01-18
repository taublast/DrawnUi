using System.Diagnostics;
using Android.Content;
using Android.Hardware.Camera2;
using Android.Media;
using Android.Telecom;
using Microsoft.Maui.Controls.PlatformConfiguration;


namespace DrawnUi.Camera;

public partial class SkiaCamera
{
    /// <summary>
    /// Pre-allocated shared buffer for pre-recording.
    /// Allocated once when EnablePreRecording=true, reused across recording sessions.
    /// Eliminates ~27MB allocation lag spike when pressing record.
    /// </summary>
    private PrerecordingEncodedBuffer _sharedPreRecordingBuffer;

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

            for (int i = 0; i < cameraIds.Length; i++)
            {
                var cameraId = cameraIds[i];
                var characteristics = manager.GetCameraCharacteristics(cameraId);

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

                cameras.Add(new CameraInfo
                {
                    Id = cameraId,
                    Name = $"Camera {i} ({position})",
                    Position = position,
                    Index = i,
                    HasFlash = flashAvailable?.BooleanValue() ?? false
                });
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

    public void DisableOtherCameras(bool all = false)
    {
        foreach (var renderer in Instances)
        {
            System.Diagnostics.Debug.WriteLine($"[CAMERA] DisableOtherCameras..");
            bool disable = false;
            if (all || renderer != this)
            {
                disable = true;
            }

            if (disable)
            {
                renderer.StopInternal(true);
                System.Diagnostics.Debug.WriteLine($"[CAMERA] Stopped {renderer.Uid} {renderer.Tag}");
            }
        }
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

            // CRITICAL: Use original format directly - duration metadata doesn't cause issues with sequential writes
            int sharedVideoTrackIndex = -1;
            for (int i = 0; i < preExtractor.TrackCount; i++)
            {
                var sourceFormat = preExtractor.GetTrackFormat(i);

                sharedVideoTrackIndex = muxer.AddTrack(sourceFormat);
                preTrackMap[i] = sharedVideoTrackIndex;
                liveTrackMap[i] = sharedVideoTrackIndex;  // Both files write to same track

                preExtractor.SelectTrack(i);
                liveExtractor.SelectTrack(i);

                System.Diagnostics.Debug.WriteLine($"[MuxVideosAndroid] Added track {sharedVideoTrackIndex} - both pre-rec and live will write to same track");
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

    private void OnAudioSampleAvailable(object sender, AudioSample e)
    {
        WriteAudioSample(e);
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

            // Stop encoder and get result
            CapturedVideo capturedVideo = await encoder?.StopAsync();
 
            // ANDROID: Single-file approach - no muxing needed!
            // Encoder already wrote buffer + live frames to ONE file
            Debug.WriteLine($"[StopRealtimeVideoProcessing] Android single-file approach - no muxing needed");
            Debug.WriteLine($"[StopRealtimeVideoProcessing] Video file: {capturedVideo?.FilePath}");

            // Clean up pre-recording file if it exists (shouldn't exist with new approach)
            if (!string.IsNullOrEmpty(_preRecordingFilePath) && File.Exists(_preRecordingFilePath))
            {
                try
                {
                    File.Delete(_preRecordingFilePath);
                    Debug.WriteLine($"[StopRealtimeVideoProcessing] Deleted old pre-recording temp file");
                }
                catch { }
            }
            ClearPreRecordingBuffer();


            if (capturedVideo != null)
            {
                OnVideoRecordingSuccess(capturedVideo);
            }

            // Update state and notify success
            SetIsRecordingVideo(false);

            IsBusy = false; // Release busy state after successful processing
        }
        catch (Exception ex)
        {
            // Clean up on error
            _frameCaptureTimer?.Dispose();
            _frameCaptureTimer = null;
            _captureVideoEncoder = null;

            SetIsRecordingVideo(false);
            IsBusy = false; // Release busy state on error
            VideoRecordingFailed?.Invoke(this, ex);
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

            if (!string.IsNullOrEmpty(_preRecordingFilePath) && File.Exists(_preRecordingFilePath))
            {
                try
                {
                    File.Delete(_preRecordingFilePath);
                    Debug.WriteLine($"[StopRealtimeVideoProcessing] Deleted old pre-recording temp file");
                }
                catch { }
            }
            ClearPreRecordingBuffer();

            SetIsRecordingVideo(false);
        }
        catch (Exception ex)
        {
            // Clean up on error
            _frameCaptureTimer?.Dispose();
            _frameCaptureTimer = null;
            _captureVideoEncoder = null;

            SetIsRecordingVideo(false);
            //VideoRecordingFailed?.Invoke(this, ex);
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

        // Control preview source: processed frames from encoder (PreviewVideoFlow=true) or raw camera (PreviewVideoFlow=false)
        // Only applies when UseRealtimeVideoProcessing is TRUE (enforced by caller)
        UseRecordingFramesForPreview = false;//PreviewVideoFlow;

        // Invalidate preview when the encoder publishes a new composed frame (Android mirror)
        if (PreviewVideoFlow && _captureVideoEncoder is AndroidCaptureVideoEncoder _droidEncPrev)
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
        SetSourceFrameDimensions(width, height);

        // Pass locked rotation to encoder for proper video orientation metadata (Android-specific)
        var audioEnabled = RecordAudio;
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

            // Try to initialize GPU camera path for zero-copy frame capture
            if (GpuCameraFrameProvider.IsSupported())
            {
                bool isFrontCamera = Facing == CameraPosition.Selfie;
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
                Debug.WriteLine($"[StartRealtimeVideoProcessing] GPU camera path not supported, using legacy SKBitmap path");
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

        // Drop the first camera frame to avoid occasional corrupted first frame from the camera/RenderScript pipeline
        _androidWarmupDropRemaining = 1;

        _captureVideoStartTime = DateTime.Now;
        _capturePtsBaseTime = null;

        // Diagnostics
        _diagStartTime = DateTime.Now;
        _diagDroppedFrames = 0;
        _diagSubmittedFrames = 0;
        _diagLastSubmitMs = 0;
        ResetRecordingFps();
        _targetFps = fps;

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

                        if ((!IsPreRecording && !IsRecordingVideo) || _captureVideoEncoder is not AndroidCaptureVideoEncoder droidEnc)
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

                        // EXIT IMMEDIATELY - camera callback complete in microseconds
                    };

                    // Subscribe to GPU frame processed event for FPS tracking
                    // This fires on the background encoding thread after each frame is successfully processed
                    gpuEncoder.OnGpuFrameProcessed += () =>
                    {
                        System.Threading.Interlocked.Increment(ref _diagSubmittedFrames);
                        CalculateRecordingFps();
                    };

                    Debug.WriteLine($"[StartRealtimeVideoProcessing] GPU camera session created");
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
                        if ((!IsPreRecording && !IsRecordingVideo) || _captureVideoEncoder is not AndroidCaptureVideoEncoder droidEnc)
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
                            using (droidEnc.BeginFrame(elapsedLocal, out var canvas, out var info))
                            {
                                var img = captured?.Image;
                                if (img == null)
                                    return;

                                var rects = GetAspectFillRects(img.Width, img.Height, info.Width, info.Height);

                                // Apply 180° flip for selfie camera in landscape (sensor is opposite orientation)
                                var isSelfieInLandscape = Facing == CameraPosition.Selfie && (RecordingLockedRotation == 90 || RecordingLockedRotation == 270);
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
                                    // Apply rotation based on device orientation
                                    var rotation = GetActiveRecordingRotation();
                                    canvas.Save();
                                    ApplyCanvasRotation(canvas, info.Width, info.Height, rotation);

                                    var (frameWidth, frameHeight) = GetRotatedDimensions(info.Width, info.Height, rotation);
                                    var frame = new DrawableFrame
                                    {
                                        Width = frameWidth,
                                        Height = frameHeight,
                                        Canvas = canvas,
                                        Time = elapsedLocal,
                                        Scale = 1f
                                    };
                                    FrameProcessor?.Invoke(frame);

                                    if (VideoDiagnosticsOn)
                                        DrawDiagnostics(canvas, info.Width, info.Height);

                                    canvas.Restore();
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
            MainThread.BeginInvokeOnMainThread(() => OnVideoRecordingProgress(duration));
        };

    }

    /// <summary>
    /// Start video recording. Run this in background thread!
    /// Locks the device rotation for the entire recording session.
    /// Uses either native video recording or capture video flow depending on UseRealtimeVideoProcessing setting.
    /// 
    /// State machine logic:
    /// - If EnablePreRecording && !IsPreRecording: Start memory-only recording (pre-recording phase)
    /// - If IsPreRecording && !IsRecordingVideo: Prepend buffer and start file recording (normal phase)
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

        Debug.WriteLine($"[StartVideoRecording] IsMainThread {MainThread.IsMainThread}, IsPreRecording={IsPreRecording}, IsRecordingVideo={IsRecordingVideo}");

        try
        {
            // State 1 -> State 2: If pre-recording enabled and not yet in pre-recording phase, start memory-only recording
            if (EnablePreRecording && !IsPreRecording && !IsRecordingVideo)
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
            // State 2 -> State 3: If in pre-recording phase, transition to file recording with muxing
            else if (IsPreRecording && !IsRecordingVideo)
            {
                Debug.WriteLine("[StartVideoRecording] Transitioning from IsPreRecording to IsRecordingVideo (file recording with mux)");

                // CRITICAL ANDROID FIX: Single-file approach - reuse existing encoder!
                // Encoder was already initialized and warmed up during pre-recording phase
                // Just call StartAsync() to write buffer + continue with live frames in same muxer session
 
                // Change states
                SetIsPreRecording(false);
                SetIsRecordingVideo(true);
                RecordingLockedRotation = DeviceRotation;
                Debug.WriteLine($"[StartVideoRecording] Locked rotation at {RecordingLockedRotation}°");

                // CRITICAL: Reuse existing encoder (single-file pattern)
                // This will write buffer to muxer, then continue with live frames in same session
                if (_captureVideoEncoder != null)
                {
                    Debug.WriteLine("[StartVideoRecording] ========================================");
                    Debug.WriteLine("[StartVideoRecording] SINGLE-FILE PATTERN (professional)");
                    Debug.WriteLine("[StartVideoRecording] Reusing pre-warmed encoder for live recording");
                    Debug.WriteLine("[StartVideoRecording] No encoder recreation = zero frame loss!");
                    Debug.WriteLine("[StartVideoRecording] Buffer already has keyframes from periodic requests");
                    Debug.WriteLine("[StartVideoRecording] ========================================");

                    await _captureVideoEncoder.StartAsync();

                    Debug.WriteLine("[StartVideoRecording] Encoder transitioned to live recording mode");
                    Debug.WriteLine("[StartVideoRecording] Buffer written, live frames continuing in same muxer");
                }
                else
                {
                    Debug.WriteLine("[StartVideoRecording] ERROR: No encoder found for transition!");
                }
 
            }
            // Normal recording (no pre-recording)
            else if (!IsRecordingVideo)
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
            VideoRecordingFailed?.Invoke(this, ex);
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
}
