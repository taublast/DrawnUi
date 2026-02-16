using DrawnUi.Camera;
using MusicNotes.UI;

namespace MusicNotes.Audio
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

        public void Reset()
        {
            Array.Clear(_audioFrontBuffer, 0, _audioFrontBuffer.Length);
            Array.Clear(_audioBackBuffer, 0, _audioBackBuffer.Length);
            _swapRequested = 0;
        }

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

        public void Render(SKCanvas canvas, SKRect viewport, float scale)
        {
            if (viewport.Width <= 0 || viewport.Height <= 0)
                return;

            //canvas.Save();
            //canvas.Translate(viewport.Left, viewport.Top);
            //canvas.ClipRect(new SKRect(0, 0, viewport.Width, viewport.Height));

            float width = viewport.Width;
            float height = viewport.Height;

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

            var oscWidth = width;
            var oscHeight = height * 0.8f;
            var oscX = viewport.Left;
            var centerY = viewport.Top + height / 2f;

            // Background
            _paintWaveform.Style = SKPaintStyle.Fill;
            _paintWaveform.Color = SKColors.Black.WithAlpha(128);
            canvas.DrawRect(viewport.Left, viewport.Top, width, height, _paintWaveform);

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

            //canvas.Restore();
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
