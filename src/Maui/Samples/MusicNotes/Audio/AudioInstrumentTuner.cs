using DrawnUi.Camera;
using MusicNotes.UI;

namespace MusicNotes.Audio
{
    /// <summary>
    /// Musical Note Detector (Tuner)
    /// Uses AMDF (Average Magnitude Difference Function) with Parabolic Interpolation for accurate pitch.
    /// </summary>
    public class AudioInstrumentTuner : IAudioVisualizer, IDisposable
    {
        private const int BufferSize = 2048; // ~46ms at 44.1kHz
        private float[] _sampleBuffer = new float[BufferSize];
        private float[] _frame = new float[BufferSize];         // Pre-allocated: avoids per-scan GC
        private float[] _amdfBuffer = new float[BufferSize];    // Pre-allocated: avoids per-scan GC
        private int _writePos = 0;
        private int _samplesAddedSinceLastScan = 0;
        private int _sampleRate = 44100;
        private const int ScanInterval = 256; // ~5.8ms at 44.1kHz

        static string placeholder = "  ";

        // Detection State
        private string _currentNote = placeholder;
        private string _currentNoteSolf = placeholder;
        private int _currentMidiNote = 0;
        private float _currentFrequency = 0;
        private float _currentCents = 0;
        private bool _hasSignal = false;

        // Note Stability (Debouncing)
        private int _potentialMidiNote = 0;
        private int _noteStabilityCounter = 0;
        private System.Collections.Generic.List<float> _centsBuffer = new System.Collections.Generic.List<float>();

        // Smoothing for UI
        private float _smoothCents = 0;

        // Dynamic Staff Centering
        private int _staffReferenceMidi = 71; // B4 default, will adjust to played notes
        private int _silenceFrameCount = 0;
        private const int SilenceThreshold = 10; // ~10 scans of silence before allowing big jumps

        // Render State
        private string _displayNote = placeholder;
        private string _displayNoteSolf = placeholder;
        private int _displayMidiNote = 0;
        private float _displayFrequency = 0;
        private float _displayCents = 0;
        private SKColor _displayColor = SKColors.Gray;
        private int _displayStaffReferenceMidi = 71;
        private bool _displayHasSignal = false;
        private int _swapRequested = 0;

        private SKPaint _paintTextLarge;
        private SKPaint _paintTextSmall;
        private SKPaint _paintGauge;
        private SKPaint _paintNeedle;

        private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        private static readonly string[] SolfegeNames = { "Do", "Do#", "Re", "Re#", "Mi", "Fa", "Fa#", "Sol", "Sol#", "La", "La#", "Si" };

        public bool UseGain { get; set; } = true;
        public int Skin { get; set; } = 0;

        public void Reset()
        {
            Array.Clear(_sampleBuffer, 0, _sampleBuffer.Length);
            _writePos = 0;
            _samplesAddedSinceLastScan = 0;
            _sampleRate = 44100;

            _currentNote = placeholder;
            _currentNoteSolf = placeholder;
            _currentMidiNote = 0;
            _currentFrequency = 0;
            _currentCents = 0;
            _hasSignal = false;

            _potentialMidiNote = 0;
            _noteStabilityCounter = 0;
            _centsBuffer?.Clear();
            _smoothCents = 0;

            _staffReferenceMidi = 71;
            _silenceFrameCount = 0;

            _displayNote = placeholder;
            _displayNoteSolf = placeholder;
            _displayMidiNote = 0;
            _displayFrequency = 0;
            _displayCents = 0;
            _displayColor = SKColors.Gray;
            _displayStaffReferenceMidi = 71;
            _displayHasSignal = false;
            _swapRequested = 0;
        }

        public void AddSample(AudioSample sample)
        {
            if (sample.SampleRate > 0)
                _sampleRate = sample.SampleRate;

            // Handle channels (use first channel only)
            int channels = sample.Channels > 0 ? sample.Channels : 1;
            int step = channels;
            int sampleCount = sample.Data.Length / 2;

            float gainMultiplier = UseGain ? 3.0f : 1.0f;

            for (int i = 0; i < sampleCount; i += step)
            {
                int byteIndex = i * 2;
                if (byteIndex + 1 < sample.Data.Length)
                {
                    short pcm = (short)(sample.Data[byteIndex] | (sample.Data[byteIndex + 1] << 8));
                    float val = (pcm / 32768f) * gainMultiplier;
                    val = Math.Clamp(val, -1.0f, 1.0f); // Prevent clipping

                    _sampleBuffer[_writePos] = val;
                    _writePos = (_writePos + 1) % BufferSize;
                }
            }

            _samplesAddedSinceLastScan += (sampleCount / channels);

            if (_samplesAddedSinceLastScan >= ScanInterval)
            {
                DetectPitch();
                _samplesAddedSinceLastScan = 0;
                System.Threading.Interlocked.Exchange(ref _swapRequested, 1);
            }
        }

