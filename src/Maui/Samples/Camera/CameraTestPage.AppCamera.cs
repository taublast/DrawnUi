using DrawnUi.Camera;

namespace CameraTests.Views
{
    public partial class CameraTestPage
    {
        public class AppCamera : SkiaCamera
        {
            // Audio visualizer (switch between AudioOscillograph and AudioLevels)
            private IAudioVisualizer _audioVisualizer = null;
            private int _visualizerIndex = 0;

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

            public void SwitchVisualizer(int index = -1)
            {
                if (index >= 0)
                {
                    _visualizerIndex = index;
                }
                else
                {
                    _visualizerIndex++;
                }
                if (_visualizerIndex > 8) _visualizerIndex = 0;

                var old = _audioVisualizer;
                bool useGain = true;

                switch (_visualizerIndex)
                {
                    case 0:
                        _audioVisualizer = new AudioSoundBars();
                        useGain = true;
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
                    //case 2:
                    //    _audioVisualizer = new AudioLevels();
                    //    VisualizerName = "Spectrum";
                    //    break;
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
                    _audioVisualizer.UseGain = useGain;
                }

                (old as IDisposable)?.Dispose();
            }

            public string RecognizedText { get; set; }

            protected override AudioSample OnAudioSampleAvailable(AudioSample sample)
            {
                _audioVisualizer?.AddSample(sample);

                return base.OnAudioSampleAvailable(sample);
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


    }
}
