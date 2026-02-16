using AppoMobi.Maui.Gestures;
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

        public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
        {
            if (args.Type == TouchActionResult.Tapped)
            {
                Visualizer?.Reset();

                Update();

                return this;
            }
            return base.ProcessGestures(args, apply);
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

        public void Reset()
        {
            if (Visualizer != null)
            {
                Visualizer.Reset();
                Update();
            }
        }

        protected override void UpdateInternal()
        {
            base.UpdateInternal();
        }
    }
}
