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
        private const int ScanInterval = 1024; // Reverted to 1024 (23ms) for better transient detection

        // Energy tracking for onset detection
        private List<float> _energyHistory = new List<float>();
        private const int MaxEnergyHistory = 800; // Adjusted for 1024 sample window (was 1600)

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
            if (frames > 0 && _sampleRate > 0)
            {
                // Rely purely on sample rate for clock advance to ensure smooth timebase for autocorrelation
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

        // Multi-band filter state
        private float _lowPassState = 0;
        private float _highPassState = 0;
        private float _prevVal = 0;
        
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
                
                // Circular buffer write
                _sampleBuffer[_writePos] = val;
                _writePos = (_writePos + 1) % BufferSize;
                
                _samplesAddedSinceLastScan++;

                if (_samplesAddedSinceLastScan >= ScanInterval)
                {
                    // Update linear energy history (lightweight)
                    ComputeEnergyFrame();
                    
                    // Run heavy detection less frequently (every ~90ms), but use full history timeline
                    // 1024 samples @ 44.1k = 23.2ms. 4 frames = ~92ms.
                    if (_framesSinceLastDetect++ >= 4) 
                    {
                        DetectMusicBPM();
                        _framesSinceLastDetect = 0;
                        System.Threading.Interlocked.Exchange(ref _swapRequested, 1);
                    }
                    
                    _samplesAddedSinceLastScan = 0;
                }
            }
        }
        
        private int _framesSinceLastDetect = 0;

        private void ComputeEnergyFrame()
        {
            // improved multi-band energy calculation
            float energyLow = 0;
            float energyMidHigh = 0;

            int windowSize = ScanInterval;
            
            // Cutoff frequency for kick detection (approx 150Hz)
            float alphaLow = 0.05f; 

            // Read backward from writePos
            for (int i = 0; i < windowSize; i++)
            {
                int idx = (_writePos - windowSize + i + BufferSize) % BufferSize;
                float val = _sampleBuffer[idx];
                
                // Low-Pass Filter (Bass/Kick)
                _lowPassState = _lowPassState + alphaLow * (val - _lowPassState);
                energyLow += _lowPassState * _lowPassState;

                // Simple High-Pass / Transient detection
                float diff = val - _prevVal;
                _prevVal = val;
                energyMidHigh += diff * diff;
            }
            
            energyLow = (float)Math.Sqrt(energyLow / windowSize);
            energyMidHigh = (float)Math.Sqrt(energyMidHigh / windowSize);

            float energy = energyLow + energyMidHigh; 

            // Safety clamp
            if (_noiseFloor < 0.0001f) _noiseFloor = 0.0001f;
            _noiseFloor = _noiseFloor * 0.999f + energy * 0.001f;

            float silenceThreshold = Math.Max(UseGain ? 0.0005f : 0.0002f, _noiseFloor * 0.8f);
            
            if (energy < silenceThreshold)
            {
                energy = 0;
                if (_hasSignal) { _hasSignal = false; }
            }
            else
            {
                _hasSignal = true;
            }

            // Add strictly linear point to history
            _energyHistory.Add(energy);
            if (_energyHistory.Count > MaxEnergyHistory)
                _energyHistory.RemoveAt(0);
        }

        private void DetectMusicBPM()
        {
            // Need enough history for BPM detection (at least 3 seconds / ~130 frames for 1024-sample window)
            if (_energyHistory.Count < 130) return;

            // 2. Onset Detection Function (ODF)
            List<float> onsets = new List<float>();
            int onsetCount = 0;
            float maxOnset = 0;
            
            // Dynamic window for local average: 6 frames (~140ms at 1024 samples/44.1k)
            int avgWindow = 6;

            for (int i = avgWindow; i < _energyHistory.Count; i++)
            {
                // Compare to local average to reduce noise
                float sum = 0;
                for (int w = 1; w <= avgWindow; w++) 
                    sum += _energyHistory[i - w];
                float localAvg = sum / avgWindow;

                float diff = _energyHistory[i] - localAvg;

                // For sparse signals (metronome) where localAvg is near zero, 
                // tiny noise fluctuations (0.0001 -> 0.0002) look like 100% increases.
                // We enforce a minimum absolute energy jump to count as an onset.
                if (diff < 0.002f) diff = 0; 

                float val = Math.Max(0, diff);
                onsets.Add(val);

                if (val > 0.005f) // Significant onset
                {
                    onsetCount++;
                    if (val > maxOnset) maxOnset = val;
                }
            }
            
            // Check for sparsity (Metronome case)
            int sparseLag = 0;
            // Relaxed check: if we have some significant peaks, try interval mode finding
            // This now runs even if onsetCount is moderately high, as long as peaks are distinct
            bool hasPeaks = (maxOnset > 0.01f); 
            
            if (hasPeaks)
            {
                // Simple Peak-to-Peak interval estimator
                List<int> peakIndices = new List<int>();
                
                // Use wider local max window (+/- 2) because of higher resolution (512 samples)
                for(int i=2; i<onsets.Count-2; i++)
                {
                    if (onsets[i] > (maxOnset * 0.35f) 
                        && onsets[i] >= onsets[i-1] && onsets[i] >= onsets[i-2]
                        && onsets[i] > onsets[i+1] && onsets[i] > onsets[i+2])
                    {
                        peakIndices.Add(i);
                        // Skip a bit to avoid double counting
                        i += 2;
                    }
                }
                
                if (peakIndices.Count >= 3)
                {
                    List<int> intervals = new List<int>();
                    for(int k=0; k<peakIndices.Count-1; k++)
                    {
                        int delta = peakIndices[k+1] - peakIndices[k];
                        // Ignore extremely short intervals (jitter/double triggers)
                        // At 11ms per frame, 8 frames = 88ms ~ 680 BPM. Safe limit.
                        if (delta > 8) intervals.Add(delta);
                    }

                    // Find most common interval (Mode) with tolerance +/- 1 frame
                    var bestGroup = intervals
                        .GroupBy(x => x)
                        .OrderByDescending(g => g.Count())
                        .FirstOrDefault();

                    if (bestGroup != null && bestGroup.Count() >= 2)
                    {
                        sparseLag = bestGroup.Key;
                    }
                }
            }

            // Autocorrelation to find periodicity
            // Expanded range to support 40-220 BPM (standard music range)
            int minBPM = 40;
            int maxBPM = 220;
            // Critical: timePerFrame is the time between energy samples (ScanInterval)
            // Use precise floating point time derived from sample rate
            float timePerFrame = (float)ScanInterval / _sampleRate; // ~0.023s at 44.1k
            
            // Calculate lag range in frames
            // Standard lag calculation: 60 / BPM / timePerFrame
            // We want to detect half-tempo (e.g. 100 for 200) carefully
            int minLag = (int)(60f / maxBPM / timePerFrame); 
            int maxLag = (int)(60f / minBPM / timePerFrame); 
            
            // Allow checking up to half the history buffer
            maxLag = Math.Min(_energyHistory.Count / 2, maxLag);

            if (minLag >= maxLag)
                return;

            // Find best lag using autocorrelation
            float maxCorrelation = 0;
            int bestLag = 0;
            Dictionary<int, float> lagCorrelations = new Dictionary<int, float>();

            // Use autocorrelation over the onset curve
            for (int lag = minLag; lag <= maxLag; lag++)
            {
                float correlation = 0;
                float normA = 0;
                float normB = 0;
                int count = 0;

                // Loop through history
                for (int i = 0; i < onsets.Count - lag; i++)
                {
                    float valA = onsets[i];
                    float valB = onsets[i + lag];
                    
                    correlation += valA * valB;
                    normA += valA * valA;
                    normB += valB * valB;
                    count++;
                }

                if (count > 0 && normA > 0 && normB > 0)
                {
                    correlation = correlation / (float)Math.Sqrt(normA * normB);
                }
                
                // Weighting to prefer standard tempos (90-140)
                float lagTime = lag * timePerFrame;
                float candidateBPM = 60f / lagTime;

                // Priority Boost for Sparse Signals (Metronome)
                // If the onset detector found a clear recurring interval, force the autocorrelation to respect it.
                if (sparseLag > 0)
                {
                    // Check if this lag aligns with the detected sparse interval
                    // Allow tiny jitter (1 frame)
                    if (Math.Abs(lag - sparseLag) <= 1) correlation += 0.4f;
                    
                    // Also check for 2x tempo (half lag) in case sparse detector found the half-note
                    if (Math.Abs(lag - (sparseLag / 2)) <= 1) correlation += 0.3f;
                    
                    // Also check for half-tempo (double lag)
                    if (Math.Abs(lag - (sparseLag * 2)) <= 1) correlation += 0.2f;
                }

                // Log-Normal preference window (standard in MIREX)
                // Peak at 120 BPM, sigma = ~1 octave
                // We flatten the curve to avoid penalizing valid tempos like 150 BPM too much
                // Was sigma 0.5f, now 1.4f (much flatter)
                float logBPM = (float)Math.Log(candidateBPM / 120f); 
                float tempoWeight = (float)Math.Exp(-0.5f * Math.Pow(logBPM / 1.4f, 2));
                
                // Don't apply weight if correlation is high enough on its own
                if (correlation > 0.6f) tempoWeight = 1.0f; else correlation *= tempoWeight;

                lagCorrelations[lag] = correlation;

                if (correlation > maxCorrelation)
                {
                    maxCorrelation = correlation;
                    bestLag = lag;
                }
            }

            // HARMONIC CHECK: Is the best lag actually a multiple (2x, 3x) of the true beat?
            // If we found 54 BPM (Lag ~ 48), check if 108 BPM (Lag ~ 24) or 162 BPM (Lag ~ 16) has a peak.
            // Often the "measure" (low BPM) has the highest raw correlation, but the "beat" (high BPM) is what we want.

            // Only perform harmonic promotion if we suspect a slow tempo (< 95 BPM) or if the correlation is weak (< 0.5)
            // 75 BPM is often half-time for 150 BPM
            if (bestLag > minLag * 1.5f) 
            {
                // Check 2x tempo (Half the lag)
                int lag2x = bestLag / 2;
                
                // Search wide window around lag2x for a local peak
                float peak2x = 0;
                int bestLag2x = 0;
                
                for (int l = lag2x - 2; l <= lag2x + 2; l++)
                {
                    if (lagCorrelations.TryGetValue(l, out float val) && val > peak2x)
                    {
                        peak2x = val;
                        bestLag2x = l;
                    }
                }

                // If 2x tempo (faster) has significant correlation (> 40% of best), prefer it
                // We are biased towards ~120-150 BPM range, so we aggressively promote
                if (peak2x > (maxCorrelation * 0.40f))
                {
                    bestLag = bestLag2x;
                    maxCorrelation = peak2x;
                }
            }

            if (bestLag > 0 && maxCorrelation > 0.15f) // Minimum correlation threshold
            {
                // Parabolic interpolation for sub-frame accuracy
                float fractionalLag = bestLag;
                if (bestLag > minLag && bestLag < maxLag)
                {
                     if (lagCorrelations.TryGetValue(bestLag - 1, out float yLeft) && 
                         lagCorrelations.TryGetValue(bestLag + 1, out float yRight))
                     {
                         float yCenter = maxCorrelation;
                         // Parabolic peak fit: offset = (left - right) / (2 * (left - 2*center + right))
                         // Note: denominator is negative for a peak
                         float denominator = (yLeft - 2 * yCenter + yRight);
                         if (Math.Abs(denominator) > 0.0001f)
                         {
                             float offset = (yLeft - yRight) / (2 * denominator);
                             fractionalLag = bestLag + offset;
                         }
                     }
                }

                // Convert lag to BPM
                float periodInSeconds = fractionalLag * timePerFrame;
                float detectedBPM = 60f / periodInSeconds;

                // Clamp to reasonable range
                detectedBPM = Math.Clamp(detectedBPM, minBPM, maxBPM);
                
                // --- APPLIED FIX: Instant Jump on drastic change ---
                if (_confidence > 0.5f)
                {
                    // If locked, allow slow transition
                    // But if drastic change, break lock
                    if (Math.Abs(detectedBPM - _currentBPM) > (_currentBPM * 0.25f))
                    {
                         // Tempo jump detected, reduce confidence to allow retargeting
                        _confidence *= 0.5f; 
                    }
                }
                else
                {
                    // Low confidence: accept new BPM faster
                     // If difference is huge, jump instantly
                    if (_currentBPM > 0 && Math.Abs(detectedBPM - _currentBPM) > (_currentBPM * 0.35f))
                    {
                         _currentBPM = detectedBPM; // Instant jump
                         _bpmHistory.Clear();       // Clear old history
                         _confidence = 0.2f;        // Reset confidence
                    }
                }

                _currentBPM = _currentBPM * 0.7f + detectedBPM * 0.3f; // Smooth update

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
            //if (!_hasSignal)
            //{
            //    _paintTextSmall.Color = SKColors.Gray;
            //    _paintTextSmall.TextSize = 18 * scale;
            //    canvas.DrawText("Waiting for music...", centerX, viewport.Bottom - 20 * scale, _paintTextSmall);
            //}
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
