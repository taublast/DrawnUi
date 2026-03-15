using System;
using System.Collections.Generic;

namespace DrawnUi.Camera
{
    /// <summary>
    /// Thread-safe audio buffer for audio samples.
    /// When maxDuration > 0: Circular mode - automatically trims old samples beyond MaxDuration.
    /// When maxDuration = 0: Linear mode - keeps ALL samples (for full session recording).
    /// Backed by List for O(log n) binary-search timestamp queries.
    /// </summary>
    public class CircularAudioBuffer
    {
        private readonly List<AudioSample> _samples = new();
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
                _samples.Add(sample);
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

        /// <summary>
        /// Returns all samples with TimestampNs strictly after the given timestamp.
        /// Uses binary search — O(log n) find, O(k) copy.
        /// </summary>
        public AudioSample[] GetSamplesAfter(long timestampNs)
        {
            lock (_lock)
            {
                if (_samples.Count == 0)
                    return Array.Empty<AudioSample>();

                if (timestampNs == long.MinValue)
                    return _samples.ToArray();

                int idx = FindFirstIndexAfter(timestampNs);
                return CopyFromIndex(idx);
            }
        }

        /// <summary>
        /// Returns all samples with TimestampNs in [startTimestampNs, endTimestampNs].
        /// Uses binary search — O(log n) find, O(k) copy.
        /// </summary>
        public AudioSample[] GetSamplesInRange(long startTimestampNs, long endTimestampNs)
        {
            lock (_lock)
            {
                if (_samples.Count == 0)
                    return Array.Empty<AudioSample>();

                int startIdx = FindFirstIndexAtOrAfter(startTimestampNs);
                int endIdx = FindFirstIndexAfter(endTimestampNs); // exclusive upper bound
                if (startIdx >= endIdx)
                    return Array.Empty<AudioSample>();

                int count = endIdx - startIdx;
                var result = new AudioSample[count];
                _samples.CopyTo(startIdx, result, 0, count);
                return result;
            }
        }

        /// <summary>
        /// Returns the trailing samples covering at most the given duration from the latest sample.
        /// Uses binary search — O(log n) find, O(k) copy.
        /// </summary>
        public AudioSample[] GetTrailingSamples(TimeSpan duration)
        {
            lock (_lock)
            {
                if (_samples.Count == 0)
                    return Array.Empty<AudioSample>();

                if (duration <= TimeSpan.Zero)
                    return _samples.ToArray();

                long durationNs = duration.Ticks * 100;
                long lastTimestampNs = _samples[_samples.Count - 1].TimestampNs;
                long thresholdNs = Math.Max(0, lastTimestampNs - durationNs);

                int idx = FindFirstIndexAtOrAfter(thresholdNs);
                return CopyFromIndex(idx);
            }
        }

        public AudioSample[] DrainFrom(long cutPointNs)
        {
            lock (_lock)
            {
                int idx = FindFirstIndexAtOrAfter(cutPointNs);
                var result = CopyFromIndex(idx);
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
                    var first = _samples[0];
                    var last = _samples[_samples.Count - 1];
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
            if (_isLinearMode) return;
            if (_samples.Count == 0) return;

            long thresholdNs = _samples[_samples.Count - 1].TimestampNs - (_maxDuration.Ticks * 100);
            int firstKeep = FindFirstIndexAtOrAfter(thresholdNs);
            if (firstKeep > 0)
                _samples.RemoveRange(0, firstKeep);
        }

        // Binary search: first index where TimestampNs > timestampNs
        private int FindFirstIndexAfter(long timestampNs)
        {
            int lo = 0, hi = _samples.Count - 1, result = _samples.Count;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                if (_samples[mid].TimestampNs > timestampNs)
                {
                    result = mid;
                    hi = mid - 1;
                }
                else
                {
                    lo = mid + 1;
                }
            }
            return result;
        }

        // Binary search: first index where TimestampNs >= timestampNs
        private int FindFirstIndexAtOrAfter(long timestampNs)
        {
            int lo = 0, hi = _samples.Count - 1, result = _samples.Count;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                if (_samples[mid].TimestampNs >= timestampNs)
                {
                    result = mid;
                    hi = mid - 1;
                }
                else
                {
                    lo = mid + 1;
                }
            }
            return result;
        }

        private AudioSample[] CopyFromIndex(int idx)
        {
            int count = _samples.Count - idx;
            if (count <= 0) return Array.Empty<AudioSample>();
            var result = new AudioSample[count];
            _samples.CopyTo(idx, result, 0, count);
            return result;
        }

        /// <summary>
        /// Finds the timestamp of the audio sample that is closest to the given video timestamp.
        /// </summary>
        public long GetSampleTimestampClosestTo(long videoKeyframeNs)
        {
            lock (_lock)
            {
                if (_samples.Count == 0) return 0;

                // Binary search to find the nearest insertion point, then check neighbours
                int idx = FindFirstIndexAtOrAfter(videoKeyframeNs);

                long bestTs = 0;
                long bestDiff = long.MaxValue;

                for (int i = Math.Max(0, idx - 1); i <= Math.Min(_samples.Count - 1, idx); i++)
                {
                    long diff = Math.Abs(_samples[i].TimestampNs - videoKeyframeNs);
                    if (diff < bestDiff)
                    {
                        bestDiff = diff;
                        bestTs = _samples[i].TimestampNs;
                    }
                }

                return bestTs;
            }
        }
    }
}
