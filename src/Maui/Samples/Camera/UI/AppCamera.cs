using DrawnUi.Camera;

namespace CameraTests.Views
{
    public partial class AppCamera : SkiaCamera
    {
        public AppCamera()
        {
            //set defaults for this camera

            //GPS metadata
            this.InjectGpsLocation = true;

            //audio 
            this.EnableAudioMonitoring = true;
            SwitchVisualizer();
        }

        // Audio visualizer (switch between AudioOscillograph and AudioLevels)
        private IAudioVisualizer _audioVisualizer = null;
        private int _visualizerIndex = -1;

        public static readonly BindableProperty VisualizerNameProperty = BindableProperty.Create(
            nameof(VisualizerName),
            typeof(string),
            typeof(AppCamera),
            "None");

        public string VisualizerName
        {
            get => (string)GetValue(VisualizerNameProperty);
            set => SetValue(VisualizerNameProperty, value);
        }

        public static readonly BindableProperty UseGainProperty = BindableProperty.Create(
            nameof(UseGain),
            typeof(bool),
            typeof(AppCamera),
            false);

        public bool UseGain
        {
            get => (bool)GetValue(UseGainProperty);
            set => SetValue(UseGainProperty, value);
        }

        /// <summary>
        /// Gain multiplier applied to raw PCM when UseGain is true.
        /// </summary>
        public float GainFactor { get; set; } = 3.0f;

        public void SwitchVisualizer(int index = -1)
        {
            if (_visualizerIndex > 8 || _visualizerIndex < -1)
            {
                _visualizerIndex = 0;
            }
            else
            {
                _visualizerIndex++;
            }

            var old = _audioVisualizer;

            switch (_visualizerIndex)
            {
                case 0:
                    _audioVisualizer = new AudioSoundBars();
                    VisualizerName = "Sound Bars";
                    break;
                case 1:
                    _audioVisualizer = new AudioLevelsVU();
                    VisualizerName = "VU Meter";
                    break;
                case 2:
                    _audioVisualizer = new AudioLevelsPeak();
                    VisualizerName = "Peak Monitor";
                    break;
                case 3:
                    _audioVisualizer = new AudioOscillograph();
                    VisualizerName = "Oscillograph";
                    break;
                case 4:
                    _audioVisualizer = new AudioRadialGauge();
                    VisualizerName = "Gauge";
                    break;
                case 5:
                    _audioVisualizer = new AudioInstrumentTuner();
                    VisualizerName = "Tuner";
                    break;
                case 6:
                    _audioVisualizer = new AudioWaveformBars();
                    VisualizerName = "Waveform Bars";
                    break;
                case 8:
                    _audioVisualizer = null;
                    VisualizerName = "None";
                    break;
            }

            if (_audioVisualizer != null)
            {
                // Gain is applied upstream in OnAudioSampleAvailable, visualizers get pre-amplified signal
                _audioVisualizer.UseGain = false;
            }

            (old as IDisposable)?.Dispose();
        }

        protected override AudioSample OnAudioSampleAvailable(AudioSample sample)
        {
            if (UseGain && sample.Data != null && sample.Data.Length > 1)
            {
                AmplifyPcm16(sample.Data, GainFactor);
            }

            _audioVisualizer?.AddSample(sample);

            return base.OnAudioSampleAvailable(sample);
        }

        /// <summary>
        /// Amplifies PCM16 audio data in-place. Zero allocations.
        /// </summary>
        private static void AmplifyPcm16(byte[] data, float gain)
        {
            for (int i = 0; i < data.Length - 1; i += 2)
            {
                int sample = (short)(data[i] | (data[i + 1] << 8));
                sample = (int)(sample * gain);

                // Clamp to 16-bit range
                if (sample > 32767) sample = 32767;
                else if (sample < -32768) sample = -32768;

                data[i] = (byte)(sample & 0xFF);
                data[i + 1] = (byte)((sample >> 8) & 0xFF);
            }
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

            if (IsRecording || IsPreRecording)
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

            if (UseRealtimeVideoProcessing && EnableAudioRecording)
            {
                _audioVisualizer?.Render(canvas, width, height, scale);
            }
        }

        private SKPaint _paintPreview;
        private SKPaint _paintRec;
    }
}
