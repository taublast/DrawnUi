using DrawnUi.Camera;

namespace CameraTests
{
    /// <summary>
    /// Frequency band level visualizer (ULTRA LOW CPU: peak detection with simple heuristics)
    /// Uses 3-band crossover filters (Low/Mid/High) for accurate separation
    /// </summary>
    public class AudioLevels : IAudioVisualizer
    {
        private const int BandCount = 8;
        private float[] _levelsFrontBuffer = new float[BandCount];
        private float[] _levelsBackBuffer = new float[BandCount];
        private int _swapRequested = 0;

        // Filters - 2-pole cascaded for better separation
        private float _b1, _b2;
        private float _m1, _m2;
        private float _prevIn, _prevOut; // DC Blocker state

        private const float AttackCoeff = 0.3f;
        private const float ReleaseCoeff = 0.65f; // Faster release for mobile (was 0.85)

        // Ballistic smoothing (attack fast, release slow like analog VU)
        public bool UseGain { get; set; } = true;
        public int Skin { get; set; } = 0;

        private SKPaint _paintBar;
        private SKPaint _paintText;

        public void AddSample(AudioSample sample)
        {
            int sampleCount = sample.Data.Length / 2;
            int step = 1; // Analyze every sample for filter stability
            float gain = UseGain ? 5.0f : 1.0f; // Global boost (was 2.5)

            float energyLow = 0;
            float energyMid = 0;
            float energyHigh = 0;
            int count = 0;

            // Simple 1-pole crossover filters
            // Tuned for ~44.1kHz (adjusts automatically to sample rate somewhat efficiently)
            const float alphaLow = 0.05f; // ~400Hz cutoff
            const float alphaMid = 0.35f; // ~3000Hz cutoff

            for (int i = 0; i < sampleCount; i += step)
            {
                int byteIndex = i * 2;
                if (byteIndex + 1 < sample.Data.Length)
                {
                    short pcmValue = (short)(sample.Data[byteIndex] | (sample.Data[byteIndex + 1] << 8));
                    float raw = pcmValue / 32768f;

                    // DC Blocking (Standard recursive, R=0.995)
                    float val = raw - _prevIn + 0.995f * _prevOut;
                    _prevIn = raw;
                    _prevOut = val;

                    // Apply filters (2-pole cascaded)
                    _b1 += alphaLow * (val - _b1);
                    _b2 += alphaLow * (_b1 - _b2);

                    _m1 += alphaMid * (val - _m1);
                    _m2 += alphaMid * (_m1 - _m2);

                    float b = _b2;              // Bass
                    float m = _m2 - _b2;        // Mids
                    float t = val - _m2;        // Highs

                    energyLow += Math.Abs(b);
                    energyMid += Math.Abs(m);
                    energyHigh += Math.Abs(t);
                    count++;
                }
            }

            if (count > 0)
            {
                energyLow = (energyLow / count) * gain;
                energyMid = (energyMid / count) * gain;
                energyHigh = (energyHigh / count) * gain;
            }

            // Map 3 bands to 8 bars with interpolation
            // Gains adjusted to normalize visual output (highs need more boost)
            UpdateBand(0, energyLow * 2.2f);
            UpdateBand(1, energyLow * 2.0f);
            UpdateBand(2, (energyLow * 0.3f + energyMid * 0.7f) * 1.8f);
            UpdateBand(3, energyMid * 2.0f);
            UpdateBand(4, energyMid * 2.2f);
            UpdateBand(5, (energyMid * 0.4f + energyHigh * 0.6f) * 2.5f);
            UpdateBand(6, energyHigh * 3.5f);
            UpdateBand(7, energyHigh * 4.0f);

            System.Threading.Interlocked.Exchange(ref _swapRequested, 1);
        }

        private void UpdateBand(int band, float newValue)
        {
            float current = _levelsBackBuffer[band];
            float coeff = (newValue > current) ? AttackCoeff : ReleaseCoeff;
            _levelsBackBuffer[band] = Math.Clamp(
                current * coeff + newValue * (1f - coeff),
                0f, 1f);
        }

        public void Render(SKCanvas canvas, float width, float height, float scale, string recognizedText = null)
        {
            if (_paintBar == null)
            {
                _paintBar = new SKPaint
                {
                    Style = SKPaintStyle.Fill,
                    IsAntialias = false // Speed
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

            // Swap buffers
            if (System.Threading.Interlocked.CompareExchange(ref _swapRequested, 0, 1) == 1)
            {
                var temp = _levelsFrontBuffer;
                _levelsFrontBuffer = _levelsBackBuffer;
                _levelsBackBuffer = temp;
            }

            var barAreaWidth = width * 0.8f;
            var maxBarHeight = 180 * scale;
            var barWidth = (barAreaWidth / BandCount) * 0.8f; // 80% width, 20% gap
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

                // Color gradient based on band
                float hue = (i / (float)(BandCount - 1)) * 140; // 0=red ... 140=green/teal

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
