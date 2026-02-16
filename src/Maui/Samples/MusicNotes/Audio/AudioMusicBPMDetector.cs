using DrawnUi.Camera;
using MusicNotes.UI;

namespace MusicNotes.Audio
{
    /// <summary>
    /// Music BPM Detector - Detects BPM from music (not just drums)
    /// Uses autocorrelation for tempo detection in musical tracks
    /// </summary>
    public class AudioMusicBPMDetector : IAudioVisualizer, IDisposable
    {
        private const int BufferSize = 8192; // Larger buffer for music analysis
        private float[] _sampleBuffer = new float[BufferSize];
        private int _writePos = 0;
        private int _samplesAddedSinceLastScan = 0;
        private int _sampleRate = 44100;
        private const int ScanInterval = 2205; // Scan every 50ms (44100/20) for accurate timing

        // Energy tracking for onset detection
        private List<float> _energyHistory = new List<float>();
        private const int MaxEnergyHistory = 200; // 200 * 50ms = 10 seconds of history

        // BPM Detection
        private float _currentBPM = 0;
        private List<float> _bpmHistory = new List<float>();
        private bool _hasSignal = false;
        private float _confidence = 0;
        private float _lockedBPM = 0; // Once confident, lock to this BPM

        // Time base
        private long _clockMs = 0;
        private long _lastTimestampMs = 0;

        // Adaptive noise floor
        private float _noiseFloor = 0.005f;

        // Render State
        private float _displayBPM = 0;
        private float _displayConfidence = 0;
        private int _swapRequested = 0;
        private List<float> _displayEnergyWave = new List<float>();

        private SKPaint _paintTextLarge;
        private SKPaint _paintTextSmall;
        private SKPaint _paintEnergyWave;
        private SKPaint _paintConfidence;
        private SKPaint _paintWaveform;

        public bool UseGain { get; set; } = true;
        public int Skin { get; set; } = 0;

        public void Reset()
        {
            Array.Clear(_sampleBuffer, 0, _sampleBuffer.Length);
            _writePos = 0;
            _samplesAddedSinceLastScan = 0;
            _sampleRate = 44100;

            _energyHistory?.Clear();
            _bpmHistory?.Clear();
            _currentBPM = 0;
            _lockedBPM = 0;
            _confidence = 0;
            _hasSignal = false;

            _clockMs = 0;
            _lastTimestampMs = 0;
            _noiseFloor = 0.005f;

            _displayBPM = 0;
            _displayConfidence = 0;
            _displayEnergyWave?.Clear();
            _swapRequested = 0;
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
            if (sample.SampleRate > 0)
                _sampleRate = sample.SampleRate;

            int frames = sample.SampleCount;
            _ = AdvanceClock(sample, frames);

            // Match AudioOscillograph gain so low-volume music still registers
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
                DetectMusicBPM();
                _samplesAddedSinceLastScan = 0;
                System.Threading.Interlocked.Exchange(ref _swapRequested, 1);
            }
        }

        private void DetectMusicBPM()
        {
            // Calculate energy (RMS)
            float energy = 0;
            int windowSize = 1024;
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
                return;
            }
            _hasSignal = true;

            // Track energy over time
            _energyHistory.Add(energy);
            if (_energyHistory.Count > MaxEnergyHistory)
                _energyHistory.RemoveAt(0);

            // Need enough history for BPM detection (at least 4 seconds)
            if (_energyHistory.Count < 80)
                return;

            // Calculate energy differences (onset detection) with smoothing
            List<float> onsets = new List<float>();
            for (int i = 3; i < _energyHistory.Count; i++)
            {
                // Compare to local average to reduce noise
                float localAvg = (_energyHistory[i - 3] + _energyHistory[i - 2] + _energyHistory[i - 1]) / 3f;
                float diff = _energyHistory[i] - localAvg;
                onsets.Add(Math.Max(0, diff)); // Only positive differences
            }

