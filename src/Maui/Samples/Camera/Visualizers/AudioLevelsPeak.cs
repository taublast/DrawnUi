using DrawnUi.Camera;

namespace CameraTests
{
    /// <summary>
    /// ABSOLUTE LOWEST CPU: Instant peak + decay (like cheap LED meters)
    /// Uses 3-band crossover filters. Displays PEAK instead of Average energy.
    /// </summary>
    public class AudioLevelsPeak : IAudioVisualizer, IDisposable
    {
        private const int BandCount = 8;
        private float[] _levelsFrontBuffer = new float[BandCount];
        private float[] _levelsBackBuffer = new float[BandCount];
        private int _swapRequested = 0;
        private const float DecayCoeff = 0.65f; // Fast decay for mobile (was 0.94)

        // Filters - 2-pole cascaded
        private float _b1, _b2;
        private float _m1, _m2;
        private float _prevIn, _prevOut; // DC Blocker state

        public bool UseGain { get; set; } = true;
        public int Skin { get; set; } = 0;

        private SKPaint _paintBar;
        private SKPaint _paintText;

        public void AddSample(AudioSample sample)
        {
            int sampleCount = sample.Data.Length / 2;
            int step = 1;
            float gain = UseGain ? 2.0f : 1.0f; // Global boost (reduced from 5.0)

            float peakLow = 0;
            float peakMid = 0;
            float peakHigh = 0;

            const float alphaLow = 0.05f;
            const float alphaMid = 0.35f;

            for (int i = 0; i < sampleCount; i += step)
            {
                int byteIndex = i * 2;
                if (byteIndex + 1 < sample.Data.Length)
                {
                    short pcm = (short)(sample.Data[byteIndex] | (sample.Data[byteIndex + 1] << 8));
                    float raw = pcm / 32768f;

                    // DC Blocking
                    float val = raw - _prevIn + 0.995f * _prevOut;
                    _prevIn = raw;
                    _prevOut = val;

                    // Filters
                    _b1 += alphaLow * (val - _b1);
                    _b2 += alphaLow * (_b1 - _b2);

                    _m1 += alphaMid * (val - _m1);
                    _m2 += alphaMid * (_m1 - _m2);

                    float b = Math.Abs(_b2);
                    float m = Math.Abs(_m2 - _b2);
                    float t = Math.Abs(val - _m2);

                    // MAX logic for PEAK meter
                    if (b > peakLow) peakLow = b;
                    if (m > peakMid) peakMid = m;
                    if (t > peakHigh) peakHigh = t;
                }
            }

            peakLow *= gain;
            peakMid *= gain;
            peakHigh *= gain;

            // Map 3 bands to 8 bars
            float[] targets = new float[BandCount];
            targets[0] = peakLow * 2.2f;
            targets[1] = peakLow * 2.0f;
            targets[2] = (peakLow * 0.3f + peakMid * 0.7f) * 1.8f;
            targets[3] = peakMid * 2.0f;
            targets[4] = peakMid * 2.2f;
            targets[5] = (peakMid * 0.4f + peakHigh * 0.6f) * 2.5f;
            targets[6] = peakHigh * 3.5f;
            targets[7] = peakHigh * 4.0f;

            for (int i = 0; i < BandCount; i++)
            {
                // Instant attack, expo decay
                _levelsBackBuffer[i] = Math.Max(_levelsBackBuffer[i] * DecayCoeff, Math.Min(1f, targets[i]));
            }

            System.Threading.Interlocked.Exchange(ref _swapRequested, 1);
        }

        public void Render(SKCanvas canvas, float width, float height, float scale, string recognizedText = null)
        {
            if (_paintBar == null)
            {
                _paintBar = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = false };
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

            if (System.Threading.Interlocked.CompareExchange(ref _swapRequested, 0, 1) == 1)
            {
                var temp = _levelsFrontBuffer;
                _levelsFrontBuffer = _levelsBackBuffer;
                _levelsBackBuffer = temp;
            }

            var barAreaWidth = width * 0.8f;
            var maxBarHeight = 180 * scale;
            var barWidth = (barAreaWidth / BandCount) * 0.8f;
            var barGap = (barAreaWidth / BandCount) * 0.2f;
            var startX = (width - barAreaWidth) / 2;
            var bottomY = height - 40 * scale;

            if (!string.IsNullOrEmpty(recognizedText))
            {
                _paintText.TextSize = 32 * scale;
                canvas.DrawText(recognizedText, width / 2, bottomY - maxBarHeight - 30 * scale, _paintText);
            }

            // Background dimmer
            using (var paintBg = new SKPaint { Color = SKColors.Black.WithAlpha(128), Style = SKPaintStyle.Fill })
            {
                canvas.DrawRect(startX - 10 * scale, bottomY - maxBarHeight - 10 * scale, barAreaWidth + 20 * scale, maxBarHeight + 20 * scale, paintBg);
            }

            for (int i = 0; i < BandCount; i++)
            {
                var level = _levelsFrontBuffer[i];
                var barHeight = level * maxBarHeight;
                var x = startX + i * (barWidth + barGap);
                var y = bottomY - barHeight;

                float hue = (i / (float)(BandCount - 1)) * 140;

                if (Skin == 0)
                {
                    // Snap to integer pixels for consistency
                    var segmentHeight = (float)Math.Round(4 * scale);
                    var segmentGap = (float)Math.Round(2 * scale);
                    if (segmentHeight < 1) segmentHeight = 1;
                    if (segmentGap < 1) segmentGap = 1;

                    var step = segmentHeight + segmentGap;
                    int totalSegments = (int)(maxBarHeight / step);

                    for (int j = 0; j < totalSegments; j++)
                    {
                        var segY = bottomY - ((j + 1) * step) + segmentGap;

                        bool active = (j * step) < barHeight;

                        if (active)
                            _paintBar.Color = SKColor.FromHsv(hue, 90, 100);
                        else
                            _paintBar.Color = SKColor.FromHsv(hue, 90, 100).WithAlpha(50);

                        canvas.DrawRect(x, segY, barWidth, segmentHeight, _paintBar);
                    }
                }
                else
                {
                    _paintBar.Color = SKColor.FromHsv(hue, 90, 100);
                    canvas.DrawRect(x, y, barWidth, barHeight, _paintBar);
                }
            }
        }

        public void Dispose()
        {
            _paintBar?.Dispose();
            _paintBar = null;
            _paintText?.Dispose();
            _paintText = null;
        }
    }
}