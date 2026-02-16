using AppoMobi.Specials;
using DrawnUi.Camera;
using MusicNotes.UI;

namespace MusicNotes.Audio
{
    /// <summary>
    /// Rhythm Detector (BPM/Tempo Detector for drummers)
    /// Detects beats and calculates BPM using onset detection
    /// </summary>
    public class AudioRhythmDetector : IAudioVisualizer, IDisposable
    {
        private int _resetting = 0;

        private const int BufferSize = 2048;
        private float[] _sampleBuffer = new float[BufferSize];
        private int _writePos = 0;
        private int _samplesAddedSinceLastScan = 0;
        private int _sampleRate = 44100;
        private const int ScanInterval = 512;

        // Beat Detection
        private List<long> _beatTimestamps = new List<long>();
        private float _currentBPM = 0;
        private bool _hasSignal = false;
        private const int MaxBeatsToTrack = 8;
        private long _lastBeatTime = 0;

        // Time base (ms). Uses AudioSample.TimestampNs when sane, otherwise sample-count clock.
        private long _clockMs = 0;
        private long _lastTimestampMs = 0;
        private long _lastNonSilentMs = 0;

        // If we reset too quickly on "silence", 60 BPM metronome clicks (1000ms apart)
        // will never accumulate 2 beats. Keep this comfortably above 1 beat at 60 BPM.
        private const long SustainedSilenceResetMs = 2200;

        // Peak tracking for accurate beat timestamping
        private float _pendingPeakAbs = 0;
        private long _pendingPeakTsMs = 0;

        // Onset detection (fast/slow energy) - works under background audio
        private float _energyFast = 0;
        private float _energySlow = 0;

        // Adaptive noise floor (very slow rise, faster fall)
        private float _noiseFloor = 0.0025f;

        // Smoothing
        private float _smoothBPM = 0;
        private const float BPMSmoothFactor = 0.3f; // Increased from 0.2 for faster response

        // UX: keep last known BPM visible (rendered in gray when stale)
        private float _lastKnownBPM = 0;

        // Render State
        private float _displayBPM = 0;
        private int _swapRequested = 0;
        private float _beatFlash = 0; // For visual beat indicator

        private SKPaint _paintTextLarge;
        private SKPaint _paintTextSmall;
        private SKPaint _paintBeatIndicator;
        private SKPaint _paintWaveform;

        public bool UseGain { get; set; } = true;
        public int Skin { get; set; } = 0;

        public void Reset()
        {
            if (System.Threading.Interlocked.Exchange(ref _resetting, 1) != 0)
                return;

            try
            {
                Array.Clear(_sampleBuffer, 0, _sampleBuffer.Length);
                _writePos = 0;
                _samplesAddedSinceLastScan = 0;
                _sampleRate = 44100;

                _smoothBPM = 0;
                _displayBPM = 0;
                _lastKnownBPM = 0;
                _swapRequested = 0;
                _beatFlash = 0;
                _energyFast = 0;
                _energySlow = 0;
                _lastKnownBPM = 0;
                _pendingPeakAbs = 0;
                _pendingPeakTsMs = 0;
                _clockMs = 0;
                _lastTimestampMs = 0;
                _lastNonSilentMs = 0;

                _hasSignal = false;
                _beatTimestamps.Clear();
                _currentBPM = 0;

                _lastBeatTime = 0;
                _pendingPeakAbs = 0;
                _pendingPeakTsMs = 0;
                _energyFast = 0;
                _energySlow = 0;
                _noiseFloor = 0.0025f;
                _lastNonSilentMs = 0;
                _clockMs = 0;
                _lastTimestampMs = 0;
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _resetting, 0);
            }
        }

        private long AdvanceClock(AudioSample sample, int frames, out long packetStartMs)
        {
            packetStartMs = _clockMs;

            double durationMs = 0;
            if (frames > 0 && _sampleRate > 0)
                durationMs = frames * 1000.0 / _sampleRate;

            long tsMs = sample.TimestampNs > 0 ? (sample.TimestampNs / 1_000_000) : 0;
            bool tsOk = tsMs > 0 && (_lastTimestampMs == 0 || tsMs >= _lastTimestampMs);

            if (tsOk)
            {
                _lastTimestampMs = tsMs;
                packetStartMs = tsMs;
                _clockMs = tsMs + (long)Math.Round(durationMs);
                return _clockMs;
            }

            // Fallback: advance by sample count (works even if TimestampNs is 0)
            _clockMs += (long)Math.Round(durationMs);
            return _clockMs;
        }