            // Autocorrelation to find periodicity
            int minBPM = 60;
            int maxBPM = 200;
            // Critical: timePerFrame is the time between energy samples
            float timePerFrame = ScanInterval / (float)_sampleRate; // Should be 0.05 seconds (50ms)
            
            int minLag = (int)(60f / maxBPM / timePerFrame); // For 200 BPM: 60/200/0.05 = 6 frames
            int maxLag = (int)(60f / minBPM / timePerFrame); // For 60 BPM: 60/60/0.05 = 20 frames
            
            minLag = Math.Max(3, minLag);
            maxLag = Math.Min(onsets.Count / 2, maxLag);

            if (minLag >= maxLag)
                return;

            // Find best lag using autocorrelation
            float maxCorrelation = 0;
            int bestLag = 0;

            for (int lag = minLag; lag <= maxLag; lag++)
            {
                float correlation = 0;
                int count = 0;

                for (int i = 0; i < onsets.Count - lag; i++)
                {
                    correlation += onsets[i] * onsets[i + lag];
                    count++;
                }

                if (count > 0)
                    correlation /= count;

                if (correlation > maxCorrelation)
                {
                    maxCorrelation = correlation;
                    bestLag = lag;
                }
            }

            if (bestLag > 0 && maxCorrelation > 0)
            {
                // Convert lag to BPM
                float periodInSeconds = bestLag * timePerFrame;
                float detectedBPM = 60f / periodInSeconds;

                // Clamp to reasonable range
                detectedBPM = Math.Clamp(detectedBPM, minBPM, maxBPM);

                _currentBPM = detectedBPM;

                // Track BPM history for stability  
                _bpmHistory.Add(_currentBPM);
                if (_bpmHistory.Count > 30)
                    _bpmHistory.RemoveAt(0);

                // Calculate confidence based on correlation strength and consistency
                float avgEnergy = _energyHistory.Average();
                float correlationConfidence = 0;
                if (avgEnergy > 0)
                    correlationConfidence = Math.Min(1.0f, maxCorrelation / (avgEnergy * avgEnergy) * 3f);

                // Check BPM stability
                float stabilityConfidence = 1.0f;
                if (_bpmHistory.Count >= 10)
                {
                    float avgBPM = _bpmHistory.Skip(_bpmHistory.Count - 10).Average();
                    float variance = _bpmHistory.Skip(_bpmHistory.Count - 10).Select(b => Math.Abs(b - avgBPM)).Average();
                    stabilityConfidence = Math.Max(0, 1.0f - variance / 15f);
                }

                _confidence = (correlationConfidence * 0.3f + stabilityConfidence * 0.7f);

                // Lock BPM when confidence is high and stable
                if (_confidence > 0.75f && _bpmHistory.Count >= 20)
                {
                    _lockedBPM = _bpmHistory.Skip(_bpmHistory.Count - 15).Average();
                }
            }

