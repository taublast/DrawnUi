using Android.Content;
using Android.Hardware.Camera2;
using Android.Media;
using Android.Telecom;
using Microsoft.Maui.Controls.PlatformConfiguration;


namespace DrawnUi.Camera;

public partial class SkiaCamera
{
    
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

    /// <summary>
    /// Mux pre-recorded and live video files using MediaMuxer
    /// CRITICAL FIX: Must add ALL tracks from BOTH files BEFORE calling muxer.Start()
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



}