        private static float ReadMonoSample(AudioSample sample, int frameIndex)
        {
            int channels = sample.Channels > 0 ? sample.Channels : 1;
            int bytesPerSample = sample.BytesPerSample;

            int offset = (frameIndex * channels) * bytesPerSample;
            if (offset < 0 || offset >= sample.Data.Length)
                return 0f;

            // Common cases: 16-bit PCM or 32-bit float
            if (bytesPerSample == 2)
            {
                if (offset + 1 >= sample.Data.Length)
                    return 0f;
                short pcm = (short)(sample.Data[offset] | (sample.Data[offset + 1] << 8));
                return pcm / 32768f;
            }

            if (bytesPerSample == 4 && sample.BitDepth == AudioBitDepth.Float32Bit)
            {
                if (offset + 3 >= sample.Data.Length)
                    return 0f;
                return BitConverter.ToSingle(sample.Data, offset);
            }

            return 0f;
        }

        public void AddSample(AudioSample sample)
        {
            if (System.Threading.Volatile.Read(ref _resetting) != 0)
                return;

            if (sample.SampleRate > 0)
                _sampleRate = sample.SampleRate;

            int frames = sample.SampleCount;
            long nowMs = AdvanceClock(sample, frames, out long packetStartMs);

            // Match AudioOscillograph gain so low-volume beats still register
            float gainMultiplier = UseGain ? 4.0f : 1.0f;

            float localPeakAbs = 0;
            int localPeakFrame = 0;

            for (int frame = 0; frame < frames; frame++)
            {
                float val = ReadMonoSample(sample, frame) * gainMultiplier;
                val = Math.Clamp(val, -1.0f, 1.0f);

                float abs = Math.Abs(val);
                if (abs > localPeakAbs)
                {
                    localPeakAbs = abs;
                    localPeakFrame = frame;
                }

                _sampleBuffer[_writePos] = val;
                _writePos = (_writePos + 1) % BufferSize;
            }

            // Track best peak timestamp across the whole scan interval
            if (localPeakAbs > _pendingPeakAbs)
            {
                _pendingPeakAbs = localPeakAbs;

                if (packetStartMs > 0 && _sampleRate > 0)
                {
                    long peakOffsetMs = (long)Math.Round(localPeakFrame * 1000.0 / _sampleRate);
                    _pendingPeakTsMs = packetStartMs + peakOffsetMs;
                }
                else
                {
                    // Fallback: use scan end time if we don't have a sane start timestamp
                    _pendingPeakTsMs = nowMs;
                }
            }

            _samplesAddedSinceLastScan += frames;

            if (_samplesAddedSinceLastScan >= ScanInterval)
            {
                DetectBeat(nowMs);
                _samplesAddedSinceLastScan = 0;
                System.Threading.Interlocked.Exchange(ref _swapRequested, 1);

                // Reset peak for next scan window
                _pendingPeakAbs = 0;
                _pendingPeakTsMs = 0;
            }
        }

