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
                ctx.Context.Canvas.Save();
                ctx.Context.Canvas.Translate(ctx.Destination.Left, ctx.Destination.Top);
                Visualizer.Render(ctx.Context.Canvas, ctx.Destination.Width, ctx.Destination.Height, ctx.Scale);
                ctx.Context.Canvas.Restore();
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


        public string SwitchVisualizer(int index = -1)
        {
            if (_visualizerIndex > 8 || _visualizerIndex < -1)
            {
                _visualizerIndex = 0;
            }
            else
            {
                _visualizerIndex++;
            }

            var old = Visualizer;

            switch (_visualizerIndex)
            {
                case 0:
                    Visualizer = new AudioSoundBars();
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
                case 3:
                    Visualizer = new AudioOscillograph();
                    VisualizerName = "Oscillograph";
                    break;
                case 4:
                    Visualizer = new AudioRadialGauge();
                    VisualizerName = "Gauge";
                    break;
                case 5:
                    Visualizer = new AudioInstrumentTuner();
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
                // Gain is applied upstream in OnAudioSampleAvailable, visualizers get pre-amplified signal
                Visualizer.UseGain = false;
            }

            DisposeObject(old);

            Update();

            return VisualizerName;
        }

        public void AddSample(AudioSample sample)
        {
            if (Visualizer != null)
            {
                Visualizer.AddSample(sample);
                Update();
            }
        }

        protected override void UpdateInternal()
        {
            base.UpdateInternal();
        }
    }
}
