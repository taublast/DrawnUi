using SkiaSharp;
using System.Diagnostics;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;
using global::Windows.Win32;
using global::Windows.Win32.Foundation;
using global::Windows.Win32.Media.MediaFoundation;

namespace DrawnUi.Camera.Platforms.Windows;

/// <summary>
/// Windows implementation of capture video encoding with real-time frame processing.
/// This implementation processes frames immediately to avoid UI freezing.
/// </summary>
public class WindowsCaptureVideoEncoder : ICaptureVideoEncoder
{
    private static int _instanceCounter = 0;
    private readonly int _instanceId;

    private StorageFile _outputFile;
    private List<SKBitmap> _frames;
    private int _width;
    private int _height;
    private int _frameRate;
    private bool _recordAudio;
    private DateTime _startTime;
    private bool _isRecording;
    private System.Threading.Timer _progressTimer;
    private int _totalFrameCount; // Track total frames processed

    // Pre-recording support
    private string _preRecordingFilePath;     // Pre-recording buffer MP4
    private string _liveRecordingFilePath;    // Live recording MP4
    private string _outputPath;               // Final output path
    private PrerecordingEncodedBuffer _preRecordingBuffer; // Not used on Windows (no direct H.264 access)
    private TimeSpan _preRecordingDuration;   // Offset for live recording timestamps
    private bool _encodingDurationSetFromFrames = false;

    // GPU composition fields (temporary bridge until full Media Foundation path is implemented)
    private GRContext _grContext;                 // Provided by DrawnUi accelerated surface
    private SKSurface _gpuSurface;                // Encoder-owned GPU surface to draw overlays
    private SKImageInfo _gpuInfo;                 // Matches encoder dimensions
    private readonly object _frameLock = new();   // Protects Begin/Submit sequence
    private TimeSpan _pendingTimestamp;

    // Preview-from-recording support
    private readonly object _previewLock = new();
    private SKImage _latestPreviewImage; // swapped out to UI on demand
    private SKBitmap _readbackBitmap;    // reused CPU buffer to avoid per-frame allocs
    public event EventHandler PreviewAvailable;


        // Media Foundation pipeline (production path)
        private global::Windows.Win32.Media.MediaFoundation.IMFSinkWriter _sinkWriter;
        private uint _streamIndex;
        private bool _mfStarted;

        // CsWin32 may not expose MF_VERSION directly; define the known value from mfapi.h
        private const uint MF_VERSION_CONST = 0x00020070; // (MF_SDK_VERSION<<16) | MF_API_VERSION
        private long _rtDurationPerFrame;   // 100-ns units
        private long _lastSampleTime100ns = -1;