        private void DetectBeat(long nowMs)
        {
            // Calculate energy (RMS) over the entire scan interval to avoid missing beats
            float energy = 0;
            // Use the full scan interval as window size, or at least cover the gap
            int windowSize = ScanInterval; 
            for (int i = 0; i < windowSize; i++)
            {
                int idx = (_writePos - windowSize + i + BufferSize) % BufferSize;
                float val = _sampleBuffer[idx];
                energy += val * val;
            }
            energy = (float)Math.Sqrt(energy / windowSize);

            // Update fast/slow energy envelopes
            if (_energyFast == 0) _energyFast = energy;
            if (_energySlow == 0) _energySlow = energy;

            // Fast tracks transients, slow tracks background
            _energyFast = _energyFast * 0.80f + energy * 0.20f;
            _energySlow = _energySlow * 0.98f + energy * 0.02f;

            // Noise floor: slow rise, faster fall
            if (energy < _noiseFloor)
            {
                _noiseFloor = _noiseFloor * 0.90f + energy * 0.10f;
            }
            else
            {
                _noiseFloor = _noiseFloor * 0.9995f + energy * 0.0005f;
            }

            // Silence detection
            // Use a lower threshold for detecting *signal presence* vs. noise floor to avoid dropping quiet beats
            float silenceThreshold = Math.Max(UseGain ? 0.0010f : 0.0005f, _noiseFloor * 1.5f);
            if (energy < silenceThreshold)
            {
                _hasSignal = false;
                _beatFlash *= 0.85f;

                if (_lastNonSilentMs == 0)
                    _lastNonSilentMs = nowMs;

                // After sustained silence, clear live detection state but keep last known BPM for UX.
                if (nowMs - _lastNonSilentMs > SustainedSilenceResetMs)
                {
                    ResetAfterSilence();
                }

                return;
            }
            _hasSignal = true;
            _lastNonSilentMs = nowMs;

            // Reset if we've had no beats for a while (tempo change / noise burst)
            if (_beatTimestamps.Count > 0 && (nowMs - _lastBeatTime) > 2500)
            {
                _beatTimestamps.Clear();
                if (_smoothBPM > 0)
                    _lastKnownBPM = _smoothBPM;
                _smoothBPM = 0;
                _currentBPM = 0;
            }

            // Onset detection: transient above background (works under ads/music)
            float onset = Math.Max(0, _energyFast - _energySlow);
            float relOnset = onset / Math.Max(1e-6f, _energySlow);

            float minPeakAbs = Math.Max(UseGain ? 0.02f : 0.015f, _noiseFloor * 6.0f);
            bool hasPeak = _pendingPeakAbs >= minPeakAbs;

            // For sparse signals like metronome, rely more on peak detection if onset is weak
            if ((relOnset > 0.35f || (hasPeak && relOnset > 0.15f)) && hasPeak)
            {
                long beatTs = _pendingPeakTsMs > 0 ? _pendingPeakTsMs : nowMs;

                // Minimum time between beats (avoid double detection)
                if (beatTs - _lastBeatTime > 180)
                {
                    // If the detected interval is wildly different from current tempo, relock quickly
                    if (_beatTimestamps.Count > 0)
                    {
                        long prev = _beatTimestamps[^1];
                        long intervalMs = beatTs - prev;
                        if (intervalMs < 250 || intervalMs > 2000)
                        {
                            // Ignore obviously wrong intervals
                            return;
                        }

                        if (_smoothBPM > 0)
                        {
                            float candidate = 60000f / intervalMs;
                            if (Math.Abs(candidate - _smoothBPM) > (_smoothBPM * 0.35f) && _beatTimestamps.Count >= 2)
                            {
                                _beatTimestamps.Clear();
                                _beatTimestamps.Add(prev);
                            }
                        }
                    }

                    _beatTimestamps.Add(beatTs);
                    _lastBeatTime = beatTs;
                    _beatFlash = 1.0f; // Trigger beat flash

                    // Keep only recent beats
                    if (_beatTimestamps.Count > MaxBeatsToTrack)
                    {
                        _beatTimestamps.RemoveAt(0);
                    }

                    // Calculate BPM from intervals
                    if (_beatTimestamps.Count >= 2)
                    {
                        CalculateBPM();
                    }
                }
            }

            // Fade beat flash
            _beatFlash *= 0.85f;
        }

        private void ResetAfterSilence()
        {
            if (_smoothBPM > 0)
                _lastKnownBPM = _smoothBPM;

            _hasSignal = false;
            _beatTimestamps.Clear();
            _currentBPM = 0;
            _smoothBPM = 0;
            _lastBeatTime = 0;
            _pendingPeakAbs = 0;
            _pendingPeakTsMs = 0;
            _energyFast = 0;
            _energySlow = 0;
            _noiseFloor = 0.0025f;
            // keep clock and last timestamp
            _lastNonSilentMs = 0;
        }

        private void CalculateBPM()
        {
            if (_beatTimestamps.Count < 2)
                return;

            // Calculate intervals and use median to reject outliers
            List<float> intervals = new List<float>();
            for (int i = 1; i < _beatTimestamps.Count; i++)
            {
                intervals.Add(_beatTimestamps[i] - _beatTimestamps[i - 1]);
            }

            // Use median interval for better accuracy
            intervals.Sort();
            float medianInterval = intervals[intervals.Count / 2];

            // Convert to BPM
            _currentBPM = 60000f / medianInterval; // 60000 ms in a minute

            // Clamp to reasonable range
            _currentBPM = Math.Clamp(_currentBPM, 40, 240);

            // Smooth BPM with higher factor for stability
            if (_smoothBPM == 0)
                _smoothBPM = _currentBPM;
            else
                _smoothBPM = _smoothBPM * (1 - BPMSmoothFactor) + _currentBPM * BPMSmoothFactor;

            if (_smoothBPM > 0)
                _lastKnownBPM = _smoothBPM;
        }

