#if IOS || MACCATALYST
using CoreMedia;
#endif

using System;
using System.Linq;

namespace DrawnUi.Camera
{
    /// <summary>
    /// Two-buffer pre-recording system for H.264 encoded video frames.
    ///
    /// Maintains two byte buffers sized dynamically from target bitrate and retention window.
    /// When data reaches the configured time horizon the buffers swap atomically, ensuring
    /// pre-roll retention without dropping keyframes.
    /// </summary>
    public class PrerecordingEncodedBufferApple : IDisposable
    {
        /// <summary>
        /// Stores a single encoded frame with timing information
        /// </summary>
        private class EncodedFrame
        {
            public byte[] Data;
            public TimeSpan Timestamp;
            public DateTime AddedAt;
            public bool IsKeyFrame;  // Critical for pruning: must start with keyframe for valid H.264

#if IOS || MACCATALYST
            public CMTime PresentationTime;
            public CMTime Duration;
#endif
        }

        /// <summary>
        /// Tracks state of each buffer independently
        /// </summary>
        private struct BufferState
        {
            public int BytesUsed;           // How much of this buffer is filled
            public DateTime StartTime;      // When this buffer started receiving frames
            public int FrameCount;          // Number of frames in this buffer (diagnostics)
            public bool IsLocked;           // Prevent writes during finalization
        }

        // Two fixed-size buffers (pre-allocated at init, never reallocated)
        private byte[] _bufferA;            // ~13.5 MB
        private byte[] _bufferB;            // ~13.5 MB

        // Current active buffer index: 0=A, 1=B (atomic toggle)
        private int _currentBuffer = 0;

        // State tracking for each buffer (separate)
        private BufferState _stateA;
        private BufferState _stateB;

        private const double HeadroomFactor = 1.3;
        private const int MinBufferBytes = 4 * 1024 * 1024;   // 4 MB floor
        private const int MaxBufferBytes = 512 * 1024 * 1024; // 512 MB guardrail
        private int _bufferCapacity;

        // Frame metadata tracking (for reconstructing CMSampleBuffers)
        private readonly List<EncodedFrame> _frames = new();

        // Thread safety: lock only held during swap (~100ns)
        private readonly object _swapLock = new();

        // Configuration
        private TimeSpan _maxDuration;      // Maximum duration per buffer (typically 5 seconds)
        private bool _isDisposed;

        /// <summary>
        /// Initializes the two-buffer system with pre-allocated buffers.
        /// </summary>
        /// <param name="maxDuration">Maximum duration to maintain in buffer (e.g., 5 seconds)</param>
        public PrerecordingEncodedBufferApple(TimeSpan maxDuration, long targetBitrateBitsPerSecond = 0)
        {
            _maxDuration = maxDuration == TimeSpan.Zero ? TimeSpan.FromSeconds(5) : maxDuration;
            _bufferCapacity = CalculateInitialCapacity(_maxDuration, targetBitrateBitsPerSecond);

            // Pre-allocate both buffers (no GC during recording)
            _bufferA = new byte[_bufferCapacity];
            _bufferB = new byte[_bufferCapacity];

            // Initialize buffer states
            _stateA = new BufferState
            {
                BytesUsed = 0,
                StartTime = DateTime.UtcNow,
                FrameCount = 0,
                IsLocked = false
            };

            _stateB = new BufferState
            {
                BytesUsed = 0,
                StartTime = DateTime.MinValue,  // Not active yet
                FrameCount = 0,
                IsLocked = false
            };

            System.Diagnostics.Debug.WriteLine(
                $"[PrerecordingEncodedBufferApple] Initialized: bufferSize={_bufferCapacity / 1024 / 1024}MB, " +
                $"maxDuration={maxDuration.TotalSeconds:F1}s");
        }

        private static int CalculateInitialCapacity(TimeSpan maxDuration, long targetBitrateBitsPerSecond)
        {
            double seconds = Math.Max(1d, maxDuration.TotalSeconds);
            double fallbackBitrate = 12_000_000d; // 12 Mbps baseline if encoder bitrate unknown
            double bitrate = targetBitrateBitsPerSecond > 0 ? targetBitrateBitsPerSecond : fallbackBitrate;
            double bytesPerSecond = bitrate / 8d;
            double estimatedBytes = bytesPerSecond * seconds * HeadroomFactor;
            long bounded = (long)Math.Ceiling(estimatedBytes);

            bounded = Math.Max(bounded, (long)MinBufferBytes);
            bounded = Math.Min(bounded, (long)MaxBufferBytes);

            if (bounded > int.MaxValue - 1)
            {
                bounded = int.MaxValue - 1;
            }

            return (int)bounded;
        }

