using DrawnUi.Camera;
using MusicNotes.UI;

namespace MusicNotes.Audio
{
    /// <summary>
    /// Metronome - Pulses at a set BPM and shows how well drummer matches the beat
    /// </summary>
    public class AudioMetronome : IAudioVisualizer, IDisposable
    {
        private int _resetting = 0;

        private const int BufferSize = 2048;
        private float[] _sampleBuffer = new float[BufferSize];
        private int _writePos = 0;
        private int _samplesAddedSinceLastScan = 0;
        private int _sampleRate = 44100;
        private const int ScanInterval = 512;

        // Metronome settings
        private float _targetBPM = 120;
        private long _lastMetronomeClick = 0;
        private float _metronomeFlash = 0;

        // Time base
        private long _clockMs = 0;
        private long _lastTimestampMs = 0;

        // Beat Detection
        private float _lastEnergy = 0;
        private long _lastUserBeat = 0;
        private float _beatFlash = 0;
        private float _timingError = 0; // in milliseconds, how far off beat
        private List<float> _timingErrors = new List<float>();
        private bool _hasSignal = false;

        // Adaptive noise floor
        private float _noiseFloor = 0.005f;

        // Render State
        private int _swapRequested = 0;
        private float _displayTimingError = 0;
        private float _displayAccuracy = 100;

        private SKPaint _paintTextLarge;
        private SKPaint _paintTextSmall;
        private SKPaint _paintMetronome;
        private SKPaint _paintUserBeat;
        private SKPaint _paintAccuracy;
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

                _lastMetronomeClick = 0;
                _metronomeFlash = 0;

                _clockMs = 0;
                _lastTimestampMs = 0;

                _lastEnergy = 0;
                _lastUserBeat = 0;
                _beatFlash = 0;
                _timingError = 0;
                _timingErrors?.Clear();
                _hasSignal = false;
                _noiseFloor = 0.005f;