        private void DetectPitch()
        {
            // Unroll buffer for analysis (Older -> Newer) — reuse pre-allocated field
            var frame = _frame;
            int head = _writePos;
            for (int i = 0; i < BufferSize; i++)
            {
                int idx = (head - BufferSize + i + BufferSize) % BufferSize;
                frame[i] = _sampleBuffer[idx];
            }

            // 1. RMS Check (Silence detection) - Check ONLY recent data
            float rms = 0;
            int rmsWindow = 1024; // Check last ~23ms
            for (int i = BufferSize - rmsWindow; i < BufferSize; i++) rms += frame[i] * frame[i];
            rms = (float)Math.Sqrt(rms / rmsWindow);

            // Adjust silence threshold based on gain setting
            float silenceThreshold = UseGain ? 0.02f : 0.01f;
            
            if (rms < silenceThreshold) // Silence Threshold
            {
                _hasSignal = false;
                // Keep last note visible (in gray) - don't clear
                //_currentNote = placeholder;
                //_currentNoteSolf = placeholder;
                //_currentMidiNote = 0;
                _noteStabilityCounter = 0;
                _centsBuffer.Clear();
                _silenceFrameCount++; // Track silence for staff re-centering
                return;
            }
            _hasSignal = true;
            _silenceFrameCount = 0; // Reset when we have signal

            // 2. AMDF Pitch Detection
            // Range: 100Hz - 2000Hz. 100Hz = G2, covers all singing voices including deep bass.
            // Using 100 instead of 60 reduces maxLag from 735→441, shifting the analysis window
            // ~7ms closer to the present — critical for catching fast note changes.
            int minFreq = 100;
            int maxFreq = 2000;
            int minLag = _sampleRate / maxFreq;
            int maxLag = _sampleRate / minFreq;

            if (maxLag >= BufferSize) maxLag = BufferSize - 1;

            // USE NEWEST DATA
            // Use a Fixed Window Size for detection at the END of the buffer
            int windowSize = 900; // Analysis Window (~20ms)
            int analysisStart = BufferSize - maxLag - windowSize;

            if (analysisStart < 0) analysisStart = 0;

            int bestLag = -1;
            float minVal = float.MaxValue;

            // Reuse pre-allocated AMDF buffer — avoids per-scan allocation in the render loop
            var amdf = _amdfBuffer;

            for (int lag = minLag; lag <= maxLag; lag++)
            {
                float diffSum = 0;

                for (int i = 0; i < windowSize; i += 2)
                {
                    int idx = analysisStart + i;
                    diffSum += Math.Abs(frame[idx] - frame[idx + lag]);
                }

                amdf[lag] = diffSum;

                if (diffSum < minVal)
                {
                    minVal = diffSum;
                    bestLag = lag;
                }
            }

            // Octave Error Correction
            // AMDF often returns double the true lag (one octave below the real pitch).
            // Check if bestLag/2 is also a strong candidate — if so, prefer it (higher frequency = less error).
            if (bestLag > 0)
            {
                int halfLag = bestLag / 2;
                if (halfLag >= minLag)
                {
                    float halfVal = amdf[halfLag];
                    // Accept the half-lag if its AMDF value is within 10% of the global minimum.
                    // Tighter than 25% to avoid wrong-direction octave errors on voiced signals.
                    if (halfVal <= minVal * 1.10f)
                    {
                        bestLag = halfLag;
                        minVal = halfVal;
                    }
                }
            }

            // Parabolic Interpolation
            if (bestLag > 0 && bestLag < maxLag)
            {
                // Parabolic Interpolation
                float y1 = amdf[bestLag - 1];
                float y2 = amdf[bestLag];
                float y3 = amdf[bestLag + 1];

                float denominator = 2 * (y1 - 2 * y2 + y3);
                float offset = 0;

                if (Math.Abs(denominator) > 0.0001f)
                {
                    offset = (y1 - y3) / denominator;
                }

                float exactLag = bestLag + offset;

                _currentFrequency = _sampleRate / exactLag;

                // Convert to Note and Cents
                double noteNum = 69 + 12 * Math.Log2(_currentFrequency / 440.0);
                int detectedMidiNote = (int)Math.Round(noteNum);

                // Stability Logic:
                // Only switch the "Main Note" if we detect a new note consistently
                if (detectedMidiNote == _potentialMidiNote)
                {
                    if (_noteStabilityCounter < 10) _noteStabilityCounter++;
                }
                else
                {
                    _potentialMidiNote = detectedMidiNote;
                    _noteStabilityCounter = 0;
                }

                // Require 2 consecutive same-note detections (approx 10-20ms)
                if (_noteStabilityCounter >= 1)
                {
                    if (_currentMidiNote != detectedMidiNote)
                    {
                        _currentMidiNote = detectedMidiNote;
                        _centsBuffer.Clear();
                    }

                    int noteIndex = _currentMidiNote % 12;
                    if (noteIndex < 0) noteIndex += 12;
                    _currentNote = NoteNames[noteIndex];
                    _currentNoteSolf = SolfegeNames[noteIndex];

                    // Dynamic Staff Centering: Adjust reference to keep notes on main staff
                    UpdateStaffReference(_currentMidiNote);
                }

                // Calculate cents relative to the STABLE note (so needle shows true drift)
                float targetFreq = 440.0f * (float)Math.Pow(2, (_currentMidiNote - 69) / 12.0f);
                if (targetFreq > 0)
                {
                    float rawCents = 1200 * (float)Math.Log2(_currentFrequency / targetFreq);

                    _centsBuffer.Add(rawCents);
                    if (_centsBuffer.Count > 5) _centsBuffer.RemoveAt(0);

                    float sum = 0;
                    foreach (var c in _centsBuffer) sum += c;
                    _currentCents = sum / _centsBuffer.Count;
                }
            }
        }

