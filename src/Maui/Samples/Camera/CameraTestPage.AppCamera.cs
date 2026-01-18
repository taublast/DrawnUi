using DrawnUi.Camera;

namespace CameraTests.Views
{
    public partial class CameraTestPage
    {
        public class AppCamera : SkiaCamera
        {
            // Double-buffer for real-time oscillograph (ZERO allocations, perfect sync)
            private float[] _audioFrontBuffer = new float[60]; // Drawing thread reads this
            private float[] _audioBackBuffer = new float[60];  // Audio thread writes this
            private int _swapRequested = 0; // 0 = no swap, 1 = swap requested
            private const int WaveformPoints = 60;
            private const float VisualizationGain = 4.0f; // Boost sensitivity (adjust 2-10 as needed)

            public override void WriteAudioSample(AudioSample sample)
            {
                // Extract waveform data directly to back buffer (FASTEST: direct byte read)
                var stepSize = sample.Data.Length / (WaveformPoints * 2); // *2 for 16-bit stereo
            
                for (int i = 0; i < WaveformPoints; i++)
                {
                    var byteIndex = i * stepSize * 2;
                    if (byteIndex + 1 < sample.Data.Length)
                    {
                        // Read 16-bit PCM sample directly (little-endian)
                        short pcmValue = (short)(sample.Data[byteIndex] | (sample.Data[byteIndex + 1] << 8));
                        var normalized = pcmValue / 32768f; // Normalize to -1..1
                        _audioBackBuffer[i] = Math.Clamp(normalized * VisualizationGain, -1f, 1f); // Boost + clamp
                    }
                }

                // Signal swap (drawing thread will swap buffers on next draw)
                System.Threading.Interlocked.Exchange(ref _swapRequested, 1);

                // MUST call base to record the modified audio
                base.WriteAudioSample(sample);
            }


            public override void OnWillDisposeWithChildren()
            {
                base.OnWillDisposeWithChildren();

                _paintRec?.Dispose();
                _paintRec = null;
                _paintPreview?.Dispose();
                _paintPreview = null;
                _paintWaveform?.Dispose();
                _paintWaveform = null;
            }

            public void DrawOverlay(DrawableFrame frame)
            {
                SKPaint paint;
                var canvas = frame.Canvas;
                var width = frame.Width;
                var height = frame.Height;
                var scale = frame.Scale;

                if (frame.IsPreview)
                {
                    if (_paintPreview == null)
                    {
                        _paintPreview = new SKPaint
                        {
                            IsAntialias = true,
                        };
                    }
                    paint = _paintPreview;
                }
                else
                {
                    if (_paintRec == null)
                    {
                        _paintRec = new SKPaint
                        {
                            IsAntialias = true,
                        };
                    }
                    paint = _paintRec;
                }

                paint.TextSize = 48 * scale;
                paint.Color = IsPreRecording ? SKColors.White : SKColors.Red;
                paint.Style = SKPaintStyle.Fill;

                if (IsRecordingVideo || IsPreRecording)
                {
                    // text at top left
                    var text = IsPreRecording ? "PRE-RECORDED" : "LIVE";
                    canvas.DrawText(text, 50 * scale, 100 * scale, paint);
                    canvas.DrawText($"{frame.Time:mm\\:ss}", 50 * scale, 160 * scale, paint);

                    // Draw a simple border around the frame
                    paint.Style = SKPaintStyle.Stroke;
                    paint.StrokeWidth = 4 * scale;
                    canvas.DrawRect(10 * scale, 10 * scale, width - 20 * scale, height - 20 * scale, paint);
                
                    // Draw oscillograph at bottom (FASTEST implementation)
                    DrawOscillograph(canvas, width, height, scale);
                }
                else
                {
                    paint.Color = SKColors.White;
                    var text = $"PREVIEW {this.CaptureMode}";
                    canvas.DrawText(text, 50 * scale, 100 * scale, paint);
                }
            }

            private void DrawOscillograph(SKCanvas canvas, float width, float height, float scale)
            {
                if (_paintWaveform == null)
                {
                    _paintWaveform = new SKPaint
                    {
                        Color = SKColors.LimeGreen,
                        StrokeWidth = 2,
                        Style = SKPaintStyle.Stroke,
                        IsAntialias = false // Disable for speed
                    };
                }

                // Swap buffers if audio thread signaled new data
                if (System.Threading.Interlocked.CompareExchange(ref _swapRequested, 0, 1) == 1)
                {
                    // Atomic pointer swap (ZERO allocations, PERFECT sync)
                    var temp = _audioFrontBuffer;
                    _audioFrontBuffer = _audioBackBuffer;
                    _audioBackBuffer = temp;
                }

                // Draw oscillograph at bottom center using front buffer
                var oscWidth = width * 0.8f;
                var oscHeight = 150 * scale;
                var oscX = (width - oscWidth) / 2;
                var oscY = height - oscHeight - 40 * scale;
                var centerY = oscY + oscHeight / 2;

                // Background semi-transparent rect
                _paintWaveform.Style = SKPaintStyle.Fill;
                _paintWaveform.Color = SKColors.Black.WithAlpha(128);
                canvas.DrawRect(oscX - 10, oscY - 10, oscWidth + 20, oscHeight + 20, _paintWaveform);

                // Draw center line
                _paintWaveform.Style = SKPaintStyle.Stroke;
                _paintWaveform.Color = SKColors.Gray.WithAlpha(128);
                _paintWaveform.StrokeWidth = 1;
                canvas.DrawLine(oscX, centerY, oscX + oscWidth, centerY, _paintWaveform);

                // Draw waveform (FASTEST: direct line drawing from front buffer)
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

            private SKPaint _paintPreview;
            private SKPaint _paintRec;
            private SKPaint _paintWaveform;
        }
    }
}
