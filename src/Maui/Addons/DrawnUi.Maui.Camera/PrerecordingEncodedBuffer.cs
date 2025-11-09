using System.Collections.Generic;
using System;

namespace DrawnUi.Camera;

/// <summary>
/// Manages pre-recorded encoded video data.
/// During pre-recording, frames are encoded and stored as raw bytes.
/// When transitioning to file recording, these bytes are prepended to the output file.
/// </summary>
public class PrerecordingEncodedBuffer
{
    private MemoryStream _encodedData;
    private readonly object _lock = new();
    private bool _isDisposed;

    public long SizeBytes
    {
        get
        {
            lock (_lock)
            {
                return _encodedData?.Length ?? 0;
            }
        }
    }

    public PrerecordingEncodedBuffer()
    {
        _encodedData = new MemoryStream(1024 * 1024); // Start with 1MB
    }

    /// <summary>
    /// Appends encoded video data from an encoder's pre-recording buffer.
    /// </summary>
    public void AppendEncodedData(byte[] data, int offset, int length)
    {
        lock (_lock)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(PrerecordingEncodedBuffer));

            if (_encodedData == null)
                _encodedData = new MemoryStream();

            _encodedData.Write(data, offset, length);
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

            if (_encodedData == null || _encodedData.Length == 0)
                return;

            _encodedData.Seek(0, SeekOrigin.Begin);
            _encodedData.CopyTo(fileStream);
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

            return _encodedData?.ToArray() ?? Array.Empty<byte>();
        }
    }

    /// <summary>
    /// Clears all buffered data.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _encodedData?.Dispose();
            _encodedData = new MemoryStream(1024 * 1024);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (!_isDisposed)
            {
                _encodedData?.Dispose();
                _encodedData = null;
                _isDisposed = true;
            }
        }
    }
}