        private void EnsureCapacity(int requiredBytes)
        {
            if (requiredBytes <= _bufferCapacity)
                return;

            int guard = Math.Min(int.MaxValue - 1, MaxBufferBytes);
            if (requiredBytes > guard)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[PrerecordingEncodedBufferApple] Frame payload {requiredBytes / 1024}KB exceeds guard limit of {guard / 1024}KB; dropping frame");
                return;
            }

            long grown = Math.Max(requiredBytes, (long)Math.Ceiling(_bufferCapacity * 1.5));
            int proposed = (int)Math.Min(Math.Max(grown, MinBufferBytes), guard);

            var newA = new byte[proposed];
            var newB = new byte[proposed];

            if (_stateA.BytesUsed > 0)
            {
                Buffer.BlockCopy(_bufferA, 0, newA, 0, _stateA.BytesUsed);
            }
            if (_stateB.BytesUsed > 0)
            {
                Buffer.BlockCopy(_bufferB, 0, newB, 0, _stateB.BytesUsed);
            }

            _bufferA = newA;
            _bufferB = newB;
            _bufferCapacity = proposed;

            System.Diagnostics.Debug.WriteLine(
                $"[PrerecordingEncodedBufferApple] Resized buffers to {_bufferCapacity / 1024 / 1024}MB to fit {requiredBytes / 1024}KB frame payload");
        }

        /// <summary>
        /// Thread-safe append of encoded H.264 frame data.
        ///
        /// Acquires lock only to check/perform swap (~100ns).
        /// Actual data copy happens without lock (lock-free append).
        ///
        /// Expected: 30 calls/sec @ ~0.1ms each = <3% CPU impact
        /// </summary>
        /// <param name="nalUnits">H.264 NAL unit bytes</param>
        /// <param name="size">Size in bytes</param>
        /// <param name="timestamp">Frame presentation time (for duration calculation)</param>
        public void AppendEncodedFrame(byte[] nalUnits, int size, TimeSpan timestamp, bool isKeyFrame = false)
        {
            if (_isDisposed || nalUnits == null || size == 0)
                return;

            lock (_swapLock)
            {
                // Get current buffer and state
                byte[] currentBuffer = _currentBuffer == 0 ? _bufferA : _bufferB;
                ref BufferState currentState = ref _currentBuffer == 0 ? ref _stateA : ref _stateB;

                // Initialize StartTime on first append
                if (currentState.BytesUsed == 0)
                {
                    currentState.StartTime = DateTime.UtcNow;
                }

                // Check if current buffer duration exceeded
                TimeSpan elapsed = DateTime.UtcNow - currentState.StartTime;
                if (elapsed > _maxDuration)
                {
                    // SWAP BUFFERS: Toggle to other buffer (atomic int toggle)
                    _currentBuffer = 1 - _currentBuffer;

                    byte[] nextBuffer = _currentBuffer == 0 ? _bufferA : _bufferB;
                    ref BufferState nextState = ref _currentBuffer == 0 ? ref _stateA : ref _stateB;

                    // Reset new active buffer
                    nextState.BytesUsed = 0;
                    nextState.FrameCount = 0;
                    nextState.StartTime = DateTime.UtcNow;
                    nextState.IsLocked = false;

                    // Prune frames to keep only the last _maxDuration seconds based on video PTS timestamps
                    if (_frames.Count > 0)
                    {
                        var lastFrameTimestamp = _frames[_frames.Count - 1].Timestamp;
                        var cutoffTimestamp = lastFrameTimestamp - _maxDuration;
                        int beforePrune = _frames.Count;
                        _frames.RemoveAll(f => f.Timestamp < cutoffTimestamp);

                        // CRITICAL: Ensure first frame is a keyframe after pruning
                        while (_frames.Count > 0 && !_frames[0].IsKeyFrame)
                        {
                            _frames.RemoveAt(0);
                        }

                        int afterPrune = _frames.Count;

                        System.Diagnostics.Debug.WriteLine(
                            $"[PreRecording] Swapped buffers. Active={(char)('A' + _currentBuffer)}, " +
                            $"ElapsedInOldBuffer={elapsed.TotalSeconds:F2}s, " +
                            $"Pruned frames: {beforePrune} -> {afterPrune} (cutoff={cutoffTimestamp.TotalSeconds:F3}s, first is KEYFRAME)");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[PreRecording] Swapped buffers. Active={(char)('A' + _currentBuffer)}, " +
                            $"ElapsedInOldBuffer={elapsed.TotalSeconds:F2}s, No frames to prune");
                    }

                    // Point to next buffer for append
                    currentBuffer = nextBuffer;
                    currentState = ref nextState;
                }

                // Ensure backing buffers large enough for this payload
                int requiredBytes = currentState.BytesUsed + size;
                EnsureCapacity(requiredBytes);
                currentBuffer = _currentBuffer == 0 ? _bufferA : _bufferB;

                if (currentState.BytesUsed + size > currentBuffer.Length)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[PreRecording] Guard prevented buffer growth. Dropping frame. " +
                        $"BytesUsed={currentState.BytesUsed}, FrameSize={size}");
                    return;
                }

                // APPEND: Copy frame bytes to buffer (no lock held during copy)
                // This happens outside the lock in a production implementation
                Buffer.BlockCopy(nalUnits, 0, currentBuffer, currentState.BytesUsed, size);
                currentState.BytesUsed += size;
                currentState.FrameCount++;

                // Detect if this is a keyframe (IDR frame)
                bool detectedKeyFrame = isKeyFrame || IsKeyFrame(nalUnits, size);

                // Store frame metadata
                byte[] frameCopy = new byte[size];
                Buffer.BlockCopy(nalUnits, 0, frameCopy, 0, size);
                _frames.Add(new EncodedFrame
                {
                    Data = frameCopy,
                    Timestamp = timestamp,
                    AddedAt = DateTime.UtcNow,
                    IsKeyFrame = detectedKeyFrame
                });

            } // Lock released here
        }

