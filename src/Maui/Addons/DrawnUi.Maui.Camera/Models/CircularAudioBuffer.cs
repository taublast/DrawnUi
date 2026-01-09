using System;
using System.Collections.Generic;
using System.Linq;

namespace DrawnUi.Camera
{
    /// <summary>
    /// Thread-safe audio buffer for audio samples.
    /// When maxDuration > 0: Circular mode - automatically trims old samples beyond MaxDuration.
    /// When maxDuration = 0: Linear mode - keeps ALL samples (for full session recording).
    /// </summary>
    public class CircularAudioBuffer
    {
        private readonly Queue<AudioSample> _samples = new();
        private readonly object _lock = new();
        private readonly TimeSpan _maxDuration;
        private readonly bool _isLinearMode;

        /// <summary>
        /// Creates an audio buffer.
        /// </summary>
        /// <param name="maxDuration">Maximum duration to keep. Use TimeSpan.Zero for linear mode (keeps all samples).</param>
        public CircularAudioBuffer(TimeSpan maxDuration)
        {
            _maxDuration = maxDuration;
            _isLinearMode = maxDuration <= TimeSpan.Zero;
        }

        /// <summary>
        /// Creates a linear audio buffer that keeps ALL samples (no trimming).
        /// Use this for full session recording where all audio needs to be preserved.
        /// </summary>
        public static CircularAudioBuffer CreateLinear() => new CircularAudioBuffer(TimeSpan.Zero);

        public void Write(AudioSample sample)
        {
            lock (_lock)
            {
                _samples.Enqueue(sample);
                Trim();
            }
        }

        public AudioSample[] GetAllSamples()
        {
            lock (_lock)
            {
                return _samples.ToArray();
            }
        }

        public AudioSample[] DrainFrom(long cutPointNs)
        {
            lock (_lock)
            {
                // Find samples that started at or after cutPointNs
                // Or maybe just samples that contain data after cutPointNs?
                // Simplest strategy: Take all samples where TimestampNs >= cutPointNs.
                // Or maybe include the one just before if it overlaps?
                // Audio frames are short (~23ms), strict cut is fine for now.
                
                var result = _samples.Where(s => s.TimestampNs >= cutPointNs).ToArray();
                _samples.Clear();
                return result;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _samples.Clear();
            }
        }

        public TimeSpan BufferedDuration
        {
            get
            {
                lock (_lock)
                {
                    if (_samples.Count == 0) return TimeSpan.Zero;
                    var first = _samples.Peek();
                    var last = _samples.Last();
                    return TimeSpan.FromTicks((last.TimestampNs - first.TimestampNs) / 100);
                }
            }
        }

        public int SampleCount
        {
            get
            {
                lock (_lock)
                {
                    return _samples.Count;
                }
            }
        }

        private void Trim()
        {
            // In linear mode, never trim - keep ALL samples
            if (_isLinearMode) return;

            if (_samples.Count == 0) return;

            var last = _samples.Last();
            var thresholdNs = last.TimestampNs - (_maxDuration.Ticks * 100);

            while (_samples.Count > 0)
            {
                var first = _samples.Peek();
                if (first.TimestampNs < thresholdNs)
                {
                    _samples.Dequeue();
                }
                else
                {
                    break;
                }
            }
        }
        
        /// <summary>
        /// Finds the timestamp of the audio sample that is closest to the given video timestamp.
        /// </summary>
        public long GetSampleTimestampClosestTo(long videoKeyframeNs)
        {
            lock (_lock)
            {
                if (_samples.Count == 0) return 0;

                long closestDiff = long.MaxValue;
                long closestTimestamp = 0;

                foreach (var sample in _samples)
                {
                    long diff = Math.Abs(sample.TimestampNs - videoKeyframeNs);
                    if (diff < closestDiff)
                    {
                        closestDiff = diff;
                        closestTimestamp = sample.TimestampNs;
                    }
                }
                
                // If we didn't find a close match within reason (e.g. empty buffer logic handled above), return closest.
                // If the buffer is totally desynced, this might return something far off, but that is detected elsewhere.
                return closestTimestamp;
            }
        }
    }
}