    public WindowsCaptureVideoEncoder(GRContext grContext = null)
    {
        _instanceId = System.Threading.Interlocked.Increment(ref _instanceCounter);
        _grContext = grContext;
        Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] CONSTRUCTOR CALLED");
    }


    public bool IsRecording => _isRecording;

    public event EventHandler<TimeSpan> ProgressReported;
    
    // Properties for platform-specific details
    public int EncodedFrameCount { get; private set; }
    public long EncodedDataSize { get; private set; }
    public TimeSpan EncodingDuration { get; private set; }
    public string EncodingStatus { get; private set; } = "Idle";

    // Interface implementation - required by ICaptureVideoEncoder
    public bool IsPreRecordingMode { get; set; }
    public SkiaCamera ParentCamera { get; set; }

    public async Task InitializeAsync(string outputPath, int width, int height, int frameRate, bool recordAudio)
    {
        Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] InitializeAsync CALLED: IsPreRecordingMode={IsPreRecordingMode}");

        _outputPath = outputPath;
        _width = width;
        _height = height;
        _frameRate = Math.Max(1, frameRate);
        _recordAudio = recordAudio;
        _preRecordingDuration = TimeSpan.Zero;

        // Prepare output directory
        var outputDir = Path.GetDirectoryName(_outputPath);
        Directory.CreateDirectory(outputDir);

        string targetFilePath;

        // Initialize based on mode
        if (IsPreRecordingMode && ParentCamera != null)
        {
            // Pre-recording mode: Write directly to temp file for pre-recording buffer
            var guid = Guid.NewGuid().ToString("N");
            _preRecordingFilePath = Path.Combine(outputDir, $"pre_rec_{guid}.mp4");
            _liveRecordingFilePath = Path.Combine(outputDir, $"live_rec_{guid}.mp4");
            targetFilePath = _preRecordingFilePath;

            Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] Pre-recording mode: Writing to {_preRecordingFilePath}");
        }
        else
        {
            // Normal/live recording mode
            targetFilePath = _outputPath;
            Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] Normal recording mode: Writing to {_outputPath}");
        }

        // Create/replace output file
        var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(targetFilePath));
        var fileName = Path.GetFileName(targetFilePath);
        _outputFile = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

        // Initialize Media Foundation
        var hr = PInvoke.MFStartup(MF_VERSION_CONST, 0);
        if (hr.Failed)
            throw new InvalidOperationException($"MFStartup failed: 0x{hr.Value:X8}");
        _mfStarted = true;

        // Create sink writer
        unsafe
        {
            fixed (char* p = _outputFile.Path)
            {
                hr = PInvoke.MFCreateSinkWriterFromURL(new PCWSTR(p), null, null, out _sinkWriter);
            }
        }
        if (hr.Failed || _sinkWriter == null)
            throw new InvalidOperationException($"MFCreateSinkWriterFromURL failed: 0x{hr.Value:X8}");

        // Configure output type (H.264)
        hr = PInvoke.MFCreateMediaType(out var outType);
        if (hr.Failed)
            throw new InvalidOperationException($"MFCreateMediaType(out) failed: 0x{hr.Value:X8}");
        try
        {
            outType.SetGUID(MFGuids.MF_MT_MAJOR_TYPE, MFGuids.MFMediaType_Video);
            outType.SetGUID(MFGuids.MF_MT_SUBTYPE, MFGuids.MFVideoFormat_H264);

            // Frame size / rate / pixel aspect
            SetAttributeSize(outType, MFGuids.MF_MT_FRAME_SIZE, (uint)_width, (uint)_height);
            SetAttributeRatio(outType, MFGuids.MF_MT_FRAME_RATE, (uint)_frameRate, 1);
            SetAttributeRatio(outType, MFGuids.MF_MT_PIXEL_ASPECT_RATIO, 1, 1);
            outType.SetUINT32(MFGuids.MF_MT_INTERLACE_MODE, 2); // Progressive
            outType.SetUINT32(MFGuids.MF_MT_MPEG2_PROFILE, 100); // H.264 High profile

            // Bitrate estimate (simple heuristic)
            uint bitrate = (uint)(Math.Max(1, _width * _height) * Math.Max(1, _frameRate) * 4 / 10);
            outType.SetUINT32(MFGuids.MF_MT_AVG_BITRATE, bitrate);

            _sinkWriter.AddStream(outType, out _streamIndex);
        }
        finally
        {
            Marshal.ReleaseComObject(outType);
        }

        // Configure input type (ARGB32 frames)
        hr = PInvoke.MFCreateMediaType(out var inType);
        if (hr.Failed)
            throw new InvalidOperationException($"MFCreateMediaType(in) failed: 0x{hr.Value:X8}");
        try
        {
            inType.SetGUID(MFGuids.MF_MT_MAJOR_TYPE, MFGuids.MFMediaType_Video);
            inType.SetGUID(MFGuids.MF_MT_SUBTYPE, MFGuids.MFVideoFormat_RGB32);
            SetAttributeSize(inType, MFGuids.MF_MT_FRAME_SIZE, (uint)_width, (uint)_height);
            SetAttributeRatio(inType, MFGuids.MF_MT_FRAME_RATE, (uint)_frameRate, 1);
            SetAttributeRatio(inType, MFGuids.MF_MT_PIXEL_ASPECT_RATIO, 1, 1);
            inType.SetUINT32(MFGuids.MF_MT_INTERLACE_MODE, 2); // Progressive
            inType.SetUINT32(MFGuids.MF_MT_ALL_SAMPLES_INDEPENDENT, 1);
            inType.SetUINT32(MFGuids.MF_MT_DEFAULT_STRIDE, (uint)(_width * 4));
            inType.SetUINT32(MFGuids.MF_MT_FIXED_SIZE_SAMPLES, 1);
            inType.SetUINT32(MFGuids.MF_MT_SAMPLE_SIZE, (uint)(_width * _height * 4));

            _sinkWriter.SetInputMediaType(_streamIndex, inType, null);
        }
        finally
        {
            Marshal.ReleaseComObject(inType);
        }

        // Ready to start writing
        _sinkWriter.BeginWriting();

        _rtDurationPerFrame = (long)(10_000_000L / Math.Max(1, _frameRate)); // 100-ns units per frame
        _lastSampleTime100ns = -1;
    }


    /// <summary>
    /// Begin a GPU frame for overlay composition. Returns a canvas bound to the encoder's surface.
    /// Note: This is a temporary bridge; final implementation will hand this surface directly to MF encoder without CPU readback.
    /// </summary>
    public IDisposable BeginFrame(TimeSpan timestamp, out SKCanvas canvas, out SKImageInfo info)
    {
        lock (_frameLock)
        {
            _pendingTimestamp = timestamp;

            if (_gpuSurface == null)
            {
                // Ensure we have image info
                if (_gpuInfo.Width != _width || _gpuInfo.Height != _height || _gpuInfo.ColorType == SKColorType.Unknown)
                {
                    _gpuInfo = new SKImageInfo(_width, _height, SKColorType.Bgra8888, SKAlphaType.Premul);
                }

                // Try to create a GPU-backed surface; fallback to CPU surface if no GRContext (debug only)
                _gpuSurface = _grContext != null
                    ? SKSurface.Create(_grContext, true, _gpuInfo)
                    : SKSurface.Create(_gpuInfo);
            }

            canvas = _gpuSurface.Canvas;
            canvas.Clear(SKColors.Transparent);
            info = _gpuInfo;

            return new FrameScope();
        }
    }

    public bool TryAcquirePreviewImage(out SKImage image)
    {
        lock (_previewLock)
        {
            image = _latestPreviewImage;
            _latestPreviewImage = null; // transfer ownership to caller
            return image != null;
        }
    }

    /// <summary>
    /// Submits current GPU frame to encoder. Temporary: performs CPU readback into existing AddFrame pipeline.
    /// </summary>
    public async Task SubmitFrameAsync()
    {
        SKImage snapshot = null;
        try
        {
            lock (_frameLock)
            {
                if (!_isRecording || _gpuSurface == null)
                    return;

                // Publish a GPU snapshot for preview
                snapshot = _gpuSurface.Snapshot();
                if (snapshot == null)
                    return;

                lock (_previewLock)
                {
                    _latestPreviewImage?.Dispose();
                    _latestPreviewImage = snapshot;
                    snapshot = null; // ownership transferred to preview holder
                }

                // Notify listeners that a new preview frame is ready
                PreviewAvailable?.Invoke(this, EventArgs.Empty);

                // Reuse a single CPU bitmap buffer for readback to minimize jitter
                if (_readbackBitmap == null || _readbackBitmap.Width != _width || _readbackBitmap.Height != _height)
                {
                    _readbackBitmap?.Dispose();
                    _readbackBitmap = new SKBitmap(new SKImageInfo(_width, _height, SKColorType.Bgra8888, SKAlphaType.Premul));
                }

                // Read pixels from the GPU surface into the reused bitmap
                var dstInfo = _readbackBitmap.Info;
                var dstPtr = _readbackBitmap.GetPixels();
                if (dstPtr == IntPtr.Zero || !_gpuSurface.ReadPixels(dstInfo, dstPtr, _readbackBitmap.RowBytes, 0, 0))
                    return;
            }

            // Encode from the reused readback bitmap (no per-frame SKBitmap allocations)
            await AddFrameAsync(_readbackBitmap, _pendingTimestamp);
        }
        finally
        {
            snapshot?.Dispose();
        }
    }

    private sealed class FrameScope : IDisposable { public void Dispose() { } }


    /// <summary>
    /// Sets the pre-recording duration offset for live recording timestamps.
    /// Call this before StartAsync() when transitioning from pre-recording to live recording.
    /// </summary>
    public void SetPreRecordingDuration(TimeSpan duration)
    {
        _preRecordingDuration = duration;
        Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] Set pre-recording duration offset: {duration.TotalSeconds:F2}s");
    }

    public Task StartAsync()
    {
        Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] StartAsync CALLED: IsPreRecordingMode={IsPreRecordingMode}");

        _isRecording = true;
        _startTime = DateTime.Now;

        // Initialize statistics
        EncodedFrameCount = 0;
        EncodedDataSize = 0;
        EncodingDuration = TimeSpan.Zero;
        EncodingStatus = "Started";

        // Start progress reporting timer
        _progressTimer = new System.Threading.Timer(ReportProgress, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

        if (_preRecordingDuration > TimeSpan.Zero)
        {
            Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] Live recording started after pre-recording offset: {_preRecordingDuration.TotalSeconds:F2}s");
        }

        return Task.CompletedTask;
    }

    public async Task AddFrameAsync(SKBitmap bitmap, TimeSpan timestamp)
    {
        if (!_isRecording || _sinkWriter == null)
            return;

        // Ensure format is BGRA8888, premultiplied, matching encoder size
        SKBitmap source = bitmap;
        if (bitmap.ColorType != SKColorType.Bgra8888 || bitmap.Width != _width || bitmap.Height != _height)
        {
            var info = new SKImageInfo(_width, _height, SKColorType.Bgra8888, SKAlphaType.Premul);
            source = new SKBitmap(info);
            using var canvas = new SKCanvas(source);
            canvas.DrawBitmap(bitmap, new SKRect(0,0,_width,_height));
        }

        try
        {
            var dataSize = (uint)(_width * _height * 4);
            var hr = PInvoke.MFCreateMemoryBuffer(dataSize, out var mediaBuffer);
            if (hr.Failed)
                throw new InvalidOperationException($"MFCreateMemoryBuffer failed: 0x{hr.Value:X8}");

            try
            {
                unsafe
                {
                    byte* dst;
                    uint maxLen, curLen;
                    mediaBuffer.Lock(&dst, &maxLen, &curLen);

                    try
                    {
                        var srcPtrInt = source.GetPixels();
                        if (srcPtrInt == IntPtr.Zero)
                            throw new InvalidOperationException("Source pixels not available");

                        byte* src = (byte*)srcPtrInt.ToPointer();

                        int srcRowBytes = (int)source.RowBytes;
                        int rowBytes = _width * 4;

                        for (int y = 0; y < _height; y++)
                        {
                            System.Buffer.MemoryCopy(src + (long)y * srcRowBytes, dst + (long)y * rowBytes, maxLen - (uint)(y * rowBytes), rowBytes);
                        }

                        mediaBuffer.SetCurrentLength(dataSize);
                    }
                    finally
                    {
                        mediaBuffer.Unlock();
                    }
                }

                hr = PInvoke.MFCreateSample(out var sample);
                if (hr.Failed)
                    throw new InvalidOperationException($"MFCreateSample failed: 0x{hr.Value:X8}");

                try
                {
                    sample.AddBuffer(mediaBuffer);

                    // Apply pre-recording offset to timestamp if live recording after pre-recording
                    double timestampSeconds = _pendingTimestamp.TotalSeconds;
                    if (_preRecordingDuration > TimeSpan.Zero)
                    {
                        timestampSeconds += _preRecordingDuration.TotalSeconds;
                    }

                    long sampleTime = (long)(timestampSeconds * 10_000_000L);
                    if (sampleTime <= _lastSampleTime100ns)
                    {
                        sampleTime = _lastSampleTime100ns + _rtDurationPerFrame;
                    }
                    sample.SetSampleTime(sampleTime);
                    sample.SetSampleDuration(_rtDurationPerFrame);

                    _sinkWriter.WriteSample(_streamIndex, sample);

                    _lastSampleTime100ns = sampleTime;
                    
                    // Update statistics
                    EncodedFrameCount++;
                    EncodedDataSize += (long)dataSize;
                    EncodingDuration = DateTime.Now - _startTime;
                    EncodingStatus = "Encoding";
                }
                finally
                {
                    Marshal.ReleaseComObject(sample);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(mediaBuffer);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsCaptureVideoEncoder] AddFrameAsync failed: {ex.Message}");
            throw;
        }
        finally
        {
            if (!ReferenceEquals(source, bitmap))
                source.Dispose();
        }
    }

    private async Task EncodeFrameToStream(SKBitmap frame)
    {
        // Immediately process frame to avoid memory buildup and UI freezing
        try
        {
            // For now, we'll still use the AVI approach but write frames immediately
            // In production, this would use MediaFoundation pipeline for real-time encoding

            // This is where you'd send frame to MediaFoundation encoder in real implementation
            // For demo: just process the frame data immediately
            var frameData = ConvertFrameToAviFormat(frame);

            // Write frame data immediately to stream (would be done by MediaFoundation)
            // For demo: store in memory but process immediately to prevent UI blocking

            Debug.WriteLine($"[WindowsCaptureVideoEncoder] Processed frame {_totalFrameCount} in real-time");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsCaptureVideoEncoder] EncodeFrameToStream failed: {ex.Message}");
        }
    }

    private byte[] ConvertFrameToAviFormat(SKBitmap frame)
    {
        // Convert SKBitmap to BGR24 format for AVI (same as before but immediate)
        var frameData = new byte[frame.Width * frame.Height * 3];

        for (int y = 0; y < frame.Height; y++)
        {
            for (int x = 0; x < frame.Width; x++)
            {
                var color = frame.GetPixel(x, frame.Height - 1 - y); // Flip vertically

                var dataIndex = (y * frame.Width + x) * 3;
                frameData[dataIndex] = color.Blue;   // B
                frameData[dataIndex + 1] = color.Green; // G
                frameData[dataIndex + 2] = color.Red;   // R
            }
        }

        return frameData;
    }

    public async Task PrependBufferedEncodedDataAsync(PrerecordingEncodedBuffer prerecordingBuffer)
    {
        if (!_isRecording || _sinkWriter == null || prerecordingBuffer == null)
            return;

        try
        {
            // Write pre-encoded data directly to media sink
            // This is a no-op for the current implementation which requires re-encoding from bitmaps
            // In a full implementation, would write prerecordingBuffer.GetBufferedData() to _sinkWriter
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsCaptureVideoEncoder] PrependBufferedEncodedDataAsync failed: {ex.Message}");
        }
    }

    public async Task<CapturedVideo> StopAsync()
    {
        Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] StopAsync CALLED: IsPreRecordingMode={IsPreRecordingMode}");

        _isRecording = false;
        _progressTimer?.Dispose();

        // Update status
        EncodingStatus = "Stopping";

        try
        {
            if (_sinkWriter != null)
            {
                try { _sinkWriter.Finalize(); } catch { }
                Marshal.ReleaseComObject(_sinkWriter);
                _sinkWriter = null;
            }
        }
        finally
        {
            if (_mfStarted)
            {
                PInvoke.MFShutdown();
                _mfStarted = false;
            }
        }

        // Handle pre-recording mode
        if (IsPreRecordingMode && !string.IsNullOrEmpty(_preRecordingFilePath))
        {
            // Pre-recording encoder stopping: file is already written
            Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] Pre-recording encoder stopped");

            if (File.Exists(_preRecordingFilePath))
            {
                var fileInfo = new FileInfo(_preRecordingFilePath);
                EncodingDuration = DateTime.Now - _startTime;
                _encodingDurationSetFromFrames = true;

                Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] Pre-recording file: {fileInfo.Length / 1024.0:F2} KB");
                Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] Actual pre-recording duration: {EncodingDuration.TotalSeconds:F3}s");

                EncodingStatus = "Completed";

                return new CapturedVideo
                {
                    FilePath = _preRecordingFilePath,
                    FileSizeBytes = fileInfo.Length,
                    Duration = EncodingDuration,
                    Time = _startTime
                };
            }
        }

        // Handle normal/live recording mode
        // Check if we need to mux pre-recording + live recording
        bool hasPreRecording = !string.IsNullOrEmpty(_preRecordingFilePath) && File.Exists(_preRecordingFilePath);
        bool hasLiveRecording = _outputFile != null && File.Exists(_outputFile.Path);

        if (hasPreRecording && hasLiveRecording)
        {
            // Mux both files
            Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] Muxing pre-recorded file with live recording");
            await MuxVideosWindows(_preRecordingFilePath, _outputFile.Path, _outputPath);

            // Clean up temporary files
            try
            {
                if (File.Exists(_preRecordingFilePath))
                    File.Delete(_preRecordingFilePath);
                if (File.Exists(_outputFile.Path))
                    File.Delete(_outputFile.Path);
            }
            catch { }
        }
        else if (hasPreRecording)
        {
            // Only pre-recording exists, use it as output
            Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] Only pre-recording exists, using as output");
            File.Copy(_preRecordingFilePath, _outputPath, true);
            try { File.Delete(_preRecordingFilePath); } catch { }
        }
        else if (hasLiveRecording)
        {
            // Only live recording exists
            Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] Only live recording exists");
            if (_outputFile.Path != _outputPath)
            {
                File.Copy(_outputFile.Path, _outputPath, true);
            }
        }

        // Update final statistics
        EncodingStatus = "Completed";
        if (!_encodingDurationSetFromFrames)
        {
            EncodingDuration = DateTime.Now - _startTime;
        }

        try
        {
            if (File.Exists(_outputPath))
            {
                var fileInfo = new FileInfo(_outputPath);
                return new CapturedVideo
                {
                    FilePath = _outputPath,
                    FileSizeBytes = fileInfo.Length,
                    Duration = EncodingDuration,
                    Time = _startTime
                };
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] Stop: failed to get file props: {ex.Message}");
        }

        return new CapturedVideo
        {
            FilePath = _outputPath ?? string.Empty,
            FileSizeBytes = 0,
            Duration = EncodingDuration,
            Time = _startTime
        };
    }

    /// <summary>
    /// Muxes pre-recorded and live recording MP4 files using FFmpeg (fallback: simple file concatenation).
    /// Windows doesn't have built-in MP4 concatenation like iOS's AVMutableComposition.
    /// </summary>
    private async Task MuxVideosWindows(string preRecordingPath, string liveRecordingPath, string outputPath)
    {
        Debug.WriteLine($"[MuxVideosWindows] Input files:");
        Debug.WriteLine($"  Pre-recorded: {preRecordingPath} (exists: {File.Exists(preRecordingPath)})");
        Debug.WriteLine($"  Live recording: {liveRecordingPath} (exists: {File.Exists(liveRecordingPath)})");
        Debug.WriteLine($"  Output: {outputPath}");

        try
        {
            // Try FFmpeg first (best quality)
            if (await TryMuxWithFFmpeg(preRecordingPath, liveRecordingPath, outputPath))
            {
                Debug.WriteLine($"[MuxVideosWindows] Successfully muxed with FFmpeg");
                return;
            }

            // Fallback: Simple binary concatenation (may not work for all MP4 files)
            Debug.WriteLine($"[MuxVideosWindows] FFmpeg not available, using simple concatenation fallback");
            await SimpleConcatenateMP4Files(preRecordingPath, liveRecordingPath, outputPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MuxVideosWindows] Error: {ex.Message}");
            throw;
        }
    }

    private async Task<bool> TryMuxWithFFmpeg(string preRecPath, string liveRecPath, string outputPath)
    {
        try
        {
            // Create concat file list for FFmpeg
            var tempListFile = Path.Combine(Path.GetTempPath(), $"concat_{Guid.NewGuid():N}.txt");
            await File.WriteAllTextAsync(tempListFile,
                $"file '{preRecPath.Replace("\\", "/")}'\n" +
                $"file '{liveRecPath.Replace("\\", "/")}'");

            var ffmpegArgs = $"-f concat -safe 0 -i \"{tempListFile}\" -c copy \"{outputPath}\"";

            var processStartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = ffmpegArgs,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(processStartInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                try { File.Delete(tempListFile); } catch { }
                return process.ExitCode == 0 && File.Exists(outputPath);
            }

            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TryMuxWithFFmpeg] Failed: {ex.Message}");
            return false;
        }
    }

    private async Task SimpleConcatenateMP4Files(string file1, string file2, string outputPath)
    {
        // WARNING: This is a naive concatenation that may not work for all MP4 files
        // It simply appends the bytes, which works for some simple cases but is not a proper MP4 mux
        Debug.WriteLine($"[SimpleConcatenateMP4Files] WARNING: Using simple byte concatenation (may produce invalid MP4)");

        using var output = File.Create(outputPath);
        using var input1 = File.OpenRead(file1);
        using var input2 = File.OpenRead(file2);

        await input1.CopyToAsync(output);
        await input2.CopyToAsync(output);

        Debug.WriteLine($"[SimpleConcatenateMP4Files] Wrote {output.Length} bytes to {outputPath}");
    }

    private async Task CreateVideoFromFrames()
    {
        if (_frames.Count == 0 || _outputFile == null)
            return;

        try
        {
            Debug.WriteLine($"[WindowsCaptureVideoEncoder] Creating real MP4 video from {_frames.Count} frames");

            // Check if we can still access the output file
            if (_outputFile == null)
            {
                Debug.WriteLine($"[WindowsCaptureVideoEncoder] Output file was disposed during CreateVideoFromFrames");
                return;
            }


            // Create MP4 video using FFmpeg-style frame-by-frame encoding
            await CreateRealMp4Video();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsCaptureVideoEncoder] CreateVideoFromFrames failed: {ex.Message}");
            throw;
        }
    }

    private async Task CreateRealMp4Video()
    {
        try
        {
            // Calculate actual recording duration
            double durationSeconds = (DateTime.Now - _startTime).TotalSeconds;
            double frameDuration = durationSeconds / _frames.Count;

            Debug.WriteLine($"[WindowsCaptureVideoEncoder] Encoding {_frames.Count} frames to MP4, duration: {durationSeconds:F2}s");

            // Use process-based approach with FFmpeg if available, otherwise create AVI
            var outputPath = _outputFile.Path;

            // Try FFmpeg first (if available on system)
            if (await TryCreateVideoWithFFmpeg(outputPath, frameDuration))
            {
                Debug.WriteLine($"[WindowsCaptureVideoEncoder] Successfully created MP4 with FFmpeg");
                return;
            }

            // Fallback: Create uncompressed AVI manually
            await CreateUncompressedAvi(outputPath);
            Debug.WriteLine($"[WindowsCaptureVideoEncoder] Created uncompressed AVI video");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsCaptureVideoEncoder] CreateRealMp4Video failed: {ex.Message}");
            throw;
        }
    }

    private async Task<bool> TryCreateVideoWithFFmpeg(string outputPath, double frameDuration)
    {
        try
        {
            // Create temporary directory for frames
            var tempDir = Path.Combine(Path.GetTempPath(), $"video_frames_{DateTime.Now.Ticks}");
            Directory.CreateDirectory(tempDir);

            try
            {
                // Save frames as numbered PNG files
                for (int i = 0; i < _frames.Count; i++)
                {
                    var frame = _frames[i];
                    if (frame == null) continue;

                    var framePath = Path.Combine(tempDir, $"frame_{i:D6}.png");

                    using var image = SKImage.FromBitmap(frame);
                    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                    using var fileStream = File.Create(framePath);
                    data.SaveTo(fileStream);
                }

                // Run FFmpeg to create MP4
                var ffmpegArgs = $"-framerate {_frameRate} -i \"{tempDir}\\frame_%06d.png\" -c:v libx264 -pix_fmt yuv420p -crf 18 \"{outputPath}\"";

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = ffmpegArgs,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(processStartInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    return process.ExitCode == 0 && File.Exists(outputPath);
                }

                return false;
            }
            finally
            {
                // Clean up temp directory
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsCaptureVideoEncoder] FFmpeg encoding failed: {ex.Message}");
            return false;
        }
    }

    private async Task CreateUncompressedAvi(string outputPath)
    {
        try
        {
            using var fileStream = File.Create(outputPath);
            using var writer = new BinaryWriter(fileStream);

            // Write AVI header
            await WriteAviHeader(writer);

            // Write frame data
            for (int i = 0; i < _frames.Count; i++)
            {
                var frame = _frames[i];
                if (frame == null) continue;

                await WriteAviFrame(writer, frame);
            }

            // Write AVI footer
            await WriteAviFooter(writer);

            Debug.WriteLine($"[WindowsCaptureVideoEncoder] Created AVI file: {new FileInfo(outputPath).Length} bytes");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsCaptureVideoEncoder] CreateUncompressedAvi failed: {ex.Message}");
            throw;
        }
    }

    private async Task WriteAviHeader(BinaryWriter writer)
    {
        // AVI RIFF header
        writer.Write("RIFF".ToCharArray());
        writer.Write((uint)0); // File size (will update later)
        writer.Write("AVI ".ToCharArray());

        // hdrl LIST
        writer.Write("LIST".ToCharArray());
        writer.Write((uint)192); // hdrl size
        writer.Write("hdrl".ToCharArray());

        // avih chunk
        writer.Write("avih".ToCharArray());
        writer.Write((uint)56); // avih size
        writer.Write((uint)(1000000 / _frameRate)); // Microseconds per frame
        writer.Write((uint)(_width * _height * 3 * _frameRate)); // Max bytes per second
        writer.Write((uint)0); // Padding
        writer.Write((uint)0x10); // Flags
        writer.Write((uint)_frames.Count); // Total frames
        writer.Write((uint)0); // Initial frames
        writer.Write((uint)1); // Number of streams
        writer.Write((uint)0); // Suggested buffer size
        writer.Write((uint)_width); // Width
        writer.Write((uint)_height); // Height
        writer.Write((uint)0); // Reserved 1
        writer.Write((uint)0); // Reserved 2
        writer.Write((uint)0); // Reserved 3
        writer.Write((uint)0); // Reserved 4

        // strl LIST
        writer.Write("LIST".ToCharArray());
        writer.Write((uint)116); // strl size
        writer.Write("strl".ToCharArray());

        // strh chunk
        writer.Write("strh".ToCharArray());
        writer.Write((uint)56); // strh size
        writer.Write("vids".ToCharArray()); // Stream type
        writer.Write("DIB ".ToCharArray()); // Handler
        writer.Write((uint)0); // Flags
        writer.Write((ushort)0); // Priority
        writer.Write((ushort)0); // Language
        writer.Write((uint)0); // Initial frames
        writer.Write((uint)_frameRate); // Scale
        writer.Write((uint)(_frameRate * _frameRate)); // Rate
        writer.Write((uint)0); // Start
        writer.Write((uint)_frames.Count); // Length
        writer.Write((uint)(_width * _height * 3)); // Suggested buffer size
        writer.Write((uint)0); // Quality
        writer.Write((uint)0); // Sample size
        writer.Write((ushort)0); // Frame left
        writer.Write((ushort)0); // Frame top
        writer.Write((ushort)_width); // Frame right
        writer.Write((ushort)_height); // Frame bottom

        // strf chunk
        writer.Write("strf".ToCharArray());
        writer.Write((uint)40); // strf size
        writer.Write((uint)40); // BITMAPINFOHEADER size
        writer.Write((uint)_width); // Width
        writer.Write((uint)_height); // Height
        writer.Write((ushort)1); // Planes
        writer.Write((ushort)24); // Bits per pixel
        writer.Write((uint)0); // Compression
        writer.Write((uint)(_width * _height * 3)); // Image size
        writer.Write((uint)0); // X pixels per meter
        writer.Write((uint)0); // Y pixels per meter
        writer.Write((uint)0); // Colors used
        writer.Write((uint)0); // Colors important

        // movi LIST header
        writer.Write("LIST".ToCharArray());
        writer.Write((uint)0); // movi size (will update later)
        writer.Write("movi".ToCharArray());
    }

    private async Task WriteAviFrame(BinaryWriter writer, SKBitmap frame)
    {
        writer.Write("00db".ToCharArray()); // Frame chunk ID

        // Convert SKBitmap to BGR24 format for AVI
        var frameData = new byte[_width * _height * 3];

        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                var pixelIndex = ((_height - 1 - y) * _width + x); // Flip vertically
                var color = frame.GetPixel(x, _height - 1 - y); // Get pixel directly and flip vertically

                var dataIndex = (y * _width + x) * 3;
                frameData[dataIndex] = color.Blue;   // B
                frameData[dataIndex + 1] = color.Green; // G
                frameData[dataIndex + 2] = color.Red;   // R
            }
        }

        writer.Write((uint)frameData.Length); // Frame size
        writer.Write(frameData); // Frame data

        // Pad to even byte boundary
        if (frameData.Length % 2 == 1)
            writer.Write((byte)0);
    }

    private async Task WriteAviFooter(BinaryWriter writer)
    {
        // Update file size in RIFF header
        var fileSize = writer.BaseStream.Length;
        writer.BaseStream.Seek(4, SeekOrigin.Begin);
        writer.Write((uint)(fileSize - 8));

        // Update movi size
        writer.BaseStream.Seek(212, SeekOrigin.Begin); // movi size offset
        var moviSize = fileSize - 220; // Size after movi header
        writer.Write((uint)moviSize);

        writer.BaseStream.Seek(0, SeekOrigin.End);
    }

    private void ReportProgress(object state)
    {
        if (_isRecording)
        {
            var elapsed = DateTime.Now - _startTime;
            ProgressReported?.Invoke(this, elapsed);
        }
    }
    private static class MFGuids
    {
        public static readonly System.Guid MF_MT_MAJOR_TYPE = new System.Guid("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
        public static readonly System.Guid MF_MT_SUBTYPE = new System.Guid("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
        public static readonly System.Guid MF_MT_FRAME_SIZE = new System.Guid("1652c33d-d6b2-4012-b834-72030849a37d");
        public static readonly System.Guid MF_MT_FRAME_RATE = new System.Guid("c459a2e8-3d2c-4e44-b132-fee5156c7bb0");
        public static readonly System.Guid MF_MT_PIXEL_ASPECT_RATIO = new System.Guid("c6376a1e-8d0a-4027-be45-6d9a0ad39bb6");
        public static readonly System.Guid MF_MT_AVG_BITRATE = new System.Guid("20332624-fb0d-4d9e-bd0d-cbf6786c102e");
        public static readonly System.Guid MF_MT_INTERLACE_MODE = new System.Guid("e2724bb8-e676-4806-b4b2-a8d6efb44ccd");
        public static readonly System.Guid MF_MT_ALL_SAMPLES_INDEPENDENT = new System.Guid("c9173739-5e56-461c-b713-46fb995cb95f");
        public static readonly System.Guid MF_MT_MPEG2_PROFILE = new System.Guid("ad76a80b-2d5c-4e0b-b375-64e520137036");
        public static readonly System.Guid MF_MT_DEFAULT_STRIDE = new System.Guid("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");
        public static readonly System.Guid MF_MT_FIXED_SIZE_SAMPLES = new System.Guid("b8ebefaf-b718-4e04-b0a9-116775e3321b");
        public static readonly System.Guid MF_MT_SAMPLE_SIZE = new System.Guid("dad3ab78-1990-408b-bce2-eba673dacc10");

        public static readonly System.Guid MFMediaType_Video = new System.Guid("73646976-0000-0010-8000-00aa00389b71");
        public static readonly System.Guid MFVideoFormat_H264 = new System.Guid("34363248-0000-0010-8000-00aa00389b71");
        public static readonly System.Guid MFVideoFormat_ARGB32 = new System.Guid("00000015-0000-0010-8000-00aa00389b71");
        public static readonly System.Guid MFVideoFormat_RGB32 = new System.Guid("00000016-0000-0010-8000-00aa00389b71");
    }


    private static void SetAttributeSize(global::Windows.Win32.Media.MediaFoundation.IMFMediaType mt, System.Guid key, uint width, uint height)
    {
        ulong val = ((ulong)width << 32) | height;
        mt.SetUINT64(key, val);
    }

    private static void SetAttributeRatio(global::Windows.Win32.Media.MediaFoundation.IMFMediaType mt, System.Guid key, uint num, uint den)
    {
        ulong val = ((ulong)num << 32) | den;
        mt.SetUINT64(key, val);
    }


    public void Dispose()
    {
        _isRecording = false;
        _progressTimer?.Dispose();

        if (_frames != null)
        {
            foreach (var frame in _frames)
            {
                frame?.Dispose();
            }
            _frames.Clear();
            _frames = null;
        }

        _outputFile = null; // Clear reference to prevent access after disposal
    }
}