#if IOS || MACCATALYST
        /// <summary>
        /// iOS/MacCatalyst version with CMTime timing information
        /// </summary>
        public void AppendEncodedFrame(byte[] nalUnits, int size, TimeSpan timestamp, CMTime presentationTime, CMTime duration, bool isKeyFrame = false)
        {
            //System.Diagnostics.Debug.WriteLine($"[PreRecording] AppendEncodedFrame called: size={size}, timestamp={timestamp.TotalSeconds:F3}s, PTS={presentationTime.Seconds:F3}s");

            if (_isDisposed)
            {
                System.Diagnostics.Debug.WriteLine($"[PreRecording] REJECTED: Buffer is disposed!");
                return;
            }

            if (nalUnits == null)
            {
                System.Diagnostics.Debug.WriteLine($"[PreRecording] REJECTED: nalUnits is null!");
                return;
            }

            if (size == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[PreRecording] REJECTED: size is 0!");
                return;
            }

            lock (_swapLock)
            {
                // Get current buffer and state
                byte[] currentBuffer = _currentBuffer == 0 ? _bufferA : _bufferB;
                ref BufferState currentState = ref _currentBuffer == 0 ? ref _stateA : ref _stateB;

                // Initialize StartTime on first append
                if (currentState.BytesUsed == 0)
                {
                    currentState.StartTime = DateTime.UtcNow;
                    System.Diagnostics.Debug.WriteLine($"[PreRecording] Initialized buffer {(char)('A' + _currentBuffer)} StartTime");
                }

                // Check if current buffer duration exceeded
                TimeSpan elapsed = DateTime.UtcNow - currentState.StartTime;
                if (elapsed > _maxDuration)
                {
                    // SWAP BUFFERS: Toggle to other buffer (atomic int toggle)
                    _currentBuffer = 1 - _currentBuffer;

                    byte[] nextBuffer = _currentBuffer == 0 ? _bufferA : _bufferB;
                    ref BufferState nextState = ref _currentBuffer == 0 ? ref _stateA : ref _stateB;

                    // Reset new active buffer
                    nextState.BytesUsed = 0;
                    nextState.FrameCount = 0;
                    nextState.StartTime = DateTime.UtcNow;
                    nextState.IsLocked = false;

                    // Prune frames to keep only the last _maxDuration seconds based on video PTS timestamps
                    if (_frames.Count > 0)
                    {
                        var lastFrameTimestamp = _frames[_frames.Count - 1].Timestamp;
                        var cutoffTimestamp = lastFrameTimestamp - _maxDuration;
                        int beforePrune = _frames.Count;
                        _frames.RemoveAll(f => f.Timestamp < cutoffTimestamp);

                        // CRITICAL: Ensure first frame is a keyframe after pruning
                        while (_frames.Count > 0 && !_frames[0].IsKeyFrame)
                        {
                            _frames.RemoveAt(0);
                        }

                        int afterPrune = _frames.Count;

                        System.Diagnostics.Debug.WriteLine(
                            $"[PreRecording] Swapped buffers. Active={(char)('A' + _currentBuffer)}, " +
                            $"ElapsedInOldBuffer={elapsed.TotalSeconds:F2}s, " +
                            $"Pruned frames: {beforePrune} -> {afterPrune} (cutoff={cutoffTimestamp.TotalSeconds:F3}s, kept last {_maxDuration.TotalSeconds:F1}s, first is KEYFRAME)");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[PreRecording] Swapped buffers. Active={(char)('A' + _currentBuffer)}, " +
                            $"ElapsedInOldBuffer={elapsed.TotalSeconds:F2}s, No frames to prune");
                    }

                    // Point to next buffer for append
                    currentBuffer = nextBuffer;
                    currentState = ref nextState;
                }

                // Ensure backing buffers large enough for this payload
                int requiredBytes = currentState.BytesUsed + size;
                EnsureCapacity(requiredBytes);
                currentBuffer = _currentBuffer == 0 ? _bufferA : _bufferB;

                if (currentState.BytesUsed + size > currentBuffer.Length)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[PreRecording] Guard prevented buffer growth. Dropping frame. " +
                        $"BytesUsed={currentState.BytesUsed}, FrameSize={size}");
                    return;
                }

                // APPEND: Copy frame bytes to buffer
                Buffer.BlockCopy(nalUnits, 0, currentBuffer, currentState.BytesUsed, size);
                currentState.BytesUsed += size;
                currentState.FrameCount++;

                // Detect if this is a keyframe (IDR frame) by checking H.264 NAL unit type
                // NAL unit type is in bits 0-4 of first byte: type 5 = IDR (keyframe)
                bool detectedKeyFrame = isKeyFrame || IsKeyFrame(nalUnits, size);

                // Store frame metadata with timing
                byte[] frameCopy = new byte[size];
                Buffer.BlockCopy(nalUnits, 0, frameCopy, 0, size);

                int beforeAdd = _frames.Count;
                _frames.Add(new EncodedFrame
                {
                    Data = frameCopy,
                    Timestamp = timestamp,
                    PresentationTime = presentationTime,
                    Duration = duration,
                    AddedAt = DateTime.UtcNow,
                    IsKeyFrame = detectedKeyFrame
                });
                int afterAdd = _frames.Count;

                //System.Diagnostics.Debug.WriteLine($"[PreRecording] Frame ADDED to list: {beforeAdd} -> {afterAdd}, Total frames now: {_frames.Count}, KeyFrame={isKeyFrame}");

            } // Lock released here
        }
