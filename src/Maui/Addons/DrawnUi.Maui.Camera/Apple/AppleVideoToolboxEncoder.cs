#if IOS || MACCATALYST
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AudioToolbox;
using AVFoundation;
using CoreMedia;
using CoreVideo;
using Foundation;
using VideoToolbox;
using SkiaSharp;
using ObjCRuntime;
using Metal; // Added for Zero-Copy path

namespace DrawnUi.Camera
{
    /// <summary>
    /// Apple encoder using AVAssetWriter for MP4 output.
    ///
    /// Two Modes:
    /// 1. Normal Recording: AVAssetWriterInputPixelBufferAdaptor (AVAssetWriter compresses)
    /// 2. Pre-Recording: VTCompressionSession → Circular buffer (compressed H.264 in memory)
    ///
    /// Pipeline (Normal Recording):
    /// Skia → CVPixelBuffer → AVAssetWriterInputPixelBufferAdaptor → MP4
    ///
    /// Pipeline (Pre-Recording):
    /// Skia → CVPixelBuffer → VTCompressionSession → H.264 → PrerecordingEncodedBufferApple (memory)
    /// </summary>
    public class AppleVideoToolboxEncoder : ICaptureVideoEncoder
    {
        // AudioSettings removed


        private static int _instanceCounter = 0;
        private readonly int _instanceId;

        private string _outputPath;               // Final output file (concatenated result)
        private string _preRecordingFilePath;     // Pre-recording buffer MP4
        private string _liveRecordingFilePath;    // Live recording MP4
        private int _width;
        private int _height;
        private int _frameRate;
        private int _deviceRotation;
        private bool _recordAudio;
        private bool _isRecording;
        private DateTime _startTime;
        private TimeSpan _preRecordingDuration;  // Duration of pre-recorded content
        private bool _encodingDurationSetFromFrames = false;  // Flag to prevent overwriting correct duration

        // Skia composition surface
        private SKSurface _surface;

        private AVAssetWriterInput _audioWriterInput;
        private CircularAudioBuffer _audioBuffer;
        private readonly object _audioWriterLock = new object();
        private CMAudioFormatDescription _audioFormatDescription;

        public bool SupportsAudio => true;

        public void SetAudioBuffer(CircularAudioBuffer buffer)
        {
            // ARCHITECTURAL FIX: Encoder handles VIDEO ONLY
            // Audio is now managed at SkiaCamera level with a session-wide linear buffer
            // that survives the pre-recording → live transition
            // This method is kept for interface compatibility but does nothing
            _audioBuffer = null; // Always null - we don't use per-encoder audio buffers anymore
        }

        public void WriteAudioSample(AudioSample sample)
        {
            // ARCHITECTURAL FIX: Encoder handles VIDEO ONLY
            // Audio is collected in SkiaCamera's session-wide buffer and muxed at final stop
            // This method is kept for interface compatibility but does nothing
            return;
        }

        private void WriteAudioToAssetWriter(AudioSample sample)
        {
            lock (_audioWriterLock)
            {
                if (_writer == null || _writer.Status != AVAssetWriterStatus.Writing 
                    || _audioWriterInput == null || !_audioWriterInput.ReadyForMoreMediaData)
                    return;

                using var cmsample = CreateSampleBuffer(sample);
                if (cmsample != null)
                {
                    _audioWriterInput.AppendSampleBuffer(cmsample);
                }
            }
        }

        [DllImport("/System/Library/Frameworks/CoreMedia.framework/CoreMedia")]
        private static extern int CMAudioFormatDescriptionCreate(
            IntPtr allocator,
            ref AudioStreamBasicDescription asbd,
            nuint layoutSize,
            IntPtr layout,
            nuint magicCookieSize,
            IntPtr magicCookie,
            IntPtr extensions,
            out IntPtr formatDescriptionOut
        );

        private CMSampleBuffer CreateSampleBuffer(AudioSample sample, List<IntPtr> memoryTracker = null)
        {
            try
            {
                // CRITICAL FIX: Calculate bytes per frame to determine numSamples correctly
                // This ensures AVAssetWriter knows the correct duration of this buffer
                int bytesPerSample = 2; // Default 16-bit
                if (sample.BitDepth == AudioBitDepth.Pcm8Bit) bytesPerSample = 1;
                else if (sample.BitDepth == AudioBitDepth.Float32Bit) bytesPerSample = 4;

                int bytesPerFrame = bytesPerSample * sample.Channels;
                nint numSamples = sample.Data.Length / bytesPerFrame;

                if (_audioFormatDescription == null)
                {
                    // Create format desc
                    AudioStreamBasicDescription audioFormat = new AudioStreamBasicDescription
                    {
                        SampleRate = sample.SampleRate,
                        Format = AudioFormatType.LinearPCM, 
                        FormatFlags = AudioFormatFlags.LinearPCMIsPacked | AudioFormatFlags.LinearPCMIsSignedInteger,
                        ChannelsPerFrame = (int)sample.Channels,
                        BytesPerPacket = (int)(sample.Channels * 2), // Assuming 16-bit
                        FramesPerPacket = 1,
                        BytesPerFrame = (int)(sample.Channels * 2),
                        BitsPerChannel = 16 
                    };
                    
                    if (sample.BitDepth == AudioBitDepth.Pcm8Bit) 
                    {
                        audioFormat.BitsPerChannel = 8;
                        audioFormat.BytesPerFrame = (int)sample.Channels;
                        audioFormat.BytesPerPacket = (int)sample.Channels;
                    }
                    else if (sample.BitDepth == AudioBitDepth.Float32Bit)
                    {
                         audioFormat.BitsPerChannel = 32;
                         audioFormat.FormatFlags = AudioFormatFlags.LinearPCMIsFloat | AudioFormatFlags.LinearPCMIsPacked;
                         audioFormat.BytesPerFrame = (int)(sample.Channels * 4);
                         audioFormat.BytesPerPacket = (int)(sample.Channels * 4);
                    }
                    
                    IntPtr formatDescPtr;
                    var result = CMAudioFormatDescriptionCreate(
                        IntPtr.Zero,
                        ref audioFormat,
                        0,
                        IntPtr.Zero,
                        0,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        out formatDescPtr
                    );

                    if (result != 0 || formatDescPtr == IntPtr.Zero)
                    {
                        return null;
                    }

                    var desc = CMFormatDescription.Create(formatDescPtr, true);

                    if (desc == null) return null;
                    _audioFormatDescription = (CMAudioFormatDescription)desc;
                }

                var unmanagedPtr = Marshal.AllocHGlobal(sample.Data.Length);
                Marshal.Copy(sample.Data, 0, unmanagedPtr, sample.Data.Length);
                
                // Track memory for deferred cleanup
                if (memoryTracker != null)
                {
                    memoryTracker.Add(unmanagedPtr);
                }

                CMBlockBuffer blockBuffer = null;
                try
                {
                    blockBuffer = CMBlockBuffer.FromMemoryBlock(
                        unmanagedPtr,
                        (nuint)sample.Data.Length,
                        null,
                        0, 
                        (nuint)sample.Data.Length,
                        CMBlockBufferFlags.AssureMemoryNow,
                        out var err);
                }
                catch
                {
                     // If tracker used, we must free it here or let finally handle it?
                     // If we fail here, we should free it immediately if we can remove from tracker
                     if (memoryTracker != null) memoryTracker.Remove(unmanagedPtr);
                     Marshal.FreeHGlobal(unmanagedPtr);
                     return null;
                }
                if (blockBuffer == null) 
                {
                    if (memoryTracker != null) memoryTracker.Remove(unmanagedPtr);
                    Marshal.FreeHGlobal(unmanagedPtr);
                    return null;
                }
                
                CMTime presentationTime = CMTime.FromSeconds((double)sample.TimestampNs / 1_000_000_000.0, 1000000000);
                var timing = new CMSampleTimingInfo { PresentationTimeStamp = presentationTime, Duration = CMTime.Invalid, DecodeTimeStamp = CMTime.Invalid };

                CMSampleBufferError sbErr;
                // Use calculated numSamples instead of 1
                var sampleBuffer = CMSampleBuffer.CreateReady(blockBuffer, _audioFormatDescription, (int)numSamples, new[] { timing }, new nuint[] { (nuint)sample.Data.Length }, out sbErr);
                
                if (sbErr != CMSampleBufferError.None) return null;
                
                return sampleBuffer;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                return null;
            }
        }

        private SKImageInfo _info;
        private readonly object _frameLock = new();
        private TimeSpan _pendingTimestamp;

        // AVAssetWriter for MP4 output (normal recording)
        private AVAssetWriter _writer;
        private AVAssetWriterInput _videoInput;
        private AVAssetWriterInputPixelBufferAdaptor _pixelBufferAdaptor;
        private System.Threading.Timer _progressTimer;

        // Zero-Copy Metal Support
        private CVMetalTextureCache _metalCache;
        private IMTLDevice _metalDevice;
        private IMTLCommandQueue _commandQueue;
        private GRContext _encodingContext;
        private CVPixelBuffer _currentPixelBuffer; // The buffer backing the current _surface
        public GRContext Context { get; set; }     // Public Context (usually UI) - deprecated/unused for encoding logic now
        public GRContext EncodingContext => _encodingContext; // Read-only access to dedicated encoding context

        // VTCompressionSession for pre-recording buffer
        private VTCompressionSession _compressionSession;
        private PrerecordingEncodedBufferApple _preRecordingBuffer;
        private CMVideoFormatDescription _videoFormatDescription;
        private long _targetBitRate;

        // Mirror-to-preview support
        private readonly object _previewLock = new();
        private SKImage _latestPreviewImage;
        public event EventHandler PreviewAvailable;

        // Cached preview surface to avoid allocating every frame
        private SKSurface _previewSurface;
        private SKImageInfo _previewSurfaceInfo;

        // Statistics
        public int EncodedFrameCount { get; private set; }
        public long EncodedDataSize { get; private set; }
        public TimeSpan EncodingDuration { get; private set; }
        public string EncodingStatus { get; private set; } = "Idle";