            // Update display with smoothed BPM
            if (_bpmHistory.Count > 8)
            {
                // Use weighted average favoring recent values
                var recentBPMs = _bpmHistory.Skip(_bpmHistory.Count - 8).ToList();
                _displayBPM = recentBPMs.Average();
                _displayConfidence = _confidence * 100;
            }
        }

        public void Render(SKCanvas canvas, SKRect viewport, float scale)
        {
            if (System.Threading.Interlocked.CompareExchange(ref _swapRequested, 0, 1) == 1)
            {
                _displayEnergyWave = new List<float>(_energyHistory);
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

            _paintEnergyWave ??= new SKPaint
            {
                Color = SKColors.Magenta,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2 * scale,
                IsAntialias = true
            };

            _paintConfidence ??= new SKPaint
            {
                Color = SKColors.LimeGreen,
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

            // Draw title
            _paintTextSmall.TextSize = 24 * scale;
            canvas.DrawText("MUSIC BPM", centerX, viewport.Top + 40 * scale, _paintTextSmall);
            _paintTextSmall.TextSize = 32 * scale;

            // Draw BPM display (keep last known; gray when no signal)
            if (_displayBPM > 0)
            {
                _paintTextLarge.Color = _hasSignal ? SKColors.White : SKColors.Gray;
                string bpmText = $"{_displayBPM:F1}";
                canvas.DrawText(bpmText, centerX, centerY - 20 * scale, _paintTextLarge);
                canvas.DrawText("BPM", centerX, centerY + 60 * scale, _paintTextSmall);

                // Draw confidence
                if (_displayConfidence > 20)
                {
                    _paintTextSmall.TextSize = 20 * scale;
                    SKColor confidenceColor = _displayConfidence > 70 ? SKColors.LimeGreen :
                                             _displayConfidence > 40 ? SKColors.Yellow : SKColors.Orange;
                    _paintTextSmall.Color = confidenceColor;
                    canvas.DrawText($"Confidence: {_displayConfidence:F0}%", centerX, centerY + 95 * scale, _paintTextSmall);
                    
                    // Debug: show history count
                    _paintTextSmall.TextSize = 16 * scale;
                    _paintTextSmall.Color = SKColors.Gray;
                    canvas.DrawText($"History: {_energyHistory.Count} | BPMs tracked: {_bpmHistory.Count}", centerX, centerY + 115 * scale, _paintTextSmall);
                    
                    _paintTextSmall.Color = SKColors.LightGray;
                    _paintTextSmall.TextSize = 32 * scale;
                }
            }
            else
            {
                _paintTextSmall.TextSize = 24 * scale;
                canvas.DrawText("Play music to detect tempo", centerX, centerY, _paintTextSmall);
                _paintTextSmall.TextSize = 32 * scale;
            }

            // Draw energy waveform
            if (_displayEnergyWave.Count > 1)
            {
                float waveHeight = 60 * scale;
                float waveY = centerY + 140 * scale;
                float step = viewport.Width / Math.Max(1, _displayEnergyWave.Count);
                float maxEnergy = _displayEnergyWave.Max();
                
                if (maxEnergy > 0)
                {
                    using (var path = new SKPath())
                    {
                        bool first = true;
                        for (int i = 0; i < _displayEnergyWave.Count; i++)
                        {
                            float val = _displayEnergyWave[i] / maxEnergy;
                            float x = viewport.Left + i * step;
                            float y = waveY - val * waveHeight;

                            if (first)
                            {
                                path.MoveTo(x, y);
                                first = false;
                            }
                            else
                            {
                                path.LineTo(x, y);
                            }
                        }
                        canvas.DrawPath(path, _paintEnergyWave);
                    }
                }
            }

            // Draw waveform at bottom
            DrawWaveform(canvas, viewport, scale);

            // Draw status
            if (!_hasSignal)
            {
                _paintTextSmall.Color = SKColors.Gray;
                _paintTextSmall.TextSize = 18 * scale;
                canvas.DrawText("Waiting for music...", centerX, viewport.Bottom - 20 * scale, _paintTextSmall);
            }
        }

        private void DrawWaveform(SKCanvas canvas, SKRect viewport, float scale)
        {
            float waveHeight = 40 * scale;
            float waveY = viewport.Bottom - 60 * scale;
            int samples = Math.Min(150, BufferSize / 4);
            float step = viewport.Width / samples;

            using (var path = new SKPath())
            {
                bool first = true;
                for (int i = 0; i < samples; i++)
                {
                    int idx = (_writePos - samples + i + BufferSize) % BufferSize;
                    float val = _sampleBuffer[idx];
                    float x = viewport.Left + i * step;
                    float y = waveY + val * waveHeight * 0.5f;

                    if (first)
                    {
                        path.MoveTo(x, y);
                        first = false;
                    }
                    else
                    {
                        path.LineTo(x, y);
                    }
                }
                canvas.DrawPath(path, _paintWaveform);
            }
        }

        public void Dispose()
        {
            _paintTextLarge?.Dispose();
            _paintTextSmall?.Dispose();
            _paintEnergyWave?.Dispose();
            _paintConfidence?.Dispose();
            _paintWaveform?.Dispose();
        }
    }
}