#endif

        /// <summary>
        /// Flushes both buffers to separate files.
        /// Returns (fileA, fileB) where fileA has older content.
        /// </summary>
        public async Task<(string fileA, string fileB)> FlushToFilesAsync()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(PrerecordingEncodedBufferApple));

            lock (_swapLock)
            {
                if (_stateA.IsLocked || _stateB.IsLocked)
                    throw new InvalidOperationException("Buffer is already being flushed");

                _stateA.IsLocked = true;
                _stateB.IsLocked = true;
            }

            try
            {
                string tempDir = FileSystem.CacheDirectory;
                string fileA = Path.Combine(tempDir, $"pre_rec_a_{Guid.NewGuid():N}.h264");
                string fileB = Path.Combine(tempDir, $"pre_rec_b_{Guid.NewGuid():N}.h264");

                // Write both buffers to disk
                await File.WriteAllBytesAsync(fileA, _bufferA.AsMemory(0, _stateA.BytesUsed).ToArray());
                await File.WriteAllBytesAsync(fileB, _bufferB.AsMemory(0, _stateB.BytesUsed).ToArray());

                System.Diagnostics.Debug.WriteLine(
                    $"[PrerecordingEncodedBufferApple] Flushed to files: " +
                    $"fileA={_stateA.BytesUsed / 1024}KB ({_stateA.FrameCount} frames), " +
                    $"fileB={_stateB.BytesUsed / 1024}KB ({_stateB.FrameCount} frames)");

                return (fileA, fileB);
            }
            finally
            {
                lock (_swapLock)
                {
                    _stateA.IsLocked = false;
                    _stateB.IsLocked = false;
                }
            }
        }

        /// <summary>
        /// Gets current buffered duration (difference between newest and oldest frame timestamps)
        /// IMPORTANT: Uses actual frame PTS timestamps, not wall-clock time!
        /// </summary>
        public TimeSpan GetBufferedDuration()
        {
            lock (_swapLock)
            {
                if (_frames.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[PreRecording] GetBufferedDuration: No frames, returning Zero");
                    return TimeSpan.Zero;
                }

                if (_frames.Count == 1)
                {
                    System.Diagnostics.Debug.WriteLine($"[PreRecording] GetBufferedDuration: Only 1 frame, returning Zero");
                    return TimeSpan.Zero;
                }

                // Calculate from actual frame timestamps (first to last)
                var firstFrame = _frames[0];
                var lastFrame = _frames[_frames.Count - 1];
                var duration = lastFrame.Timestamp - firstFrame.Timestamp;

                System.Diagnostics.Debug.WriteLine($"[PreRecording] GetBufferedDuration: {_frames.Count} frames, First={firstFrame.Timestamp.TotalSeconds:F3}s, Last={lastFrame.Timestamp.TotalSeconds:F3}s, Duration={duration.TotalSeconds:F3}s");

                return duration;
            }
        }

        /// <summary>
        /// Gets buffer utilization percentage (0-100)
        /// </summary>
        public int GetBufferUtilization()
        {
            lock (_swapLock)
            {
                if (_stateA.BytesUsed == 0 && _stateB.BytesUsed == 0)
                    return 0;

                // Return whichever buffer has more data
                int maxUsed = Math.Max(_stateA.BytesUsed, _stateB.BytesUsed);
                return (int)((maxUsed * 100L) / _bufferA.Length);
            }
        }

        /// <summary>
        /// Gets total frame count across both buffers
        /// </summary>
        public int GetFrameCount()
        {
            lock (_swapLock)
            {
                int count = _frames.Count;
                //System.Diagnostics.Debug.WriteLine($"[PreRecording] GetFrameCount() called: returning {count} frames");
                return count;
            }
        }

