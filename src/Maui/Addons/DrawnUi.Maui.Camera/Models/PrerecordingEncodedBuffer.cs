using System.Collections.Generic;
using System;
using System.Diagnostics;

namespace DrawnUi.Camera;

/// <summary>
/// Thread-safe memory buffer for storing pre-recorded encoded video frames.
/// Implements circular buffer with time-based rotation to enforce duration limits.
/// Stores actual encoded bytes (H.264/H.265) rather than uncompressed bitmaps for memory efficiency.
/// 
/// Memory efficiency example:
/// - Encoded H.264: ~75 KB per frame (5 sec @ 30fps = ~11.25 MB)
/// - Uncompressed 1080p bitmap: ~8.3 MB per frame (same 5 sec = 1.245 GB!)
/// 
/// This achieves ~100:1 compression ratio vs storing SKBitmaps.
/// </summary>
public class PrerecordingEncodedBuffer : IDisposable
{
    private class EncodedFrame
    {
        /// <summary>Encoded frame data (H.264/H.265 bytes)</summary>
        public byte[] Data { get; set; }
        
        /// <summary>Timestamp when frame was added</summary>
        public DateTime Timestamp { get; set; }
    }

    private readonly Queue<EncodedFrame> _frameQueue = new();
    private readonly object _lock = new();
    private TimeSpan _maxDuration;
    private long _totalBytes = 0;
    private bool _isDisposed;

    /// <summary>Current number of buffered frames</summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _frameQueue.Count;
            }
        }
    }

    /// <summary>Total memory used by buffered frames (bytes)</summary>
    public long SizeBytes
    {
        get
        {
            lock (_lock)
            {
                return _totalBytes;
            }
        }
    }

    /// <summary>
    /// Initializes the buffer with a maximum duration.
    /// Older frames are automatically pruned when duration is exceeded.
    /// </summary>
    /// <param name="maxDuration">Maximum time to keep in buffer (e.g., 5 seconds)</param>
    public PrerecordingEncodedBuffer(TimeSpan maxDuration = default)
    {
        _maxDuration = maxDuration == default ? TimeSpan.FromSeconds(5) : maxDuration;
    }

    public PrerecordingEncodedBuffer()
    {
        _maxDuration = TimeSpan.FromSeconds(5); // Default 5 second pre-recording buffer
    }

    /// <summary>
    /// Adds an encoded frame to the buffer, automatically rotating old frames if duration exceeded
    /// </summary>
    /// <param name="frameData">Encoded frame bytes (H.264/H.265)</param>
    public void AddFrame(byte[] frameData)
    {
        if (frameData == null || frameData.Length == 0)
            return;

        lock (_lock)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(PrerecordingEncodedBuffer));

            _frameQueue.Enqueue(new EncodedFrame
            {
                Data = frameData,
                Timestamp = DateTime.UtcNow
            });
            _totalBytes += frameData.Length;

            // Rotate out old frames if we've exceeded max duration
            PruneExpiredFrames();
        }
    }

    /// <summary>
    /// Appends encoded video data from an encoder's pre-recording buffer (legacy compatibility).
    /// </summary>
    public void AppendEncodedData(byte[] data, int offset, int length)
    {
        if (data == null || length == 0)
            return;

        byte[] frameData = new byte[length];
        Array.Copy(data, offset, frameData, 0, length);
        AddFrame(frameData);
    }

    /// <summary>
    /// Removes frames older than max duration
    /// </summary>
    private void PruneExpiredFrames()
    {
        DateTime expirationTime = DateTime.UtcNow - _maxDuration;
        
        while (_frameQueue.Count > 0)
        {
            EncodedFrame oldestFrame = _frameQueue.Peek();
            if (oldestFrame.Timestamp < expirationTime)
            {
                _frameQueue.Dequeue();
                _totalBytes -= oldestFrame.Data.Length;
            }
            else
            {
                break;
            }
        }
    }

    /// <summary>
    /// Writes all buffered encoded data to a file stream.
    /// </summary>
    public async Task WriteToFileAsync(FileStream fileStream)
    {
        lock (_lock)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(PrerecordingEncodedBuffer));

            if (_frameQueue.Count == 0)
                return;

            foreach (var frame in _frameQueue)
            {
                fileStream.Write(frame.Data, 0, frame.Data.Length);
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets a copy of the buffered data as a byte array.
    /// </summary>
    public byte[] GetBufferedData()
    {
        lock (_lock)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(PrerecordingEncodedBuffer));

            if (_frameQueue.Count == 0)
                return Array.Empty<byte>();

            // Concatenate all frame data
            using (var ms = new MemoryStream((int)_totalBytes))
            {
                foreach (var frame in _frameQueue)
                {
                    ms.Write(frame.Data, 0, frame.Data.Length);
                }
                return ms.ToArray();
            }
        }
    }

    /// <summary>
    /// Clears all buffered frames.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _frameQueue.Clear();
            _totalBytes = 0;
        }
    }

    /// <summary>
    /// Gets current buffer statistics for debugging
    /// </summary>
    public string GetStats()
    {
        lock (_lock)
        {
            int count = _frameQueue.Count;
            if (count == 0)
                return "Buffer: empty";

            DateTime oldest = _frameQueue.First().Timestamp;
            DateTime newest = _frameQueue.Last().Timestamp;
            TimeSpan duration = newest - oldest;
            double percentFull = (duration.TotalSeconds / _maxDuration.TotalSeconds) * 100;
            double sizeMB = _totalBytes / 1024.0 / 1024.0;
            
            return $"PreRecord: {count} frames, {duration.TotalSeconds:F1}s, {sizeMB:F2}MB, {percentFull:F0}% of {_maxDuration.TotalSeconds}s max";
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (!_isDisposed)
            {
                _frameQueue.Clear();
                _totalBytes = 0;
                _isDisposed = true;
            }
        }
    }
}
