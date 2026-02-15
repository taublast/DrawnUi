using CameraTests.Visualizers;
using DrawnUi.Camera;

namespace CameraTests.UI
{
    public interface IAppOverlay
    {
        void AddAudioSample(AudioSample sample);

        /// <summary>
        /// Return the name of the visualizer switched to, or null if no visualizer was switched to
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public string SwitchVisualizer(int index = -1);
    }

    //public class HUD
    //{
    //    public static AudioVisualizer Visualizer;

    //    static HUD()
    //    {
    //        Visualizer = new AudioVisualizer()
    //        {
    //            BackgroundColor = Colors.YellowGreen,
    //            WidthRequest = 200,
    //            HeightRequest = 100,
    //            VerticalOptions = LayoutOptions.Start,
    //            HorizontalOptions = LayoutOptions.End,
    //        };

    //    }
    //    public static void SwitchVisualizer(int index = -1)
    //    {
    //        Visualizer?.SwitchVisualizer(index);
    //    }

    //    public static void AddAudioSample(AudioSample sample)
    //    {
    //        Visualizer?.AddSample(sample);
    //    }
    //}

    public class FrameOverlay : SkiaLayer, IAppOverlay
    {
        public AudioVisualizer Visualizer;

        public FrameOverlay()
        {
            UseCache = SkiaCacheType.Operations;
            VerticalOptions = LayoutOptions.Fill;
            Children = new List<SkiaControl>()
            {
                new AudioVisualizer()
                {
                    Margin=16,
                    WidthRequest = 150,
                    HeightRequest = 80,
                    VerticalOptions = LayoutOptions.Start,
                    HorizontalOptions = LayoutOptions.End,
                }.Assign(out Visualizer)
            };
        }

        public void AddAudioSample(AudioSample sample)
        {
            Visualizer?.AddSample(sample);
        }

        public string SwitchVisualizer(int index = -1)
        {
            return Visualizer?.SwitchVisualizer(index);
        }

        protected override void Paint(DrawingContext ctx)
        {
            base.Paint(ctx);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
        }

        protected override bool UseRenderingObject(DrawingContext context, SKRect recordArea)
        {
            var ret = base.UseRenderingObject(context, recordArea);

            return ret;
        }

        public override void UpdateByChild(SkiaControl child)
        {
            base.UpdateByChild(child);
        }
    }

}
