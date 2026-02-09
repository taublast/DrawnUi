using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DrawnUi.Camera;
using static CameraTests.Views.CameraTestPage;

namespace CameraTests.Visualizers
{
    public class AudioVisualizer : SkiaLayout
    {
        private int _visualizerIndex = 0;

        public static readonly BindableProperty VisualizerNameProperty = BindableProperty.Create(
            nameof(VisualizerName),
            typeof(string),
            typeof(AudioVisualizer),
            "None");

        public string VisualizerName
        {
            get => (string)GetValue(VisualizerNameProperty);
            set => SetValue(VisualizerNameProperty, value);
        }

        public IAudioVisualizer Visualizer { get; protected set; }

        public AudioVisualizer()
        {
            SwitchVisualizer(0);
        }

        protected override void Paint(DrawingContext ctx)
        {
            base.Paint(ctx);

            if (Visualizer != null)
            {
                Visualizer.Render(ctx.Context.Canvas, ctx.Destination.Width, ctx.Destination.Height, ctx.Scale);
            }
        }

        public override void OnDisposing()
        {
            base.OnDisposing();

            if (Visualizer is IDisposable disposable)
            {
                disposable.Dispose();
            }
            Visualizer = null;
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

            var old = Visualizer;
            bool useGain = true;

            switch (_visualizerIndex)
            {
                case 0:
                    Visualizer = new AudioSoundBars();
                    useGain = true;
                    VisualizerName = "Sound Bars";
                    break;
                case 1:
                    Visualizer = new AudioLevelsVU();
                    VisualizerName = "VU Meter";
                    break;
                case 2:
                    Visualizer = new AudioLevelsPeak();
                    VisualizerName = "Peak Monitor";
                    break;
                //case 2:
                //    Visualizer = new AudioLevels();
                //    VisualizerName = "Spectrum";
                //    break;
                case 3:
                    Visualizer = new AudioOscillograph();
                    useGain = true;
                    VisualizerName = "Oscillograph";
                    break;
                case 4:
                    Visualizer = new AudioRadialGauge();
                    VisualizerName = "Gauge";
                    break;
                case 5:
                    Visualizer = new AudioInstrumentTuner();
                    useGain = true;
                    VisualizerName = "Tuner";
                    break;
                case 6:
                    Visualizer = new AudioWaveformBars();
                    VisualizerName = "Waveform Bars";
                    break;
                case 8:
                    Visualizer = null;
                    VisualizerName = "None";
                    break;
            }

            if (Visualizer != null)
            {
                Visualizer.UseGain = useGain;
            }

            DisposeObject(old);
        }

        public void AddSample(AudioSample sample)
        {
            if (Visualizer != null)
            {
                Visualizer.AddSample(sample);
            }
        }
    }
}
