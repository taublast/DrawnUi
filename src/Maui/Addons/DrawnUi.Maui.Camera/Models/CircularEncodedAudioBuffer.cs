using System;
using System.Collections.Generic;

namespace DrawnUi.Camera
{
    /// <summary>
    /// Thread-safe circular buffer for pre-encoded AAC audio frames.
    /// Stores encoded audio with timestamps already normalized to video timeline.
    /// At transition, frames can be written directly to muxer without re-encoding.
    /// </summary>
    public class CircularEncodedAudioBuffer
    {
        private readonly List<(byte[] Data, long TimestampUs, int Size)> _frames = new();
        private readonly object _lock = new();
        private readonly TimeSpan _maxDuration;

        public CircularEncodedAudioBuffer(TimeSpan maxDuration)
        {
            _maxDuration = maxDuration == TimeSpan.Zero ? TimeSpan.FromSeconds(5) : maxDuration;
        }

        /// <summary>
        /// Appends an encoded AAC frame. Timestamps should already be normalized to video timeline.
        /// </summary>
        public void AppendEncodedFrame(byte[] aacData, int size, long timestampUs)
        {
            if (aacData == null || size == 0) return;

            byte[] dataCopy = new byte[size];
            Buffer.BlockCopy(aacData, 0, dataCopy, 0, size);

            lock (_lock)
            {
                _frames.Add((dataCopy, timestampUs, size));
                PruneOldFrames();
            }
        }

        /// <summary>
        /// Gets all frames. Timestamps are already normalized - write directly to muxer.
        /// </summary>
        public List<(byte[] Data, long TimestampUs, int Size)> GetAllFrames()
        {
            lock (_lock)
            {
                return new List<(byte[], long, int)>(_frames);
            }
        }

        public int FrameCount
        {
            get { lock (_lock) { return _frames.Count; } }
        }

        public void Clear()
        {
            lock (_lock) { _frames.Clear(); }
        }

        private void PruneOldFrames()
        {
            if (_frames.Count < 2) return;

            long maxDurationUs = (long)_maxDuration.TotalMicroseconds;
            long newestTs = _frames[_frames.Count - 1].TimestampUs;
            long cutoff = newestTs - maxDurationUs;

            while (_frames.Count > 0 && _frames[0].TimestampUs < cutoff)
            {
                _frames.RemoveAt(0);
            }
        }
    }
}