        private void UpdateStaffReference(int detectedMidi)
        {
            // Calculate diatonic distance from current reference
            int[] diatonicOffsets = { 0, 0, 1, 1, 2, 3, 3, 4, 4, 5, 5, 6 };
            int octave = (detectedMidi / 12) - 1;
            int noteInOctave = detectedMidi % 12;
            int detectedStep = octave * 7 + diatonicOffsets[noteInOctave];

            int refOctave = (_staffReferenceMidi / 12) - 1;
            int refNoteInOctave = _staffReferenceMidi % 12;
            int refStep = refOctave * 7 + diatonicOffsets[refNoteInOctave];

            int stepDistance = Math.Abs(detectedStep - refStep);

            // First note ever, or after long silence: center on detected note
            if (_staffReferenceMidi == 71 && _displayMidiNote == 0) // Initial state
            {
                _staffReferenceMidi = detectedMidi;
            }
            // After silence, allow re-centering to new octave
            else if (_silenceFrameCount >= SilenceThreshold && stepDistance > 7) // More than an octave away
            {
                _staffReferenceMidi = detectedMidi;
            }
            // If note is far from current reference (would need many ledger lines), adjust closer
            else if (stepDistance > 6) // Outside main staff comfort zone
            {
                // Gradually move reference toward the detected note
                int targetMidi = detectedMidi;
                // Snap to nearest octave of the same note class to keep staff stable
                int noteClass = _staffReferenceMidi % 12;
                int detectedOctaveBase = (detectedMidi / 12) * 12;
                int candidate1 = detectedOctaveBase + noteClass;
                int candidate2 = candidate1 + 12;
                int candidate3 = candidate1 - 12;
                
                // Pick closest candidate to detected note
                int best = candidate1;
                int bestDist = Math.Abs(detectedMidi - candidate1);
                if (Math.Abs(detectedMidi - candidate2) < bestDist) best = candidate2;
                if (Math.Abs(detectedMidi - candidate3) < bestDist) best = candidate3;
                
                _staffReferenceMidi = best;
            }
        }

