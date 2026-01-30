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
                if (_visualizerIndex > 6) _visualizerIndex = 0;

                var old = _audioVisualizer;
                bool useGain = false; 

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
                        useGain = true;
                        VisualizerName = "Oscillograph";
                        break;
                    case 4:
                        _audioVisualizer = new AudioRadialGauge();
                        VisualizerName = "Gauge";
                        break;
                    case 5:
                        _audioVisualizer = new AudioInstrumentTuner();
                        useGain = true;
                        VisualizerName = "Tuner";
                        break;
                    case 6:
                        _audioVisualizer = null;
                        VisualizerName = "None";
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
                float gain = UseGain ? 3.0f : 1.0f;

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

            public void Render(SKCanvas canvas, float width, float height, float scale, string recognizedText = null)
            {
                if (_paintArc == null)
                {
                    _paintArc = new SKPaint
                    {
                        Style = SKPaintStyle.Stroke,
                        StrokeWidth = 30 * scale,
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

                var cx = width / 2;
                var cy = height - 60 * scale;
                var radius = 100 * scale;

                // Draw Text
                if (!string.IsNullOrEmpty(recognizedText))
                {
                    canvas.DrawText(recognizedText, cx, cy - radius - 60 * scale, _paintText);
                }

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
                path.MoveTo(cx, cy - 10 * scale);
                path.LineTo(cx, cy + 10 * scale);
                path.LineTo(cx + radius - 10 * scale, cy); // Points Right (which is needleAngle direction)
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
                for(int i=0; i<BufferSize; i++)
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
                    }

                    // Calculate cents relative to the STABLE note (so needle shows true drift)
                    float targetFreq = 440.0f * (float)Math.Pow(2, (_currentMidiNote - 69) / 12.0f);
                    if (targetFreq > 0)
                    {
                        float rawCents = 1200 * (float)Math.Log2(_currentFrequency / targetFreq);
                        
                        _centsBuffer.Add(rawCents);
                        if (_centsBuffer.Count > 10) _centsBuffer.RemoveAt(0);

                        float sum = 0;
                        foreach(var c in _centsBuffer) sum += c;
                        _currentCents = sum / _centsBuffer.Count;
                    }
                }
            }

            public void Render(SKCanvas canvas, float width, float height, float scale, string recognizedText = null)
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
                    canvas.DrawLine(cx - barWidth/2, barY, cx + barWidth/2, barY, _paintGauge);
                    canvas.DrawLine(cx, barY - 15*scale, cx, barY + 15*scale, _paintGauge); // Center tick
                    
                    // Gauge Needle
                    float offset = (_displayCents / 50.0f) * (barWidth / 2); 
                    offset = Math.Clamp(offset, -barWidth/2, barWidth/2);
                    
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
            private const float AttackCoeff = 0.3f;    // Fast attack (was 0.15)
            private const float ReleaseCoeff = 0.65f;  // Faster release for mobiles (was 0.92)

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
