using DrawnUi.Camera;

namespace CameraTests
{
    /// <summary>
    /// Musical Note Detector (Tuner)
    /// Uses AMDF (Average Magnitude Difference Function) with Parabolic Interpolation for accurate pitch.
    /// </summary>
    public class AudioInstrumentTuner : IAudioVisualizer, IDisposable
    {
        private const int BufferSize = 4096; // ~90ms at 44.1kHz
        private float[] _sampleBuffer = new float[BufferSize];
        private int _writePos = 0;
        private int _samplesAddedSinceLastScan = 0;
        private int _sampleRate = 44100;
        private const int ScanInterval = 512; // Scan much faster! (was 2048, ~46ms -> now ~11ms)

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

        // Render State
        private string _displayNote = placeholder;
        private string _displayNoteSolf = placeholder;
        private int _displayMidiNote = 0;
        private float _displayFrequency = 0;
        private float _displayCents = 0;
        private SKColor _displayColor = SKColors.Gray;
        private int _swapRequested = 0;

        private SKPaint _paintTextLarge;
        private SKPaint _paintTextSmall;
        private SKPaint _paintGauge;
        private SKPaint _paintNeedle;

        private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        private static readonly string[] SolfegeNames = { "Do", "Do#", "Re", "Re#", "Mi", "Fa", "Fa#", "Sol", "Sol#", "La", "La#", "Si" };

        public bool UseGain { get; set; } = true;
        public int Skin { get; set; } = 0;

        public void AddSample(AudioSample sample)
        {
            if (sample.SampleRate > 0)
                _sampleRate = sample.SampleRate;

            // Handle channels (use first channel only)
            int channels = sample.Channels > 0 ? sample.Channels : 1;
            int step = channels;
            int sampleCount = sample.Data.Length / 2;

            for (int i = 0; i < sampleCount; i += step)
            {
                int byteIndex = i * 2;
                if (byteIndex + 1 < sample.Data.Length)
                {
                    short pcm = (short)(sample.Data[byteIndex] | (sample.Data[byteIndex + 1] << 8));
                    float val = (pcm / 32768f);

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
            // Unroll buffer for analysis (Older -> Newer)
            float[] frame = new float[BufferSize];
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

                if (rms < 0.01f) // Silence Threshold
            {
                _hasSignal = false;
                _currentNote = placeholder;
                    _currentNoteSolf = placeholder;
                _currentMidiNote = 0; // Fix: Reset note on silence to prevent "ghost" note on next attack
                _noteStabilityCounter = 0;
                _centsBuffer.Clear();
                return;
            }
            _hasSignal = true;

            // 2. AMDF Pitch Detection
            // Range: 60Hz - 2000Hz (Extended for whistling/high soprano)
            int minFreq = 60;
            int maxFreq = 2000;
            int minLag = _sampleRate / maxFreq;
            int maxLag = _sampleRate / minFreq;

            if (maxLag >= BufferSize) maxLag = BufferSize - 1;

            // USE NEWEST DATA
            // Use a Fixed Window Size for detection at the END of the buffer
            int windowSize = 1500; // Analysis Window (~34ms)
            int analysisStart = BufferSize - maxLag - windowSize;

            if (analysisStart < 0) analysisStart = 0;

            int bestLag = -1;
            float minVal = float.MaxValue;

            // Simplified AMDF scan
            float[] amdf = new float[maxLag + 1];

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

                // Lower threshold for faster response (approx 20-30ms)
                if (_noteStabilityCounter > 1)
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
                }

                // Calculate cents relative to the STABLE note (so needle shows true drift)
                float targetFreq = 440.0f * (float)Math.Pow(2, (_currentMidiNote - 69) / 12.0f);
                if (targetFreq > 0)
                {
                    float rawCents = 1200 * (float)Math.Log2(_currentFrequency / targetFreq);

                    _centsBuffer.Add(rawCents);
                    if (_centsBuffer.Count > 10) _centsBuffer.RemoveAt(0);

                    float sum = 0;
                    foreach (var c in _centsBuffer) sum += c;
                    _currentCents = sum / _centsBuffer.Count;
                }
            }
        }

