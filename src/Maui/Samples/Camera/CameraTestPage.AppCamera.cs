using DrawnUi.Camera;

namespace CameraTests.Views
{
    public partial class CameraTestPage
    {
        public class AppCamera : SkiaCamera
        {
            // Audio visualizer (switch between AudioOscillograph and AudioLevels)
            private IAudioVisualizer _audioVisualizer = new AudioLevelsVU();
            private int _visualizerIndex = 0;

            public static readonly BindableProperty VisualizerNameProperty = BindableProperty.Create(
                nameof(VisualizerName),
                typeof(string),
                typeof(AppCamera),
                "VU Meter");

            public string VisualizerName
            {
                get => (string)GetValue(VisualizerNameProperty);
                set => SetValue(VisualizerNameProperty, value);
            }

            public void SwitchVisualizer()
            {
                _visualizerIndex++;
                if (_visualizerIndex > 3) _visualizerIndex = 0;

                var old = _audioVisualizer;
                bool useGain = old?.UseGain ?? true;

                switch (_visualizerIndex)
                {
                    case 0:
                        _audioVisualizer = new AudioLevelsVU();
                        VisualizerName = "VU Meter";
                        break;
                    case 1:
                        _audioVisualizer = new AudioLevelsPeak();
                        VisualizerName = "Peak Monitor";
                        break;
                    case 2:
                        _audioVisualizer = new AudioLevels();
                        VisualizerName = "Spectrum";
                        break;
                    case 3:
                        _audioVisualizer = new AudioOscillograph();
                        VisualizerName = "Oscillograph";
                        break;
                }

                if (_audioVisualizer != null)
                {
                    _audioVisualizer.UseGain = useGain;
                }
                
                (old as IDisposable)?.Dispose();
            }

            public string RecognizedText { get; set; }

            protected override void OnAudioSampleReceived(AudioSample sample)
            {
                base.OnAudioSampleReceived(sample);

                _audioVisualizer?.AddSample(sample);
            }


            public override void OnWillDisposeWithChildren()
            {
                base.OnWillDisposeWithChildren();

                _paintRec?.Dispose();
                _paintRec = null;
                _paintPreview?.Dispose();
                _paintPreview = null;
                
                (_audioVisualizer as IDisposable)?.Dispose();
                _audioVisualizer = null;
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
                }
                else
                {
                    paint.Color = SKColors.White;
                    var text = $"PREVIEW {this.CaptureMode}";
                    canvas.DrawText(text, 50 * scale, 100 * scale, paint);
                }

                if (UseRealtimeVideoProcessing && RecordAudio)
                {
                    _audioVisualizer?.Render(canvas, width, height, scale, RecognizedText);
                }
            }

            private SKPaint _paintPreview;
            private SKPaint _paintRec;
        }

        /// <summary>
        /// Interface for audio visualizers
        /// </summary>
        public interface IAudioVisualizer
        {
            void AddSample(AudioSample sample);
            void Render(SKCanvas canvas, float width, float height, float scale, string recognizedText = null);
            bool UseGain { get; set; }
            int Skin { get; set; }
        }

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

        /// <summary>
        /// Frequency band level visualizer (ULTRA LOW CPU: peak detection with simple heuristics)
        /// Uses 3-band crossover filters (Low/Mid/High) for accurate separation
        /// </summary>
        public class AudioLevels : IAudioVisualizer, IDisposable
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
            private const float ReleaseCoeff = 0.85f;

            // Ballistic smoothing (attack fast, release slow like analog VU)
            public bool UseGain { get; set; } = true;
            public int Skin { get; set; } = 0;

            private SKPaint _paintBar;
            private SKPaint _paintText;

            public void AddSample(AudioSample sample)
            {
                int sampleCount = sample.Data.Length / 2;
                int step = 1; // Analyze every sample for filter stability
                float gain = UseGain ? 2.5f : 1.0f; // Global boost

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
            private const float DecayCoeff = 0.94f; // Fast decay

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
                float gain = UseGain ? 2.5f : 1.0f;

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

        /// <summary>
        /// Analog-style VU meter (classic ballistics with filter bank)
        /// Uses 3-band crossover for frequency separation.
        /// </summary>
        public class AudioLevelsVU : IAudioVisualizer, IDisposable
        {
            private const int BandCount = 8;
            private float[] _levelsFrontBuffer = new float[BandCount];
            private float[] _levelsBackBuffer = new float[BandCount];
            private int _swapRequested = 0;

            // Filters - 2-pole cascaded
            private float _b1, _b2;
            private float _m1, _m2;
            private float _prevIn, _prevOut; // DC Blocker state

            // Classic VU ballistics
            private const float AttackCoeff = 0.15f;   // Fast-ish attack
            private const float ReleaseCoeff = 0.92f;  // Slow release

            public bool UseGain { get; set; } = true;
            public int Skin { get; set; } = 0;

            private SKPaint _paintBar;
            private SKPaint _paintText;

            public void AddSample(AudioSample sample)
            {
                int sampleCount = sample.Data.Length / 2;
                int step = 1;
                float gain = UseGain ? 2.5f : 1.0f;

                float energyLow = 0;
                float energyMid = 0;
                float energyHigh = 0;
                int count = 0;

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

                        float b = _b2;
                        float m = _m2 - _b2;
                        float t = val - _m2;

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

                float[] targets = new float[BandCount];
                targets[0] = energyLow * 2.2f;
                targets[1] = energyLow * 2.0f;
                targets[2] = (energyLow * 0.3f + energyMid * 0.7f) * 1.8f;
                targets[3] = energyMid * 2.0f;
                targets[4] = energyMid * 2.2f;
                targets[5] = (energyMid * 0.4f + energyHigh * 0.6f) * 2.5f;
                targets[6] = energyHigh * 3.5f;
                targets[7] = energyHigh * 4.0f;

                // Apply VU ballistics
                for (int band = 0; band < BandCount; band++)
                {
                    float current = _levelsBackBuffer[band];
                    float coeff = (targets[band] > current) ? AttackCoeff : ReleaseCoeff;
                    _levelsBackBuffer[band] = Math.Clamp(
                        current * coeff + targets[band] * (1f - coeff),
                        0f, 1f);
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
}
