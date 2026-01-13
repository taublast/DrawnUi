using System.Diagnostics;
using DrawnUi.Camera.Platforms.Windows;
using DrawnUi.Views;
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
    /// Call on UI thread only. Called by CheckPermissions.
    /// </summary>
    /// <returns></returns>
    public async Task<bool> RequestPermissions()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        return status == PermissionStatus.Granted;
    }

    /// <summary>
    /// Request gallery/media write permissions so saving captures succeeds without later prompts.
    /// Call on UI thread only.
    /// </summary>
    public async Task<bool> RequestGalleryPermissions()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.StorageWrite>();
        }

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
                        throw new InvalidOperationException($"MFCreateSourceReaderFromURL 1 failed for live: 0x{hr.Value:X8}");
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

    private async Task StopCaptureVideoFlowInternal()
    {

        if (_captureVideoEncoder.LiveRecordingDuration < TimeSpan.FromSeconds(1))
        {
            await AbortCaptureVideoFlowInternal();
            return;
        }

        ICaptureVideoEncoder encoder = null;

        try
        {
            // CRITICAL: Stop frame capture timer FIRST before clearing encoder reference
            // This prevents race conditions where CaptureFrame is still executing
            _frameCaptureTimer?.Dispose();
            _frameCaptureTimer = null;

            // Give any in-flight CaptureFrame calls time to complete
            await Task.Delay(50);

            _useWindowsPreviewDrivenCapture = false;
            // Clear frame callback
            if (NativeControl is NativeCamera winCamCleanup)
            {
                winCamCleanup.PreviewCaptureSuccess = null;
            }


            // Get local reference to encoder before clearing field to prevent disposal race
            encoder = _captureVideoEncoder;
            _captureVideoEncoder = null;


            // Stop mirroring recording frames to preview and detach event
            UseRecordingFramesForPreview = false;
            if (encoder is WindowsCaptureVideoEncoder _winEncPrev)
            {
                try
                {
                    _winEncPrev.PreviewAvailable -= _encoderPreviewInvalidateHandler;
                }
                catch
                {
                }
            }


            // Stop encoder and get result
            CapturedVideo capturedVideo = await encoder?.StopAsync();

            // iOS/Windows: Mux two files together (legacy approach)
            if (capturedVideo != null && !string.IsNullOrEmpty(_preRecordingFilePath) && File.Exists(_preRecordingFilePath))
            {
                Debug.WriteLine($"[StopCaptureVideoFlow] Muxing pre-recorded file with live recording");
                try
                {
                    // Save original live recording path before overwriting capturedVideo
                    string originalLiveRecordingPath = capturedVideo.FilePath;

                    // Mux pre-recorded file + live file into final output
                    string finalOutputPath = await MuxVideosAsync(_preRecordingFilePath, originalLiveRecordingPath);
                    if (!string.IsNullOrEmpty(finalOutputPath) && File.Exists(finalOutputPath))
                    {
                        // Update captured video to point to muxed file
                        var muxedInfo = new FileInfo(finalOutputPath);
                        capturedVideo = new CapturedVideo
                        {
                            FilePath = finalOutputPath,
                            FileSizeBytes = muxedInfo.Length,
                            Duration = capturedVideo.Duration,
                            Time = capturedVideo.Time
                        };

                        // Delete temp live recording file (NOT the muxed file!)
                        try { File.Delete(originalLiveRecordingPath); } catch { }

                        Debug.WriteLine($"[StopCaptureVideoFlow] Muxing successful: {finalOutputPath}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[StopCaptureVideoFlow] Muxing failed: {ex.Message}. Using live recording only.");
                }
                finally
                {
                    ClearPreRecordingBuffer();
                }
            }
            else
            {
                ClearPreRecordingBuffer();
            }


            // Update state and notify success
            IsRecordingVideo = false;
            if (capturedVideo != null)
            {
                OnVideoRecordingSuccess(capturedVideo);
            }
        }
        catch (Exception ex)
        {
            // Clean up on error
            _frameCaptureTimer?.Dispose();
            _frameCaptureTimer = null;
            _captureVideoEncoder = null;

            IsRecordingVideo = false;
            VideoRecordingFailed?.Invoke(this, ex);
            throw;
        }
        finally
        {
            // Clean up encoder after StopAsync completes
            encoder?.Dispose();
        }
    }

    private async Task AbortCaptureVideoFlowInternal()
    {
        ICaptureVideoEncoder encoder = null;

        try
        {
            // CRITICAL: Stop frame capture timer FIRST before clearing encoder reference
            // This prevents race conditions where CaptureFrame is still executing
            _frameCaptureTimer?.Dispose();
            _frameCaptureTimer = null;

            // Give any in-flight CaptureFrame calls time to complete
            await Task.Delay(50);


            _useWindowsPreviewDrivenCapture = false;
            // Clear frame callback
            if (NativeControl is NativeCamera winCamCleanup)
            {
                winCamCleanup.PreviewCaptureSuccess = null;
            }

            // Get local reference to encoder before clearing field to prevent disposal race
            encoder = _captureVideoEncoder;
            _captureVideoEncoder = null;


            // Stop mirroring recording frames to preview and detach event
            UseRecordingFramesForPreview = false;
            if (encoder is WindowsCaptureVideoEncoder _winEncPrev)
            {
                try
                {
                    _winEncPrev.PreviewAvailable -= _encoderPreviewInvalidateHandler;
                }
                catch
                {
                }
            }

            // Stop encoder
            await encoder?.AbortAsync();

            IsRecordingVideo = false;
        }
        catch (Exception ex)
        {
            // Clean up on error
            _frameCaptureTimer?.Dispose();
            _frameCaptureTimer = null;
            _captureVideoEncoder = null;

            IsRecordingVideo = false;
            //VideoRecordingFailed?.Invoke(this, ex);
            throw;
        }
        finally
        {
            // Clean up encoder after StopAsync completes
            encoder?.Dispose();
        }
    }

    private async Task StartCaptureVideoFlow()
    {
        // Create platform-specific encoder with existing GRContext (GPU path)
        // BUGFIX: Passing GRContext from the UI thread causes freeze/deadlock during window resize because the context 
        // is destroyed/recreated while the background encoder task is trying to use it. 
        // Also, since we do CPU readback anyway (ReadPixels), using a GPU surface here is actually slower (upload + readback).
        // By passing null, we force a CPU-backed surface which is thread-safe and faster for this specific pipeline.
        GRContext grContext = null; // (Superview?.CanvasView as SkiaViewAccelerated)?.GRContext;
        _captureVideoEncoder = new WindowsCaptureVideoEncoder(grContext);

        // Set parent reference and pre-recording mode
        _captureVideoEncoder.ParentCamera = this;
        _captureVideoEncoder.IsPreRecordingMode = IsPreRecording;
        Debug.WriteLine($"[StartCaptureVideoFlow] Encoder initialized with IsPreRecordingMode={IsPreRecording}");

        // Generate output path
        // If pre-recording, use temp file for streaming encoded frames
        string outputPath;
        if (IsPreRecording)
        {
            // Use temp file path from InitializePreRecordingBuffer
            outputPath = _preRecordingFilePath;
            if (string.IsNullOrEmpty(outputPath))
            {
                Debug.WriteLine("[StartCaptureVideoFlow] ERROR: Pre-recording file path not initialized");
                return;
            }
            Debug.WriteLine($"[StartCaptureVideoFlow] Pre-recording to file: {outputPath}");
        }
        else
        {
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            outputPath = Path.Combine(documentsPath, $"CaptureVideo_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");
            Debug.WriteLine($"[StartCaptureVideoFlow] Recording to file: {outputPath}");
        }

        // CRITICAL: Set start time BEFORE initializing encoder to avoid losing initial frames!
        // Encoder initialization can take 1-2 seconds on Android, and we calculate PTS from this time
        _captureVideoStartTime = DateTime.Now;
        _capturePtsBaseTime = null;

        // Get current video format dimensions from native camera
        var currentFormat = NativeControl?.GetCurrentVideoFormat();
        var width = currentFormat?.Width ?? 1280;
        var height = currentFormat?.Height ?? 720;
        var fps = currentFormat?.FrameRate > 0 ? currentFormat.FrameRate : 30;

        // Initialize encoder with current settings (follow camera defaults)
        _diagEncWidth = width;
        _diagEncHeight = height;
        _diagBitrate = (long)Math.Max(1, width * height) * Math.Max(1, fps) * 4 / 10;
        SetSourceFrameDimensions(width, height);
        await _captureVideoEncoder.InitializeAsync(outputPath, width, height, fps, RecordAudio);

        // CRITICAL: In pre-recording mode, do NOT call StartAsync during initialization
        // Pre-recording mode should just buffer frames in memory without starting file writing
        // StartAsync will be called later when transitioning to live recording
        if (!IsPreRecording)
        {
            await _captureVideoEncoder.StartAsync();
            Debug.WriteLine($"[StartCaptureVideoFlow] StartAsync called for live/normal recording");
        }
        else
        {
            Debug.WriteLine($"[StartCaptureVideoFlow] Skipping StartAsync - pre-recording mode will buffer frames in memory");
        }

        // Reset diagnostics
        _diagStartTime = DateTime.Now;
        _diagDroppedFrames = 0;
        _diagSubmittedFrames = 0;
        _diagLastSubmitMs = 0;
        ResetRecordingFps();
        _targetFps = fps;

        // Don't use preview-driven capture - use callback like Android
        _useWindowsPreviewDrivenCapture = false;

        // Control preview source: raw camera frames (preview works normally)
        UseRecordingFramesForPreview = false;

        // Set up progress reporting
        _captureVideoEncoder.ProgressReported += (sender, duration) =>
        {
            MainThread.BeginInvokeOnMainThread(() => OnVideoRecordingProgress(duration));
        };

        // Use PreviewCaptureSuccess callback like Android - encoder gets frames without stealing from preview
        if (NativeControl is NativeCamera winCam)
        {
            winCam.PreviewCaptureSuccess = (captured) =>
            {
                CalculateCameraInputFps();

                if ((!IsPreRecording && !IsRecordingVideo) || _captureVideoEncoder is not WindowsCaptureVideoEncoder winEnc)
                    return;

                if (System.Threading.Interlocked.CompareExchange(ref _frameInFlight, 1, 0) != 0)
                {
                    System.Threading.Interlocked.Increment(ref _diagDroppedFrames);
                    return;
                }

                // Make a copy of the image (original may be disposed after callback returns)
                var srcImg = captured?.Image;
                if (srcImg == null)
                {
                    System.Threading.Interlocked.Exchange(ref _frameInFlight, 0);
                    return;
                }

                // Create a raster copy that's safe to use on main thread
                SKImage imgCopy;
                try
                {
                    using var bmp = new SKBitmap(srcImg.Width, srcImg.Height);
                    using var canvas = new SKCanvas(bmp);
                    canvas.DrawImage(srcImg, 0, 0);
                    imgCopy = SKImage.FromBitmap(bmp);
                }
                catch
                {
                    System.Threading.Interlocked.Exchange(ref _frameInFlight, 0);
                    return;
                }

                var elapsed = DateTime.Now - _captureVideoStartTime;

                // GPU surface must be accessed from main thread
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        using (imgCopy)
                        using (winEnc.BeginFrame(elapsed, out var canvas, out var info))
                        {
                            if (canvas != null)
                            {
                                var rects = GetAspectFillRects(imgCopy.Width, imgCopy.Height, info.Width, info.Height);
                                canvas.DrawImage(imgCopy, rects.src, rects.dst);

                                if (FrameProcessor != null || VideoDiagnosticsOn)
                                {
                                    var rotation = GetActiveRecordingRotation();
                                    canvas.Save();
                                    ApplyCanvasRotation(canvas, info.Width, info.Height, rotation);

                                    var (frameWidth, frameHeight) = GetRotatedDimensions(info.Width, info.Height, rotation);
                                    var frame = new DrawableFrame
                                    {
                                        Width = frameWidth,
                                        Height = frameHeight,
                                        Canvas = canvas,
                                        Time = elapsed,
                                        Scale = 1f
                                    };
                                    FrameProcessor?.Invoke(frame);

                                    if (VideoDiagnosticsOn)
                                        DrawDiagnostics(canvas, info.Width, info.Height);

                                    canvas.Restore();
                                }

                                var sw = System.Diagnostics.Stopwatch.StartNew();
                                await winEnc.SubmitFrameAsync();
                                sw.Stop();
                                _diagLastSubmitMs = sw.Elapsed.TotalMilliseconds;
                                System.Threading.Interlocked.Increment(ref _diagSubmittedFrames);
                                CalculateRecordingFps();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Windows CaptureFrame] Error: {ex.Message}");
                    }
                    finally
                    {
                        System.Threading.Interlocked.Exchange(ref _frameInFlight, 0);
                    }
                });
            };
        }
    }

    /// <summary>
    /// Start video recording. Run this in background thread!
    /// Locks the device rotation for the entire recording session.
    /// Uses either native video recording or capture video flow depending on UseCaptureVideoFlow setting.
    /// 
    /// State machine logic:
    /// - If EnablePreRecording && !IsPreRecording: Start memory-only recording (pre-recording phase)
    /// - If IsPreRecording && !IsRecordingVideo: Prepend buffer and start file recording (normal phase)
    /// - Otherwise: Start normal file recording
    /// </summary>
    /// <returns>Async task</returns>
    public async Task StartVideoRecording()
    {
        if (IsBusy)
            return;

        Debug.WriteLine($"[StartVideoRecording] IsMainThread {MainThread.IsMainThread}, IsPreRecording={IsPreRecording}, IsRecordingVideo={IsRecordingVideo}");

        IsBusy = true;

        try
        {
            // State 1 -> State 2: If pre-recording enabled and not yet in pre-recording phase, start memory-only recording
            if (EnablePreRecording && !IsPreRecording && !IsRecordingVideo)
            {
                Debug.WriteLine("[StartVideoRecording] Transitioning to IsPreRecording (memory-only recording)");
                IsPreRecording = true;
                InitializePreRecordingBuffer();

                // Lock the current device rotation for the entire recording session
                RecordingLockedRotation = DeviceRotation;
                Debug.WriteLine($"[StartVideoRecording] Locked rotation at {RecordingLockedRotation}°");

                // Start recording in memory-only mode
                if (UseCaptureVideoFlow && FrameProcessor != null)
                {
                    await StartCaptureVideoFlow();
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

                // iOS: Create new encoder FIRST, swap atomically, THEN stop old one (prevents frame loss)
                ICaptureVideoEncoder oldEncoder = null;
                if (_captureVideoEncoder != null)
                {
                    Debug.WriteLine("[StartVideoRecording] Preparing to transition encoders without frame loss");

                    // Keep reference to old encoder
                    oldEncoder = _captureVideoEncoder;
                    Debug.WriteLine("[StartVideoRecording] Old encoder captured for transition");
                }

                // Update state flags BEFORE creating new encoder
                IsPreRecording = false;
                IsRecordingVideo = true;
                RecordingLockedRotation = DeviceRotation;
                Debug.WriteLine($"[StartVideoRecording] Locked rotation at {RecordingLockedRotation}°");

                if (UseCaptureVideoFlow && FrameProcessor != null)
                {
                    // Create new encoder - this ATOMICALLY swaps _captureVideoEncoder to the new instance
                    // Any frames arriving now will go to the new encoder (no gap!)
                    await StartCaptureVideoFlow();
                    Debug.WriteLine("[StartVideoRecording] New encoder created and active - frames now routing to encoder #2");

                    // NOTE: Pre-recording offset will be set AFTER stopping old encoder and getting correct duration
                }

                // NOW stop the old encoder (frames already going to new encoder, zero frame loss)
                if (oldEncoder != null)
                {
                    Debug.WriteLine("[StartVideoRecording] Stopping old pre-recording encoder to finalize file");

                    try
                    {
                        var preRecResult = await oldEncoder.StopAsync();
                        Debug.WriteLine("[StartVideoRecording] Pre-recording encoder stopped and file finalized");

                        if (preRecResult != null && !string.IsNullOrEmpty(preRecResult.FilePath))
                        {
                            _preRecordingFilePath = preRecResult.FilePath;

                            // CRITICAL: Use corrected duration from StopAsync result, NOT the wall-clock duration captured earlier
                            _preRecordingDurationTracked = preRecResult.Duration;
                            Debug.WriteLine($"[StartVideoRecording] Captured pre-recording file: {_preRecordingFilePath}");
                            Debug.WriteLine($"[StartVideoRecording] Captured pre-recording duration (corrected): {_preRecordingDurationTracked.TotalSeconds:F2}s");

                            // CRITICAL: Set pre-recording duration offset on NEW encoder with CORRECTED duration
                            if (_preRecordingDurationTracked > TimeSpan.Zero && _captureVideoEncoder != null)
                            {
                                // Platform-specific: set offset on encoder

                                if (_captureVideoEncoder is WindowsCaptureVideoEncoder winEncoder)
                                {
                                    winEncoder.SetPreRecordingDuration(_preRecordingDurationTracked);
                                    Debug.WriteLine($"[StartVideoRecording] Set pre-recording offset on new Windows encoder: {_preRecordingDurationTracked.TotalSeconds:F2}s");
                                }

                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[StartVideoRecording] Error stopping pre-recording encoder: {ex.Message}");
                    }

                    oldEncoder?.Dispose();
                    oldEncoder = null;
                    Debug.WriteLine("[StartVideoRecording] Old encoder disposed");
                }
                else
                {
                    await StartNativeVideoRecording();
                    Debug.WriteLine("[StartVideoRecording] Native recording started for live recording");
                }

            }
            // Normal recording (no pre-recording)
            else if (!IsRecordingVideo)
            {
                Debug.WriteLine("[StartVideoRecording] Starting normal recording (no pre-recording)");
                IsRecordingVideo = true;

                // Lock the current device rotation for the entire recording session
                RecordingLockedRotation = DeviceRotation;
                Debug.WriteLine($"[StartVideoRecording] Locked rotation at {RecordingLockedRotation}°");

                if (UseCaptureVideoFlow && FrameProcessor != null)
                {
                    await StartCaptureVideoFlow();
                }
                else
                {
                    await StartNativeVideoRecording();
                }
            }
        }
        catch (Exception ex)
        {
            IsRecordingVideo = false;
            IsPreRecording = false;
            IsBusy = false;
            RecordingLockedRotation = -1; // Reset on error
            ClearPreRecordingBuffer();
            VideoRecordingFailed?.Invoke(this, ex);
            throw;
        }

        IsBusy = false;
    }

    protected async Task<List<string>> GetAvailableAudioCodecsPlatform()
    {
        var codecNames = new List<string>();
        try
        {
            var query = new Windows.Media.Core.CodecQuery();
            var codecs = await query.FindAllAsync(Windows.Media.Core.CodecKind.Audio, Windows.Media.Core.CodecCategory.Encoder, null);

            var aacGuid = new Guid("00001610-0000-0010-8000-00aa00389b71");

            foreach (var codec in codecs)
            {
                // Clean up display name (optional, but helpful if they are verbose like "Microsoft AAC Encoder")
                codecNames.Add(codec.DisplayName);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SkiaCameraWindows] GetAvailableAudioCodecsPlatform error: {ex.Message}");
        }
        return codecNames;
    }


    protected async Task<List<string>> GetAvailableAudioDevicesPlatform()
    {
        var deviceNames = new List<string>();
        try
        {
            var devices = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(Windows.Devices.Enumeration.DeviceClass.AudioCapture);
            foreach (var device in devices)
            {
                deviceNames.Add(device.Name);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SkiaCameraWindows] GetAvailableAudioDevicesPlatform error: {ex.Message}");
        }
        return deviceNames;
    }
}