        // Diagnostic counter for frames dropped due to encoder backpressure
        public int BackpressureDroppedFrames { get; private set; }

        // ✅ No GCHandle needed - callback is instance method

        public bool IsRecording => _isRecording;

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

        // Interface properties
        public bool IsPreRecordingMode { get; set; }
        public SkiaCamera ParentCamera { get; set; }
        public event EventHandler<TimeSpan> ProgressReported;

        public AppleVideoToolboxEncoder()
        {
            _instanceId = System.Threading.Interlocked.Increment(ref _instanceCounter);
            System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder #{_instanceId}] CONSTRUCTOR CALLED");
        }

        // ICaptureVideoEncoder interface
        public Task InitializeAsync(string outputPath, int width, int height, int frameRate, bool recordAudio)
        {
            return InitializeAsync(outputPath, width, height, frameRate, recordAudio, 0);
        }

        public async Task InitializeAsync(string outputPath, int width, int height, int frameRate, bool recordAudio, int deviceRotation)
        {
            System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder #{_instanceId}] InitializeAsync CALLED: IsPreRecordingMode={IsPreRecordingMode}");

            // Ensure Metal Context is initialized immediately
            EnsureMetalContext();

            _outputPath = outputPath;
            _width = Math.Max(16, width);
            _height = Math.Max(16, height);
            _frameRate = Math.Max(1, frameRate);
            _recordAudio = recordAudio;
            _deviceRotation = deviceRotation;
            _preRecordingDuration = TimeSpan.Zero;

            // Prepare output directory
            Directory.CreateDirectory(Path.GetDirectoryName(_outputPath));
            if (File.Exists(_outputPath))
            {
                try { File.Delete(_outputPath); } catch { }
            }

            // Initialize based on mode
            if (IsPreRecordingMode && ParentCamera != null)
            {
                // Pre-recording mode: Set up separate file paths
                var outputDir = Path.GetDirectoryName(_outputPath);
                var guid = Guid.NewGuid().ToString("N");
                _preRecordingFilePath = Path.Combine(outputDir, $"pre_rec_{guid}.mp4");
                _liveRecordingFilePath = Path.Combine(outputDir, $"live_rec_{guid}.mp4");

                // Initialize VTCompressionSession + circular buffer
                InitializeCompressionSession();

                // Initialize circular buffer for storing encoded frames
                var preRecordDuration = ParentCamera.PreRecordDuration;
                _preRecordingBuffer = new PrerecordingEncodedBufferApple(preRecordDuration, _targetBitRate);

                // CRITICAL: Start recording immediately to buffer frames
                _isRecording = true;
                _startTime = DateTime.Now;
                EncodedFrameCount = 0;
                EncodedDataSize = 0;
                EncodingDuration = TimeSpan.Zero;
                BackpressureDroppedFrames = 0;
                EncodingStatus = "Buffering";

                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder #{_instanceId}] Pre-recording mode initialized and started:");
                System.Diagnostics.Debug.WriteLine($"  Pre-recording file: {_preRecordingFilePath}");
                System.Diagnostics.Debug.WriteLine($"  Live recording file: {_liveRecordingFilePath}");
                System.Diagnostics.Debug.WriteLine($"  Final output file: {_outputPath}");
                System.Diagnostics.Debug.WriteLine($"  Buffer duration: {preRecordDuration.TotalSeconds}s");
                System.Diagnostics.Debug.WriteLine($"  Buffering frames to memory...");
            }
            else
            {
                // Normal recording mode: Use AVAssetWriter directly to outputPath
                InitializeAssetWriter();
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder #{_instanceId}] Normal recording mode: AVAssetWriter -> {_outputPath}");
            }

            await Task.CompletedTask;
        }

        private void EnsureMetalContext()
        {
            lock (_frameLock)
            {
                if (_encodingContext == null)
                {
                    try
                    {
                        if (_metalDevice == null)
                            _metalDevice = MTLDevice.SystemDefault;

                        if (_metalDevice != null)
                        {
                            if (_commandQueue == null)
                                _commandQueue = _metalDevice.CreateCommandQueue();

                            var backend = new GRMtlBackendContext
                            {
                                Device = _metalDevice,
                                Queue = _commandQueue
                            };
                            _encodingContext = GRContext.CreateMetal(backend);
                            _metalCache = new CVMetalTextureCache(_metalDevice);
                            
                            System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Dedicated Metal GRContext initialized.");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Failed to initialize Metal context: {ex.Message}");
                    }
                }
            }
        }

        private void InitializeAssetWriter()
        {
            // Create AVAssetWriter for MP4 container
            var url = NSUrl.FromFilename(_outputPath);
            _writer = new AVAssetWriter(url, "public.mpeg-4", out var err);
            if (_writer == null || err != null)
                throw new InvalidOperationException($"AVAssetWriter failed: {err?.LocalizedDescription}");

            // Video settings (H.264)
            var videoSettings = new AVVideoSettingsCompressed
            {
                Codec = AVVideoCodec.H264,
                Width = _width,
                Height = _height
            };

            _videoInput = new AVAssetWriterInput(AVMediaTypes.Video.GetConstant(), videoSettings);
            _videoInput.ExpectsMediaDataInRealTime = true;

            // Set transform based on device rotation to ensure correct playback orientation
            _videoInput.Transform = GetTransformForRotation(_deviceRotation);

            if (!_writer.CanAddInput(_videoInput))
                throw new InvalidOperationException("Cannot add video input to AVAssetWriter");
            _writer.AddInput(_videoInput);

            // ARCHITECTURAL FIX: Encoder handles VIDEO ONLY
            // Audio is now managed at SkiaCamera level with a session-wide linear buffer
            // Audio will be muxed into final output file at StopCaptureVideoFlow time
            // Keeping _recordAudio flag for reference but not creating audio input
            if (_recordAudio)
            {
                System.Diagnostics.Debug.WriteLine("[AppleVideoToolboxEncoder] VIDEO ONLY mode - audio handled at SkiaCamera level");
            }

            // Zero-Copy Requirement: Attributes must allow Metal access
            var pbaAttributes = new CVPixelBufferAttributes
            {
                PixelFormatType = CVPixelFormatType.CV32BGRA,
                Width = _width,
                Height = _height,
                 MetalCompatibility = true
            };

            _pixelBufferAdaptor = new AVAssetWriterInputPixelBufferAdaptor(_videoInput, pbaAttributes);

            System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] AVAssetWriter initialized: {_width}x{_height} @ {_frameRate}fps");
        }