        public void Render(SKCanvas canvas, SKRect viewport, float scale)
        {

            if (System.Threading.Interlocked.CompareExchange(ref _swapRequested, 0, 1) == 1)
            {
                _displayBPM = _smoothBPM > 0 ? _smoothBPM : _lastKnownBPM;
            }

            // Initialize paints
            _paintTextLarge ??= new SKPaint
            {
                Color = SKColors.White,
                TextSize = 120 * scale,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center,
                Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
            };

            _paintTextSmall ??= new SKPaint
            {
                Color = SKColors.LightGray,
                TextSize = 32 * scale,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center
            };

            _paintBeatIndicator ??= new SKPaint
            {
                Color = SKColors.Red,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };

            _paintWaveform ??= new SKPaint
            {
                Color = SKColors.Cyan,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2 * scale,
                IsAntialias = true
            };

            float centerX = viewport.MidX;
            float centerY = viewport.MidY;

            // Clear background
            canvas.DrawRect(viewport, new SKPaint { Color = new SKColor(20, 20, 30) });

            // Draw BPM display
            if (_displayBPM > 0)
            {
                bool isLive = _hasSignal && _smoothBPM > 0 && _beatTimestamps.Count >= 2 && (_clockMs - _lastBeatTime) < 1400;
                _paintTextLarge.Color = isLive ? SKColors.White : SKColors.Gray;

                string bpmText = $"{_displayBPM:F1}";
                canvas.DrawText(bpmText, centerX, centerY - 20 * scale, _paintTextLarge);
                canvas.DrawText("BPM", centerX, centerY + 60 * scale, _paintTextSmall);

                // Debug: show beat count
                _paintTextSmall.TextSize = 16 * scale;
                canvas.DrawText($"Beats: {_beatTimestamps.Count}", centerX, centerY + 85 * scale, _paintTextSmall);
                _paintTextSmall.TextSize = 32 * scale;
            }
            else
            {
                canvas.DrawText("Listening..", centerX, centerY, _paintTextSmall);
            }

            // Draw beat indicator (pulsing circle)
            if (_beatFlash > 0.1f)
            {
                float radius = 40 * scale * _beatFlash;
                _paintBeatIndicator.Color = new SKColor(255, 0, 0, (byte)(255 * _beatFlash));
                canvas.DrawCircle(centerX, centerY + 120 * scale, radius, _paintBeatIndicator);
            }

            // Draw waveform at bottom
            DrawWaveform(canvas, viewport, scale);

            // Draw status
            //if (!_hasSignal)
            //{
            //    var prev = _paintTextSmall.Color;
            //    _paintTextSmall.Color = SKColors.Gray;
            //    canvas.DrawText("Waiting for sound...", centerX, viewport.Bottom - 20 * scale, _paintTextSmall);
            //    _paintTextSmall.Color = prev;
            //}
        }

        private void DrawWaveform(SKCanvas canvas, SKRect viewport, float scale)
        {
            float waveHeight = 60 * scale;
            float waveY = viewport.Bottom - waveHeight - 60 * scale;

            // Draw a min/max envelope over a longer time window so transient clicks are visible.
            // BufferSize=2048 at 44.1kHz is ~46ms, so we use the full buffer and render it as an envelope.
            int columns = Math.Max(60, (int)Math.Min(240, viewport.Width / (3 * scale)));
            if (columns <= 0)
                return;

            int windowSamples = BufferSize;
            int start = (_writePos - windowSamples + BufferSize) % BufferSize;
            int samplesPerColumn = Math.Max(1, windowSamples / columns);
            float stepX = viewport.Width / columns;

            // If the waveform amplitude is tiny, scale it up a bit so it doesn't look flat.
            // (This does not affect detection, only visualization.)
            float visGain = 1.0f;
            if (_noiseFloor > 0)
                visGain = Math.Clamp(0.02f / _noiseFloor, 1.0f, 8.0f);

            for (int c = 0; c < columns; c++)
            {
                float min = 1f;
                float max = -1f;
                int baseIdx = (start + c * samplesPerColumn) % BufferSize;

                for (int s = 0; s < samplesPerColumn; s++)
                {
                    int idx = (baseIdx + s) % BufferSize;
                    float v = Math.Clamp(_sampleBuffer[idx] * visGain, -1f, 1f);
                    if (v < min) min = v;
                    if (v > max) max = v;
                }

                float x = viewport.Left + c * stepX;
                float y1 = waveY + min * waveHeight * 0.5f;
                float y2 = waveY + max * waveHeight * 0.5f;
                canvas.DrawLine(x, y1, x, y2, _paintWaveform);
            }
        }

        public void Dispose()
        {
            _paintTextLarge?.Dispose();
            _paintTextSmall?.Dispose();
            _paintBeatIndicator?.Dispose();
            _paintWaveform?.Dispose();
        }
    }
}
