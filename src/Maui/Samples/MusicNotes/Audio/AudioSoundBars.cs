using DrawnUi.Camera;
using MusicNotes.UI;

namespace MusicNotes.Audio
{
    /// <summary>
    /// High-resolution frequency spectrum sound bars (music player style).
    /// Uses Goertzel algorithm to compute energy at logarithmically-spaced
    /// frequency bins. Bars with energy are ON, bars without are fully OFF,
    /// creating an organic pattern of active/silent frequency clusters.
    /// ZERO render allocations, double-buffered.
    /// </summary>
    public class AudioSoundBars : IAudioVisualizer, IDisposable
    {
        public bool UseGain { get; set; } = false;
        public int Skin { get; set; } = 1;
        public bool ShowPeakDots { get; set; } = true;

        private const int BarCount = 48;
        private const int AnalysisSize = 1024; // Samples to analyze (power of 2 for cleaner bins)

        // Ballistics — per-second rates so behaviour is frame-rate/device-independent.
        // Decay is applied in Render() every frame, not in AddSample(), so it keeps
        // running even after audio input stops.
        /// <summary>Bar release: fraction remaining per second. ~10% left after ~0.2 s.</summary>
        private const float ReleaseCoeffPerSec = 0.00005f;
        /// <summary>Peak hold decay: fraction remaining per second. ~15% left after 1 s → dots gone in ~1.7 s.</summary>
        private const float PeakDecayPerSec = 0.15f;

        // Hard cutoff: bins below this fraction of max energy are completely OFF
        private const float CutoffRatio = 0.12f;

        // Absolute noise gate: if max power across all bins is below this,
        // all bars stay OFF. Prevents ambient noise from triggering the display.
        private const float NoiseGate = 1.00f;

        private float[] _barsFrontBuffer = new float[BarCount];
        private float[] _barsBackBuffer = new float[BarCount];
        // _peakHold is written by audio thread (new maximums only) and decayed by render thread.
        // Both accesses are benign races on float — acceptable for a visualizer.
        private float[] _peakHold = new float[BarCount];
        private int _swapRequested = 0;
        private long _lastRenderTimestamp = 0;

        // Pre-computed Goertzel coefficients per bin
        private float[] _goertzelCoeff = new float[BarCount];
        private float[] _goertzelFreqs = new float[BarCount]; // For debug/tuning
        private bool _coeffReady = false;
        private int _lastSampleRate = 0;


        // Per-bin gain compensation (higher freqs need more boost)
        private float[] _binGain = new float[BarCount];

        private SKPaint _paintBar;
        private SKPaint _paintDot;
        private SKPaint _paintText;

        public void Reset()
        {
            Array.Clear(_barsFrontBuffer, 0, _barsFrontBuffer.Length);
            Array.Clear(_barsBackBuffer, 0, _barsBackBuffer.Length);
            Array.Clear(_peakHold, 0, _peakHold.Length);
            _swapRequested = 0;
            _lastRenderTimestamp = 0;

            _coeffReady = false;
            _lastSampleRate = 0;
        }
 

        private void InitCoefficients(int sampleRate)
        {
            if (_coeffReady && sampleRate == _lastSampleRate)
                return;

            _lastSampleRate = sampleRate;

            // Logarithmic frequency mapping: 60Hz to 8000Hz
            float minFreq = 60f;
            float maxFreq = 8000f;
            float logMin = (float)Math.Log(minFreq);
            float logMax = (float)Math.Log(maxFreq);

            for (int i = 0; i < BarCount; i++)
            {
                float t = i / (float)(BarCount - 1);
                float freq = (float)Math.Exp(logMin + t * (logMax - logMin));
                _goertzelFreqs[i] = freq;

                // Goertzel: k = round(N * freq / sampleRate)
                // coeff = 2 * cos(2*pi*k/N)
                float k = AnalysisSize * freq / sampleRate;
                _goertzelCoeff[i] = 2f * (float)Math.Cos(2.0 * Math.PI * k / AnalysisSize);

                // Higher frequency bins get more gain to compensate
                // for natural spectral rolloff in music
                _binGain[i] = 1.0f + t * 2.5f;
            }

            _coeffReady = true;
        }

        public void AddSample(AudioSample sample)
        {
            int sampleRate = sample.SampleRate > 0 ? sample.SampleRate : 44100;
            InitCoefficients(sampleRate);

            int sampleCount = sample.Data.Length / 2;

            // Use at most AnalysisSize samples from the end of the buffer (most recent audio)
            int analyzeCount = Math.Min(sampleCount, AnalysisSize);
            int startOffset = (sampleCount - analyzeCount) * 2; // byte offset

            // Goertzel for each frequency bin
            float maxPower = 0f;

            for (int bin = 0; bin < BarCount; bin++)
            {
                float coeff = _goertzelCoeff[bin];
                float s1 = 0f, s2 = 0f;

                for (int i = 0; i < analyzeCount; i++)
                {
                    int byteIndex = startOffset + i * 2;
                    if (byteIndex + 1 >= sample.Data.Length)
                        break;

                    short pcm = (short)(sample.Data[byteIndex] | (sample.Data[byteIndex + 1] << 8));
                    float x = pcm / 32768f;

                    float s0 = x + coeff * s1 - s2;
                    s2 = s1;
                    s1 = s0;
                }

                // Power = s1^2 + s2^2 - coeff*s1*s2, apply per-bin gain
                float power = (s1 * s1 + s2 * s2 - coeff * s1 * s2) * _binGain[bin];
                if (power < 0) power = 0; // Safety clamp

                _barsBackBuffer[bin] = power;
                if (power > maxPower) maxPower = power;
            }

            // Absolute noise gate + relative cutoff, then progressive boost for ON bars
            float cutoff = maxPower * CutoffRatio;
            bool gated = maxPower < NoiseGate; // Silence when just ambient noise

            for (int bin = 0; bin < BarCount; bin++)
            {
                float power = _barsBackBuffer[bin];

                float target;
                if (gated || power < cutoff)
                {
                    target = 0f; // Completely OFF
                }
                else
                {
                    // Normalize to 0..1 relative to max, remapping from cutoff..max
                    float normalized = (power - cutoff) / (maxPower - cutoff);
                    // Square root curve: compresses range so mid-level bins
                    // appear tall while keeping proportions between ON bars
                    target = (float)Math.Sqrt(normalized);
                }

                // Instant attack: jump up immediately. Release/decay is handled in Render()
                // so it keeps running even after audio input stops.
                float current = _barsFrontBuffer[bin];
                _barsBackBuffer[bin] = target > current ? target : current;

                // Peak: only ever goes up here. Render() decays it every frame.
                if (_barsBackBuffer[bin] > _peakHold[bin])
                    _peakHold[bin] = _barsBackBuffer[bin];
            }

            System.Threading.Interlocked.Exchange(ref _swapRequested, 1);
        }