        public bool Render(SKCanvas canvas, SKRect viewport, float scale)
        {
            if (viewport.Width <= 0 || viewport.Height <= 0)
                return false;

            float width = viewport.Width;
            float height = viewport.Height;
            float left = viewport.Left;
            float top = viewport.Top;

            if (_paintTextLarge == null)
            {
                _paintTextLarge = new SKPaint { Color = SKColors.White, IsAntialias = true, TextAlign = SKTextAlign.Center, FakeBoldText = true };
                _paintTextSmall = new SKPaint { Color = SKColors.Gray, IsAntialias = true, TextAlign = SKTextAlign.Center };
                _paintGauge = new SKPaint { Color = SKColors.DarkGray, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 6 * scale };
                _paintNeedle = new SKPaint { Color = SKColors.Cyan, IsAntialias = true, Style = SKPaintStyle.Fill };
            }

            if (System.Threading.Interlocked.CompareExchange(ref _swapRequested, 0, 1) == 1)
            {
                _displayNote = _currentNote;
                _displayNoteSolf = _currentNoteSolf;
                _displayMidiNote = _currentMidiNote;
                _displayFrequency = _currentFrequency;
                _displayStaffReferenceMidi = _staffReferenceMidi;
                _displayHasSignal = _hasSignal;

                // Smooth cents for display
                if (_displayHasSignal)
                    _smoothCents = _smoothCents * 0.5f + _currentCents * 0.5f;
                else
                    _smoothCents = _smoothCents * 0.9f;

                _displayCents = _smoothCents;

                // Color logic
                if (_displayHasSignal)
                {
                    if (Math.Abs(_displayCents) < 10) _displayColor = SKColors.Lime;
                    else if (Math.Abs(_displayCents) < 25) _displayColor = SKColors.Yellow;
                    else _displayColor = SKColors.Orange;
                }
                else
                {
                    _displayColor = SKColors.DarkGray;
                    // Keep showing last note in gray - don't clear
                    //_displayNote = placeholder;
                    //_displayNoteSolf = placeholder;
                }
            }

            float cx = left + width / 2f;

            float minDim = Math.Min(width, height);
            float cyNote = top + height * 0.35f;
            float cyInfo = top + height * 0.52f;
            float cyStaff = top + height * 0.72f;
            float cyGauge = top + height * 0.88f;

            float textLarge = Math.Max(12f * scale, minDim * 0.45f);
            float textSmall = Math.Max(10f * scale, minDim * 0.18f);
            float textInfo = Math.Max(8f * scale, minDim * 0.10f);

            // Draw Note Name
            _paintTextLarge.TextSize = textLarge;
            _paintTextLarge.Color = _displayColor;

            var useText = _displayNoteSolf;//_displayNote
            var bounds = new SKRect();
            _paintTextLarge.MeasureText(useText, ref bounds);
            canvas.DrawText(useText, cx, cyNote, _paintTextLarge);

            // Draw Solfège (Do-Re-Mi) just below the large note
            //_paintTextSmall.TextSize = textSmall;
            //_paintTextSmall.Color = _displayColor;
            //canvas.DrawText(_displayNoteSolf, cx, cyNote + textSmall * 0.6f, _paintTextSmall);

            // Always Draw Staff
            float staffWidth = width * 0.75f;
            float lineSpacing = Math.Max(2f * scale, height * 0.035f);
            // Show notes if we have a detected note (even if signal isn't perfect)
            DrawMusicalStaff(canvas, cx, cyStaff, lineSpacing, staffWidth, scale, _displayMidiNote > 0 ? _displayMidiNote : 0, _displayStaffReferenceMidi);

            // Draw Info
            if (_displayHasSignal)
            {
                _paintTextSmall.Color = SKColors.White.WithAlpha(95);
                _paintGauge.Color = SKColors.Gray.WithAlpha(95);
            }
            else
            {
                _paintTextSmall.Color = SKColors.White.WithAlpha(50);
                _paintGauge.Color = SKColors.Gray.WithAlpha(50);
            }

            _paintGauge.StrokeWidth = 6 * scale;
            _paintTextSmall.TextSize = textInfo;

            canvas.DrawText($"{_displayFrequency:F1} Hz", cx, cyInfo, _paintTextSmall);

            // Gauge Background
            float barWidth = width * 0.75f;
            float barY = cyGauge;

            canvas.DrawLine(cx - barWidth / 2, barY, cx + barWidth / 2, barY, _paintGauge);
            float tick = Math.Max(2f * scale, height * 0.03f);
            canvas.DrawLine(cx, barY - tick, cx, barY + tick, _paintGauge); // Center tick

            // Gauge Needle
            float offset = (_displayCents / 50.0f) * (barWidth / 2);
            offset = Math.Clamp(offset, -barWidth / 2, barWidth / 2);

            _paintNeedle.Color = _displayColor;
            float dotRadius = Math.Max(2f * scale, height * 0.03f);
            canvas.DrawCircle(cx + offset, barY, dotRadius, _paintNeedle);

            // Cents Text
            _paintTextSmall.TextSize = Math.Max(8f * scale, minDim * 0.08f);
            canvas.DrawText($"{_displayCents:+0;-0} cents", cx, barY + _paintTextSmall.TextSize * 1.6f, _paintTextSmall);


            return false;
        }

