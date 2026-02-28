using DrawnUi.Camera;

namespace CameraTests
{
    /// <summary>
    /// Radial Gauge Visualizer (Speedometer style)
    /// Low CPU: Computes single energy value + simple drawing
    /// </summary>
    public class AudioRadialGauge : IAudioVisualizer, IDisposable
    {
        private float _levelFront = 0f;
        private float _levelBack = 0f;
        private int _swapRequested = 0;

        // Ballistics
        private const float AttackCoeff = 0.3f;
        private const float ReleaseCoeff = 0.65f; // Faster release for mobile (was 0.9)

        public bool UseGain { get; set; } = true;
        public int Skin { get; set; } = 0;

        private SKPaint _paintArc;
        private SKPaint _paintNeedle;
        private SKPaint _paintText;
        private SKShader _gradient;

        public void AddSample(AudioSample sample)
        {
            int step = 2;
            float sum = 0;
            int count = 0;
            float gain = UseGain ? 5.0f : 1.0f;

            for (int i = 0; i < sample.Data.Length; i += step * 2)
            {
                if (i + 1 < sample.Data.Length)
                {
                    short pcm = (short)(sample.Data[i] | (sample.Data[i + 1] << 8));
                    sum += Math.Abs(pcm / 32768f);
                    count++;
                }
            }

            float instantaneous = 0;
            if (count > 0)
                instantaneous = (sum / count) * gain;

            // Physics
            if (instantaneous > _levelBack)
                _levelBack = _levelBack + (instantaneous - _levelBack) * AttackCoeff;
            else
                _levelBack = _levelBack * ReleaseCoeff;

            _levelBack = Math.Clamp(_levelBack, 0f, 1.2f); // Overdrive allowed

            System.Threading.Interlocked.Exchange(ref _swapRequested, 1);
        }

        public void Render(SKCanvas canvas, SKRect viewport, float scale)
        {
            if (viewport.Width <= 0 || viewport.Height <= 0)
                return;

            float width = viewport.Width;
            float height = viewport.Height;
            float left = viewport.Left;
            float top = viewport.Top;

            if (_paintArc == null)
            {
                _paintArc = new SKPaint
                {
                    Style = SKPaintStyle.Stroke,
                    IsAntialias = true,
                    StrokeCap = SKStrokeCap.Round
                };

                _paintNeedle = new SKPaint
                {
                    Color = SKColors.OrangeRed,
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true
                };
            }

            if (_paintText == null)
            {
                _paintText = new SKPaint
                {
                    Color = SKColors.Cyan,
                    IsAntialias = true,
                    TextAlign = SKTextAlign.Center,
                    TextSize = 32 * scale
                };
            }

            if (System.Threading.Interlocked.CompareExchange(ref _swapRequested, 0, 1) == 1)
            {
                _levelFront = _levelBack;
            }

            var minDim = Math.Min(width, height);
            var stroke = Math.Min(30 * scale, minDim * 0.12f);
            if (stroke < 1f) stroke = 1f;
            _paintArc.StrokeWidth = stroke;

            var radius = (minDim - stroke) / 2f;
            if (radius < 1f) radius = 1f;

            var cx = left + width / 2f;
            var cy = top + height / 2f;

            var rect = new SKRect(cx - radius, cy - radius, cx + radius, cy + radius);

            // Draw Background Arc (Dark)
            _paintArc.Color = SKColors.DarkSlateGray.WithAlpha(100);
            _paintArc.Shader = null;
            canvas.DrawArc(rect, 180, 180, false, _paintArc);

            // Draw Active Arc (Gradient)
            if (_gradient == null)
            {
                _gradient = SKShader.CreateLinearGradient(
                    new SKPoint(cx - radius, cy),
                    new SKPoint(cx + radius, cy),
                    new SKColor[] { SKColors.Lime, SKColors.Yellow, SKColors.Red },
                    new float[] { 0.0f, 0.5f, 1.0f },
                    SKShaderTileMode.Clamp
                );
            }

            _paintArc.Shader = _gradient;
            _paintArc.Color = SKColors.White; // modulation

            // Map level to angle
            float sweep = _levelFront * 180f;
            if (sweep > 180) sweep = 180;

            if (sweep > 1)
                canvas.DrawArc(rect, 180, sweep, false, _paintArc);

            // Needle
            canvas.Save();
            float needleAngle = 180 + sweep; // Start at Left (180), rotate clockwise
            canvas.RotateDegrees(needleAngle, cx, cy);

            // Draw triangular needle pointing RIGHT (angle 0 relative to rotation)
            var path = new SKPath();
            var needleHalfThickness = Math.Max(2f * scale, radius * 0.08f);
            var needleTipInset = Math.Max(2f * scale, radius * 0.05f);
            path.MoveTo(cx, cy - needleHalfThickness);
            path.LineTo(cx, cy + needleHalfThickness);
            path.LineTo(cx + radius - needleTipInset, cy); // Points Right (which is needleAngle direction)
            path.Close();

            canvas.DrawPath(path, _paintNeedle);
            canvas.DrawCircle(cx, cy, 15 * scale, _paintNeedle); // Center cap

            canvas.Restore();

        }

        public void Dispose()
        {
            _paintArc?.Dispose();
            _paintArc = null;
            _paintNeedle?.Dispose();
            _paintNeedle = null;
            _paintText?.Dispose();
            _paintText = null;
            _gradient?.Dispose();
            _gradient = null;
        }
    }
}