        public bool Render(SKCanvas canvas, SKRect viewport, float scale)
        {
            if (viewport.Width <= 0 || viewport.Height <= 0)
                return false;

            float width = viewport.Width;
            float height = viewport.Height;
            float left = viewport.Left;
            float top = viewport.Top;

            if (_paintBar == null)
            {
                _paintBar = new SKPaint
                {
                    Style = SKPaintStyle.Fill,
                    IsAntialias = false
                };
            }

            if (_paintDot == null)
            {
                _paintDot = new SKPaint
                {
                    Style = SKPaintStyle.Fill,
                    IsAntialias = false
                };
            }

            if (_paintText == null)
            {
                _paintText = new SKPaint
                {
                    Color = SKColors.Yellow,
                    IsAntialias = true,
                    TextAlign = SKTextAlign.Center
                };
            }

            // Swap buffers if audio thread produced new data
            if (System.Threading.Interlocked.CompareExchange(ref _swapRequested, 0, 1) == 1)
                Array.Copy(_barsBackBuffer, _barsFrontBuffer, BarCount);

            // Time-based decay applied every frame — works with or without incoming audio
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            long last = System.Threading.Interlocked.Exchange(ref _lastRenderTimestamp, now);
            float elapsedSec = last == 0
                ? (1f / 60f)
                : Math.Min((now - last) / (float)System.Diagnostics.Stopwatch.Frequency, 0.5f);
            float releaseThisFrame = (float)Math.Pow(ReleaseCoeffPerSec, elapsedSec);
            float peakDecayThisFrame = (float)Math.Pow(PeakDecayPerSec, elapsedSec);

            bool dirty = false;
            for (int i = 0; i < BarCount; i++)
            {
                _barsFrontBuffer[i] *= releaseThisFrame;
                _peakHold[i] *= peakDecayThisFrame;

                if (_barsFrontBuffer[i] > 0.01f || (ShowPeakDots && _peakHold[i] > 0.03f))
                    dirty = true;
            }

            var startX = left;
            var bottomY = top + height;
            var maxBarHeight = height;
            var totalSlot = width / BarCount;
            var barWidth = Math.Max(1f, totalSlot * 0.55f);

            if (Skin == 0)
            {
                // Skin 0: White/gray bars with floating peak dots
                for (int i = 0; i < BarCount; i++)
                {
                    var level = _barsFrontBuffer[i];
                    var peakLevel = _peakHold[i];
                    var x = startX + i * totalSlot + (totalSlot - barWidth) / 2;

                    // Only draw bars that are actually ON
                    if (level > 0.01f)
                    {
                        var barH = level * maxBarHeight;
                        byte barAlpha = (byte)(140 + Math.Min(115, level * 200));
                        _paintBar.Color = new SKColor(220, 225, 235, barAlpha);
                        canvas.DrawRect(x, bottomY - barH, barWidth, barH, _paintBar);
                    }

                    // Floating peak dot
                    if (ShowPeakDots && peakLevel > 0.03f)
                    {
                        var dotY = bottomY - peakLevel * maxBarHeight;
                        var dotH = 2f * scale;
                        _paintDot.Color = SKColors.White;
                        canvas.DrawRect(x, dotY - dotH, barWidth, dotH, _paintDot);
                    }
                }
            }
            else
            {
                // Skin 1: Colored bars, hue based on frequency position
                for (int i = 0; i < BarCount; i++)
                {
                    var level = _barsFrontBuffer[i];
                    var peakLevel = _peakHold[i];
                    var x = startX + i * totalSlot + (totalSlot - barWidth) / 2;

                    if (level > 0.01f)
                    {
                        var barH = level * maxBarHeight;
                        float hue = (i / (float)(BarCount - 1)) * 200 + 160; // blue->cyan->green
                        if (hue >= 360) hue -= 360;
                        _paintBar.Color = SKColor.FromHsv(hue, 80, 100);
                        canvas.DrawRect(x, bottomY - barH, barWidth, barH, _paintBar);
                    }

                    if (ShowPeakDots && peakLevel > 0.03f)
                    {
                        var dotY = bottomY - peakLevel * maxBarHeight;
                        var dotH = 2f * scale;
                        _paintDot.Color = SKColors.White;
                        canvas.DrawRect(x, dotY - dotH, barWidth, dotH, _paintDot);
                    }
                }
            }

            return dirty;
        }

        public void Dispose()
        {
            _paintBar?.Dispose();
            _paintBar = null;
            _paintDot?.Dispose();
            _paintDot = null;
            _paintText?.Dispose();
            _paintText = null;
 
        }
    }
}