                _swapRequested = 0;
                _displayTimingError = 0;
                _displayAccuracy = 100;
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _resetting, 0);
            }
        }

        private long AdvanceClock(AudioSample sample, int frames)
        {
            long tsMs = sample.TimestampNs > 0 ? (sample.TimestampNs / 1_000_000) : 0;

            if (tsMs > 0)
            {
                if (_lastTimestampMs == 0 || tsMs >= _lastTimestampMs)
                {
                    _lastTimestampMs = tsMs;
                    _clockMs = tsMs;
                    return _clockMs;
                }
            }

            if (frames > 0 && _sampleRate > 0)
            {
                _clockMs += (long)Math.Round(frames * 1000.0 / _sampleRate);
            }

            return _clockMs;
        }

        private static float ReadMonoSample(AudioSample sample, int frameIndex)
        {
            int channels = sample.Channels > 0 ? sample.Channels : 1;
            int bytesPerSample = sample.BytesPerSample;

            int offset = (frameIndex * channels) * bytesPerSample;
            if (offset < 0 || offset >= sample.Data.Length)
                return 0f;

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
            long nowMs = AdvanceClock(sample, frames);

            // Match AudioOscillograph gain so low-volume hits still register
            float gainMultiplier = UseGain ? 4.0f : 1.0f;

            for (int frame = 0; frame < frames; frame++)
            {
                float val = ReadMonoSample(sample, frame) * gainMultiplier;
                val = Math.Clamp(val, -1.0f, 1.0f);

                _sampleBuffer[_writePos] = val;
                _writePos = (_writePos + 1) % BufferSize;
            }

            _samplesAddedSinceLastScan += frames;

            if (_samplesAddedSinceLastScan >= ScanInterval)
            {
                UpdateMetronome(nowMs);
                DetectUserBeat(nowMs);
                _samplesAddedSinceLastScan = 0;
                System.Threading.Interlocked.Exchange(ref _swapRequested, 1);
            }
        }

        private void UpdateMetronome(long nowMs)
        {
            long beatInterval = (long)(60000f / _targetBPM);

            // Initialize metronome clock on first audio
            if (_lastMetronomeClick == 0)
                _lastMetronomeClick = nowMs;

            // Check if it's time for a metronome click
            if (nowMs - _lastMetronomeClick >= beatInterval)
            {
                _lastMetronomeClick = nowMs;
                _metronomeFlash = 1.0f;
            }

            // Fade metronome flash
            _metronomeFlash *= 0.85f;
        }

        private void DetectUserBeat(long nowMs)
        {
            // Calculate energy
            float energy = 0;
            int windowSize = 512;
            for (int i = 0; i < windowSize; i++)
            {
                int idx = (_writePos - windowSize + i + BufferSize) % BufferSize;
                float val = _sampleBuffer[idx];
                energy += val * val;
            }
            energy = (float)Math.Sqrt(energy / windowSize);

            _noiseFloor = _noiseFloor * 0.995f + energy * 0.005f;

            // Silence detection
            float silenceThreshold = Math.Max(UseGain ? 0.004f : 0.002f, _noiseFloor * 1.8f);
            if (energy < silenceThreshold)
            {
                _hasSignal = false;
                _beatFlash *= 0.85f;
                return;
            }
            _hasSignal = true;

            // Beat detection
            float minBeatEnergy = Math.Max(UseGain ? 0.02f : 0.015f, _noiseFloor * 3.0f);
            float threshold = Math.Max(minBeatEnergy, _lastEnergy * 1.35f);

            if (energy > threshold)
            {
                // Minimum time between beats
                if (nowMs - _lastUserBeat > 200)
                {
                    _lastUserBeat = nowMs;
                    _beatFlash = 1.0f;

                    // Calculate timing error (how far from nearest metronome beat)
                    long timeSinceLastClick = nowMs - _lastMetronomeClick;
                    long beatInterval = (long)(60000f / _targetBPM);
                    
                    // Check if closer to previous or next beat
                    float errorFromLast = timeSinceLastClick;
                    float errorFromNext = beatInterval - timeSinceLastClick;
                    
                    if (errorFromLast < errorFromNext)
                    {
                        _timingError = errorFromLast;
                    }
                    else
                    {
                        _timingError = -errorFromNext; // negative means early
                    }

                    // Track accuracy
                    _timingErrors.Add(Math.Abs(_timingError));
                    if (_timingErrors.Count > 20)
                        _timingErrors.RemoveAt(0);

                    // Calculate accuracy percentage
                    if (_timingErrors.Count > 0)
                    {
                        float avgError = _timingErrors.Average();
                        float maxError = beatInterval / 2f;
                        _displayAccuracy = Math.Max(0, 100 - (avgError / maxError * 100));
                    }
                }
            }

            _lastEnergy = _lastEnergy * 0.95f + energy * 0.05f;
            _beatFlash *= 0.85f;
        }

        public bool Render(SKCanvas canvas, SKRect viewport, float scale)
        {
            if (System.Threading.Interlocked.CompareExchange(ref _swapRequested, 0, 1) == 1)
            {
                _displayTimingError = _timingError;
            }

            // Initialize paints
            _paintTextLarge ??= new SKPaint
            {
                Color = SKColors.White,
                TextSize = 80 * scale,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center,
                Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
            };

            _paintTextSmall ??= new SKPaint
            {
                Color = SKColors.LightGray,
                TextSize = 28 * scale,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center
            };

            _paintMetronome ??= new SKPaint
            {
                Color = SKColors.Cyan,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };

            _paintUserBeat ??= new SKPaint
            {
                Color = SKColors.Orange,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };

            _paintAccuracy ??= new SKPaint
            {
                Color = SKColors.LimeGreen,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 3 * scale,
                IsAntialias = true
            };

            _paintWaveform ??= new SKPaint
            {
                Color = SKColors.Gray,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1 * scale,
                IsAntialias = true
            };

            float centerX = viewport.MidX;
            float centerY = viewport.MidY;

            // Clear background
            canvas.DrawRect(viewport, new SKPaint { Color = new SKColor(20, 20, 30) });

            // Draw BPM
            canvas.DrawText($"{_targetBPM:F0} BPM", centerX, viewport.Top + 50 * scale, _paintTextSmall);

            // Draw metronome click indicator (cyan circle)
            if (_metronomeFlash > 0.1f)
            {
                float metronomRadius = 60 * scale * _metronomeFlash;
                _paintMetronome.Color = new SKColor(0, 255, 255, (byte)(255 * _metronomeFlash));
                canvas.DrawCircle(centerX - 80 * scale, centerY - 40 * scale, metronomRadius, _paintMetronome);
            }

            // Draw user beat indicator (orange circle)
            if (_beatFlash > 0.1f)
            {
                float userRadius = 60 * scale * _beatFlash;
                _paintUserBeat.Color = new SKColor(255, 165, 0, (byte)(255 * _beatFlash));
                canvas.DrawCircle(centerX + 80 * scale, centerY - 40 * scale, userRadius, _paintUserBeat);
            }

            // Draw labels
            _paintTextSmall.TextSize = 20 * scale;
            canvas.DrawText("Metronome", centerX - 80 * scale, centerY + 40 * scale, _paintTextSmall);
            canvas.DrawText("Your Beat", centerX + 80 * scale, centerY + 40 * scale, _paintTextSmall);
            _paintTextSmall.TextSize = 28 * scale;

            // Draw timing error
            if (_timingErrors.Count > 0)
            {
                string errorText = $"{_displayTimingError:F0}ms";
                SKColor errorColor = Math.Abs(_displayTimingError) < 50 ? SKColors.LimeGreen : 
                                     Math.Abs(_displayTimingError) < 100 ? SKColors.Yellow : SKColors.Red;
                _paintTextLarge.Color = errorColor;
                canvas.DrawText(errorText, centerX, centerY + 100 * scale, _paintTextLarge);
                
                _paintTextSmall.Color = SKColors.LightGray;
                canvas.DrawText(_displayTimingError < 0 ? "Early" : "Late", centerX, centerY + 135 * scale, _paintTextSmall);
            }

            // Draw accuracy meter
            float accuracy = _displayAccuracy;
            canvas.DrawText($"Accuracy: {accuracy:F0}%", centerX, centerY + 180 * scale, _paintTextSmall);

            // Draw accuracy bar
            float barWidth = 200 * scale;
            float barHeight = 20 * scale;
            float barX = centerX - barWidth / 2;
            float barY = centerY + 200 * scale;
            
            // Background bar
            canvas.DrawRect(barX, barY, barWidth, barHeight, new SKPaint { Color = SKColors.DarkGray });
            
            // Accuracy bar
            float accuracyWidth = barWidth * (accuracy / 100f);
            SKColor accuracyColor = accuracy > 90 ? SKColors.LimeGreen :
                                   accuracy > 70 ? SKColors.Yellow : SKColors.Red;
            canvas.DrawRect(barX, barY, accuracyWidth, barHeight, new SKPaint { Color = accuracyColor });

            // Draw controls hint
            _paintTextSmall.Color = SKColors.Gray;
            _paintTextSmall.TextSize = 16 * scale;
            canvas.DrawText("Tap in time with the metronome", centerX, viewport.Bottom - 20 * scale, _paintTextSmall);
            return false;
        }

        public void SetBPM(float bpm)
        {
            _targetBPM = Math.Clamp(bpm, 40, 240);
        }

        public void Dispose()
        {
            _paintTextLarge?.Dispose();
            _paintTextSmall?.Dispose();
            _paintMetronome?.Dispose();
            _paintUserBeat?.Dispose();
            _paintAccuracy?.Dispose();
            _paintWaveform?.Dispose();
        }
    }
}
