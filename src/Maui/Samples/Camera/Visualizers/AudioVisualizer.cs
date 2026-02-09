using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CameraTests.Visualizers
{
    public class AudioVisualizer : SkiaLayout
    {
        public IAudioVisualizer Visualizer { get; protected set; }

        public AudioVisualizer()
        {
            Visualizer = new AudioInstrumentTuner();
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
    }
}