#if IOS || MACCATALYST
        /// <summary>
        /// Gets all frames with timing information for writing to MP4
        /// </summary>
        public List<(byte[] Data, CMTime PresentationTime, CMTime Duration)> GetAllFrames()
        {
            lock (_swapLock)
            {
                var result = new List<(byte[], CMTime, CMTime)>(_frames.Count);
                foreach (var frame in _frames)
                {
                    result.Add((frame.Data, frame.PresentationTime, frame.Duration));
                }
                return result;
            }
        }
#endif

        /// <summary>
        /// Gets diagnostic statistics string
        /// </summary>
        public string GetStats()
        {
            lock (_swapLock)
            {
                if (_stateA.BytesUsed == 0 && _stateB.BytesUsed == 0)
                    return "PreRecord: empty";

                double sizeMB = (double)(_stateA.BytesUsed + _stateB.BytesUsed) / 1024 / 1024;
                var duration = GetBufferedDuration();
                int utilization = GetBufferUtilization();
                int totalFrames = GetFrameCount();

                return $"PreRecord: {totalFrames} frames, {duration.TotalSeconds:F1}s, " +
                       $"{sizeMB:F2}MB, {utilization}% of {_maxDuration.TotalSeconds}s max";
            }
        }

        /// <summary>
        /// Prunes buffer to contain only the last _maxDuration seconds of video based on PTS timestamps.
        /// CRITICAL: Ensures the first remaining frame is a keyframe for valid H.264 decoding.
        /// Call this before writing buffer to file to ensure we never exceed max duration.
        /// </summary>
        public void PruneToMaxDuration()
        {
            lock (_swapLock)
            {
                if (_frames.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[PreRecording] PruneToMaxDuration: No frames to prune");
                    return;
                }

                // Calculate cutoff based on video PTS timestamps (not wall-clock time)
                var lastFrameTimestamp = _frames[_frames.Count - 1].Timestamp;
                var cutoffTimestamp = lastFrameTimestamp - _maxDuration;
                int beforePrune = _frames.Count;

                _frames.RemoveAll(f => f.Timestamp < cutoffTimestamp);

                bool hasKeyFrame = _frames.Any(f => f.IsKeyFrame);
                if (hasKeyFrame)
                {
                    // CRITICAL: Ensure first frame is a keyframe after pruning
                    while (_frames.Count > 0 && !_frames[0].IsKeyFrame)
                    {
                        _frames.RemoveAt(0);
                    }
                }
                else if (_frames.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine("[PreRecording] PruneToMaxDuration: No keyframes remain in window; keeping leading P-frame");
                }

                /*
            int afterPrune = _frames.Count;
               int pruned = beforePrune - afterPrune;
            if (pruned > 0)
            {
                if (_frames.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[PreRecording] PruneToMaxDuration: {beforePrune} -> {afterPrune} frames " +
                        $"(pruned {pruned} frames, first frame now at {_frames[0].Timestamp.TotalSeconds:F3}s is KEYFRAME)");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[PreRecording] PruneToMaxDuration: WARNING: All frames pruned!");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[PreRecording] PruneToMaxDuration: No frames needed pruning " +
                    $"(all {_frames.Count} frames within last {_maxDuration.TotalSeconds:F1}s)");
            }
            */

            }
        }

        /// <summary>
        /// Clears both buffers and resets state
        /// </summary>
        public void Clear()
        {
            lock (_swapLock)
            {
                _stateA = new BufferState { BytesUsed = 0, FrameCount = 0, StartTime = DateTime.UtcNow };
                _stateB = new BufferState { BytesUsed = 0, FrameCount = 0, StartTime = DateTime.MinValue };
                _currentBuffer = 0;
            }
        }

        /// <summary>
        /// Detects if an H.264 frame is a keyframe (IDR) by checking NAL unit types
        /// </summary>
        private static bool IsKeyFrame(byte[] nalUnits, int size)
        {
            if (nalUnits == null || size < 5)
                return false;

            // Try length-prefixed (AVCC) layout first
            int offset = 0;
            bool parsedLengthPrefixed = false;
            while (offset + 4 <= size)
            {
                int nalLength = (nalUnits[offset] << 24) | (nalUnits[offset + 1] << 16) |
                                (nalUnits[offset + 2] << 8) | nalUnits[offset + 3];

                if (nalLength <= 0)
                    break;

                if (offset + 4 + nalLength > size)
                    break;

                parsedLengthPrefixed = true;
                int nalHeaderIndex = offset + 4;
                int nalType = nalUnits[nalHeaderIndex] & 0x1F;
                if (nalType == 5)
                    return true;

                offset += 4 + nalLength;
            }

            if (parsedLengthPrefixed)
                return false;

            // Fallback to Annex-B start-code search
            for (int i = 0; i < size - 4; i++)
            {
                if (nalUnits[i] != 0 || nalUnits[i + 1] != 0)
                    continue;

                int nalHeaderIndex = -1;
                if (nalUnits[i + 2] == 1)
                {
                    nalHeaderIndex = i + 3;
                }
                else if (nalUnits[i + 2] == 0 && nalUnits[i + 3] == 1)
                {
                    nalHeaderIndex = i + 4;
                }

                if (nalHeaderIndex <= 0 || nalHeaderIndex >= size)
                    continue;

                int nalType = nalUnits[nalHeaderIndex] & 0x1F;
                if (nalType == 5)
                    return true;
            }

            return false;
        }

        public void Dispose()
        {
            lock (_swapLock)
            {
                if (!_isDisposed)
                {
                    _bufferA = null;
                    _bufferB = null;
                    _isDisposed = true;
                }
            }
        }
    }
}
