using DrawnUi.Camera;

namespace CameraTests
{
    /// <summary>
    /// Waveform oscillograph visualizer (ZERO allocations, perfect sync)
    /// </summary>
    public class AudioOscillograph : IAudioVisualizer, IDisposable
    {
        private float[] _audioFrontBuffer = new float[60];
        private float[] _audioBackBuffer = new float[60];
        private int _swapRequested = 0;
        private const int WaveformPoints = 60;

        public bool UseGain { get; set; } = true;
        public int Skin { get; set; } = 0;

        private SKPaint _paintWaveform;
        private SKPaint _paintText;

        public void AddSample(AudioSample sample)
        {
            var stepSize = sample.Data.Length / (WaveformPoints * 2);
            float gain = UseGain ? 4.0f : 1.0f;

            for (int i = 0; i < WaveformPoints; i++)
            {
                var byteIndex = i * stepSize * 2;
                if (byteIndex + 1 < sample.Data.Length)
                {
                    short pcmValue = (short)(sample.Data[byteIndex] | (sample.Data[byteIndex + 1] << 8));
                    var normalized = pcmValue / 32768f;
                    _audioBackBuffer[i] = Math.Clamp(normalized * gain, -1f, 1f);
                }
            }

            System.Threading.Interlocked.Exchange(ref _swapRequested, 1);
        }

        public void Render(SKCanvas canvas, float width, float height, float scale, string recognizedText = null)
        {
            if (_paintWaveform == null)
            {
                _paintWaveform = new SKPaint
                {
                    Color = SKColors.LimeGreen,
                    StrokeWidth = 2,
                    Style = SKPaintStyle.Stroke,
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

            // Swap buffers if audio thread signaled new data
            if (System.Threading.Interlocked.CompareExchange(ref _swapRequested, 0, 1) == 1)
            {
                var temp = _audioFrontBuffer;
                _audioFrontBuffer = _audioBackBuffer;
                _audioBackBuffer = temp;
            }

            var oscWidth = width * 0.8f;
            var oscHeight = 150 * scale;
            var oscX = (width - oscWidth) / 2;
            var oscY = height - oscHeight - 40 * scale;
            var centerY = oscY + oscHeight / 2;

            if (!string.IsNullOrEmpty(recognizedText))
            {
                _paintText.TextSize = 32 * scale;
                canvas.DrawText(recognizedText, oscX + oscWidth / 2, oscY - 20 * scale, _paintText);
            }

            // Background
            _paintWaveform.Style = SKPaintStyle.Fill;
            _paintWaveform.Color = SKColors.Black.WithAlpha(128);
            canvas.DrawRect(oscX - 10, oscY - 10, oscWidth + 20, oscHeight + 20, _paintWaveform);

            // Center line
            _paintWaveform.Style = SKPaintStyle.Stroke;
            _paintWaveform.Color = SKColors.Gray.WithAlpha(128);
            _paintWaveform.StrokeWidth = 1;
            canvas.DrawLine(oscX, centerY, oscX + oscWidth, centerY, _paintWaveform);

            // Waveform
            _paintWaveform.Color = SKColors.LimeGreen;
            _paintWaveform.StrokeWidth = 2;

            var stepX = oscWidth / (WaveformPoints - 1);
            for (int i = 0; i < WaveformPoints - 1; i++)
            {
                var x1 = oscX + i * stepX;
                var y1 = centerY - (_audioFrontBuffer[i] * oscHeight / 2);
                var x2 = oscX + (i + 1) * stepX;
                var y2 = centerY - (_audioFrontBuffer[i + 1] * oscHeight / 2);

                canvas.DrawLine(x1, y1, x2, y2, _paintWaveform);
            }
        }

        public void Dispose()
        {
            _paintWaveform?.Dispose();
            _paintWaveform = null;
            _paintText?.Dispose();
            _paintText = null;
        }
    }
}