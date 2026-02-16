using DrawnUi.Camera;
using MusicNotes.UI;

namespace MusicNotes.Audio
{
    /// <summary>
    /// Scrolling waveform bars visualizer (music player style, like SoundCloud/Spotify).
    /// Displays many thin vertical bars growing symmetrically from center.
    /// New audio scrolls in from the right, older bars fade on the left.
    /// ZERO allocations during render, double-buffered.
    /// </summary>
    public class AudioWaveformBars : IAudioVisualizer, IDisposable
    {
        private const int BarCount = 64;

        private float[] _barsFrontBuffer = new float[BarCount];
        private float[] _barsBackBuffer = new float[BarCount];
        private int _swapRequested = 0;

        // Rolling accumulator for incoming samples
        private int _samplesAccumulated = 0;
        private float _currentPeak = 0f;

        // How many raw PCM samples per bar (controls scroll speed)
        // At 44100Hz with ~30fps render, each frame ~1470 samples.
        // We want roughly 2-4 bars per frame for smooth scrolling.
        private int _samplesPerBar = 512;

        public bool UseGain { get; set; } = false;
        public int Skin { get; set; } = 0;

        public void Reset()
        {
            Array.Clear(_barsFrontBuffer, 0, _barsFrontBuffer.Length);
            Array.Clear(_barsBackBuffer, 0, _barsBackBuffer.Length);
            _swapRequested = 0;
            _samplesAccumulated = 0;
            _currentPeak = 0f;
        }

        private SKPaint _paintBar;
        private SKPaint _paintText;
        private SKPaint _paintBg;

        public void AddSample(AudioSample sample)
        {
            int sampleCount = sample.Data.Length / 2;
            float gain = UseGain ? 3.5f : 1.0f;

            for (int i = 0; i < sampleCount; i++)
            {
                int byteIndex = i * 2;
                if (byteIndex + 1 >= sample.Data.Length)
                    break;

                short pcm = (short)(sample.Data[byteIndex] | (sample.Data[byteIndex + 1] << 8));
                float val = Math.Abs(pcm / 32768f) * gain;

                if (val > _currentPeak)
                    _currentPeak = val;

                _samplesAccumulated++;

                if (_samplesAccumulated >= _samplesPerBar)
                {
                    // Shift all bars left by one
                    Array.Copy(_barsBackBuffer, 1, _barsBackBuffer, 0, BarCount - 1);

                    // Push new peak value at the right end
                    _barsBackBuffer[BarCount - 1] = Math.Clamp(_currentPeak, 0f, 1f);

                    _currentPeak = 0f;
                    _samplesAccumulated = 0;

                    System.Threading.Interlocked.Exchange(ref _swapRequested, 1);
                }
            }
        }

        public void Render(SKCanvas canvas, SKRect viewport, float scale)
        {
            if (viewport.Width <= 0 || viewport.Height <= 0)
                return;

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

            if (_paintText == null)
            {
                _paintText = new SKPaint
                {
                    Color = SKColors.Yellow,
                    IsAntialias = true,
                    TextAlign = SKTextAlign.Center
                };
            }

            if (_paintBg == null)
            {
                _paintBg = new SKPaint
                {
                    Color = SKColors.Black.WithAlpha(128),
                    Style = SKPaintStyle.Fill
                };
            }

            // Swap buffers
            if (System.Threading.Interlocked.CompareExchange(ref _swapRequested, 0, 1) == 1)
            {
                var temp = _barsFrontBuffer;
                _barsFrontBuffer = _barsBackBuffer;
                _barsBackBuffer = temp;
            }

            // Background fills viewport
            canvas.DrawRect(viewport, _paintBg);

            // Bar dimensions
            var totalBarSlot = width / BarCount;
            var barWidth = Math.Max(1f, totalBarSlot * 0.6f);
            var barGap = totalBarSlot - barWidth;
            var halfHeight = height / 2;
            var minBarHeight = Math.Max(1f, 2f * scale); // Minimum dot size for silence

            var startX = left;
            var centerY = top + height / 2;

            if (Skin == 0)
            {
                // Skin 0: Symmetrical bars from center, white with opacity fade
                for (int i = 0; i < BarCount; i++)
                {
                    var level = _barsFrontBuffer[i];
                    var x = startX + i * totalBarSlot + barGap / 2;

                    // Opacity: older bars (left) fade out
                    byte alpha = (byte)(100 + (155f * i / (BarCount - 1)));

                    var barH = Math.Max(minBarHeight, level * halfHeight);

                    _paintBar.Color = SKColors.White.WithAlpha(alpha);

                    // Top half (grows upward from center)
                    canvas.DrawRect(x, centerY - barH, barWidth, barH, _paintBar);

                    // Bottom half (mirror, grows downward from center)
                    canvas.DrawRect(x, centerY, barWidth, barH, _paintBar);
                }
            }
            else
            {
                // Skin 1: Single-sided bars from bottom, colored gradient
                var bottomY = top + height;

                for (int i = 0; i < BarCount; i++)
                {
                    var level = _barsFrontBuffer[i];
                    var x = startX + i * totalBarSlot + barGap / 2;

                    byte alpha = (byte)(100 + (155f * i / (BarCount - 1)));

                    var barH = Math.Max(minBarHeight, level * height);

                    // Hue shifts from purple (left/old) to cyan (right/new)
                    float hue = 220 + (140f * i / (BarCount - 1)); // 220=blue -> 360=red wrap
                    if (hue >= 360) hue -= 360;

                    _paintBar.Color = SKColor.FromHsv(hue, 70, 100).WithAlpha(alpha);

                    canvas.DrawRect(x, bottomY - barH, barWidth, barH, _paintBar);
                }
            }
        }


        public void Dispose()
        {
            _paintBar?.Dispose();
            _paintBar = null;
            _paintText?.Dispose();
            _paintText = null;
            _paintBg?.Dispose();
            _paintBg = null;
        }
    }
}
