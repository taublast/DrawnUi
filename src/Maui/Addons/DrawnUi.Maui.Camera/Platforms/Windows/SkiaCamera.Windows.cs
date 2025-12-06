using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace DrawnUi.Camera;

public partial class SkiaCamera : SkiaControl
{
  
    public virtual void SetZoom(double value)
    {
        TextureScale = value;
        NativeControl?.SetZoom((float)value);

        if (Display != null)
        {
            Display.ZoomX = TextureScale;
            Display.ZoomY = TextureScale;
        }

        Zoomed?.Invoke(this, value);
    }

    public static void OpenFileInGallery(string imageFilePath)
    {
        Task.Run(async () =>
        {
            try
            {
                if (string.IsNullOrEmpty(imageFilePath) || !File.Exists(imageFilePath))
                {
                    Debug.WriteLine($"[SkiaCamera Windows] File not found: {imageFilePath}");
                    return;
                }

                var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(imageFilePath);
                var success = await Windows.System.Launcher.LaunchFileAsync(file);

                if (!success)
                {
                    Debug.WriteLine($"[SkiaCamera Windows] Failed to launch file: {imageFilePath}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SkiaCamera Windows] Error opening file in gallery: {ex.Message}");
            }
        });
    }

    public virtual Metadata CreateMetadata()
    {
        return new Metadata()
        {
            Software = "SkiaCamera Windows",
            Vendor = Environment.MachineName,
            Model = Environment.OSVersion.ToString(),
        };
    }

    protected virtual void CreateNative()
    {
        if (!IsOn || NativeControl != null)
        {
            Debug.WriteLine($"[SkiaCameraWindows] CreateNative skipped - IsOn: {IsOn}, NativeControl exists: {NativeControl != null}");
            return;
        }

        Debug.WriteLine("[SkiaCameraWindows] Creating native camera...");
        NativeControl = new NativeCamera(this);
        Debug.WriteLine("[SkiaCameraWindows] Native camera created");

        NativeControl?.ApplyDeviceOrientation(DeviceRotation);
    }

    protected async Task<List<CameraInfo>> GetAvailableCamerasPlatform(bool refresh)
    {
        var cameras = new List<CameraInfo>();

        try
        {
            var devices = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(Windows.Devices.Enumeration.DeviceClass.VideoCapture);

            for (int i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                var position = CameraPosition.Default;

                if (device.EnclosureLocation?.Panel != null)
                {
                    position = device.EnclosureLocation.Panel switch
                    {
                        Windows.Devices.Enumeration.Panel.Front => CameraPosition.Selfie,
                        Windows.Devices.Enumeration.Panel.Back => CameraPosition.Default,
                        _ => CameraPosition.Default
                    };
                }

                cameras.Add(new CameraInfo
                {
                    Id = device.Id,
                    Name = device.Name,
                    Position = position,
                    Index = i,
                    HasFlash = false // TODO: Detect flash support
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SkiaCameraWindows] Error enumerating cameras: {ex.Message}");
        }

        return cameras;
    }

    protected async Task<List<CaptureFormat>> GetAvailableCaptureFormatsPlatform()
    {
        var formats = new List<CaptureFormat>();

        try
        {
            var devices = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(Windows.Devices.Enumeration.DeviceClass.VideoCapture);

            // Find current camera device or use default
            Windows.Devices.Enumeration.DeviceInformation currentDevice = null;
            
            // Manual camera selection
            if (Facing == CameraPosition.Manual && CameraIndex >= 0 && CameraIndex < devices.Count)
            {
                currentDevice = devices[CameraIndex];
                Debug.WriteLine($"[SkiaCameraWindows] Manual camera selection: Index {CameraIndex}, Device: {currentDevice.Name}");
            }
            else
            {
                // Automatic selection based on facing
                currentDevice = devices.FirstOrDefault(d =>
                    (Facing == CameraPosition.Selfie && d.EnclosureLocation?.Panel == Windows.Devices.Enumeration.Panel.Front) ||
                    (Facing == CameraPosition.Default && d.EnclosureLocation?.Panel == Windows.Devices.Enumeration.Panel.Back))
                    ?? devices.FirstOrDefault();
                Debug.WriteLine($"[SkiaCameraWindows] Automatic camera selection: Facing {Facing}, Device: {currentDevice?.Name ?? "None"}");
            }

            if (currentDevice != null)
            {
                var frameSourceGroups = await Windows.Media.Capture.Frames.MediaFrameSourceGroup.FindAllAsync();
                var selectedGroup = frameSourceGroups.FirstOrDefault(g =>
                    g.SourceInfos.Any(si => si.DeviceInformation?.Id == currentDevice.Id));

                if (selectedGroup != null)
                {
                    using var mediaCapture = new Windows.Media.Capture.MediaCapture();
                    var settings = new Windows.Media.Capture.MediaCaptureInitializationSettings
                    {
                        SourceGroup = selectedGroup,
                        SharingMode = Windows.Media.Capture.MediaCaptureSharingMode.SharedReadOnly,
                        StreamingCaptureMode = Windows.Media.Capture.StreamingCaptureMode.Video,
                        MemoryPreference = Windows.Media.Capture.MediaCaptureMemoryPreference.Cpu
                    };

                    await mediaCapture.InitializeAsync(settings);

                    var frameSource = mediaCapture.FrameSources.Values.FirstOrDefault(s =>
                        s.Info.MediaStreamType == Windows.Media.Capture.MediaStreamType.VideoRecord);

                    if (frameSource?.SupportedFormats != null)
                    {
                        // Get unique resolutions (remove duplicates from different pixel formats)
                        var uniqueResolutions = frameSource.SupportedFormats
                            .Where(f => f.VideoFormat.Width > 0 && f.VideoFormat.Height > 0)
                            .GroupBy(f => new { f.VideoFormat.Width, f.VideoFormat.Height })
                            .Select(group => group.First())
                            .OrderByDescending(f => f.VideoFormat.Width * f.VideoFormat.Height)
                            .ToList();

                        Debug.WriteLine($"[SkiaCameraWindows] Found {uniqueResolutions.Count} unique video formats:");

                        for (int i = 0; i < uniqueResolutions.Count; i++)
                        {
                            var format = uniqueResolutions[i];
                            Debug.WriteLine($"  [{i}] {format.VideoFormat.Width}x{format.VideoFormat.Height}");

                            formats.Add(new CaptureFormat
                            {
                                Width = (int)format.VideoFormat.Width,
                                Height = (int)format.VideoFormat.Height,
                                FormatId = $"windows_{currentDevice.Id}_{i}"
                            });
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SkiaCameraWindows] Error getting capture formats: {ex.Message}");
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
            Debug.WriteLine($"[SkiaCameraWindows] Error getting video formats: {ex.Message}");
        }

        return formats;
    }

    /// <summary>
    /// Updates preview format to match current capture format aspect ratio
    /// </summary>
    protected virtual void UpdatePreviewFormatForAspectRatio()
    {
        if (NativeControl is NativeCamera windowsCamera)
        {
            Debug.WriteLine("[SkiaCameraWindows] Updating preview format for aspect ratio match");

            // Trigger preview format update in native camera
            Task.Run(async () =>
            {
                try
                {
                    await windowsCamera.UpdatePreviewFormatAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SkiaCameraWindows] Error updating preview format: {ex.Message}");
                }
            });
        }
    }

    /// <summary>
    /// Call on UI thread only. Called by CheckPermissions.
    /// </summary>
    /// <returns></returns>
    public async Task<bool> RequestPermissions()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        return status == PermissionStatus.Granted;
    }

    /// <summary>
    /// Mux pre-recorded and live video files using Windows Media Foundation APIs.
    /// NO FFmpeg - Windows native APIs only.
    /// </summary>
    private async Task<string> MuxVideosWindows(string preRecordedPath, string liveRecordingPath, string outputPath)
    {
        Debug.WriteLine($"[MuxVideosWindows] ========== MUXING WITH WINDOWS MEDIA FOUNDATION ==========");
        Debug.WriteLine($"[MuxVideosWindows] Pre-recording: {preRecordedPath} ({(File.Exists(preRecordedPath) ? new FileInfo(preRecordedPath).Length / 1024 : 0)} KB)");
        Debug.WriteLine($"[MuxVideosWindows] Live recording: {liveRecordingPath} ({(File.Exists(liveRecordingPath) ? new FileInfo(liveRecordingPath).Length / 1024 : 0)} KB)");
        Debug.WriteLine($"[MuxVideosWindows] Output: {outputPath}");

        global::Windows.Win32.Media.MediaFoundation.IMFSourceReader? reader1 = null;
        global::Windows.Win32.Media.MediaFoundation.IMFSourceReader? reader2 = null;
        global::Windows.Win32.Media.MediaFoundation.IMFSinkWriter? writer = null;

        try
        {
            // Create source readers for both input files
            unsafe
            {
                fixed (char* p1 = preRecordedPath)
                {
                    var hr = PInvoke.MFCreateSourceReaderFromURL(new PCWSTR(p1), null, out reader1);
                    if (hr.Failed)
                        throw new InvalidOperationException($"MFCreateSourceReaderFromURL failed for pre-rec: 0x{hr.Value:X8}");
                }

                fixed (char* p2 = liveRecordingPath)
                {
                    var hr = PInvoke.MFCreateSourceReaderFromURL(new PCWSTR(p2), null, out reader2);
                    if (hr.Failed)
                        throw new InvalidOperationException($"MFCreateSourceReaderFromURL failed for live: 0x{hr.Value:X8}");
                }

                fixed (char* pOut = outputPath)
                {
                    var hr = PInvoke.MFCreateSinkWriterFromURL(new PCWSTR(pOut), null, null, out writer);
                    if (hr.Failed)
                        throw new InvalidOperationException($"MFCreateSinkWriterFromURL failed for output: 0x{hr.Value:X8}");
                }
            }

            // Configure sink writer based on first input file's media type
            reader1.GetCurrentMediaType(0xFFFFFFFC, out var inputMediaType); // MF_SOURCE_READER_FIRST_VIDEO_STREAM = 0xFFFFFFFC

            if (inputMediaType == null)
                throw new InvalidOperationException("Failed to get input media type");

            // Add output stream to sink writer using same media type
            writer.AddStream(inputMediaType, out var streamIndex);
            System.Runtime.InteropServices.Marshal.ReleaseComObject(inputMediaType);

            // Begin writing
            writer.BeginWriting();

            // Copy samples from first file (pre-recording)
            long lastTimestamp = await CopySamplesFromReader(reader1, writer, streamIndex, 0, "pre-rec");

            Debug.WriteLine($"[MuxVideosWindows] Pre-rec end timestamp: {lastTimestamp / 10000000.0:F2}s");

            // Copy samples from second file (live recording) with offset
            // Media Foundation resets timestamps to 0 in files, so we apply offset here during muxing
            await CopySamplesFromReader(reader2, writer, streamIndex, lastTimestamp, "live");

            // Finalize output
            writer.Finalize();

            Debug.WriteLine($"[MuxVideosWindows] Finalized output");

            if (File.Exists(outputPath))
            {
                var outputSize = new FileInfo(outputPath).Length;
                Debug.WriteLine($"[MuxVideosWindows] Output file created: {outputSize / 1024} KB ({outputSize} bytes)");
            }

            Debug.WriteLine($"[MuxVideosWindows] ========== MUXING COMPLETE ==========");

            return outputPath;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MuxVideosWindows] ERROR: {ex.Message}");
            Debug.WriteLine($"[MuxVideosWindows] Stack trace: {ex.StackTrace}");
            throw;
        }
        finally
        {
            // Clean up COM objects
            if (reader1 != null)
            {
                System.Runtime.InteropServices.Marshal.ReleaseComObject(reader1);
            }
            if (reader2 != null)
            {
                System.Runtime.InteropServices.Marshal.ReleaseComObject(reader2);
            }
            if (writer != null)
            {
                System.Runtime.InteropServices.Marshal.ReleaseComObject(writer);
            }
        }
    }

    /// <summary>
    /// Platform interface for muxing videos. Delegates to Windows-specific MuxVideos() with MediaComposition and fallback.
    /// </summary>
    private async Task<string> MuxVideosInternal(string preRecordedPath, string liveRecordingPath, string outputPath)
    {
        return await MuxVideos(preRecordedPath, liveRecordingPath, outputPath);
    }

    /// <summary>
    /// Muxes two video files using Windows.Media.Editing.MediaComposition (WinRT API).
    /// This handles H.264 keyframe discontinuities automatically and uses lossless stream copy when possible.
    /// </summary>
    private async Task<string> MuxVideosWithMediaComposition(string preRecPath, string liveRecPath, string outputPath)
    {
        try
        {
            Debug.WriteLine($"[MediaComposition] Starting lossless concatenation");
            Debug.WriteLine($"  Pre-rec: {preRecPath}");
            Debug.WriteLine($"  Live: {liveRecPath}");
            Debug.WriteLine($"  Output: {outputPath}");

            // Ensure output file exists for GetFileFromPathAsync
            if (!File.Exists(outputPath))
            {
                await File.WriteAllBytesAsync(outputPath, Array.Empty<byte>());
            }

            // Load both video files as StorageFile
            var file1 = await global::Windows.Storage.StorageFile.GetFileFromPathAsync(preRecPath);
            var file2 = await global::Windows.Storage.StorageFile.GetFileFromPathAsync(liveRecPath);
            var outputFile = await global::Windows.Storage.StorageFile.GetFileFromPathAsync(outputPath);

            // Create composition and add clips
            var composition = new global::Windows.Media.Editing.MediaComposition();
            composition.Clips.Add(await global::Windows.Media.Editing.MediaClip.CreateFromFileAsync(file1));
            composition.Clips.Add(await global::Windows.Media.Editing.MediaClip.CreateFromFileAsync(file2));

            Debug.WriteLine($"[MediaComposition] Added {composition.Clips.Count} clips to composition");

            // Create encoding profile (will attempt stream copy if compatible)
            var profile = global::Windows.Media.MediaProperties.MediaEncodingProfile.CreateMp4(
                global::Windows.Media.MediaProperties.VideoEncodingQuality.HD1080p);

            // Ensure H.264 + AAC (matches our encoder output)
            profile.Video.Subtype = "H264";
            profile.Audio.Subtype = "AAC";

            Debug.WriteLine($"[MediaComposition] Rendering to file (will use stream copy if possible)...");

            // Render composition to file (automatically uses stream copy if compatible, otherwise re-encodes)
            var result = await composition.RenderToFileAsync(outputFile, global::Windows.Media.Editing.MediaTrimmingPreference.Fast);

            Debug.WriteLine($"[MediaComposition] Concatenation successful!");

            // Verify output file exists and has content
            if (!File.Exists(outputPath))
            {
                throw new InvalidOperationException("Output file was not created");
            }

            var outputSize = new FileInfo(outputPath).Length;
            Debug.WriteLine($"[MediaComposition] Output file: {outputSize / 1024} KB");

            return outputPath;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MediaComposition] ERROR: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Muxes two video files with automatic fallback strategy.
    /// Tries MediaComposition first (handles H.264 keyframes properly), falls back to Media Foundation if needed.
    /// </summary>
    private async Task<string> MuxVideos(string preRecPath, string liveRecPath, string outputPath)
    {
        try
        {
            // Try MediaComposition first (handles H.264 keyframes properly, lossless)
            return await MuxVideosWithMediaComposition(preRecPath, liveRecPath, outputPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Muxing] MediaComposition failed, falling back to Media Foundation: {ex.Message}");

            // Fallback to Media Foundation (may have keyframe issues but works as last resort)
            return await MuxVideosWindows(preRecPath, liveRecPath, outputPath);
        }
    }

    /// <summary>
    /// Copies all video samples from an IMFSourceReader to an IMFSinkWriter.
    /// </summary>
    private async Task<long> CopySamplesFromReader(
        global::Windows.Win32.Media.MediaFoundation.IMFSourceReader reader,
        global::Windows.Win32.Media.MediaFoundation.IMFSinkWriter writer,
        uint streamIndex,
        long timestampOffset,
        string debugName)
    {
        int sampleCount = 0;
        long lastTimestamp = timestampOffset;

        while (true)
        {
            global::Windows.Win32.Media.MediaFoundation.IMFSample? sample = null;

            try
            {
                // Read next sample - using pointers for all output parameters
                uint actualStreamIndex = 0;
                uint streamFlags = 0;
                long timestamp = 0;

                unsafe
                {
                    global::Windows.Win32.Media.MediaFoundation.IMFSample_unmanaged* pSample = null;
                    reader.ReadSample(
                        0xFFFFFFFC, // MF_SOURCE_READER_FIRST_VIDEO_STREAM
                        0,          // No flags
                        &actualStreamIndex,
                        &streamFlags,
                        &timestamp,
                        &pSample
                    );

                    // Convert unmanaged pointer to managed interface
                    if (pSample != null)
                    {
                        sample = (global::Windows.Win32.Media.MediaFoundation.IMFSample)System.Runtime.InteropServices.Marshal.GetObjectForIUnknown(new IntPtr(pSample));
                    }
                }

                // Check if end of stream
                if ((streamFlags & 1) != 0 || sample == null) // MF_SOURCE_READERF_ENDOFSTREAM = 1
                {
                    break;
                }

                // Adjust timestamp
                long adjustedTimestamp = timestamp + timestampOffset;
                sample.SetSampleTime(adjustedTimestamp);

                // Get sample duration
                sample.GetSampleDuration(out var duration);
                lastTimestamp = adjustedTimestamp + duration;

                // Write sample to output
                writer.WriteSample(streamIndex, sample);

                sampleCount++;
            }
            finally
            {
                if (sample != null)
                {
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(sample);
                }
            }
        }

        await Task.CompletedTask;
        return lastTimestamp;
    }

    //public SKBitmap GetPreviewBitmap()
    //{
    //    var preview = NativeControl?.GetPreviewImage();
    //    if (preview?.Image != null)
    //    {
    //        return SKBitmap.FromImage(preview.Image);
    //    }
    //    return null;
    //}
}