        private void InitializeCompressionSession()
        {
            // Source pixel buffer attributes (input format from Skia)
            var sourceAttributes = new CVPixelBufferAttributes
            {
                PixelFormatType = CVPixelFormatType.CV32BGRA,
                Width = _width,
                Height = _height,
                MetalCompatibility = true
            };

            // Create VTCompressionSession for H.264 encoding
            _compressionSession = VTCompressionSession.Create(
                _width,
                _height,
                CMVideoCodecType.H264,
                CompressionOutputCallback,
                encoderSpecification: null,
                sourceImageBufferAttributes: sourceAttributes.Dictionary);

            if (_compressionSession == null)
                throw new InvalidOperationException($"VTCompressionSession.Create failed");

            // Enable real-time encoding
            _compressionSession.SetProperty(
                VTCompressionPropertyKey.RealTime,
                new NSNumber(true));

            // Set H.264 High Profile
            _compressionSession.SetProperty(
                VTCompressionPropertyKey.ProfileLevel,
                new NSNumber((int)VTProfileLevel.H264HighAutoLevel));

            // Set bitrate
            int bitrate = _width * _height * _frameRate / 10;
            _targetBitRate = Math.Max(1, bitrate);
            _compressionSession.SetProperty(
                VTCompressionPropertyKey.AverageBitRate,
                new NSNumber(bitrate));

            // Keyframe interval (1 per second)
            _compressionSession.SetProperty(
                VTCompressionPropertyKey.MaxKeyFrameInterval,
                new NSNumber(_frameRate));
            _compressionSession.SetProperty(
                VTCompressionPropertyKey.MaxKeyFrameIntervalDuration,
                new NSNumber(1.0));

            _compressionSession.SetProperty(
                VTCompressionPropertyKey.ExpectedFrameRate,
                new NSNumber(_frameRate));

            // Disable B-frames
            _compressionSession.SetProperty(
                VTCompressionPropertyKey.AllowFrameReordering,
                new NSNumber(false));

            _compressionSession.PrepareToEncodeFrames();

            System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] VTCompressionSession created for pre-recording");
        }

        private void CompressionOutputCallback(
            nint sourceFrame,
            VTStatus status,
            VTEncodeInfoFlags infoFlags,
            CMSampleBuffer sampleBuffer)
        {
            if (status != VTStatus.Ok)
            {
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Compression failed: {status}");
                return;
            }

            if (sampleBuffer == null || sampleBuffer.Handle == IntPtr.Zero)
                return;

            try
            {
                // Buffer the encoded H.264 frame in circular buffer
                BufferEncodedFrame(sampleBuffer);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Callback error: {ex.Message}");
            }
        }

        private void BufferEncodedFrame(CMSampleBuffer sampleBuffer)
        {
            if (_preRecordingBuffer == null)
                return;

            // Save format description from first frame (contains SPS/PPS for H.264)
            if (_videoFormatDescription == null)
            {
                _videoFormatDescription = sampleBuffer.GetVideoFormatDescription();
                if (_videoFormatDescription != null)
                {
                    var dims = _videoFormatDescription.Dimensions;
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Captured video format description: {dims.Width}x{dims.Height}, MediaSubType={_videoFormatDescription.MediaSubType}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] WARNING: Failed to capture video format description!");
                }
            }

            // Extract H.264 data from sample buffer
            var blockBuffer = sampleBuffer.GetDataBuffer();
            if (blockBuffer == null || blockBuffer.DataLength == 0)
                return;

            try
            {
                // Get pointer to encoded data
                nint dataPointer = IntPtr.Zero;
                var result = blockBuffer.GetDataPointer(
                    offset: 0,
                    lengthAtOffset: out nuint lengthAtOffset,
                    totalLength: out nuint totalLength,
                    dataPointer: ref dataPointer);

                if (result != CMBlockBufferError.None || dataPointer == IntPtr.Zero)
                    return;

                // Copy H.264 bytes to managed array
                byte[] h264Data = new byte[totalLength];
                Marshal.Copy(dataPointer, h264Data, 0, (int)totalLength);

                // Get timing information from sample buffer
                var presentationTime = sampleBuffer.PresentationTimeStamp;
                var duration = sampleBuffer.Duration;
                var timestamp = TimeSpan.FromSeconds(presentationTime.Seconds);

                // Log first few frames for diagnostics
                //if (_preRecordingBuffer.GetFrameCount() < 3)
                //{
                //    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Buffering frame #{_preRecordingBuffer.GetFrameCount() + 1}: PTS={presentationTime.Seconds:F3}s, Duration={duration.Seconds:F3}s, Size={h264Data.Length} bytes");
                //}

                // Append to buffer with full timing info
                _preRecordingBuffer.AppendEncodedFrame(h264Data, h264Data.Length, timestamp, presentationTime, duration);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] BufferEncodedFrame error: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a CGAffineTransform for the given device rotation.
        /// This sets video metadata for proper playback orientation without re-encoding.
        /// </summary>
        private CoreGraphics.CGAffineTransform GetTransformForRotation(int rotation)
        {
            var normalizedRotation = rotation % 360;
            if (normalizedRotation < 0)
                normalizedRotation += 360;

            var transform = CoreGraphics.CGAffineTransform.MakeIdentity();

            switch (normalizedRotation)
            {
                case 90:
                    // Rotate 90° clockwise: rotate then translate
                    transform = CoreGraphics.CGAffineTransform.MakeRotation((float)(Math.PI / 2));
                    transform = CoreGraphics.CGAffineTransform.Translate(transform, 0, -_width);
                    break;

                case 180:
                    // Rotate 180°: rotate then translate
                    transform = CoreGraphics.CGAffineTransform.MakeRotation((float)Math.PI);
                    transform = CoreGraphics.CGAffineTransform.Translate(transform, -_width, -_height);
                    break;

                case 270:
                    // Rotate 270° clockwise (90° counter-clockwise): rotate then translate
                    transform = CoreGraphics.CGAffineTransform.MakeRotation((float)(-Math.PI / 2));
                    transform = CoreGraphics.CGAffineTransform.Translate(transform, -_height, 0);
                    break;

                default:
                    // 0° - no rotation needed
                    break;
            }

            return transform;
        }


        public async Task StartAsync()
        {
            System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder #{_instanceId}] StartAsync CALLED: IsPreRecordingMode={IsPreRecordingMode}, _compressionSession={(_compressionSession != null ? "EXISTS" : "NULL")}");

            // Pre-recording mode: Write buffer to file, then switch to AVAssetWriter for live recording
            if (IsPreRecordingMode && _compressionSession != null)
            {
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder #{_instanceId}] Pre-recording mode: User pressed record");

                // Step 1: Write circular buffer to pre-recording file
                int bufferFrameCount = _preRecordingBuffer?.GetFrameCount() ?? 0;
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Buffer has {bufferFrameCount} frames");

                if (_preRecordingBuffer != null && bufferFrameCount > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Buffer stats: {_preRecordingBuffer.GetStats()}");
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Writing {bufferFrameCount} buffered frames to: {_preRecordingFilePath}");

                    // ARCHITECTURAL FIX: Video only - audio is handled at SkiaCamera level
                    await WriteBufferedFramesToMp4Async(_preRecordingBuffer, _preRecordingFilePath, null);

                    // Verify file was written
                    if (File.Exists(_preRecordingFilePath))
                    {
                        var fileInfo = new FileInfo(_preRecordingFilePath);
                        System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Pre-recording file written: {fileInfo.Length / 1024.0:F2} KB");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] WARNING: Pre-recording file was not created!");
                    }

                    // Calculate pre-recording duration for later concatenation
                    _preRecordingDuration = _preRecordingBuffer.GetBufferedDuration();
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Pre-recording duration: {_preRecordingDuration.TotalSeconds:F2}s");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] No buffered frames to write (buffer null or empty)");
                }

                // Step 2: Stop buffering to circular buffer (we'll switch to AVAssetWriter)
                _compressionSession?.Dispose();
                _compressionSession = null;
                _preRecordingBuffer = null;

                // Step 3: Initialize AVAssetWriter for live recording
                var url = NSUrl.FromFilename(_liveRecordingFilePath);
                _writer = new AVAssetWriter(url, "public.mpeg-4", out var err);
                if (_writer == null || err != null)
                    throw new InvalidOperationException($"AVAssetWriter failed: {err?.LocalizedDescription}");

                // ARCHITECTURAL FIX: Encoder handles VIDEO ONLY (Live recording)
                // Audio is managed at SkiaCamera level with session-wide linear buffer
                if (_recordAudio)
                {
                    System.Diagnostics.Debug.WriteLine("[AppleVideoToolboxEncoder] VIDEO ONLY mode (Live) - audio handled at SkiaCamera level");
                }

                var videoSettings = new AVVideoSettingsCompressed
                {
                    Codec = AVVideoCodec.H264,
                    Width = _width,
                    Height = _height
                };

                _videoInput = new AVAssetWriterInput(AVMediaTypes.Video.GetConstant(), videoSettings);
                _videoInput.ExpectsMediaDataInRealTime = true;

                _videoInput.Transform = GetTransformForRotation(_deviceRotation);

                if (!_writer.CanAddInput(_videoInput))
                    throw new InvalidOperationException("Cannot add video input to AVAssetWriter");
                _writer.AddInput(_videoInput);

                _pixelBufferAdaptor = new AVAssetWriterInputPixelBufferAdaptor(_videoInput,
                    new CVPixelBufferAttributes
                    {
                        PixelFormatType = CVPixelFormatType.CV32BGRA,
                        Width = _width,
                        Height = _height
                    });

                // Step 4: Start AVAssetWriter for live recording
                if (!_writer.StartWriting())
                    throw new InvalidOperationException($"AVAssetWriter StartWriting failed: {_writer.Error?.LocalizedDescription}");

                _writer.StartSessionAtSourceTime(CMTime.Zero);

                _isRecording = true;
                _startTime = DateTime.Now;
                EncodedFrameCount = 0;
                EncodedDataSize = 0;
                EncodingDuration = TimeSpan.Zero;
                BackpressureDroppedFrames = 0;
                EncodingStatus = "Recording Live";

                _progressTimer = new System.Threading.Timer(_ =>
                {
                    if (_isRecording)
                    {
                        var elapsed = DateTime.Now - _startTime;
                        ProgressReported?.Invoke(this, elapsed);
                    }
                }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Live recording started -> {_liveRecordingFilePath}");
                return;
            }

            // Normal recording mode: Start AVAssetWriter
            if (_isRecording)
            {
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Already recording, ignoring StartAsync");
                return;
            }

            if (_writer == null)
            {
                throw new InvalidOperationException("AVAssetWriter not initialized for normal recording mode");
            }

            if (!_writer.StartWriting())
                throw new InvalidOperationException($"AVAssetWriter StartWriting failed: {_writer.Error?.LocalizedDescription}");

            // Start session at source time zero
            var startTime = _preRecordingDuration > TimeSpan.Zero ? _preRecordingDuration.TotalSeconds : 0;
            var start = CMTime.FromSeconds(startTime, 1_000_000);
            _writer.StartSessionAtSourceTime(start);

            _isRecording = true;
            _startTime = DateTime.Now;

            if (_preRecordingDuration > TimeSpan.Zero)
            {
                // DO NOT RESET EncodingDuration - it continues from pre-recording offset
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Live recording started after pre-recording offset: {_preRecordingDuration.TotalSeconds:F2}s");
            }
            else
            {
                // Normal recording (no pre-recording): start fresh
                EncodingDuration = TimeSpan.Zero;
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Normal recording started");
            }

            // Initialize statistics (only reset if normal recording)
            EncodedFrameCount = 0;
            EncodedDataSize = 0;
            BackpressureDroppedFrames = 0;
            EncodingStatus = "Started";

            _progressTimer = new System.Threading.Timer(_ =>
            {
                if (_isRecording)
                {
                    var elapsed = DateTime.Now - _startTime;
                    ProgressReported?.Invoke(this, elapsed);
                }
            }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

            System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Recording started");
            System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Pre-recording offset: {_preRecordingDuration.TotalSeconds:F2}s");
        }

        /// <summary>
        /// Set the duration of pre-recorded content so that live recording frames are offset correctly.
        /// This ensures live frames start AFTER pre-recorded frames in the final timeline.
        /// </summary>
        public void SetPreRecordingDuration(TimeSpan duration)
        {
            _preRecordingDuration = duration;
            System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Set pre-recording duration offset: {duration.TotalSeconds:F2}s");
        }

        /// <summary>
        /// Begin a frame for Skia composition. Returns canvas to draw on.
        /// </summary>
        public IDisposable BeginFrame(TimeSpan timestamp, out SKCanvas canvas, out SKImageInfo info, int orientation)
        {
            lock (_frameLock)
            {
                _pendingTimestamp = timestamp;

                // ZERO-COPY PATH: Create Metal-backed Surface directly from Encoder's PixelBuffer
                
                // 1. Initialize Thread-Safe Metal Context if needed
                EnsureMetalContext();

                if (_encodingContext != null)
                {
                    try
                    {
                        // 1. Get Destination Buffer from Pool (AVAssetWriter or VTCompressionSession)
                        CVPixelBufferPool pool = IsPreRecordingMode && _compressionSession != null
                            ? _compressionSession.GetPixelBufferPool()
                            : _pixelBufferAdaptor?.PixelBufferPool;

                        CVPixelBuffer pixelBuffer = null;
                        if (pool != null)
                        {
                            pixelBuffer = pool.CreatePixelBuffer(null, out var err);
                        }

                        // Fallback allocation if pool is empty/invalid
                        if (pixelBuffer == null)
                        {
                            var attrs = new CVPixelBufferAttributes
                            {
                                PixelFormatType = CVPixelFormatType.CV32BGRA,
                                Width = _width,
                                Height = _height,
                                MetalCompatibility = true
                            };
                            pixelBuffer = new CVPixelBuffer(_width, _height, CVPixelFormatType.CV32BGRA, attrs);
                        }

                        // 2. Prepare for new frame
                        _currentPixelBuffer?.Dispose();
                        _currentPixelBuffer = pixelBuffer;
                        _surface?.Dispose();

                        // 3. Create Metal Texture from Pixel Buffer
                        var cvTexture = _metalCache.TextureFromImage(pixelBuffer, MTLPixelFormat.BGRA8Unorm, _width, _height, 0, out var cvErr);

                        if (cvTexture != null)
                        {
                            // 4. Create Skia Surface wrapping the Metal texture
                            // Note: Skia will draw directly into the CVPixelBuffer via GPU
                            var textureInfo = new GRMtlTextureInfo(cvTexture.Texture.Handle);
                            var backendTexture = new GRBackendTexture(_width, _height, false, textureInfo);

                            _surface = SKSurface.Create(_encodingContext, backendTexture, GRSurfaceOrigin.TopLeft, SKColorType.Bgra8888);
                            
                            // cvTexture can be let go, underlying texture is kept by backendTexture/pixelBuffer interaction scope
                            // but generally explicit dispose of CVMetalTextureRef is safer? 
                            // usage: using (cvTexture) { ... } but we need it for the life of surface.
                            // The CVPixelBuffer is the root owner.
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Zero-Copy Init Failed: {ex}");
                        // Fallback to CPU
                        _currentPixelBuffer?.Dispose();
                        _currentPixelBuffer = null;
                        _encodingContext = null; // Disable for session
                    }
                }

                if (_surface == null || _info.Width != _width || _info.Height != _height)
                {
                    // Always update info if dimensions match current target
                    _info = new SKImageInfo(_width, _height, SKColorType.Bgra8888, SKAlphaType.Premul);
                    
                    if (_surface?.Handle == IntPtr.Zero || _surface == null) // Check if valid (Zero-Copy might have created it already)
                    {
                        if (_currentPixelBuffer == null) // Only create CPU-backed surface if NOT in Zero-Copy mode
                        {
                            _surface?.Dispose();
                            _surface = SKSurface.Create(_info);
                        }
                    }
                }

                canvas = _surface.Canvas;
                canvas.Clear(SKColors.Transparent);
                info = _info;

                return new FrameScope();
            }
        }

        /// <summary>
        /// Submit the composed frame for encoding
        /// Routes to VTCompressionSession (pre-recording) or AVAssetWriter (normal recording)
        /// </summary>
        public async Task SubmitFrameAsync()
        {
            if (!_isRecording)
                return;

            SKImage snapshot = null;
            CVPixelBuffer pixelBuffer = null;

            try
            {
                // ============================================================================
                // CREATE PREVIEW SNAPSHOT
                // ============================================================================
                lock (_frameLock)
                {
                    if (_surface == null)
                        return;

                    _surface.Canvas.Flush();

                    // Create CPU-backed preview snapshot (downscaled to reduce memory)
                    using var gpuSnap = _surface.Snapshot();
                    if (gpuSnap != null)
                    {
                        //int pw = Math.Min(_width, 480);
                        //int ph = Math.Max(1, (int)Math.Round(_height * (pw / (double)_width)));

                        int maxPreviewWidth = ParentCamera?.NativeControl?.PreviewWidth ?? 800;
                        int pw = Math.Min(_width, maxPreviewWidth);
                        int ph = Math.Max(1, (int)Math.Round(_height * (pw / (double)_width)));

                        var pInfo = new SKImageInfo(pw, ph, SKColorType.Bgra8888, SKAlphaType.Premul);

                        if (_previewSurface == null || _previewSurfaceInfo.Width != pInfo.Width || _previewSurfaceInfo.Height != pInfo.Height)
                        {
                            _previewSurface?.Dispose();
                            _previewSurfaceInfo = pInfo;
                            _previewSurface = SKSurface.Create(pInfo);
                        }
                        _previewSurface.Canvas.DrawImage(gpuSnap, new SKRect(0, 0, pw, ph));
                        snapshot = _previewSurface.Snapshot();

                        lock (_previewLock)
                        {
                            _latestPreviewImage?.Dispose();
                            _latestPreviewImage = snapshot;
                            snapshot = null; // Transfer ownership
                        }
                        PreviewAvailable?.Invoke(this, EventArgs.Empty);
                    }
                }

                // ============================================================================
                // ROUTE TO CORRECT ENCODER
                // ============================================================================

                // FLUSH GPU COMMANDS (Zero-Copy)
                if (_currentPixelBuffer != null)
                {
                    _surface.Canvas.Flush();
                    _encodingContext?.Flush(); // Ensure Metal commands are committed to CVPixelBuffer
                }

                if (IsPreRecordingMode && _compressionSession != null)
                {
                    // PRE-RECORDING MODE: Write ONLY to circular buffer in memory
                    // VTCompressionSession → circular buffer (H.264 in memory, no file)
                    await SubmitFrameToCompressionSession();
                }
                else
                {
                    // NORMAL RECORDING MODE: Use AVAssetWriter only
                    await SubmitFrameToAssetWriter();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] SubmitFrameAsync error: {ex.Message}");
            }
            finally
            {
                snapshot?.Dispose();
            }

            await Task.CompletedTask;
        }

        private async Task SubmitFrameToCompressionSession()
        {
            // Zero-Copy Optimization:
            // If we are in Metal mode, _currentPixelBuffer already contains the rendered frame (GPU-resident).
            // We just submit it directly.
            CVPixelBuffer pixelBuffer = _currentPixelBuffer;
            bool isZeroCopy = pixelBuffer != null;

            try
            {
                if (pixelBuffer == null)
                {
                    // CPU/Raster Path: Allocate new buffer
                    var pool = _compressionSession?.GetPixelBufferPool();
                    if (pool != null)
                    {
                        pixelBuffer = pool.CreatePixelBuffer(null, out var err);
                    }

                    if (pixelBuffer == null)
                    {
                        var attrs = new CVPixelBufferAttributes
                        {
                            PixelFormatType = CVPixelFormatType.CV32BGRA,
                            Width = _width,
                            Height = _height
                        };
                        pixelBuffer = new CVPixelBuffer(_width, _height, CVPixelFormatType.CV32BGRA, attrs);
                    }
                }

                if (pixelBuffer == null)
                    return;

                // CPU Copy (only if not Zero-Copy)
                if (!isZeroCopy)
                {
                    pixelBuffer.Lock(CVPixelBufferLock.None);
                    try
                    {
                        IntPtr baseAddress = pixelBuffer.BaseAddress;
                        nint bytesPerRow = pixelBuffer.BytesPerRow;

                        lock (_frameLock)
                        {
                            if (_surface == null) return;
                            var srcInfo = new SKImageInfo(_width, _height, SKColorType.Bgra8888, SKAlphaType.Premul);
                            if (!_surface.ReadPixels(srcInfo, baseAddress, (int)bytesPerRow, 0, 0))
                                return;
                        }
                    }
                    finally
                    {
                        pixelBuffer.Unlock(CVPixelBufferLock.None);
                    }
                }
                else
                {
                    // Zero-Copy: Just ensure GPU commands are flushed
                     // (Done in SubmitFrameAsync main block)
                }

                // Submit to VTCompressionSession
                CMTime presentationTime = CMTime.FromSeconds(_pendingTimestamp.TotalSeconds, 1_000_000);
                CMTime duration = CMTime.FromSeconds(1.0 / _frameRate, 1_000_000);

                var status = _compressionSession.EncodeFrame(
                    imageBuffer: pixelBuffer,
                    presentationTimestamp: presentationTime,
                    duration: duration,
                    frameProperties: null,
                    sourceFrame: 0,
                    infoFlags: out VTEncodeInfoFlags infoFlags);

                if (status != VTStatus.Ok)
                {
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] EncodeFrame failed: {status}");
                }
            }
            finally
            {
                // In Zero-Copy mode, buffer is owned by _currentPixelBuffer (reused/disposed next frame)
                // In CPU mode, we dispose our local allocation
                if (!isZeroCopy)
                    pixelBuffer?.Dispose();
            }

            await Task.CompletedTask;
        }

        private async Task SubmitFrameToAssetWriter()
        {
            // Zero-Copy Optimization:
            // Use current pixel buffer (Metal-backed) if available
            CVPixelBuffer pixelBuffer = _currentPixelBuffer;
            bool isZeroCopy = pixelBuffer != null;

            try
            {
                if (!_videoInput.ReadyForMoreMediaData)
                {
                    BackpressureDroppedFrames++;
                    return; // Backpressure: drop frame
                }

                if (pixelBuffer == null)
                {
                    // Allocate from pool (CPU Path)
                    CVReturn errCode = CVReturn.Error;
                    var pool = _pixelBufferAdaptor?.PixelBufferPool;
                    if (pool == null)
                        return;
                    pixelBuffer = pool.CreatePixelBuffer(null, out errCode);
                    if (pixelBuffer == null || errCode != CVReturn.Success)
                        return;
                }

                if (!isZeroCopy)
                {
                    // Copy pixels (CPU Slow Path)
                    pixelBuffer.Lock(CVPixelBufferLock.None);
                    try
                    {
                        IntPtr baseAddress = pixelBuffer.BaseAddress;
                        int bytesPerRow = (int)pixelBuffer.BytesPerRow;

                        lock (_frameLock)
                        {
                            if (_surface == null) return;
                            SKImageInfo srcInfo = new SKImageInfo(_width, _height, SKColorType.Bgra8888, SKAlphaType.Premul);
                            if (!_surface.ReadPixels(srcInfo, baseAddress, bytesPerRow, 0, 0))
                                return;
                        }
                    }
                    finally
                    {
                        pixelBuffer.Unlock(CVPixelBufferLock.None);
                    }
                }
                else
                {
                    // Zero-Copy: Just ensure GPU commands are flushed
                     // (Done in SubmitFrameAsync main block)
                }

                // Append to AVAssetWriter
                // CRITICAL: If we have a pre-recording offset, ADD it to frame timestamps
                // Session started at _preRecordingDuration, so all frames must be >= that time
                double timestamp = _pendingTimestamp.TotalSeconds;
                if (_preRecordingDuration > TimeSpan.Zero)
                {
                    timestamp += _preRecordingDuration.TotalSeconds;
                }
                CMTime ts = CMTime.FromSeconds(timestamp, 1_000_000);

                if (!_pixelBufferAdaptor.AppendPixelBufferWithPresentationTime(pixelBuffer, ts))
                {
                    // Drop silently on failure
                }
                else
                {
                    // Update statistics
                    EncodedFrameCount++;
                    if (pixelBuffer != null)
                    {
                        EncodedDataSize += (long)pixelBuffer.DataSize;
                    }
                    EncodingDuration = DateTime.Now - _startTime;
                    EncodingStatus = "Encoding";
                }
            }
            finally
            {
                if (!isZeroCopy)
                    pixelBuffer?.Dispose();
            }

            await Task.CompletedTask;
        }

        public async Task AbortAsync()
        {
            System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder #{_instanceId}] AbortAsync CALLED");

            _isRecording = false;
            _progressTimer?.Dispose();
            EncodingStatus = "Canceled";

            if (IsPreRecordingMode)
            {
                _compressionSession?.Dispose();
                _compressionSession = null;
                _preRecordingBuffer?.Dispose();
                _preRecordingBuffer = null;
            }

            try
            {
                _videoInput?.MarkAsFinished();
                _writer?.CancelWriting();
            }
            catch { }
            finally
            {
                _pixelBufferAdaptor = null;
                _videoInput?.Dispose(); _videoInput = null;
                _writer?.Dispose(); _writer = null;

                lock (_previewLock)
                {
                    _latestPreviewImage?.Dispose();
                    _latestPreviewImage = null;
                }

                _surface?.Dispose(); _surface = null;
                _previewSurface?.Dispose(); _previewSurface = null;
            }

            // Cleanup files
            try
            {
                if (!string.IsNullOrEmpty(_preRecordingFilePath) && File.Exists(_preRecordingFilePath))
                    File.Delete(_preRecordingFilePath);
                if (!string.IsNullOrEmpty(_liveRecordingFilePath) && File.Exists(_liveRecordingFilePath))
                    File.Delete(_liveRecordingFilePath);
                if (!string.IsNullOrEmpty(_outputPath) && File.Exists(_outputPath))
                    File.Delete(_outputPath);
            }
            catch { }
        }

        public async Task<CapturedVideo> StopAsync()
        {
            System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder #{_instanceId}] StopAsync CALLED: IsPreRecordingMode={IsPreRecordingMode}, BufferFrames={(_preRecordingBuffer?.GetFrameCount() ?? 0)}");

            _isRecording = false;
            _progressTimer?.Dispose();

            // Update status
            EncodingStatus = "Stopping";

            // CRITICAL: If pre-recording mode and buffer has frames, write buffer to file NOW
            if (IsPreRecordingMode && _preRecordingBuffer != null && _compressionSession != null)
            {
                int bufferFrameCount = _preRecordingBuffer.GetFrameCount();
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder #{_instanceId}] Pre-recording encoder stopping with {bufferFrameCount} buffered frames");

                if (bufferFrameCount > 0 && !string.IsNullOrEmpty(_preRecordingFilePath))
                {
                    // CRITICAL: Prune buffer to max duration BEFORE writing to file
                    // This ensures we never write more than PreRecordDuration seconds
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder #{_instanceId}] Pruning buffer to max duration before writing...");
                    _preRecordingBuffer.PruneToMaxDuration();

                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder #{_instanceId}] Writing buffer to: {_preRecordingFilePath}");
                    // ARCHITECTURAL FIX: Video only - audio is handled at SkiaCamera level
                    await WriteBufferedFramesToMp4Async(_preRecordingBuffer, _preRecordingFilePath, null);

                    // CRITICAL: Update EncodingDuration to reflect ACTUAL video duration from frame timestamps
                    // NOT wall-clock time! This is used by SkiaCamera for timestamp offset calculation
                    EncodingDuration = _preRecordingBuffer.GetBufferedDuration();
                    _encodingDurationSetFromFrames = true;  // Flag to prevent overwriting later
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder #{_instanceId}] Actual pre-recording video duration: {EncodingDuration.TotalSeconds:F3}s (NOT wall-clock time!)");

                    // Verify file was written
                    if (File.Exists(_preRecordingFilePath))
                    {
                        var fileInfo = new FileInfo(_preRecordingFilePath);
                        System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder #{_instanceId}] Pre-recording file written: {fileInfo.Length / 1024.0:F2} KB");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder #{_instanceId}] ERROR: Pre-recording file was not created!");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder #{_instanceId}] No buffered frames to write or invalid file path");
                }

                // Clean up compression session and buffer
                _compressionSession?.Dispose();
                _compressionSession = null;
                _preRecordingBuffer?.Dispose();
                _preRecordingBuffer = null;
            }

            try
            {
                // Finalize live recording
                _videoInput?.MarkAsFinished();
                if (_writer?.Status == AVAssetWriterStatus.Writing)
                {
                    var tcs = new TaskCompletionSource<bool>();
                    _writer?.FinishWriting(() => tcs.TrySetResult(true));
                    await tcs.Task.ConfigureAwait(false);
                }
            }
            catch { }
            finally
            {
                _pixelBufferAdaptor = null;
                _videoInput?.Dispose(); _videoInput = null;
                _writer?.Dispose(); _writer = null;

                lock (_previewLock)
                {
                    _latestPreviewImage?.Dispose();
                    _latestPreviewImage = null;
                }

                _surface?.Dispose(); _surface = null;
                _previewSurface?.Dispose(); _previewSurface = null;
            }

            // If pre-recording mode: concatenate pre-recording + live recording → final output
            if (IsPreRecordingMode && !string.IsNullOrEmpty(_preRecordingFilePath) && !string.IsNullOrEmpty(_liveRecordingFilePath))
            {
                // Check if live recording file actually exists and has content
                bool hasLiveRecording = File.Exists(_liveRecordingFilePath) && new FileInfo(_liveRecordingFilePath).Length > 0;
                bool hasPreRecording = File.Exists(_preRecordingFilePath) && new FileInfo(_preRecordingFilePath).Length > 0;

                if (hasPreRecording && hasLiveRecording)
                {
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Concatenating files:");
                    System.Diagnostics.Debug.WriteLine($"  Pre-recording: {_preRecordingFilePath}");
                    System.Diagnostics.Debug.WriteLine($"  Live recording: {_liveRecordingFilePath}");
                    System.Diagnostics.Debug.WriteLine($"  Final output: {_outputPath}");

                    await ConcatenateVideosAsync(_preRecordingFilePath, _liveRecordingFilePath, _outputPath);

                    // Clean up intermediate files
                    try
                    {
                        if (File.Exists(_preRecordingFilePath))
                            File.Delete(_preRecordingFilePath);
                        if (File.Exists(_liveRecordingFilePath))
                            File.Delete(_liveRecordingFilePath);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Failed to delete intermediate files: {ex.Message}");
                    }
                }
                else if (hasLiveRecording)
                {
                    // Only live recording exists, use it as output
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Only live recording exists, using as output");
                    if (File.Exists(_outputPath))
                        File.Delete(_outputPath);
                    File.Copy(_liveRecordingFilePath, _outputPath, true);

                    // Clean up
                    try
                    {
                        if (File.Exists(_liveRecordingFilePath))
                            File.Delete(_liveRecordingFilePath);
                        if (File.Exists(_preRecordingFilePath))
                            File.Delete(_preRecordingFilePath);
                    }
                    catch { }
                }
                else if (hasPreRecording)
                {
                    // Only pre-recording exists, use it as output
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Only pre-recording exists, using as output");
                    if (File.Exists(_outputPath))
                        File.Delete(_outputPath);
                    File.Copy(_preRecordingFilePath, _outputPath, true);

                    // Clean up
                    try
                    {
                        if (File.Exists(_preRecordingFilePath))
                            File.Delete(_preRecordingFilePath);
                        if (File.Exists(_liveRecordingFilePath))
                            File.Delete(_liveRecordingFilePath);
                    }
                    catch { }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] ERROR: Neither pre-recording nor live recording exist!");
                }
            }
            else if (IsPreRecordingMode && !string.IsNullOrEmpty(_liveRecordingFilePath))
            {
                // No pre-recording, just move live recording to output
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] No pre-recording, moving live recording to output");
                if (File.Exists(_liveRecordingFilePath))
                {
                    if (File.Exists(_outputPath))
                        File.Delete(_outputPath);
                    File.Copy(_liveRecordingFilePath, _outputPath, true);

                    // Clean up
                    try
                    {
                        File.Delete(_liveRecordingFilePath);
                    }
                    catch { }
                }
            }

            var info = new FileInfo(_outputPath);

            // Update final statistics
            EncodingStatus = "Completed";

            // CRITICAL: If EncodingDuration was set from frame timestamps, do NOT overwrite with wall-clock time!
            if (!_encodingDurationSetFromFrames)
            {
                EncodingDuration = DateTime.Now - _startTime;
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder #{_instanceId}] EncodingDuration set from wall-clock time: {EncodingDuration.TotalSeconds:F3}s");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder #{_instanceId}] Keeping pre-recording duration from frame timestamps: {EncodingDuration.TotalSeconds:F3}s (not using wall-clock time)");
            }

            return new CapturedVideo
            {
                FilePath = _outputPath,
                FileSizeBytes = info.Exists ? info.Length : 0,
                Duration = EncodingDuration,
                Time = _startTime
            };
        }

        public bool TryAcquirePreviewImage(out SKImage image)
        {
            lock (_previewLock)
            {
                image = _latestPreviewImage;
                _latestPreviewImage = null;
                return image != null;
            }
        }

        public Task AddFrameAsync(SKBitmap bitmap, TimeSpan timestamp)
        {
            // CPU fallback not used in VideoToolbox GPU path; keep for interface compatibility
            return Task.CompletedTask;
        }

        private async Task ConcatenateVideosAsync(string preRecordingPath, string liveRecordingPath, string outputPath)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] === Starting Video Concatenation ===");

                // Verify input files exist and log sizes
                if (!File.Exists(preRecordingPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Pre-recording file not found: {preRecordingPath}");
                    // Just copy live recording to output
                    if (File.Exists(liveRecordingPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Copying live recording to output (no pre-recording)");
                        File.Copy(liveRecordingPath, outputPath, true);
                    }
                    return;
                }

                if (!File.Exists(liveRecordingPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Live recording file not found: {liveRecordingPath}");
                    // Just copy pre-recording to output
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Copying pre-recording to output (no live recording)");
                    File.Copy(preRecordingPath, outputPath, true);
                    return;
                }

                // Log file sizes
                var preRecInfo = new FileInfo(preRecordingPath);
                var liveRecInfo = new FileInfo(liveRecordingPath);
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Pre-recording file: {preRecInfo.Length / 1024.0:F2} KB");
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Live recording file: {liveRecInfo.Length / 1024.0:F2} KB");

                // Load pre-recording asset and wait for it to load
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Loading pre-recording asset...");
                var preRecordingAsset = AVAsset.FromUrl(NSUrl.FromFilename(preRecordingPath));

                // Wait for asset to load
                var preRecLoadTcs = new TaskCompletionSource<bool>();
                preRecordingAsset.LoadValuesAsynchronously(new[] { "tracks", "duration" }, () =>
                {
                    preRecLoadTcs.TrySetResult(true);
                });
                await preRecLoadTcs.Task;

                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Pre-recording asset duration: {preRecordingAsset.Duration.Seconds:F3}s");
                var preRecordingVideoTrack = preRecordingAsset.TracksWithMediaType(AVMediaTypes.Video.GetConstant()).FirstOrDefault();
                var preRecordingAudioTrack = preRecordingAsset.TracksWithMediaType(AVMediaTypes.Audio.GetConstant()).FirstOrDefault();

                if (preRecordingVideoTrack == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] ERROR: Pre-recording has no video track!");
                    // Fall back to live recording only
                    File.Copy(liveRecordingPath, outputPath, true);
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] ============ MUXING DEBUG: SOURCE CHUNKS ============");
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Pre-recording track NaturalSize: {preRecordingVideoTrack.NaturalSize.Width}x{preRecordingVideoTrack.NaturalSize.Height}");

                // Load live recording asset and wait for it to load
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Loading live recording asset...");
                var liveRecordingAsset = AVAsset.FromUrl(NSUrl.FromFilename(liveRecordingPath));

                var liveRecLoadTcs = new TaskCompletionSource<bool>();
                liveRecordingAsset.LoadValuesAsynchronously(new[] { "tracks", "duration" }, () =>
                {
                    liveRecLoadTcs.TrySetResult(true);
                });
                await liveRecLoadTcs.Task;

                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Live recording asset duration: {liveRecordingAsset.Duration.Seconds:F3}s");
                var liveRecordingVideoTrack = liveRecordingAsset.TracksWithMediaType(AVMediaTypes.Video.GetConstant()).FirstOrDefault();
                var liveRecordingAudioTrack = liveRecordingAsset.TracksWithMediaType(AVMediaTypes.Audio.GetConstant()).FirstOrDefault();

                if (liveRecordingVideoTrack == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] ERROR: Live recording has no video track!");
                    // Fall back to pre-recording only
                    File.Copy(preRecordingPath, outputPath, true);
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Live recording track NaturalSize: {liveRecordingVideoTrack.NaturalSize.Width}x{liveRecordingVideoTrack.NaturalSize.Height}");
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Encoder _width={_width}, _height={_height}, _frameRate={_frameRate}");

                // Create AVMutableComposition for concatenation
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Creating composition...");
                var composition = AVMutableComposition.Create();
                var videoTrack = composition.AddMutableTrack(AVMediaTypes.Video.GetConstant(), 0);
                var audioTrack = composition.AddMutableTrack(AVMediaTypes.Audio.GetConstant(), 0);

                if (videoTrack == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] ERROR: Failed to create composition video track!");
                    return;
                }

                // Insert pre-recording track at time zero
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Inserting pre-recording track (duration: {preRecordingAsset.Duration.Seconds:F3}s)...");
                NSError error;
                var preRecTimeRange = new CMTimeRange { Start = CMTime.Zero, Duration = preRecordingAsset.Duration };

                videoTrack.InsertTimeRange(
                    preRecTimeRange,
                    preRecordingVideoTrack,
                    CMTime.Zero,
                    out error);

                if (error != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] ERROR: Failed to insert pre-recording: {error.LocalizedDescription} (Code: {error.Code})");
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Falling back to live recording only");
                    File.Copy(liveRecordingPath, outputPath, true);
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Pre-recording inserted successfully");

                // Insert pre-recording audio if available
                if (audioTrack != null && preRecordingAudioTrack != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Inserting pre-recording audio track...");
                    audioTrack.InsertTimeRange(
                        preRecTimeRange,
                        preRecordingAudioTrack,
                        CMTime.Zero,
                        out var audioError);
                    
                    if (audioError != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] WARNING: Failed to insert pre-recording audio: {audioError.LocalizedDescription}");
                    }
                }

                // Insert live recording track after pre-recording
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Inserting live recording track (duration: {liveRecordingAsset.Duration.Seconds:F3}s) at time {preRecordingAsset.Duration.Seconds:F3}s...");
                var liveRecTimeRange = new CMTimeRange { Start = CMTime.Zero, Duration = liveRecordingAsset.Duration };

                videoTrack.InsertTimeRange(
                    liveRecTimeRange,
                    liveRecordingVideoTrack,
                    preRecordingAsset.Duration,
                    out error);

                if (error != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] ERROR: Failed to insert live recording: {error.LocalizedDescription} (Code: {error.Code})");
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Falling back to pre-recording only");
                    File.Copy(preRecordingPath, outputPath, true);
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Live recording inserted successfully");

                // Insert live recording audio if available
                if (audioTrack != null && liveRecordingAudioTrack != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Inserting live recording audio track...");
                    audioTrack.InsertTimeRange(
                        liveRecTimeRange,
                        liveRecordingAudioTrack,
                        preRecordingAsset.Duration,
                        out var audioError);

                    if (audioError != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] WARNING: Failed to insert live recording audio: {audioError.LocalizedDescription}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Total composition duration: {composition.Duration.Seconds:F3}s");

                // CRITICAL: Set preferred transform on composition video track to preserve orientation
                // Without this, the exported video will always be portrait regardless of recording orientation
                // Log source track transforms for diagnostics
                var preTransform = preRecordingVideoTrack.PreferredTransform;
                var liveTransform = liveRecordingVideoTrack.PreferredTransform;
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Pre-recording track transform: xx={preTransform.xx}, xy={preTransform.xy}, yx={preTransform.yx}, yy={preTransform.yy}, tx={preTransform.x0}, ty={preTransform.y0}");
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Live recording track transform: xx={liveTransform.xx}, xy={liveTransform.xy}, yx={liveTransform.yx}, yy={liveTransform.yy}, tx={liveTransform.x0}, ty={liveTransform.y0}");

                // Use _deviceRotation directly since both segments were recorded with the same orientation
                var compositionTransform = GetTransformForRotation(_deviceRotation);
                videoTrack.PreferredTransform = compositionTransform;
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Set composition preferredTransform for deviceRotation={_deviceRotation}, dimensions={_width}x{_height}: xx={compositionTransform.xx}, xy={compositionTransform.xy}, yx={compositionTransform.yx}, yy={compositionTransform.yy}, tx={compositionTransform.x0}, ty={compositionTransform.y0}");

                // CRITICAL BUG FIX: Create AVMutableVideoComposition with explicit renderSize
                // Without this, AVAssetExportSession.PresetPassthrough may use default/preview dimensions
                // This was causing 1920x1080 videos to be exported as 568x320 (preview size)
                var videoComposition = AVMutableVideoComposition.Create();
                videoComposition.FrameDuration = new CMTime(1, _frameRate);
                videoComposition.RenderSize = new CoreGraphics.CGSize(_width, _height);

                var instruction = new AVMutableVideoCompositionInstruction
                {
                    TimeRange = new CMTimeRange { Start = CMTime.Zero, Duration = composition.Duration }
                };
                var layerInstruction = AVMutableVideoCompositionLayerInstruction.FromAssetTrack(videoTrack);
                layerInstruction.SetTransform(compositionTransform, CMTime.Zero);
                instruction.LayerInstructions = new[] { layerInstruction };
                videoComposition.Instructions = new[] { instruction };

                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Created video composition with renderSize={_width}x{_height}, frameDuration=1/{_frameRate}");

                // Export composition to output file
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Starting export to: {outputPath}");
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Deleted existing output file");
                }

                // Use highest quality preset instead of Passthrough when we have video composition
                var exportSession = new AVAssetExportSession(composition, AVAssetExportSession.PresetHighestQuality);
                if (exportSession == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] ERROR: Failed to create AVAssetExportSession");
                    return;
                }

                exportSession.OutputUrl = NSUrl.FromFilename(outputPath);
                exportSession.OutputFileType = AVFileTypes.Mpeg4.GetConstant();
                exportSession.VideoComposition = videoComposition;

                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Export session created:");
                System.Diagnostics.Debug.WriteLine($"  Preset: {AVAssetExportSession.PresetHighestQuality}");
                System.Diagnostics.Debug.WriteLine($"  VideoComposition: renderSize={videoComposition.RenderSize.Width}x{videoComposition.RenderSize.Height}");
                System.Diagnostics.Debug.WriteLine($"  Output URL: {exportSession.OutputUrl}");
                System.Diagnostics.Debug.WriteLine($"  Output file type: {exportSession.OutputFileType}");
                System.Diagnostics.Debug.WriteLine($"  Supported file types: {string.Join(", ", exportSession.SupportedFileTypes ?? new string[0])}");

                var tcs = new TaskCompletionSource<bool>();
                exportSession.ExportAsynchronously(() =>
                {
                    if (exportSession.Status == AVAssetExportSessionStatus.Completed)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Export completed successfully!");
                        var fileInfo = new FileInfo(outputPath);
                        if (fileInfo.Exists)
                        {
                            System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Final file size: {fileInfo.Length / 1024.0:F2} KB");
                            System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] === Concatenation Complete ===");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] WARNING: Output file doesn't exist after export!");
                        }
                        tcs.TrySetResult(true);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Export FAILED!");
                        System.Diagnostics.Debug.WriteLine($"  Status: {exportSession.Status}");
                        System.Diagnostics.Debug.WriteLine($"  Error: {exportSession.Error?.LocalizedDescription}");
                        System.Diagnostics.Debug.WriteLine($"  Error Code: {exportSession.Error?.Code}");
                        System.Diagnostics.Debug.WriteLine($"  Error Domain: {exportSession.Error?.Domain}");

                        if (exportSession.Status == AVAssetExportSessionStatus.Failed)
                        {
                            System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] FALLBACK: Copying live recording as output");
                            try
                            {
                                File.Copy(liveRecordingPath, outputPath, true);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Fallback copy failed: {ex.Message}");
                            }
                        }

                        tcs.TrySetResult(false);
                    }
                });

                await tcs.Task;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] ConcatenateVideosAsync error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private async Task WriteBufferedFramesToMp4Async(PrerecordingEncodedBufferApple buffer, string outputPath, CircularAudioBuffer audioBuffer = null)
        {
            var frames = buffer.GetAllFrames();
            if (frames == null || frames.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] No frames to write");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Writing {frames.Count} buffered frames to MP4: {outputPath}");

            // Use a temporary file to avoid conflicts with existing file
            var tempPath = Path.Combine(Path.GetDirectoryName(outputPath), $"temp_{Guid.NewGuid():N}.mp4");
            
            // Memory tracking to prevent use-after-free in async writer
            var allocatedPointers = new List<IntPtr>();

            AVAssetWriter writer = null;
            AVAssetWriterInput videoInput = null;

            try
            {
                // Delete output file if it exists
                if (File.Exists(outputPath))
                {
                    try
                    {
                        File.Delete(outputPath);
                        System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Deleted existing file: {outputPath}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Failed to delete existing file: {ex.Message}");
                    }
                }

                // Get format description first (contains SPS/PPS, needed for AVAssetWriterInput)
                CMVideoFormatDescription formatDesc = _videoFormatDescription;
                if (formatDesc == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] No format description available");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] ============ PRE-RECORDING BUFFER FORMAT ============");
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] _videoFormatDescription dimensions: {formatDesc.Dimensions.Width}x{formatDesc.Dimensions.Height}");
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] _videoFormatDescription MediaSubType: {formatDesc.MediaSubType}");
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Encoder _width={_width}, _height={_height}");

                // Create AVAssetWriter for MP4 output (use temp path)
                var url = NSUrl.FromFilename(tempPath);
                writer = new AVAssetWriter(url, "public.mpeg-4", out var err);
                if (writer == null || err != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] AVAssetWriter creation failed: {err?.LocalizedDescription}");
                    return;
                }

                // CRITICAL: For pass-through of already-compressed H.264 data:
                // - Use null outputSettings (we don't want re-encoding)
                // - Provide sourceFormatHint so AVAssetWriter knows the format
                videoInput = new AVAssetWriterInput(
                    AVMediaTypes.Video.GetConstant(),
                    (AVVideoSettingsCompressed)null,
                    formatDesc);
                videoInput.ExpectsMediaDataInRealTime = false;

                // Set transform based on device rotation
                videoInput.Transform = GetTransformForRotation(_deviceRotation);

                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Created AVAssetWriterInput with null settings and format hint: {formatDesc.Dimensions.Width}x{formatDesc.Dimensions.Height}");

                if (!writer.CanAddInput(videoInput))
                {
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Cannot add video input to writer");
                    return;
                }

                writer.AddInput(videoInput);

                AVAssetWriterInput audioInput = null;
                if (audioBuffer != null)
                {
                     var audioSettings = new AudioSettings
                    {
                        Format = AudioFormatType.MPEG4AAC,
                        SampleRate = 44100,
                        NumberChannels = 1,
                        EncoderBitRate = 128000
                    };
                    audioInput = new AVAssetWriterInput(AVMediaTypes.Audio.GetConstant(), audioSettings) { ExpectsMediaDataInRealTime = false };
                    if (writer.CanAddInput(audioInput)) writer.AddInput(audioInput);
                }

                if (!writer.StartWriting())
                {
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] StartWriting failed: {writer.Error?.LocalizedDescription}");
                    return;
                }

                writer.StartSessionAtSourceTime(CMTime.Zero);

                var audioSamples = audioBuffer?.GetAllSamples();

                // CRITICAL: Get first frame's PTS to use as offset for adjusting timestamps to start from zero
                // This is needed because after pruning, frames might start at PTS=5.0s instead of 0.0s
                CMTime firstFramePts = frames.Count > 0 ? frames[0].PresentationTime : CMTime.Zero;
                long firstAudioNs = 0;

                if (audioSamples != null && audioSamples.Length > 0)
                {
                    firstAudioNs = audioSamples[0].TimestampNs;
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] First Audio PTS: {firstAudioNs} ns");
                }
                
                int appendedCount = 0;
                int frameIndex = 0;
                int vIndex = 0;
                int aIndex = 0;
                int vCount = frames.Count;
                int aCount = audioSamples?.Length ?? 0;
                int audioWrittenCount = 0;
                int audioFailedCount = 0;

                // DIAGNOSTIC DUMP
                if (frames.Count > 0)
                {
                    var lastV = frames.Last().PresentationTime;
                    System.Diagnostics.Debug.WriteLine($"[Debug] Video Range: {firstFramePts.Seconds:F3}s -> {lastV.Seconds:F3}s (Duration: {(lastV - firstFramePts).Seconds:F3}s)");
                }
                if (aCount > 0)
                {
                     double firstA = audioSamples[0].TimestampNs / 1_000_000_000.0;
                     double lastA = audioSamples.Last().TimestampNs / 1_000_000_000.0;
                     System.Diagnostics.Debug.WriteLine($"[Debug] Audio Range (Raw): {firstA:F3}s -> {lastA:F3}s (Duration: {lastA - firstA:F3}s)");

                     // ALIGNMENT CHECK: Video and Audio should have similar timestamps
                     // If audio uses system uptime correctly, both should be within ~1s of each other
                     double alignmentDiff = Math.Abs(firstFramePts.Seconds - firstA);
                     if (alignmentDiff > 1.0)
                     {
                         System.Diagnostics.Debug.WriteLine($"[WARNING] TIMESTAMP MISALIGNMENT! Video starts at {firstFramePts.Seconds:F3}s but audio starts at {firstA:F3}s (diff: {alignmentDiff:F3}s)");
                         System.Diagnostics.Debug.WriteLine($"[WARNING] This will cause audio/video desync. Check timestamp source in AudioCaptureApple!");
                     }
                     else
                     {
                         System.Diagnostics.Debug.WriteLine($"[Debug] Timestamp alignment OK (Video: {firstFramePts.Seconds:F3}s, Audio: {firstA:F3}s, diff: {alignmentDiff:F3}s)");
                     }
                }

                // Interleaved write loop with Independent Normalization
                // We normalize both streams to start at 0.0 to ensure correct interleaving 
                // and to avoid 'Writer DEADLOCK' caused by large gaps (e.g. Video@0, Audio@1.3s).
                // This assumes the buffer content is roughly synced by wall-clock or that strict sync isn't critical for pre-recording.
                
                int loopGuard = 0;
                while (vIndex < vCount || aIndex < aCount)
                {
                    loopGuard++;
                    if (loopGuard % 100 == 0) // Log heartbeat every 100 items processed
                        System.Diagnostics.Debug.WriteLine($"[Debug] Process loop: V={vIndex}/{vCount}, A={aIndex}/{aCount}");

                    bool writeVideo = false;

                    // Decision logic: Write video if it's earlier than next audio sample (normalized)
                    if (vIndex < vCount && aIndex < aCount)
                    {
                         // Normalize Video to 0.0
                         double vPos = (frames[vIndex].PresentationTime - firstFramePts).Seconds;
                         
                         // Normalize Audio to 0.0 (Independent)
                         double aPos = (double)(audioSamples[aIndex].TimestampNs - firstAudioNs) / 1_000_000_000.0;
                         
                         if (vPos <= aPos) 
                            writeVideo = true;
                    }
                    else if (vIndex < vCount)
                    {
                        writeVideo = true;
                    }
                    // else writeVideo = false (implies audio)

                    if (writeVideo)
                    {
                        var (data, presentationTime, duration) = frames[vIndex];
                        vIndex++;
                        frameIndex++;

                        // Skip empty frames but keep indexing
                        if (data == null || data.Length == 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Frame {frameIndex}: SKIPPED (no data)");
                            continue;
                        }

                        // Create CMBlockBuffer from H.264 data
                        var unmanagedPtr = Marshal.AllocHGlobal(data.Length);
                        Marshal.Copy(data, 0, unmanagedPtr, data.Length);
                        allocatedPointers.Add(unmanagedPtr);

                        var blockBuffer = CMBlockBuffer.FromMemoryBlock(
                            unmanagedPtr,
                            (nuint)data.Length,
                            null,
                            0,
                            (nuint)data.Length,
                            CMBlockBufferFlags.AssureMemoryNow,
                            out var blockError);

                        if (blockBuffer == null || blockError != CMBlockBufferError.None)
                        {
                            System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Frame {frameIndex}: Failed to create block buffer: {blockError}");
                            // Cannot easily remove from list safely if exceptions happen, but it's fine to free later
                            continue;
                        }

                        try
                        {
                            // Adjust timestamp to be relative to start (starts at 0)
                            var adjustedPts = CMTime.Subtract(presentationTime, firstFramePts);

                            if (frameIndex == 1 || frameIndex == frames.Count || frameIndex % 30 == 0)
                            {
                                System.Diagnostics.Debug.WriteLine($"[Debug] Writing Video Frame {frameIndex}: PTS={adjustedPts.Seconds:F3}s");
                            }

                            var timing = new CMSampleTimingInfo
                            {
                                PresentationTimeStamp = adjustedPts,
                                Duration = duration,
                                DecodeTimeStamp = CMTime.Invalid
                            };

                            var sampleSizes = new nuint[] { (nuint)data.Length };
                            var sampleBuffer = CMSampleBuffer.CreateReady(
                                blockBuffer,
                                formatDesc,
                                1,
                                new[] { timing },
                                sampleSizes,
                                out var sampleError);

                            if (sampleBuffer == null || sampleError != CMSampleBufferError.None)
                            {
                                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Frame {frameIndex}: Failed to create sample buffer: {sampleError}");
                                blockBuffer.Dispose();
                                Marshal.FreeHGlobal(unmanagedPtr);
                                continue;
                            }

                            try
                            {
                                // Wait for Ready (Blocking with timeout)
                                int safety = 0;
                                while (!videoInput.ReadyForMoreMediaData && writer.Status == AVAssetWriterStatus.Writing && safety < 500)
                                {
                                    safety++;
                                    await Task.Delay(10);
                                }

                                if (writer.Status != AVAssetWriterStatus.Writing)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Writer failed or cancelled (Status: {writer.Status}, Error: {writer.Error?.LocalizedDescription})");
                                    // Stop processing
                                    break;
                                }

                                if (!videoInput.ReadyForMoreMediaData)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] WARNING: Video input not ready after 5s wait. Dropping frame {frameIndex}.");
                                }
                                else
                                {
                                    if (videoInput.AppendSampleBuffer(sampleBuffer))
                                    {
                                        appendedCount++;
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Video AppendSampleBuffer FAILED (Status: {writer.Status}, Error: {writer.Error?.LocalizedDescription})");
                                    }
                                }
                            }
                            finally
                            {
                                sampleBuffer.Dispose();
                            }
                        }
                        finally
                        {
                            blockBuffer.Dispose();
                            // Do NOT free unmanagedPtr here - writer might still be using it!
                            // It will be freed in main finally block
                        }
                    }
                    else
                    {
                        // Write Audio
                        var sample = audioSamples[aIndex];
                        // Do NOT increment aIndex yet

                        using var sBuf = CreateSampleBuffer(sample, allocatedPointers);
                        if (sBuf != null)
                        {
                             // Create timing relative to First Audio (starts at 0)
                             double relSeconds = (double)(sample.TimestampNs - firstAudioNs) / 1_000_000_000.0;
                             var adjPts = CMTime.FromSeconds(relSeconds, 1_000_000_000);
                             
                             // Set duration correctly (1024 samples / 44100 = ~0.023s)
                             // sBuf already has duration from CreateSampleBuffer? 
                             // We should ensure duration is valid.
                             var duration = sBuf.Duration;
                             
                            var timingInfo = new CMSampleTimingInfo { PresentationTimeStamp = adjPts, Duration = duration, DecodeTimeStamp = CMTime.Invalid };
                            nint createTxErr;
                            var adjustedBuf = CMSampleBuffer.CreateWithNewTiming(sBuf, new[] { timingInfo }, out createTxErr);
                             
                            if (createTxErr == 0 && adjustedBuf != null)
                            {
                                // Wait for Ready
                                int safety = 0;
                                while (!audioInput.ReadyForMoreMediaData && writer.Status == AVAssetWriterStatus.Writing && safety < 500)
                                {
                                    safety++;
                                    await Task.Delay(10);
                                }

                                if (writer.Status != AVAssetWriterStatus.Writing)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Writer failed during audio (Status: {writer.Status})");
                                    adjustedBuf.Dispose(); // Ensure disposal
                                    break;
                                }

                                if (audioInput.ReadyForMoreMediaData)
                                {
                                    if (audioInput.AppendSampleBuffer(adjustedBuf))
                                    {
                                        audioWrittenCount++;
                                        aIndex++;
                                    }
                                    else
                                    {
                                         System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Audio AppendSampleBuffer FAILED at {aIndex} (Status: {writer.Status}, Error: {writer.Error?.LocalizedDescription})");
                                         audioFailedCount++;
                                         aIndex++;
                                    }
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] WARNING: Audio input not ready after 5s wait. Dropping sample {aIndex}.");
                                    audioFailedCount++;
                                    aIndex++;
                                }
                                adjustedBuf.Dispose();
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Failed to create adjusted timing for audio sample {aIndex}");
                                audioFailedCount++;
                                aIndex++;
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] CreateSampleBuffer failed for audio sample {aIndex}");
                            audioFailedCount++;
                            aIndex++;
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Write complete: Video {appendedCount}/{frames.Count}, Audio {audioWrittenCount}/{aCount} (failed: {audioFailedCount})");

                if (audioInput != null)
                {
                    audioInput.MarkAsFinished();
                }

                // Finalize writing
                videoInput.MarkAsFinished();
                
                if (writer.Status == AVAssetWriterStatus.Writing)
                {
                    var tcs = new TaskCompletionSource<bool>();
                    writer.FinishWriting(() => tcs.TrySetResult(true));
                    await tcs.Task;
                    
                    if (writer.Status == AVAssetWriterStatus.Completed)
                    {
                         System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] MP4 file finalized to temp: {tempPath}");
                    }
                    else
                    {
                         System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] FinishWriting failed. Status: {writer.Status}, Error: {writer.Error?.LocalizedDescription}");
                         // Force cleanup?
                         File.Delete(tempPath);
                         return; // Abort
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Cannot finalize writing, status is {writer.Status}");
                    return;
                }

                // CRITICAL: Dispose writer and input immediately to release file handles
                videoInput?.Dispose();
                videoInput = null;
                writer?.Dispose();
                writer = null;

                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Writer disposed, file handles released");

                // Move temp file to final output path
                if (File.Exists(tempPath))
                {
                    try
                    {
                        // Delete target if it exists
                        if (File.Exists(outputPath))
                        {
                            File.Delete(outputPath);
                        }

                        File.Move(tempPath, outputPath);
                        System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Moved temp file to final path: {outputPath}");

                        var fileInfo = new FileInfo(outputPath);
                        System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Pre-recording MP4 written: {fileInfo.Length / 1024.0:F2} KB, {appendedCount} frames");

                        // CRITICAL DEBUG: Read back pre-recording file dimensions
                        #if DEBUG
                        try
                        {
                            var preRecAsset = AVAsset.FromUrl(NSUrl.FromFilename(outputPath));
                            var preRecLoadTcs = new TaskCompletionSource<bool>();
                            preRecAsset.LoadValuesAsynchronously(new[] { "tracks" }, () => preRecLoadTcs.TrySetResult(true));
                            await preRecLoadTcs.Task.ConfigureAwait(false);

                            var preRecVideoTrack = preRecAsset.TracksWithMediaType(AVMediaTypes.Video.GetConstant()).FirstOrDefault();
                            if (preRecVideoTrack != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] ============ PRE-RECORDING FILE DIMENSIONS ============");
                                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Pre-recording file NaturalSize: {preRecVideoTrack.NaturalSize.Width}x{preRecVideoTrack.NaturalSize.Height}");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Error reading pre-recording file dimensions: {ex.Message}");
                        }
                        #endif
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Failed to move temp file: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] WriteBufferedFramesToMp4Async error: {ex.Message}\n{ex.StackTrace}");
                writer?.CancelWriting();

                // Clean up temp file on error
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }
            }
            finally
            {
                videoInput?.Dispose();
                writer?.Dispose();
                
                // Free all tracked allocations
                foreach (var ptr in allocatedPointers)
                {
                    Marshal.FreeHGlobal(ptr);
                }
                allocatedPointers.Clear();
            }
        }

        public async Task<string> GetCombinedPreRecordingFileAsync(PrerecordingEncodedBufferApple prerecordingBuffer)
        {
            if (prerecordingBuffer == null || prerecordingBuffer.GetFrameCount() == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] No buffered frames to get");
                return null;
            }

            try
            {
                // Log buffer statistics
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] Pre-recording buffer stats: {prerecordingBuffer.GetStats()}");
                
                // Flush buffers to disk files
                var (fileA, fileB) = await prerecordingBuffer.FlushToFilesAsync();
                
                System.Diagnostics.Debug.WriteLine(
                    $"[AppleVideoToolboxEncoder] Pre-recording buffers flushed to files: " +
                    $"FileA={fileA}, FileB={fileB}");

                // Return both files so caller can combine them
                return $"{fileA}|{fileB}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppleVideoToolboxEncoder] GetCombinedPreRecordingFileAsync error: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            if (_isRecording)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await StopAsync();
                    }
                    catch
                    {
                    }
                    _isRecording = false;
                });
            }
            _progressTimer?.Dispose();
            _compressionSession?.Dispose();
            _compressionSession = null;
            _preRecordingBuffer?.Dispose();
            _preRecordingBuffer = null;

            _encodingContext?.Dispose();
            _encodingContext = null;

            _metalCache?.Dispose();
            _metalCache = null;
            _commandQueue = null; // Release reference

            _previewSurface?.Dispose();
            _previewSurface = null;
            _surface?.Dispose();
            _surface = null;
        }

        public sealed class FrameScope : IDisposable
        {
            public void Dispose() { }
        }
    }
}
#endif
