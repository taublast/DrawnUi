using DrawnUi.Camera;
using MusicNotes.UI;

namespace MusicNotes.Audio
{
    public class AudioVisualizer : SkiaLayout
    {
 


        public IAudioVisualizer Visualizer { get; protected set; }

        public AudioVisualizer(IAudioVisualizer visualizer)
        {
            UseCache = SkiaCacheType.Operations;
            Visualizer = visualizer;
        }

        protected override void Paint(DrawingContext ctx)
        {
            base.Paint(ctx);

            if (Visualizer != null)
            {
                Visualizer.Render(ctx.Context.Canvas, DrawingRect, ctx.Scale);
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