        private void DrawMusicalStaff(SKCanvas canvas, float cx, float cy, float lineSpacing, float staffWidth, float scale, int midiNote, int referenceMidi)
        {
            float startX = cx - staffWidth / 2;
            float endX = cx + staffWidth / 2;

            _paintGauge.StrokeWidth = 2 * scale;
            _paintGauge.Color = SKColors.LightGray.WithAlpha(200);

            // Draw 5 lines (staff dynamically centered on referenceMidi)
            for (int i = -2; i <= 2; i++)
            {
                float y = cy - (i * lineSpacing);
                canvas.DrawLine(startX, y, endX, y, _paintGauge);
            }

            if (midiNote <= 0) return;

            // Map MIDI to visual steps
            int[] diatonicOffsets = { 0, 0, 1, 1, 2, 3, 3, 4, 4, 5, 5, 6 };
            int octave = (midiNote / 12) - 1;
            int noteInOctave = midiNote % 12;
            int absStep = octave * 7 + diatonicOffsets[noteInOctave];

            // Dynamic reference based on currently playing notes
            int refOctave = (referenceMidi / 12) - 1;
            int refNoteInOctave = referenceMidi % 12;
            int refStep = refOctave * 7 + diatonicOffsets[refNoteInOctave];
            int stepsFromCenter = absStep - refStep;

            float noteY = cy - (stepsFromCenter * (lineSpacing / 2));

            // Clamp noteY to reasonable bounds (within 3x staff height from center)
            float maxOffsetFromStaff = lineSpacing * 12; // About 6 octaves range
            noteY = Math.Clamp(noteY, cy - maxOffsetFromStaff, cy + maxOffsetFromStaff);

            // Ledger Lines (only draw if within reasonable range)
            _paintGauge.Color = SKColors.White;
            // Upper (limit to avoid drawing far outside viewport)
            int maxLedgerSteps = Math.Min(stepsFromCenter, 20); // Cap at 20 steps
            for (int s = 6; s <= maxLedgerSteps; s += 2)
            {
                float ly = cy - (s * (lineSpacing / 2));
                canvas.DrawLine(cx - 24 * scale, ly, cx + 24 * scale, ly, _paintGauge);
            }
            // Lower (limit to avoid drawing far outside viewport)
            int minLedgerSteps = Math.Max(stepsFromCenter, -20); // Cap at -20 steps
            for (int s = -6; s >= minLedgerSteps; s -= 2)
            {
                float ly = cy - (s * (lineSpacing / 2));
                canvas.DrawLine(cx - 24 * scale, ly, cx + 24 * scale, ly, _paintGauge);
            }

            // Note Head
            _paintNeedle.Color = _displayColor;
            canvas.DrawOval(cx, noteY, 15 * scale, 11 * scale, _paintNeedle);

            // Stem
            _paintGauge.Color = _displayColor;
            _paintGauge.StrokeWidth = 3 * scale;
            float stemHeight = 50 * scale;
            if (stepsFromCenter >= 0) // Stem Down (Left)
                canvas.DrawLine(cx - 13 * scale, noteY, cx - 13 * scale, noteY + stemHeight, _paintGauge);
            else // Stem Up (Right)
                canvas.DrawLine(cx + 13 * scale, noteY, cx + 13 * scale, noteY - stemHeight, _paintGauge);

            // Sharp symbol
            bool isSharp = noteInOctave == 1 || noteInOctave == 3 || noteInOctave == 6 || noteInOctave == 8 || noteInOctave == 10;
            if (isSharp)
            {
                _paintTextSmall.TextSize = 30 * scale;
                _paintTextSmall.Color = _displayColor;
                _paintTextSmall.TextAlign = SKTextAlign.Right;
                _paintTextSmall.FakeBoldText = true;
                canvas.DrawText("#", cx - 22 * scale, noteY + 10 * scale, _paintTextSmall);
                _paintTextSmall.FakeBoldText = false;
                _paintTextSmall.TextAlign = SKTextAlign.Center;
                _paintTextSmall.Color = SKColors.White.WithAlpha(180); // Restore
            }
        }

        public void Dispose()
        {
            _paintTextLarge?.Dispose(); _paintTextLarge = null;
            _paintTextSmall?.Dispose(); _paintTextSmall = null;
            _paintGauge?.Dispose(); _paintGauge = null;
            _paintNeedle?.Dispose(); _paintNeedle = null;
        }
    }
}