        public void Render(SKCanvas canvas, float width, float height, float scale)
        {
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

                // Smooth cents for display
                if (_hasSignal)
                    _smoothCents = _smoothCents * 0.7f + _currentCents * 0.3f;
                else
                    _smoothCents = _smoothCents * 0.9f;

                _displayCents = _smoothCents;

                // Color logic
                if (_hasSignal)
                {
                    if (Math.Abs(_displayCents) < 10) _displayColor = SKColors.Lime;
                    else if (Math.Abs(_displayCents) < 25) _displayColor = SKColors.Yellow;
                    else _displayColor = SKColors.Orange;
                }
                else
                {
                    _displayColor = SKColors.DarkGray;
                    _displayNote = placeholder;
                    _displayNoteSolf = placeholder;
                }
            }

            float cx = width / 2;
            float cy = height / 2;

            // Draw Note Name
            _paintTextLarge.TextSize = 140 * scale;
            _paintTextLarge.Color = _displayColor;

            var bounds = new SKRect();
            _paintTextLarge.MeasureText(_displayNote, ref bounds);
            // Move text UP to make room for staff
            canvas.DrawText(_displayNote, cx, cy - 80 * scale, _paintTextLarge);

            // Draw Solfège (Do-Re-Mi) just below the large note
            _paintTextSmall.TextSize = 56 * scale;
            _paintTextSmall.Color = _displayColor;
            canvas.DrawText(_displayNoteSolf, cx, cy - 30 * scale, _paintTextSmall);

            // Always Draw Staff (so user knows it's there)
            DrawMusicalStaff(canvas, cx, cy + 180 * scale, scale, _hasSignal ? _displayMidiNote : 0);

            // Draw Info
            if (_hasSignal)
            {
                _paintTextSmall.TextSize = 28 * scale;
                _paintTextSmall.Color = SKColors.White.WithAlpha(180);
                canvas.DrawText($"{_displayFrequency:F1} Hz", cx, cy + 10 * scale, _paintTextSmall);

                // Gauge Background
                float barWidth = 300 * scale;
                float barY = cy + 280 * scale;

                _paintGauge.Color = SKColors.Gray.WithAlpha(80);
                _paintGauge.StrokeWidth = 6 * scale;
                canvas.DrawLine(cx - barWidth / 2, barY, cx + barWidth / 2, barY, _paintGauge);
                canvas.DrawLine(cx, barY - 15 * scale, cx, barY + 15 * scale, _paintGauge); // Center tick

                // Gauge Needle
                float offset = (_displayCents / 50.0f) * (barWidth / 2);
                offset = Math.Clamp(offset, -barWidth / 2, barWidth / 2);

                _paintNeedle.Color = _displayColor;
                canvas.DrawCircle(cx + offset, barY, 12 * scale, _paintNeedle);

                // Cents Text
                _paintTextSmall.TextSize = 20 * scale;
                canvas.DrawText($"{_displayCents:+0;-0} cents", cx, barY + 40 * scale, _paintTextSmall);
            }
        }

        private void DrawMusicalStaff(SKCanvas canvas, float cx, float cy, float scale, int midiNote)
        {
            float lineSpacing = 16 * scale;
            float staffWidth = 200 * scale;
            float startX = cx - staffWidth / 2;
            float endX = cx + staffWidth / 2;

            _paintGauge.StrokeWidth = 2 * scale;
            _paintGauge.Color = SKColors.LightGray.WithAlpha(200);

            // Draw 5 lines (Treble Clef E4..F5) centered at B4 (Midi 71)
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
            int absStep = octave * 7 + diatonicOffsets[noteInOctave]; // E.g. C4 = 4*7+0 = 28

            // Reference Center Line is B4 (Midi 71).
            // 71 = 5*12 + 11. Octave 5? No. MIDI 60 is C4. MIDI 71 is B4.
            // 71 / 12 = 5 (so octave 4 base 0..11? No standard is C4=60)
            // Let's use logic: Octave = (midi / 12) - 1. 71/12 = 5. Octave index 4.
            // Note 71 % 12 = 11 (B).
            // Step = 4 * 7 + 6 = 34.

            // Reference B4 = 34.
            int refStep = 34;
            int stepsFromCenter = absStep - refStep;

            float noteY = cy - (stepsFromCenter * (lineSpacing / 2));

            // Ledger Lines
            _paintGauge.Color = SKColors.White;
            // Upper
            for (int s = 6; s <= stepsFromCenter; s += 2)
            {
                float ly = cy - (s * (lineSpacing / 2));
                canvas.DrawLine(cx - 24 * scale, ly, cx + 24 * scale, ly, _paintGauge);
            }
            // Lower
            for (int s = -6; s >= stepsFromCenter; s -= 2)
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
