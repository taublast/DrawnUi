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
    // Track first and last frame timestamps for each buffer (pre-recording mode)
    private TimeSpan _bufferAFirstTimestamp = TimeSpan.Zero;
    private TimeSpan _bufferALastTimestamp = TimeSpan.Zero;
    private TimeSpan _bufferBFirstTimestamp = TimeSpan.Zero;
    private TimeSpan _bufferBLastTimestamp = TimeSpan.Zero;

    // Track first video frame for normal recording mode (audio sync)
    private bool _hasFirstVideoFrame = false;
    private TimeSpan _firstVideoFrameTimestamp = TimeSpan.Zero;
    private volatile bool _pauseAudioWrites = false; // Pause audio during buffer swap

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
    private long _totalFrameCount; // Track total frames processed
    private CircularAudioBuffer _audioBuffer;
    private Guid _selectedAudioCodecGuid = Guid.Empty;

    public bool SupportsAudio => true;

    /// <summary>
    /// Returns true if audio encoding was successfully initialized.
    /// Check this BEFORE starting audio capture to avoid wasting resources.
    /// </summary>
    public bool IsAudioEncodingEnabled => _recordAudio && _sinkWriter != null;

    public void SetAudioBuffer(CircularAudioBuffer buffer)
    {
        _audioBuffer = buffer;
    }

    public void WriteAudio(AudioSample sample)
    {
        // CRITICAL: Early exit to avoid any overhead when audio is disabled
        if (!_recordAudio || !_isRecording || _sinkWriter == null)
            return;

        // SYNCHRONOUS write - no Task creation, no thread pool
        // Try to acquire semaphore with short timeout
        // Drop sample if we can't acquire quickly (better than lagging)
        if (!_audioSemaphore.Wait(5)) // 5ms timeout
        {
            // Semaphore busy - drop this sample to avoid backup
            return;
        }

        try
        {
            WriteAudioSampleSync(sample.Data, sample.TimestampNs);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsCaptureVideoEncoder] Audio write error: {ex.Message}");
        }
        finally
        {
            _audioSemaphore.Release();
        }
    }

    /// <summary>
    /// Synchronous audio sample write - MUST be called with _audioSemaphore held
    /// Uses native encoder with IMFTransform Resampler to normalize format, then writes to SinkWriter for AAC transcoding
    /// </summary>
    private unsafe void WriteAudioSampleSync(byte[] pcmData, long timestampNs)
    {
        // Drop audio samples if we're pausing for buffer swap
        if (_pauseAudioWrites)
        {
            return;
        }

        if (_sinkWriter == null || pcmData == null || pcmData.Length == 0)
        {
            Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] WriteAudioSample SKIP: _sinkWriter={(_sinkWriter != null ? "OK" : "NULL")}, pcmData={pcmData?.Length ?? 0}, PreRecMode={IsPreRecordingMode}");
            return;
        }

        // Normal recording mode: drop audio samples before first video frame
        if (!_hasFirstVideoFrame && !IsPreRecordingMode)
        {
            //Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] WriteAudioSample DROP: Waiting for first video frame (timestamp={timestampNs / 1_000_000.0:F1}ms), PreRecMode={IsPreRecordingMode}");
            return; // No video frame yet, drop audio
        }

        // CRITICAL: First apply audio timestamp offset to convert from audio time base to video time base
        // Audio timestamps are relative to when AudioGraphCapture started, but video timestamps are
        // relative to _captureVideoStartTime which is set BEFORE audio capture starts.
        // The offset corrects for this delay so audio and video are in the same time base.
        long adjustedTimestampNs = timestampNs + _audioTimestampOffsetNs;

        // Convert nanoseconds to 100-nanosecond units (HNS)
        long hnsTime = adjustedTimestampNs / 100;

        // In pre-recording mode, further adjust timestamps relative to buffer start
        if (IsPreRecordingMode)
        {
            // Pre-recording: adjust timestamps relative to buffer start
            TimeSpan baseTimestamp = _isBufferA ? _bufferAFirstTimestamp : _bufferBFirstTimestamp;
            if (baseTimestamp == TimeSpan.Zero)
            {
                // Drop audio samples if we haven't processed any video frames yet for this buffer
                //Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] WriteAudioSample DROP: Buffer {(_isBufferA ? "A" : "B")} not initialized yet");
                return;
            }
            long baseHns = (long)(baseTimestamp.TotalSeconds * 10_000_000L);
            hnsTime -= baseHns;
            if (hnsTime < 0) hnsTime = 0;
        }
        // In normal/live mode: hnsTime is already in video time base (after offset correction above)

        //Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] WriteAudioSample WRITE: timestamp={timestampNs / 1_000_000.0:F1}ms, hnsTime={hnsTime / 10000.0:F1}ms, length={pcmData.Length}, PreRecMode={IsPreRecordingMode}, hasNativeEncoder={_nativeAudioEncoder != IntPtr.Zero}");

        // Use native encoder with resampler (bypasses .NET MAUI COM restrictions)
        if (_nativeAudioEncoder != IntPtr.Zero)
        {
            int result = AudioEncoderNative.audio_encoder_write_pcm(
                _nativeAudioEncoder,
                pcmData,
                (uint)pcmData.Length,
                hnsTime);

            if (result != 0) // 0 = AUDIO_ENCODER_SUCCESS
            {
                string error = AudioEncoderNative.GetError(_nativeAudioEncoder);
                Debug.WriteLine($"[WindowsCaptureVideoEncoder] Native encoder write failed: {error}");
            }
            return;
        }

        // Fallback: Native encoder not available
        Debug.WriteLine($"[WindowsCaptureVideoEncoder] Native encoder not initialized, dropping audio sample");
    }

    // Pre-recording circular buffer files (2-file swap pattern like iOS)
    private string _preRecBufferA;           // First buffer file
    private string _preRecBufferB;           // Second buffer file
    private string _currentPreRecBuffer;     // Which buffer is active (A or B)
    private string _outputPath;              // Final output path
    private TimeSpan _preRecordDuration;     // Max duration per buffer (e.g., 5 seconds)
    private DateTime _currentBufferStartTime; // When current buffer started writing
    private bool _isBufferA = true;          // Track which buffer is active
    private TimeSpan _preRecordingDuration;   // Offset for live recording timestamps
    private TimeSpan _preRecordDurationLimit; // Limit for clamping offset (matches PreRecordDuration property)
    private bool _encodingDurationSetFromFrames = false;

    // GPU composition fields (temporary bridge until full Media Foundation path is implemented)
    private GRContext _grContext;                 // Provided by DrawnUi accelerated surface
    private SKSurface _gpuSurface;                // Encoder-owned GPU surface to draw overlays
    private SKImageInfo _gpuInfo;                 // Matches encoder dimensions
    private readonly object _frameLock = new();   // Protects Begin/Submit sequence
    private TimeSpan _pendingTimestamp;

    // Native audio encoder (bypasses .NET MAUI COM restrictions)
    private IntPtr _nativeAudioEncoder = IntPtr.Zero;

    // Audio timestamp offset: difference between video start time and audio capture start time
    // This corrects for the delay between _captureVideoStartTime and when AudioGraphCapture actually started
    private long _audioTimestampOffsetNs = 0;

    /// <summary>
    /// Sets the audio timestamp offset in nanoseconds.
    /// This is the time difference between when video recording started (_captureVideoStartTime)
    /// and when audio capture actually started. Audio timestamps need to be adjusted by this
    /// amount to align with video timestamps.
    /// </summary>
    public void SetAudioTimestampOffset(long offsetNs)
    {
        _audioTimestampOffsetNs = offsetNs;
        Debug.WriteLine($"[WindowsCaptureVideoEncoder] Audio timestamp offset set to {offsetNs / 1_000_000.0:F1}ms");
    }

    // Preview-from-recording support
    private readonly object _previewLock = new();
    private SKImage _latestPreviewImage; // swapped out to UI on demand
    private SKBitmap _readbackBitmap;    // reused CPU buffer to avoid per-frame allocs
    public event EventHandler PreviewAvailable;


    // Media Foundation pipeline (production path)
    private global::Windows.Win32.Media.MediaFoundation.IMFSinkWriter _sinkWriter;
    private readonly System.Threading.SemaphoreSlim _sinkWriterSemaphore = new(1, 1);
    private readonly System.Threading.SemaphoreSlim _audioSemaphore = new(1, 1); // Separate semaphore for audio to avoid blocking video
    private uint _streamIndex;
    private uint _audioStreamIndex;
    private bool _mfStarted;

    // Audio resampler (IMFTransform) - normalizes audio format before SinkWriter
    private IMFTransform _audioResampler;
    private global::Windows.Win32.Media.MediaFoundation.IMFSample _audioResamplerInputSample;

    // CsWin32 may not expose MF_VERSION directly; define the known value from mfapi.h
    private const uint MF_VERSION_CONST = 0x00020070; // (MF_SDK_VERSION<<16) | MF_API_VERSION
    private long _rtDurationPerFrame;   // 100-ns units
    private long _lastSampleTime100ns = -1;


    private int _audioSampleRate = 44100;
    private int _audioChannels = 1;
    private bool _audioInputIsFloat = false; // Tracks whether we're using Float or 16-bit PCM input
    private bool _deviceAudioIsFloat = false; // What the device actually outputs

    public void SetAudioParameters(int sampleRate, int channels, bool isFloat = false)
    {
        _audioSampleRate = sampleRate > 0 ? sampleRate : 44100;
        _audioChannels = channels > 0 ? channels : 1;
        _deviceAudioIsFloat = isFloat;
        Debug.WriteLine($"[WindowsCaptureVideoEncoder] Audio parameters set to: {_audioSampleRate}Hz, {_audioChannels}ch, Format={(_deviceAudioIsFloat ? "Float" : "PCM")}");
    }

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

    private void CreateSinkWriterInternal(string path)
    {
        unsafe
        {
            fixed (char* p = path)
            {
                // Create attributes for Sink Writer
                PInvoke.MFCreateAttributes(out var attributes, 1);

                // Allow MFReadWrite to use whatever transforms (software or
                // hardware) the system exposes for the selected output type.
                // On many systems the Microsoft AAC encoder is exposed as
                // an MFT that requires hardware transforms to be enabled;
                // forcing this flag to 0 can cause MF_E_TOPO_CODEC_NOT_FOUND
                // even though CodecQuery / MFTranscodeGetAudioOutputAvailableTypes
                // report AAC support.
                attributes.SetUINT32(MFGuids.MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS, 1);

                var hr = PInvoke.MFCreateSinkWriterFromURL(new PCWSTR(p), null, attributes, out _sinkWriter);

                Marshal.ReleaseComObject(attributes);

                if (hr.Failed)
                    throw new InvalidOperationException($"MFCreateSinkWriterFromURL failed: 0x{hr.Value:X8}");
            }
        }
    }

    public async Task InitializeAsync(string outputPath, int width, int height, int frameRate, bool recordAudio)
    {
        Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] InitializeAsync CALLED: IsPreRecordingMode={IsPreRecordingMode}");

        _outputPath = outputPath;
        _width = width;
        _height = height;
        _frameRate = Math.Max(1, frameRate);
        _recordAudio = recordAudio;
        _preRecordingDuration = TimeSpan.Zero;

        if (_recordAudio)
        {
            await DetectAvailableEncoderTypes();
        }

        // Prepare output directory
        var outputDir = Path.GetDirectoryName(_outputPath);
        Directory.CreateDirectory(outputDir);

        // Initialize Media Foundation (needed for both paths)
        var hr = PInvoke.MFStartup(MF_VERSION_CONST, 0);
        if (hr.Failed)
            throw new InvalidOperationException($"MFStartup failed: 0x{hr.Value:X8}");
        _mfStarted = true;

        _rtDurationPerFrame = (long)(10_000_000L / Math.Max(1, _frameRate)); // 100-ns units per frame
        _lastSampleTime100ns = -1;

        // Initialize limit for BOTH pre-rec and live encoders (needed for offset clamping)
        if (ParentCamera != null)
        {
            _preRecordDurationLimit = ParentCamera.PreRecordDuration;
            Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] Initialized pre-recording duration limit: {_preRecordDurationLimit.TotalSeconds:F2}s");
        }

        // Initialize based on mode
        if (IsPreRecordingMode && ParentCamera != null)
        {
            // PRE-RECORDING MODE: Use IMFSinkWriter with 2-file circular buffer (iOS-like)
            // NOTE: This encoder handles ONLY pre-recording. Live recording is handled by a separate encoder instance.
            var guid = Guid.NewGuid().ToString("N");

            // Create two temp buffer files for circular buffering
            _preRecBufferA = Path.Combine(outputDir, $"pre_rec_a_{guid}.mp4");
            _preRecBufferB = Path.Combine(outputDir, $"pre_rec_b_{guid}.mp4");
            _outputPath = outputPath;
            _preRecordDuration = ParentCamera.PreRecordDuration;
            _isBufferA = true;
            _currentPreRecBuffer = _preRecBufferA;
            _currentBufferStartTime = DateTime.UtcNow;

            Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] Pre-recording mode (2-encoder pattern):");
            Debug.WriteLine($"  Buffer A: {_preRecBufferA}");
            Debug.WriteLine($"  Buffer B: {_preRecBufferB}");
            Debug.WriteLine($"  Output path: {_outputPath}");
            Debug.WriteLine($"  Max duration per buffer: {_preRecordDuration.TotalSeconds}s");
            Debug.WriteLine($"  NOTE: Live recording will be handled by separate encoder instance");

            // Create IMFSinkWriter for first buffer (Buffer A)
            var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(_currentPreRecBuffer));
            var fileName = Path.GetFileName(_currentPreRecBuffer);
            _outputFile = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

            // Create sink writer (SAME as normal mode)
            var path = _outputFile.Path;
            await Task.Run(() =>
            {
                CreateSinkWriterInternal(path);
                ConfigureH264OutputAndRGB32Input();
                _sinkWriter.BeginWriting();
            });

            _isRecording = true;

            Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] Pre-recording started to buffer A");
            return;
        }
        else
        {
            // NORMAL RECORDING MODE: Use IMFSinkWriter directly (existing behavior)
            string targetFilePath = _outputPath;
            Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] Normal recording mode: Writing to {_outputPath}");

            // Create/replace output file
            var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(targetFilePath));
            var fileName = Path.GetFileName(targetFilePath);
            _outputFile = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

            // Create sink writer
            var path = _outputFile.Path;
            await Task.Run(() =>
            {
                CreateSinkWriterInternal(path);
                ConfigureH264OutputAndRGB32Input();
                _sinkWriter.BeginWriting();
            });
        }
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

            if (_gpuSurface != null)
            {
                canvas = _gpuSurface.Canvas;
                canvas.Clear(SKColors.Transparent);
            }
            else
            {
                canvas = null;
            }

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
        // Clamp to PreRecordDuration limit (for trimming scenarios)
        // If pre-rec buffers will be trimmed to last N seconds, live must start at N (not actual time)
        var clampedDuration = duration > _preRecordDurationLimit
            ? _preRecordDurationLimit
            : duration;

        _preRecordingDuration = clampedDuration;

        Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] Set pre-recording duration offset: actual={duration.TotalSeconds:F2}s, limit={_preRecordDurationLimit.TotalSeconds:F2}s, using={clampedDuration.TotalSeconds:F2}s");
    }

    public async Task StartAsync()
    {
        Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] ========== StartAsync CALLED ==========");
        Debug.WriteLine($"  IsPreRecordingMode: {IsPreRecordingMode}");
        Debug.WriteLine($"  _sinkWriter != null: {_sinkWriter != null}");
        Debug.WriteLine($"  _isRecording: {_isRecording}");

        // PRE-RECORDING MODE: This encoder handles ONLY pre-recording.
        // In the 2-encoder pattern, there is NO transition from pre-rec to live.
        // Live recording is handled by a completely separate encoder instance.
        if (IsPreRecordingMode)
        {
            Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] Pre-recording encoder - StartAsync is NO-OP (2-encoder pattern)");
            Debug.WriteLine($"  NOTE: Live recording will be handled by separate encoder instance");
            Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] ========== StartAsync END ==========");
            return;
        }

        Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] Normal mode - starting recording");
        Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] ========== StartAsync END ==========");

        // NORMAL RECORDING MODE: Just start recording (existing behavior)
        _isRecording = true;
        _startTime = DateTime.Now;

        // Reset first video frame tracking for audio sync
        _hasFirstVideoFrame = false;
        _firstVideoFrameTimestamp = TimeSpan.Zero;

        // CRITICAL: Reset timestamp tracking for NEW recording (prevents duration from including pre-recording)
        _lastSampleTime100ns = -1;
        _encodingDurationSetFromFrames = false;

        Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] RESET: _lastSampleTime100ns = -1, _hasFirstVideoFrame = false");

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
    }

    public async Task AddFrameAsync(SKBitmap bitmap, TimeSpan timestamp)
    {
        // Track first and last frame timestamps for the active buffer
        if (IsPreRecordingMode)
        {
            if (_isBufferA)
            {
                if (_bufferAFirstTimestamp == TimeSpan.Zero || timestamp < _bufferAFirstTimestamp)
                    _bufferAFirstTimestamp = timestamp;
                _bufferALastTimestamp = timestamp;
            }
            else
            {
                if (_bufferBFirstTimestamp == TimeSpan.Zero || timestamp < _bufferBFirstTimestamp)
                    _bufferBFirstTimestamp = timestamp;
                _bufferBLastTimestamp = timestamp;
            }
        }
        else
        {
            // Normal recording mode: track first video frame for audio sync
            if (!_hasFirstVideoFrame)
            {
                _firstVideoFrameTimestamp = timestamp;
                _hasFirstVideoFrame = true;
                Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] First video frame timestamp: {timestamp.TotalMilliseconds:F1}ms");
            }
        }

        if (!_isRecording)
            return;

        // Track frame count for debugging
        _totalFrameCount++;

        await _sinkWriterSemaphore.WaitAsync();
        try
        {
            // PRE-RECORDING MODE: Check if we need to swap buffers
            if (IsPreRecordingMode && _sinkWriter != null)
            {
                // Check if current buffer duration exceeded
                var elapsed = DateTime.UtcNow - _currentBufferStartTime;
                if (elapsed >= _preRecordDuration)
                {
                    await SwapPreRecordingBuffer();

                    // CRITICAL: The current frame belongs to the NEW buffer now.
                    // We must initialize the timestamp trackers for the new buffer with this frame's timestamp
                    // so that the base timestamp calculation below works correctly (resulting in 0 for the first frame).
                    if (_isBufferA)
                    {
                        _bufferAFirstTimestamp = timestamp;
                        _bufferALastTimestamp = timestamp;
                    }
                    else
                    {
                        _bufferBFirstTimestamp = timestamp;
                        _bufferBLastTimestamp = timestamp;
                    }
                }

                // Fall through to normal frame writing (same RGB32 encoding path!)
            }

            // NORMAL RECORDING MODE (and pre-recording): Write directly to IMFSinkWriter
            if (_sinkWriter == null || !_isRecording)
            {
                return;
            }

            // Ensure format is BGRA8888, premultiplied, matching encoder size
            SKBitmap source = bitmap;
            if (bitmap.ColorType != SKColorType.Bgra8888 || bitmap.Width != _width || bitmap.Height != _height)
            {
                var info = new SKImageInfo(_width, _height, SKColorType.Bgra8888, SKAlphaType.Premul);
                source = new SKBitmap(info);
                using var canvas = new SKCanvas(source);
                canvas.DrawBitmap(bitmap, new SKRect(0, 0, _width, _height));
            }

            try
            {
                // Capture state for background thread
                var dataSize = (uint)(_width * _height * 4);
                var width = _width;
                var height = _height;
                var streamIndex = _streamIndex;
                var rtDurationPerFrame = _rtDurationPerFrame;
                var pendingTimestamp = _pendingTimestamp;
                var isPreRecordingMode = IsPreRecordingMode;
                var isBufferA = _isBufferA;
                var bufferAFirstTimestamp = _bufferAFirstTimestamp;
                var bufferBFirstTimestamp = _bufferBFirstTimestamp;

                // We need to copy the bitmap data to a byte array or similar to pass to the background thread safely
                // OR we can just do the memory copy inside the Task.Run if we keep 'source' alive.
                // Since 'source' is disposed in finally, we must ensure Task.Run completes before finally.

                await Task.Run(() =>
                {
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
                                int rowBytes = width * 4;

                                for (int y = 0; y < height; y++)
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

                            // Calculate sample time
                            long sampleTime = (long)(pendingTimestamp.TotalSeconds * 10_000_000L);

                            // Adjust timestamps in pre-recording mode only
                            if (isPreRecordingMode)
                            {
                                TimeSpan baseTimestamp = isBufferA ? bufferAFirstTimestamp : bufferBFirstTimestamp;
                                sampleTime -= (long)(baseTimestamp.TotalSeconds * 10_000_000L);
                            }
                            // In normal/live mode: Use absolute timestamps (no adjustment)
                            // MediaComposition will handle timeline alignment automatically
                            
                            if (sampleTime <= _lastSampleTime100ns)
                            {
                                sampleTime = _lastSampleTime100ns + rtDurationPerFrame;
                            }
                            
                            sample.SetSampleTime(sampleTime);
                            sample.SetSampleDuration(rtDurationPerFrame);

                            _sinkWriter.WriteSample(streamIndex, sample);

                            _lastSampleTime100ns = sampleTime;

                            // Update statistics (thread-safe increment needed or just simple increment since we are in semaphore)
                            EncodedFrameCount++;
                            EncodedDataSize += (long)dataSize;
                            // EncodingDuration is calculated on read, so no write needed here usually, but we set it
                            // EncodingDuration = DateTime.Now - _startTime; // This accesses _startTime which is fine
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
                });

                EncodingDuration = DateTime.Now - _startTime;
                EncodingStatus = "Encoding";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] AddFrameAsync failed: {ex.Message}");
                // Don't throw here to avoid crashing the app loop, just log
            }
            finally
            {
                if (!ReferenceEquals(source, bitmap))
                    source.Dispose();
            }
        }
        finally
        {
            _sinkWriterSemaphore.Release();
        }
    }

    public async Task AddAudioSamplesAsync(byte[] pcmData, long timestampNs)
    {
        if (!_isRecording || !_recordAudio || _sinkWriter == null)
            return;

        // Use separate audio semaphore to avoid blocking video writes
        await _audioSemaphore.WaitAsync();
        try
        {
            if (_sinkWriter == null) return;

            var dataSize = (uint)pcmData.Length;
            var hr = PInvoke.MFCreateMemoryBuffer(dataSize, out var mediaBuffer);
            if (hr.Failed) return;

            try
            {
                unsafe
                {
                    byte* dst;
                    uint maxLen, curLen;
                    mediaBuffer.Lock(&dst, &maxLen, &curLen);
                    Marshal.Copy(pcmData, 0, (IntPtr)dst, (int)dataSize);
                    mediaBuffer.SetCurrentLength(dataSize);
                    mediaBuffer.Unlock();
                }

                hr = PInvoke.MFCreateSample(out var sample);
                if (hr.Failed) return;

                try
                {
                    sample.AddBuffer(mediaBuffer);

                    long hnsTime = timestampNs / 100;
                    if (IsPreRecordingMode)
                    {
                        TimeSpan baseTimestamp = _isBufferA ? _bufferAFirstTimestamp : _bufferBFirstTimestamp;
                        if (baseTimestamp == TimeSpan.Zero)
                        {
                            // Drop audio samples if we haven't processed any video frames yet for this buffer
                            // preventing out-of-sync timestamps
                            return;
                        }

                        long baseHns = baseTimestamp.Ticks;
                        hnsTime -= baseHns;
                        if (hnsTime < 0) hnsTime = 0;
                    }
                    else
                    {
                        // Normal recording mode: drop audio samples before first video frame
                        // This prevents audio/video sync issues (audio crack) at the start
                        if (!_hasFirstVideoFrame)
                            return; // No video frame yet, drop audio

                        // Adjust audio timestamp relative to first video frame
                        long baseHns = _firstVideoFrameTimestamp.Ticks;
                        hnsTime -= baseHns;
                        if (hnsTime < 0) hnsTime = 0;
                    }

                    sample.SetSampleTime(hnsTime);

                    // Duration = samples per channel / sampleRate
                    // pcmData is 16-bit PCM, interleaved channels.
                    int bytesPerSamplePerChannel = 2;
                    int channels = Math.Max(1, _audioChannels);
                    double samplesPerChannel = pcmData.Length / (double)(bytesPerSamplePerChannel * channels);
                    int sampleRate = Math.Max(1, _audioSampleRate);
                    long durationHns = (long)((samplesPerChannel / sampleRate) * 10_000_000.0);
                    sample.SetSampleDuration(durationHns);

                    _sinkWriter.WriteSample(_audioStreamIndex, sample);
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
            Debug.WriteLine($"[WindowsCaptureVideoEncoder] AddAudioSamplesAsync error: {ex}");
        }
        finally
        {
            _audioSemaphore.Release();
        }
    }

    public TimeSpan LiveRecordingDuration
    {
        get
        {
            if (_isRecording)
            {
                return DateTime.Now - _startTime;
            }
            return TimeSpan.Zero;
        }
    }

    public async Task AbortAsync()
    {
        Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] AbortAsync CALLED");

        _isRecording = false;
        _progressTimer?.Dispose();

        EncodingStatus = "Canceled";

        await _sinkWriterSemaphore.WaitAsync();
        try
        {
            // Finalize native audio encoder first (drain resampler buffer)
            if (_nativeAudioEncoder != IntPtr.Zero)
            {
                Debug.WriteLine($"[WindowsCaptureVideoEncoder] Finalizing native audio encoder (draining resampler)...");
                int result = AudioEncoderNative.audio_encoder_finalize(_nativeAudioEncoder);
                if (result != 0)
                {
                    string error = AudioEncoderNative.GetError(_nativeAudioEncoder);
                    Debug.WriteLine($"[WindowsCaptureVideoEncoder] Audio encoder finalize failed: {error}");
                }
            }

            if (_sinkWriter != null)
            {
                try
                {
                    // Try to finalize to close file handle properly, but ignore errors
                    await Task.Run(() =>
                    {
                        try
                        {
                            _sinkWriter.Finalize();
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                    });
                }
                catch { }
                finally
                {
                    try
                    {
                        if (_sinkWriter != null)
                        {
                            Marshal.ReleaseComObject(_sinkWriter);
                        }
                    }
                    catch { }
                    _sinkWriter = null;
                }
            }
        }
        finally
        {
            _sinkWriterSemaphore.Release();
        }

        // Cleanup files
        try
        {
            if (_outputFile != null)
            {
                try { await _outputFile.DeleteAsync(); } catch { }
                _outputFile = null;
            }
            if (!string.IsNullOrEmpty(_preRecBufferA) && File.Exists(_preRecBufferA)) File.Delete(_preRecBufferA);
            if (!string.IsNullOrEmpty(_preRecBufferB) && File.Exists(_preRecBufferB)) File.Delete(_preRecBufferB);
            if (!string.IsNullOrEmpty(_outputPath) && File.Exists(_outputPath)) File.Delete(_outputPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] Abort cleanup error: {ex.Message}");
        }

        lock (_previewLock)
        {
            _latestPreviewImage?.Dispose();
            _latestPreviewImage = null;
        }
        _readbackBitmap?.Dispose();
        _readbackBitmap = null;
        _gpuSurface?.Dispose();
        _gpuSurface = null;
    }

    public async Task<CapturedVideo> StopAsync()
    {
        Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] StopAsync CALLED: IsPreRecordingMode={IsPreRecordingMode}");

        _isRecording = false;
        _progressTimer?.Dispose();

        // Update status
        EncodingStatus = "Stopping";

        // Pre-recording mode: Handle file finalization and muxing
        bool wasPreRecordingMode = false;
        if (!string.IsNullOrEmpty(_preRecBufferA) || !string.IsNullOrEmpty(_preRecBufferB))
        {
            wasPreRecordingMode = true;

            // Finalize whichever sink is active
            await _sinkWriterSemaphore.WaitAsync();
            try
            {
                // Finalize native audio encoder first (drain resampler buffer)
                if (_nativeAudioEncoder != IntPtr.Zero)
                {
                    Debug.WriteLine($"[WindowsCaptureVideoEncoder] Finalizing native audio encoder (draining resampler)...");
                    int result = AudioEncoderNative.audio_encoder_finalize(_nativeAudioEncoder);
                    if (result != 0)
                    {
                        string error = AudioEncoderNative.GetError(_nativeAudioEncoder);
                        Debug.WriteLine($"[WindowsCaptureVideoEncoder] Audio encoder finalize failed: {error}");
                    }
                }

                if (_sinkWriter != null)
                {
                    try
                    {
                        await Task.Run(() => _sinkWriter.Finalize());
                    }
                    catch (Exception ex)
                    {
                        Super.Log($"[WindowsCaptureVideoEncoder #{_instanceId}] Finalize error: {ex.Message}");
                    }
                    finally
                    {
                        try
                        {
                            if (_sinkWriter != null)
                            {
                                Marshal.ReleaseComObject(_sinkWriter);
                            }
                        }
                        catch { }
                        _sinkWriter = null;
                    }

                    // CRITICAL: Verify file is fully flushed and readable
                    // Wait until IMFSourceReader can successfully open the file
                    string fileToVerify = _isBufferA ? _preRecBufferA : _preRecBufferB;
                    if (!string.IsNullOrEmpty(fileToVerify) && File.Exists(fileToVerify))
                    {
                        await WaitForFileReadable(fileToVerify);
                    }
                }
                else
                {
                    Super.Log($"[WindowsCaptureVideoEncoder #{_instanceId}] No active sink writer to finalize");
                }
            }
            finally
            {
                _sinkWriterSemaphore.Release();
            }

            // Determine which files exist
            bool hasBufferA = !string.IsNullOrEmpty(_preRecBufferA) && File.Exists(_preRecBufferA);
            bool hasBufferB = !string.IsNullOrEmpty(_preRecBufferB) && File.Exists(_preRecBufferB);

            // Collect files to output (in order: older buffer → newer buffer)
            var filesToOutput = new List<string>();

            // Always add buffers in chronological order: older first, newer second
            // The buffer that is NOT currently active is the older one
            if (_isBufferA)
            {
                // Buffer B is older, Buffer A is newer
                if (hasBufferB) filesToOutput.Add(_preRecBufferB);
                if (hasBufferA) filesToOutput.Add(_preRecBufferA);
            }
            else
            {
                // Buffer A is older, Buffer B is newer
                if (hasBufferA) filesToOutput.Add(_preRecBufferA);
                if (hasBufferB) filesToOutput.Add(_preRecBufferB);
            }

            if (filesToOutput.Count == 0)
            {
                Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] WARNING: No pre-recording buffers to output!");
            }
            else if (filesToOutput.Count == 1)
            {

                // Only one buffer - check if it needs trimming
                var singleBuffer = filesToOutput[0];
                var sourceSize = new FileInfo(singleBuffer).Length;

                // Extra debug: check if this buffer is the live buffer by comparing file paths
                if (_outputPath == singleBuffer)
                {
                    Debug.WriteLine($"  [WARNING] Single buffer is the output path itself! This may indicate a logic error.");
                }


                try
                {
                    var singleDuration = await GetVideoDuration(singleBuffer);

                    if (singleDuration > _preRecordDuration)
                    {
                        // Single buffer exceeds limit - trim to last N seconds
                        var trimmedBuffer = await TrimVideoFromEnd(singleBuffer, _preRecordDuration);
                        File.Copy(trimmedBuffer, _outputPath, overwrite: true);

                        // Clean up trimmed temp file
                        if (trimmedBuffer != singleBuffer && File.Exists(trimmedBuffer))
                        {
                            try { File.Delete(trimmedBuffer); } catch { }
                        }
                    }
                    else
                    {
                        // Single buffer within limit - just copy
                        Debug.WriteLine($"  Single buffer within limit, copying as-is");
                        File.Copy(singleBuffer, _outputPath, overwrite: true);
                    }

                    var outputSize = new FileInfo(_outputPath).Length;
                    Debug.WriteLine($"  Result: {outputSize / 1024} KB");

                    // Check actual output duration
                    var finalDuration = await GetVideoDuration(_outputPath);
                    Debug.WriteLine($"[INVESTIGATION] Final pre-rec output duration: {finalDuration.TotalSeconds:F3}s");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"  Duration check failed: {ex.Message}, copying without trim");
                    File.Copy(singleBuffer, _outputPath, overwrite: true);
                }

                // Clean up temp files
                CleanupPreRecTempFiles();
            }
            else if (filesToOutput.Count == 2)
            {
                // Two buffers - robust: trim older buffer to valid timestamp duration, then concatenate, then trim combined file to last N seconds
                var olderBuffer = filesToOutput[0];
                var newerBuffer = filesToOutput[1];

                // Use tracked timestamps for valid duration
                TimeSpan validOlderDuration = TimeSpan.Zero;
                if (olderBuffer == _preRecBufferA)
                {
                    validOlderDuration = _bufferALastTimestamp - _bufferAFirstTimestamp;
                }
                else if (olderBuffer == _preRecBufferB)
                {
                    validOlderDuration = _bufferBLastTimestamp - _bufferBFirstTimestamp;
                }
                else
                {
                    validOlderDuration = await GetVideoDuration(olderBuffer); // fallback
                }

                string trimmedOlderPath = null;
                string combinedPath = Path.Combine(Path.GetDirectoryName(_outputPath), $"combined_{Guid.NewGuid():N}.mp4");
                string trimmedPath = null;
                try
                {
                    // DEBUG: SKIP TRIMMING OLDER BUFFER - use full content as it should be valid now
                    trimmedOlderPath = olderBuffer;

                    // Step 2: Concatenate trimmed older buffer and newer buffer
                    await MuxVideos(trimmedOlderPath, newerBuffer, combinedPath);

                    // Step 3: Trim the combined file to the last N seconds
                    // NOTE: We use the combined path as input, and trim to keep the last N seconds
                    trimmedPath = await TrimVideoFromEnd(combinedPath, _preRecordDuration);

                    // Step 4: Copy trimmed file to output
                    File.Copy(trimmedPath, _outputPath, overwrite: true);

                    if (!File.Exists(_outputPath))
                    {
                        Super.Log($"[WindowsCaptureVideoEncoder #{_instanceId}] ERROR: Mux+Trim output file not created!");
                    }
                }
                catch (Exception ex)
                {
                    Super.Log($"[WindowsCaptureVideoEncoder #{_instanceId}] Robust two-buffer finalization failed: {ex}");

                    File.Copy(newerBuffer, _outputPath, overwrite: true);
                    if (!File.Exists(_outputPath))
                    {
                        Super.Log($"[WindowsCaptureVideoEncoder #{_instanceId}] ERROR: Fallback output file not created!");
                    }
                }
                finally
                {
                    // Clean up temp files
                    if (trimmedPath != null && File.Exists(trimmedPath))
                    {
                        try { File.Delete(trimmedPath); } catch { }
                    }
                    if (combinedPath != null && File.Exists(combinedPath))
                    {
                        try { File.Delete(combinedPath); } catch { }
                    }
                    if (trimmedOlderPath != null && trimmedOlderPath != olderBuffer && File.Exists(trimmedOlderPath))
                    {
                        try { File.Delete(trimmedOlderPath); } catch { }
                    }
                    CleanupPreRecTempFiles();
                }
            }

        }

        // Finalize Media Foundation (if not already done)
        Debug.WriteLine($"[WindowsCaptureVideoEncoder] StopAsync: About to finalize (wasPreRecordingMode={wasPreRecordingMode})");
        try
        {
            await _sinkWriterSemaphore.WaitAsync();
            Debug.WriteLine($"[WindowsCaptureVideoEncoder] StopAsync: Semaphore acquired, _sinkWriter={(_sinkWriter != null ? "EXISTS" : "NULL")}, _nativeAudioEncoder={_nativeAudioEncoder}");
            try
            {
                // Finalize native audio encoder first (drain resampler buffer)
                if (_nativeAudioEncoder != IntPtr.Zero)
                {
                    Debug.WriteLine($"[WindowsCaptureVideoEncoder] Finalizing native audio encoder (draining resampler)...");
                    int result = AudioEncoderNative.audio_encoder_finalize(_nativeAudioEncoder);
                    if (result != 0)
                    {
                        string error = AudioEncoderNative.GetError(_nativeAudioEncoder);
                        Debug.WriteLine($"[WindowsCaptureVideoEncoder] Audio encoder finalize failed: {error}");
                    }
                    else
                    {
                        Debug.WriteLine($"[WindowsCaptureVideoEncoder] Audio encoder finalize returned success");
                    }
                }
                else
                {
                    Debug.WriteLine($"[WindowsCaptureVideoEncoder] No native audio encoder to finalize");
                }

                if (_sinkWriter != null)
                {
                    try
                    {
                        await Task.Run(() => _sinkWriter.Finalize());
                    }
                    catch { }
                    finally
                    {
                        try
                        {
                            if (_sinkWriter != null)
                            {
                                Marshal.ReleaseComObject(_sinkWriter);
                            }
                        }
                        catch { }
                        _sinkWriter = null;
                    }
                }
            }
            finally
            {
                _sinkWriterSemaphore.Release();
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

        // Handle normal recording mode (no pre-recording)
        if (!wasPreRecordingMode && _outputFile != null && File.Exists(_outputFile.Path))
        {
            var fileSize = new FileInfo(_outputFile.Path).Length;
            //Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] ========== NORMAL RECORDING STOP DEBUG ==========");
            //Debug.WriteLine($"  Output file path: {_outputFile.Path}");
            //Debug.WriteLine($"  Output file size: {fileSize / 1024} KB ({fileSize} bytes)");
            //Debug.WriteLine($"  _outputPath: {_outputPath}");
            //Debug.WriteLine($"  EncodedFrameCount: {EncodedFrameCount}");
            //Debug.WriteLine($"  EncodingDuration: {EncodingDuration.TotalSeconds:F2}s");

            if (_outputFile.Path != _outputPath)
            {
                Debug.WriteLine($"  Copying file to: {_outputPath}");
                File.Copy(_outputFile.Path, _outputPath, true);
                var copiedSize = new FileInfo(_outputPath).Length;
                Debug.WriteLine($"  Copied successfully: {copiedSize / 1024} KB");
            }
        }
        //else if (!wasPreRecordingMode)
        //{
        //    Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] ========== NORMAL RECORDING STOP DEBUG ==========");
        //    Debug.WriteLine($"  ERROR: Output file not found!");
        //    Debug.WriteLine($"  _outputFile != null: {_outputFile != null}");
        //    Debug.WriteLine($"  _outputFile?.Path: {_outputFile?.Path}");
        //    Debug.WriteLine($"  File.Exists: {(_outputFile != null ? File.Exists(_outputFile.Path) : false)}");
        //    Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] ========== NORMAL RECORDING STOP END ==========");
        //}

        // CRITICAL: Use actual last sample timestamp for duration, not wall-clock time
        // This fixes the issue where EncodingDuration includes idle time before first frame
        if (_lastSampleTime100ns > 0)
        {
            EncodingDuration = TimeSpan.FromSeconds(_lastSampleTime100ns / 10_000_000.0);
            _encodingDurationSetFromFrames = true;
            //Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] Set duration from last sample timestamp: {EncodingDuration.TotalSeconds:F3}s");
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
            Super.Log($"[WindowsCaptureVideoEncoder #{_instanceId}] Stop: failed to get file props: {ex.Message}");
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
    /// Swaps pre-recording buffer (A → B or B → A) - circular buffer pattern like iOS
    /// </summary>
    private async Task SwapPreRecordingBuffer()
    {
        Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] Pre-rec buffer swap triggered");
        Debug.WriteLine($"  Current buffer: {(_isBufferA ? "A" : "B")}");
        Debug.WriteLine($"  Duration: {(DateTime.UtcNow - _currentBufferStartTime).TotalSeconds:F2}s");

        // CRITICAL: Pause audio writes to prevent samples arriving during finalization
        _pauseAudioWrites = true;
        Debug.WriteLine($"[WindowsCaptureVideoEncoder] Audio writes PAUSED for buffer swap");
        
        // Wait for any in-flight audio samples to complete (50ms should be plenty)
        await Task.Delay(50);

        // Finalize current buffer
        if (_sinkWriter != null)
        {
            try
            {
                // CRITICAL: Finalize native audio encoder FIRST (drain resampler buffer)
                if (_nativeAudioEncoder != IntPtr.Zero)
                {
                    Debug.WriteLine($"[WindowsCaptureVideoEncoder] Finalizing native audio encoder before buffer swap...");
                    int result = AudioEncoderNative.audio_encoder_finalize(_nativeAudioEncoder);
                    if (result != 0)
                    {
                        string error = AudioEncoderNative.GetError(_nativeAudioEncoder);
                        Debug.WriteLine($"[WindowsCaptureVideoEncoder] Audio encoder finalize failed: {error}");
                    }
                    
                    // Destroy the encoder (it references the old SinkWriter)
                    AudioEncoderNative.audio_encoder_destroy(_nativeAudioEncoder);
                    _nativeAudioEncoder = IntPtr.Zero;
                    Debug.WriteLine($"[WindowsCaptureVideoEncoder] Native audio encoder destroyed");
                }
                
                await Task.Run(() => _sinkWriter.Finalize());
            }
            catch (Exception ex)
            {
                Super.Log($"[WindowsCaptureVideoEncoder #{_instanceId}] Buffer finalize error: {ex.Message}");
            }
            finally
            {
                try
                {
                    if (_sinkWriter != null)
                    {
                        Marshal.ReleaseComObject(_sinkWriter);
                    }
                }
                catch { }
                _sinkWriter = null;
            }
        }

        // Switch to other buffer
        _isBufferA = !_isBufferA;

        // On buffer swap, reset timestamp tracking for the new buffer
        if (_isBufferA)
        {
            // Swapped to A, so reset A's timestamps
            _bufferAFirstTimestamp = TimeSpan.Zero;
            _bufferALastTimestamp = TimeSpan.Zero;
        }
        else
        {
            // Swapped to B, so reset B's timestamps
            _bufferBFirstTimestamp = TimeSpan.Zero;
            _bufferBLastTimestamp = TimeSpan.Zero;
        }

        // Determine which buffer to write to next
        string nextBuffer = _isBufferA ? _preRecBufferA : _preRecBufferB;
        string oldBuffer = _isBufferA ? _preRecBufferB : _preRecBufferA;

        // Do NOT delete the old buffer here. Both buffers are needed for correct prerecording reconstruction.

        // Create new sink writer for next buffer
        _currentPreRecBuffer = nextBuffer;
        _currentBufferStartTime = DateTime.UtcNow;

        var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(_currentPreRecBuffer));
        var fileName = Path.GetFileName(_currentPreRecBuffer);
        _outputFile = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

        var path = _outputFile.Path;
        await Task.Run(() =>
        {
            CreateSinkWriterInternal(path);
            ConfigureH264OutputAndRGB32Input();
            _sinkWriter.BeginWriting();
        });

        // Reset last sample time for the new writer to ensure timestamps start from 0
        _lastSampleTime100ns = -1;

        // Resume audio writes now that new encoder is ready
        _pauseAudioWrites = false;
        Debug.WriteLine($"[WindowsCaptureVideoEncoder] Audio writes RESUMED");

        Debug.WriteLine($"[WindowsCaptureVideoEncoder #{_instanceId}] Swapped to buffer {(_isBufferA ? "A" : "B")}: {nextBuffer}");
    }

    /// <summary>
    /// Muxes two MP4 files using Windows Media Foundation APIs (IMFSourceReader + IMFSinkWriter).
    /// NO FFmpeg - Windows native APIs only.
    /// </summary>
    private async Task MuxVideosWindows(string preRecordingPath, string liveRecordingPath, string outputPath)
    {
        Debug.WriteLine($"[MuxVideosWindows] ========== MUXING WITH WINDOWS MEDIA FOUNDATION ==========");
        Debug.WriteLine($"[MuxVideosWindows] Pre-recording: {preRecordingPath} ({(File.Exists(preRecordingPath) ? new FileInfo(preRecordingPath).Length / 1024 : 0)} KB)");
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
                fixed (char* p1 = preRecordingPath)
                {
                    var hr = PInvoke.MFCreateSourceReaderFromURL(new PCWSTR(p1), null, out reader1);
                    if (hr.Failed)
                        throw new InvalidOperationException($"MFCreateSourceReaderFromURL failed for pre-rec: 0x{hr.Value:X8}");
                }

                fixed (char* p2 = liveRecordingPath)
                {
                    var hr = PInvoke.MFCreateSourceReaderFromURL(new PCWSTR(p2), null, out reader2);
                    if (hr.Failed)
                        throw new InvalidOperationException($"MFCreateSourceReaderFromURL 2 failed for live: 0x{hr.Value:X8}");
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
            Marshal.ReleaseComObject(inputMediaType);

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
        }
        catch (Exception ex)
        {
            Super.Log($"[MuxVideosWindows] ERROR: {ex}");
            throw;
        }
        finally
        {
            // Clean up COM objects
            if (reader1 != null)
            {
                Marshal.ReleaseComObject(reader1);
            }
            if (reader2 != null)
            {
                Marshal.ReleaseComObject(reader2);
            }
            if (writer != null)
            {
                Marshal.ReleaseComObject(writer);
            }
        }

        await Task.CompletedTask;
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
            var clip1 = await global::Windows.Media.Editing.MediaClip.CreateFromFileAsync(file1);
            var clip2 = await global::Windows.Media.Editing.MediaClip.CreateFromFileAsync(file2);
            
            // Debug: Log clip durations
            Debug.WriteLine($"[MediaComposition] Clip 1 (pre-rec) duration: {clip1.OriginalDuration.TotalSeconds:F3}s");
            Debug.WriteLine($"[MediaComposition] Clip 2 (live) duration: {clip2.OriginalDuration.TotalSeconds:F3}s");
            
            composition.Clips.Add(clip1);
            composition.Clips.Add(clip2);

            Debug.WriteLine($"[MediaComposition] Added {composition.Clips.Count} clips to composition");
            Debug.WriteLine($"[MediaComposition] Total composition duration: {composition.Duration.TotalSeconds:F3}s");

            // Create encoding profile matching the source dimensions and framerate
            var profile = global::Windows.Media.MediaProperties.MediaEncodingProfile.CreateMp4(
                global::Windows.Media.MediaProperties.VideoEncodingQuality.HD1080p);

            // Override with actual settings to avoid unnecessary re-encoding/scaling
            profile.Video.Width = (uint)_width;
            profile.Video.Height = (uint)_height;
            profile.Video.FrameRate.Numerator = (uint)_frameRate;
            profile.Video.FrameRate.Denominator = 1;
            profile.Video.Bitrate = (uint)(Math.Max(1, _width * _height) * Math.Max(1, _frameRate) * 4 / 10); // Match encoder bitrate

            // Ensure H.264 + AAC (matches our encoder output)
            profile.Video.Subtype = "H264";
            profile.Audio.Subtype = "AAC";

            Debug.WriteLine($"[MediaComposition] Rendering to file (will use stream copy if possible)...");

            // Render composition to file (automatically uses stream copy if compatible, otherwise re-encodes)
            var result = await composition.RenderToFileAsync(
                outputFile,
                global::Windows.Media.Editing.MediaTrimmingPreference.Fast,
                profile
            );

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
            Super.Log($"[MediaComposition] ERROR: {ex.Message}");
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
            Super.Log($"[Muxing] MediaComposition failed, falling back to Media Foundation: {ex.Message}");

            // Fallback to Media Foundation (may have keyframe issues but works as last resort)
            await MuxVideosWindows(preRecPath, liveRecPath, outputPath);
            return outputPath;
        }
    }

    /// <summary>
    /// Gets the duration of a video file using MediaComposition.
    /// </summary>
    private async Task<TimeSpan> GetVideoDuration(string videoPath)
    {
        var file = await global::Windows.Storage.StorageFile.GetFileFromPathAsync(videoPath);
        var clip = await global::Windows.Media.Editing.MediaClip.CreateFromFileAsync(file);
        return clip.OriginalDuration;
    }

    /// <summary>
    /// Trims video to keep only the last N seconds by trimming the beginning.
    /// Returns path to trimmed file.
    /// </summary>
    private async Task<string> TrimVideoFromEnd(string inputPath, TimeSpan keepDuration)
    {
        var file = await global::Windows.Storage.StorageFile.GetFileFromPathAsync(inputPath);
        var clip = await global::Windows.Media.Editing.MediaClip.CreateFromFileAsync(file);

        var totalDuration = clip.OriginalDuration;
        var trimStart = totalDuration - keepDuration;

        if (trimStart <= TimeSpan.Zero)
        {
            // No trim needed, duration is already <= keepDuration
            return inputPath;
        }

        // Trim from start to keep last N seconds
        clip.TrimTimeFromStart = trimStart;

        // Create output file
        var trimmedPath = Path.Combine(
            Path.GetDirectoryName(inputPath),
            $"trimmed_{Path.GetFileName(inputPath)}"
        );

        if (!File.Exists(trimmedPath))
        {
            await File.WriteAllBytesAsync(trimmedPath, Array.Empty<byte>());
        }

        var outputFile = await global::Windows.Storage.StorageFile.GetFileFromPathAsync(trimmedPath);

        // Render single clip with trim
        var composition = new global::Windows.Media.Editing.MediaComposition();
        composition.Clips.Add(clip);

        // Create encoding profile matching the source dimensions and framerate
        var profile = global::Windows.Media.MediaProperties.MediaEncodingProfile.CreateMp4(
            global::Windows.Media.MediaProperties.VideoEncodingQuality.HD1080p);

        // Override with actual settings to avoid unnecessary re-encoding/scaling
        profile.Video.Width = (uint)_width;
        profile.Video.Height = (uint)_height;
        profile.Video.FrameRate.Numerator = (uint)_frameRate;
        profile.Video.FrameRate.Denominator = 1;
        profile.Video.Bitrate = (uint)(Math.Max(1, _width * _height) * Math.Max(1, _frameRate) * 4 / 10); // Match encoder bitrate

        profile.Video.Subtype = "H264";
        profile.Audio.Subtype = "AAC";

        await composition.RenderToFileAsync(
            outputFile,
            global::Windows.Media.Editing.MediaTrimmingPreference.Fast,
            profile
        );

        Debug.WriteLine($"[TrimVideoFromEnd] Trimmed {inputPath}");
        Debug.WriteLine($"  Original duration: {totalDuration.TotalSeconds:F2}s");
        Debug.WriteLine($"  Trim start: {trimStart.TotalSeconds:F2}s");
        Debug.WriteLine($"  Output duration: {keepDuration.TotalSeconds:F2}s");

        return trimmedPath;
    }

    /// <summary>
    /// Copies all video samples from an IMFSourceReader to an IMFSinkWriter.
    /// </summary>
    /// <param name="reader">Source reader to read samples from</param>
    /// <param name="writer">Sink writer to write samples to</param>
    /// <param name="streamIndex">Output stream index in the sink writer</param>
    /// <param name="timestampOffset">Timestamp offset to add to each sample (100-nanosecond units)</param>
    /// <param name="debugName">Name for debug logging</param>
    /// <returns>The timestamp of the last sample written (100-nanosecond units)</returns>
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
                        sample = (global::Windows.Win32.Media.MediaFoundation.IMFSample)Marshal.GetObjectForIUnknown(new IntPtr(pSample));
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
                    Marshal.ReleaseComObject(sample);
                }
            }
        }

        await Task.CompletedTask;
        return lastTimestamp;
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
            Super.Log($"[WindowsCaptureVideoEncoder] CreateVideoFromFrames failed: {ex.Message}");
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
            Super.Log($"[WindowsCaptureVideoEncoder] CreateRealMp4Video failed: {ex.Message}");
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
            Super.Log($"[WindowsCaptureVideoEncoder] FFmpeg encoding failed: {ex.Message}");
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
            Super.Log($"[WindowsCaptureVideoEncoder] CreateUncompressedAvi failed: {ex.Message}");
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

        public static readonly System.Guid MFT_CATEGORY_VIDEO_ENCODER = new System.Guid("f79eac7d-e545-4387-bdee-d647d7bde42a");
        public static readonly System.Guid MFT_CATEGORY_AUDIO_EFFECT = new System.Guid("11064c48-3648-4ed0-932e-05ce8ac811b7");
        public static readonly System.Guid MFSampleExtension_CleanPoint = new System.Guid("9cdf01d8-a0f0-43ba-b077-eaa06cbd728a");

        // Audio GUIDs
        public static readonly System.Guid MFMediaType_Audio = new System.Guid("73646961-0000-0010-8000-00aa00389b71");
        public static readonly System.Guid MFAudioFormat_AAC = new System.Guid("00001610-0000-0010-8000-00aa00389b71");
        public static readonly System.Guid MFAudioFormat_PCM = new System.Guid("00000001-0000-0010-8000-00aa00389b71");
        public static readonly System.Guid MFAudioFormat_Float = new System.Guid("00000003-0000-0010-8000-00aa00389b71"); // IEEE Float

        public static readonly System.Guid MF_MT_AUDIO_SAMPLES_PER_SECOND = new System.Guid("5faeeae7-0290-4c31-9e8a-c534f68d9dba");
        public static readonly System.Guid MF_MT_AUDIO_NUM_CHANNELS = new System.Guid("37e48bf5-645e-4c5b-89de-ada9e29b696a");
        public static readonly System.Guid MF_MT_AUDIO_BITS_PER_SAMPLE = new System.Guid("f2deb57f-330f-481f-986a-4301d512cf9f");
        public static readonly System.Guid MF_MT_AUDIO_BLOCK_ALIGNMENT = new System.Guid("322de230-9eeb-43bd-ab7a-ff412251541d");
        public static readonly System.Guid MF_MT_AUDIO_AVG_BYTES_PER_SECOND = new System.Guid("1aab75c8-29bb-443f-95bb-584637e66c9f");

        public static readonly System.Guid MF_MT_AAC_PAYLOAD_TYPE = new System.Guid("bfbabe79-7434-4d1c-94f0-72a3b9e17188");
        public static readonly System.Guid MF_MT_AAC_AUDIO_PROFILE_LEVEL_INDICATION = new System.Guid("7632f0e6-5038-4b86-8469-7963eb172ca6");

        public static readonly System.Guid MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS = new System.Guid("a634a91c-822b-41b9-a494-4ae46436892d");
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

    [DllImport("mf.dll", ExactSpelling = true, PreserveSig = true)]
    private static extern int MFTranscodeGetAudioOutputAvailableTypes(
        [In] ref Guid guidSubType,
        [In] uint dwMFTFlags,
        [In, MarshalAs(UnmanagedType.Interface)] object pCodecConfig,
        [Out, MarshalAs(UnmanagedType.Interface)] out object ppAvailableTypes);

    // COM interop for manual MFT encoder
    [DllImport("ole32.dll", ExactSpelling = true, PreserveSig = true)]
    private static extern int CoCreateInstance(
        [In] ref Guid rclsid,
        IntPtr pUnkOuter,
        uint dwClsContext,
        [In] ref Guid riid,
        out IntPtr ppv);

    [ComImport, Guid("BF94C121-5B05-4E6F-8000-BA598F1B6A4A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFTransform
    {
        void GetStreamLimits(out uint pdwInputMinimum, out uint pdwInputMaximum, out uint pdwOutputMinimum, out uint pdwOutputMaximum);
        void GetStreamCount(out uint pcInputStreams, out uint pcOutputStreams);
        void GetStreamIDs(uint dwInputIDArraySize, [Out, MarshalAs(UnmanagedType.LPArray)] uint[] pdwInputIDs, uint dwOutputIDArraySize, [Out, MarshalAs(UnmanagedType.LPArray)] uint[] pdwOutputIDs);
        void GetInputStreamInfo(uint dwInputStreamID, out MFT_INPUT_STREAM_INFO pStreamInfo);
        void GetOutputStreamInfo(uint dwOutputStreamID, out MFT_OUTPUT_STREAM_INFO pStreamInfo);
        void GetAttributes([MarshalAs(UnmanagedType.Interface)] out object ppAttributes);
        void GetInputStreamAttributes(uint dwInputStreamID, [MarshalAs(UnmanagedType.Interface)] out object ppAttributes);
        void GetOutputStreamAttributes(uint dwOutputStreamID, [MarshalAs(UnmanagedType.Interface)] out object ppAttributes);
        void DeleteInputStream(uint dwStreamID);
        void AddInputStreams(uint cStreams, [In, MarshalAs(UnmanagedType.LPArray)] uint[] adwStreamIDs);
        void GetInputAvailableType(uint dwInputStreamID, uint dwTypeIndex, [MarshalAs(UnmanagedType.Interface)] out global::Windows.Win32.Media.MediaFoundation.IMFMediaType ppType);
        void GetOutputAvailableType(uint dwOutputStreamID, uint dwTypeIndex, [MarshalAs(UnmanagedType.Interface)] out global::Windows.Win32.Media.MediaFoundation.IMFMediaType ppType);
        void SetInputType(uint dwInputStreamID, [In, MarshalAs(UnmanagedType.Interface)] global::Windows.Win32.Media.MediaFoundation.IMFMediaType pType, uint dwFlags);
        void SetOutputType(uint dwOutputStreamID, [In, MarshalAs(UnmanagedType.Interface)] global::Windows.Win32.Media.MediaFoundation.IMFMediaType pType, uint dwFlags);
        void GetInputCurrentType(uint dwInputStreamID, [MarshalAs(UnmanagedType.Interface)] out global::Windows.Win32.Media.MediaFoundation.IMFMediaType ppType);
        void GetOutputCurrentType(uint dwOutputStreamID, [MarshalAs(UnmanagedType.Interface)] out global::Windows.Win32.Media.MediaFoundation.IMFMediaType ppType);
        void GetInputStatus(uint dwInputStreamID, out uint pdwFlags);
        void GetOutputStatus(out uint pdwFlags);
        void SetOutputBounds(long hnsLowerBound, long hnsUpperBound);
        void ProcessEvent(uint dwInputStreamID, [In, MarshalAs(UnmanagedType.Interface)] object pEvent);
        void ProcessMessage(uint eMessage, UIntPtr ulParam);
        void ProcessInput(uint dwInputStreamID, IntPtr pSample, uint dwFlags);
        void ProcessOutput(uint dwFlags, uint cOutputBufferCount, IntPtr pOutputSamples, out uint pdwStatus);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MFT_INPUT_STREAM_INFO
    {
        public long hnsMaxLatency;
        public uint dwFlags;
        public uint cbSize;
        public uint cbMaxLookahead;
        public uint cbAlignment;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MFT_OUTPUT_STREAM_INFO
    {
        public uint dwFlags;
        public uint cbSize;
        public uint cbAlignment;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MFT_OUTPUT_DATA_BUFFER
    {
        public uint dwStreamID;
        public IntPtr pSample;
        public uint dwStatus;
        public IntPtr pEvents;
    }

    private const uint MFT_MESSAGE_NOTIFY_END_OF_STREAM = 0x00000008;
    private const uint MFT_MESSAGE_COMMAND_DRAIN = 0x00000009;
    private const int MF_E_TRANSFORM_NEED_MORE_INPUT = unchecked((int)0xC00D6D72);

    // MFTEnumEx for encoder discovery
    [DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = true)]
    private static extern int MFTEnumEx(
        Guid guidCategory,
        uint Flags,
        ref MFT_REGISTER_TYPE_INFO pInputType,
        ref MFT_REGISTER_TYPE_INFO pOutputType,
        ref IntPtr pppMFTActivate,
        ref uint pnumMFTActivate);

    [StructLayout(LayoutKind.Sequential)]
    private struct MFT_REGISTER_TYPE_INFO
    {
        public Guid guidMajorType;
        public Guid guidSubtype;
    }

    private const uint MFT_ENUM_FLAG_SYNCMFT = 0x00000001;
    private const uint MFT_ENUM_FLAG_ASYNCMFT = 0x00000002;
    private const uint MFT_ENUM_FLAG_HARDWARE = 0x00000004;
    private const uint MFT_ENUM_FLAG_SORTANDFILTER = 0x00000040;
    private const uint MFT_ENUM_FLAG_ALL = 0x0000003F;

    [ComImport]
    [Guid("7FEE9E9A-4A89-47a6-899C-B6A53A70FB67")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFActivate
    {
        // IMFAttributes methods (IMFActivate inherits from IMFAttributes)
        void GetItem(ref Guid guidKey, IntPtr pValue);
        void GetItemType(ref Guid guidKey, out ushort pType);
        void CompareItem(ref Guid guidKey, IntPtr Value, out int pbResult);
        void Compare(IntPtr pTheirs, int MatchType, out int pbResult);
        void GetUINT32(ref Guid guidKey, out uint punValue);
        void GetUINT64(ref Guid guidKey, out ulong punValue);
        void GetDouble(ref Guid guidKey, out double pfValue);
        void GetGUID(ref Guid guidKey, out Guid pguidValue);
        void GetStringLength(ref Guid guidKey, out uint pcchLength);
        void GetString(ref Guid guidKey, IntPtr pwszValue, uint cchBufSize, out uint pcchLength);
        void GetAllocatedString(ref Guid guidKey, out IntPtr ppwszValue, out uint pcchLength);
        void GetBlobSize(ref Guid guidKey, out uint pcbBlobSize);
        void GetBlob(ref Guid guidKey, IntPtr pBuf, uint cbBufSize, out uint pcbBlobSize);
        void GetAllocatedBlob(ref Guid guidKey, out IntPtr ppBuf, out uint pcbSize);
        void GetUnknown(ref Guid guidKey, ref Guid riid, out IntPtr ppv);
        void SetItem(ref Guid guidKey, IntPtr Value);
        void DeleteItem(ref Guid guidKey);
        void DeleteAllItems();
        void SetUINT32(ref Guid guidKey, uint unValue);
        void SetUINT64(ref Guid guidKey, ulong unValue);
        void SetDouble(ref Guid guidKey, double fValue);
        void SetGUID(ref Guid guidKey, ref Guid guidValue);
        void SetString(ref Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] string wszValue);
        void SetBlob(ref Guid guidKey, IntPtr pBuf, uint cbBufSize);
        void SetUnknown(ref Guid guidKey, [MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
        void LockStore();
        void UnlockStore();
        void GetCount(out uint pcItems);
        void GetItemByIndex(uint unIndex, out Guid pguidKey, IntPtr pValue);
        void CopyAllItems(IntPtr pDest);
        
        // IMFActivate methods
        void ActivateObject(ref Guid riid, out IntPtr ppv);
        void ShutdownObject();
        void DetachObject();
    }

    [ComImport]
    [Guid("5BC8A76B-869A-46A3-9B03-FA218A66AEBE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFCollectionLoc
    {
        void GetElementCount(out uint pcElements);
        void GetElement(uint dwElementIndex, [MarshalAs(UnmanagedType.IUnknown)] out object ppUnkElement);
        void AddElement([MarshalAs(UnmanagedType.IUnknown)] object pUnkElement);
        void RemoveElement(uint dwElementIndex, [MarshalAs(UnmanagedType.IUnknown)] out object ppUnkElement);
        void InsertElementAt(uint dwIndex, [MarshalAs(UnmanagedType.IUnknown)] object pUnkElement);
        void RemoveAllElements();
    }

    private const uint MFT_ENUM_FLAG_FIELDOFUSE = 0x00000008;
    private const uint MFT_ENUM_FLAG_LOCALMFT = 0x00000010;
    private const uint MFT_ENUM_FLAG_TRANSCODE_ONLY = 0x00000020;

    private unsafe global::Windows.Win32.Media.MediaFoundation.IMFMediaType GetSupportedAacMediaType(uint preferredRate, uint preferredChannels)
    {
        object collectionObj = null;
        var aacGuid = MFGuids.MFAudioFormat_AAC;

        // Use strict flags to ensure we get valid transcode types
        uint flags = MFT_ENUM_FLAG_ALL | MFT_ENUM_FLAG_SORTANDFILTER;

        var hr = MFTranscodeGetAudioOutputAvailableTypes(ref aacGuid, flags, null, out collectionObj);

        if (hr < 0 || collectionObj == null)
        {
            Debug.WriteLine($"[WindowsCaptureVideoEncoder] MFTranscodeGetAudioOutputAvailableTypes failed (0x{hr:X})");
            return null;
        }

        var collection = collectionObj as IMFCollectionLoc;
        if (collection == null) return null;

        try
        {
            collection.GetElementCount(out uint count);

            // We only accept an AAC type that EXACTLY matches the
            // requested sample rate and channel count. Returning a
            // "close" type can cause the sink writer to fail later
            // with MF_E_TOPO_CODEC_NOT_FOUND when we set the PCM
            // input media type (the TODO at the audio SetInputMediaType
            // call).

            for (uint i = 0; i < count; i++)
            {
                object item;
                collection.GetElement(i, out item);
                var candidate = item as global::Windows.Win32.Media.MediaFoundation.IMFMediaType;

                if (candidate == null)
                {
                    continue;
                }

                try
                {
                    candidate.GetGUID(MFGuids.MF_MT_SUBTYPE, out var subtype);
                    if (subtype != MFGuids.MFAudioFormat_AAC)
                    {
                        Marshal.ReleaseComObject(candidate);
                        continue;
                    }

                    candidate.GetUINT32(MFGuids.MF_MT_AUDIO_SAMPLES_PER_SECOND, out uint rate);
                    candidate.GetUINT32(MFGuids.MF_MT_AUDIO_NUM_CHANNELS, out uint channels);

                    if (rate == preferredRate && channels == preferredChannels)
                    {
                        // Found an exact match; keep this one and
                        // let the caller configure the PCM input
                        // with the same rate/channels.
                        return candidate;
                    }
                }
                catch
                {
                    // If anything goes wrong while inspecting this
                    // candidate, just release it and move on.
                }

                Marshal.ReleaseComObject(candidate);
            }

            // No exact match was found for the requested
            // sample rate / channels.
            return null;
        }
        finally
        {
            if (collection != null && Marshal.IsComObject(collection))
            {
                Marshal.ReleaseComObject(collection);
            }
        }
    }

    /// <summary>
    /// Configures IMFSinkWriter with H.264 output and RGB32 input.
    /// Extracted from InitializeAsync normal mode for reuse in pre-recording.
    /// </summary>
    private void ConfigureH264OutputAndRGB32Input()
    {
        // Configure OUTPUT type (H.264)
        var hr = PInvoke.MFCreateMediaType(out var outType);
        if (hr.Failed)
            throw new InvalidOperationException($"MFCreateMediaType(out) failed: 0x{hr.Value:X8}");

        try
        {
            outType.SetGUID(MFGuids.MF_MT_MAJOR_TYPE, MFGuids.MFMediaType_Video);
            outType.SetGUID(MFGuids.MF_MT_SUBTYPE, MFGuids.MFVideoFormat_H264);

            SetAttributeSize(outType, MFGuids.MF_MT_FRAME_SIZE, (uint)_width, (uint)_height);
            SetAttributeRatio(outType, MFGuids.MF_MT_FRAME_RATE, (uint)_frameRate, 1);
            SetAttributeRatio(outType, MFGuids.MF_MT_PIXEL_ASPECT_RATIO, 1, 1);
            outType.SetUINT32(MFGuids.MF_MT_INTERLACE_MODE, 2); // Progressive
            outType.SetUINT32(MFGuids.MF_MT_MPEG2_PROFILE, 100); // H.264 High profile

            uint bitrate = (uint)(Math.Max(1, _width * _height) * Math.Max(1, _frameRate) * 4 / 10);
            outType.SetUINT32(MFGuids.MF_MT_AVG_BITRATE, bitrate);

            _sinkWriter.AddStream(outType, out _streamIndex);
        }
        finally
        {
            Marshal.ReleaseComObject(outType);
        }

        // Configure INPUT type (RGB32)
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
            inType.SetUINT32(MFGuids.MF_MT_INTERLACE_MODE, 2);
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

        if (_recordAudio)
        {
            // Use native encoder for direct PCM→AAC transcoding via SinkWriter
            Debug.WriteLine($"[WindowsCaptureVideoEncoder] Configuring native audio encoder...");
            
            try
            {
                // Step 1: Add AAC output stream
                var aacType = GetSupportedAacMediaType((uint)_audioSampleRate, (uint)_audioChannels);
                _sinkWriter.AddStream(aacType, out _audioStreamIndex);
                Marshal.ReleaseComObject(aacType);
                Debug.WriteLine($"[WindowsCaptureVideoEncoder] AAC output stream added (Stream #{_audioStreamIndex})");
                
                // Step 2: DO NOT call SetInputMediaType - it fails in MAUI environment with 0xC00D5212
                // writing PCM samples directly to SinkWriter works without SetInputMediaType
                // SinkWriter will infer the format from the sample attributes (timestamp, duration, buffer size)
                Debug.WriteLine($"[WindowsCaptureVideoEncoder] Skipping SetInputMediaType");
                
                // Step 3: Create native encoder (will write PCM directly to SinkWriter)
                if (!CreateAudioResampler())
                {
                    Debug.WriteLine($"[WindowsCaptureVideoEncoder] Failed to create native encoder. Disabling audio.");
                    _recordAudio = false;
                }
                else
                {
                    Debug.WriteLine($"[WindowsCaptureVideoEncoder] Native audio encoder ready: PCM16 → SinkWriter → AAC");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowsCaptureVideoEncoder] Audio configuration failed: {ex.Message}");
                _recordAudio = false;
            }
        }

        Debug.WriteLine($"[WindowsCaptureVideoEncoder] Configured H.264 output + RGB32 input");
    }

    /// <summary>
    /// Creates and configures the manual AAC encoder MFT.
    /// Returns true if successful, false otherwise.
    /// </summary>
    /// <summary>
    /// Creates and configures the native audio resampler.
    /// Normalizes device audio format to PCM16 for SinkWriter AAC encoding.
    /// Uses native C++ DLL to access CLSID_CResamplerMediaObject (inaccessible from .NET MAUI).
    /// </summary>
    private bool CreateAudioResampler()
    {
        try
        {
            // Get SinkWriter COM pointer to pass to native encoder
            IntPtr sinkWriterPtr = Marshal.GetIUnknownForObject(_sinkWriter);
            
            // Create native encoder with resampler
            // AudioGraphCapture already converts to PCM16, so inputIsFloat = 0
            _nativeAudioEncoder = AudioEncoderNative.audio_encoder_create(
                sinkWriterPtr,
                _audioStreamIndex,
                _audioSampleRate,
                _audioChannels,
                inputIsFloat: 0);

            Marshal.Release(sinkWriterPtr);

            if (_nativeAudioEncoder == IntPtr.Zero)
            {
                Debug.WriteLine($"[WindowsCaptureVideoEncoder] Native encoder create returned NULL");
                return false;
            }

            // Check if encoder initialized successfully
            string error = AudioEncoderNative.GetError(_nativeAudioEncoder);
            if (error.Contains("failed") || error.Contains("0x"))
            {
                Debug.WriteLine($"[WindowsCaptureVideoEncoder] Native encoder error: {error}");
                AudioEncoderNative.audio_encoder_destroy(_nativeAudioEncoder);
                _nativeAudioEncoder = IntPtr.Zero;
                return false;
            }

            _audioInputIsFloat = false;
            Debug.WriteLine($"[WindowsCaptureVideoEncoder] Native audio encoder created successfully");
            Debug.WriteLine($"[WindowsCaptureVideoEncoder] Using native resampler for PCM16→AAC transcoding");
            return true;
        }
        catch (DllNotFoundException ex)
        {
            Debug.WriteLine($"[WindowsCaptureVideoEncoder] AudioEncoderNative.dll not found: {ex.Message}");
            Debug.WriteLine($"[WindowsCaptureVideoEncoder] Audio encoding will be disabled");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsCaptureVideoEncoder] Native encoder creation failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Tries to set the audio input type to Float or PCM for automatic transcoding.
    /// Returns true if successful.
    /// </summary>
    private bool TrySetAudioInputType(bool useFloat)
    {
        var hr = PInvoke.MFCreateMediaType(out var inAudioType);
        if (hr.Failed) return false;

        try
        {
            inAudioType.SetGUID(MFGuids.MF_MT_MAJOR_TYPE, MFGuids.MFMediaType_Audio);
            inAudioType.SetUINT32(MFGuids.MF_MT_AUDIO_SAMPLES_PER_SECOND, (uint)_audioSampleRate);
            inAudioType.SetUINT32(MFGuids.MF_MT_AUDIO_NUM_CHANNELS, (uint)_audioChannels);

            if (useFloat)
            {
                inAudioType.SetGUID(MFGuids.MF_MT_SUBTYPE, MFGuids.MFAudioFormat_Float);
                inAudioType.SetUINT32(MFGuids.MF_MT_AUDIO_BITS_PER_SAMPLE, 32);
                uint blockAlign = (uint)(_audioChannels * 4); // 32 bits = 4 bytes
                inAudioType.SetUINT32(MFGuids.MF_MT_AUDIO_BLOCK_ALIGNMENT, blockAlign);
                inAudioType.SetUINT32(MFGuids.MF_MT_AUDIO_AVG_BYTES_PER_SECOND, (uint)(_audioSampleRate * blockAlign));
            }
            else
            {
                inAudioType.SetGUID(MFGuids.MF_MT_SUBTYPE, MFGuids.MFAudioFormat_PCM);
                inAudioType.SetUINT32(MFGuids.MF_MT_AUDIO_BITS_PER_SAMPLE, 16);
                uint blockAlign = (uint)(_audioChannels * 2); // 16 bits = 2 bytes
                inAudioType.SetUINT32(MFGuids.MF_MT_AUDIO_BLOCK_ALIGNMENT, blockAlign);
                inAudioType.SetUINT32(MFGuids.MF_MT_AUDIO_AVG_BYTES_PER_SECOND, (uint)(_audioSampleRate * blockAlign));
                inAudioType.SetUINT32(MFGuids.MF_MT_ALL_SAMPLES_INDEPENDENT, 1u); // PCM samples are independent
            }

            try
            {
                _sinkWriter.SetInputMediaType(_audioStreamIndex, inAudioType, null);
                _audioInputIsFloat = useFloat;
                Debug.WriteLine($"[WindowsCaptureVideoEncoder] Configured AAC output + {(useFloat ? "Float" : "16-bit PCM")} input (Stream #{_audioStreamIndex})");
                Debug.WriteLine($"[WindowsCaptureVideoEncoder]   Format: {_audioSampleRate}Hz, {_audioChannels}ch, {(useFloat ? "Float32" : "PCM16")}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowsCaptureVideoEncoder] {(useFloat ? "Float" : "16-bit PCM")} input failed: {ex.Message}");
                return false;
            }
        }
        finally
        {
            Marshal.ReleaseComObject(inAudioType);
        }
    }

    private string _preferredAudioCodecName;

    /// <summary>
    /// Sets the preferred audio codec name to use. 
    /// If null or empty, or not found, it falls back to AAC auto-detection.
    /// </summary>
    public void SetAudioCodec(string codecName)
    {
        _preferredAudioCodecName = codecName;
    }

    private async Task DetectAvailableEncoderTypes()
    {
        Debug.WriteLine("---- Querying WinRT Codecs (Discovery) ----");
        try
        {
            _selectedAudioCodecGuid = Guid.Empty;

            var query = new global::Windows.Media.Core.CodecQuery();
            var codecs = await query.FindAllAsync(global::Windows.Media.Core.CodecKind.Audio, global::Windows.Media.Core.CodecCategory.Encoder, null);

            Debug.WriteLine($"Found {codecs.Count} Audio Encoders via WinRT. Scanning...");

            // If a specific codec name was requested, try to find it first
            if (!string.IsNullOrEmpty(_preferredAudioCodecName))
            {
                Debug.WriteLine($"Searching for preferred codec: '{_preferredAudioCodecName}'");
                foreach (var codec in codecs)
                {
                    if (codec.DisplayName.Equals(_preferredAudioCodecName, StringComparison.OrdinalIgnoreCase))
                    {
                        // Take the first subtype that looks like a valid GUID
                        foreach (var subtype in codec.Subtypes)
                        {
                            if (Guid.TryParse(subtype, out var guid))
                            {
                                _selectedAudioCodecGuid = guid;
                                Debug.WriteLine($"[SELECTED] Found Preferred Encoder: '{codec.DisplayName}' with Subtype {subtype}");
                                return;
                            }
                        }
                    }
                }
                Debug.WriteLine($"[WARNING] Preferred codec '{_preferredAudioCodecName}' not found or had no valid subtypes. Falling back to AAC search.");
            }

            // Standard AAC GUID
            var targetAac = new Guid("00001610-0000-0010-8000-00aa00389b71");
            bool foundExactMatch = false;

            // pass 1: look for exact standard GUID
            foreach (var codec in codecs)
            {
                foreach (var subtype in codec.Subtypes)
                {
                    if (Guid.TryParse(subtype, out var guid))
                    {
                        if (guid == targetAac)
                        {
                            _selectedAudioCodecGuid = guid;
                            Debug.WriteLine($"[SELECTED] Found Standard AAC Encoder: '{codec.DisplayName}' with Subtype {subtype}");
                            foundExactMatch = true;
                            break;
                        }
                    }
                }
                if (foundExactMatch) break;
            }

            // pass 2: if not found, trust the DisplayName (fallback)
            if (!foundExactMatch)
            {
                Debug.WriteLine("[INFO] Standard AAC GUID not found. Scanning by name...");
                foreach (var codec in codecs)
                {
                    if (codec.DisplayName.Contains("AAC", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var subtype in codec.Subtypes)
                        {
                            if (Guid.TryParse(subtype, out var guid))
                            {
                                _selectedAudioCodecGuid = guid;
                                Debug.WriteLine($"[SELECTED] Found Encoder by Name: '{codec.DisplayName}' with Subtype {subtype}");
                                foundExactMatch = true;
                                break;
                            }
                        }
                    }
                    if (foundExactMatch) break;
                }
            }

            if (!foundExactMatch && _selectedAudioCodecGuid == Guid.Empty)
            {
                Debug.WriteLine("[WARNING] No AAC encoder found via WinRT!");
            }
            else
            {
                // print others for debug
                Debug.WriteLine("[INFO] Ignoring other encoders...");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error querying WinRT codecs: {ex}");
        }
    }



    /// <summary>
    /// Waits for an MP4 file to be fully flushed and readable by IMFSourceReader.
    /// Retries up to 10 times with 50ms delays between attempts.
    /// </summary>
    private async Task WaitForFileReadable(string filePath)
    {
        const int maxRetries = 10;
        const int delayMs = 50;

        Debug.WriteLine($"[WaitForFileReadable] Verifying file is readable: {Path.GetFileName(filePath)}");

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            global::Windows.Win32.Media.MediaFoundation.IMFSourceReader? testReader = null;
            try
            {
                // Try to create a source reader - this will fail if MP4 structure is incomplete
                unsafe
                {
                    fixed (char* p = filePath)
                    {
                        var hr = PInvoke.MFCreateSourceReaderFromURL(new PCWSTR(p), null, out testReader);
                        if (hr.Succeeded && testReader != null)
                        {
                            // Try to get media type - verifies MP4 has valid video track
                            testReader.GetCurrentMediaType(0xFFFFFFFC, out var mediaType);
                            if (mediaType != null)
                            {
                                Marshal.ReleaseComObject(mediaType);
                                Debug.WriteLine($"[WaitForFileReadable] File is readable after {attempt} attempts ({attempt * delayMs}ms)");
                                return; // Success!
                            }
                        }
                    }
                }
            }
            catch
            {
                // File not ready yet, continue retrying
            }
            finally
            {
                if (testReader != null)
                {
                    Marshal.ReleaseComObject(testReader);
                }
            }

            if (attempt < maxRetries - 1)
            {
                await Task.Delay(delayMs);
            }
        }

        Debug.WriteLine($"[WaitForFileReadable] WARNING: File may not be fully readable after {maxRetries} attempts ({maxRetries * delayMs}ms)");
    }

    /// <summary>
    /// Cleans up pre-recording temporary buffer files
    /// </summary>
    private void CleanupPreRecTempFiles()
    {
        try
        {
            if (!string.IsNullOrEmpty(_preRecBufferA) && File.Exists(_preRecBufferA))
            {
                File.Delete(_preRecBufferA);
            }
        }
        catch (Exception ex)
        {
            Super.Log($"[WindowsCaptureVideoEncoder] Failed to delete buffer A: {ex.Message}");
        }

        try
        {
            if (!string.IsNullOrEmpty(_preRecBufferB) && File.Exists(_preRecBufferB))
            {
                File.Delete(_preRecBufferB);
            }
        }
        catch (Exception ex)
        {
            Super.Log($"[WindowsCaptureVideoEncoder] Failed to delete buffer B: {ex.Message}");
        }
    }


    public void Dispose()
    {
        _isRecording = false;
        _progressTimer?.Dispose();
        _sinkWriterSemaphore?.Dispose();
        _audioSemaphore?.Dispose();

        // Clean up audio resampler
        if (_audioResampler != null)
        {
            try
            {
                Marshal.ReleaseComObject(_audioResampler);
            }
            catch { }
            _audioResampler = null;
        }

        // Clean up native audio encoder
        if (_nativeAudioEncoder != IntPtr.Zero)
        {
            try
            {
                AudioEncoderNative.audio_encoder_destroy(_nativeAudioEncoder);
                _nativeAudioEncoder = IntPtr.Zero;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowsCaptureVideoEncoder] Failed to destroy native encoder: {ex.Message}");
            }
        }

        if (_audioResamplerInputSample != null)
        {
            try
            {
                Marshal.ReleaseComObject(_audioResamplerInputSample);
            }
            catch { }
            _audioResamplerInputSample = null;
        }

        // Clean up pre-recording temp files
        CleanupPreRecTempFiles();

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
