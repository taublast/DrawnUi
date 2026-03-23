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
            UseCache = SkiaCacheType.Image;
            VerticalOptions = LayoutOptions.Fill;
            Children = new List<SkiaControl>()
            {
                new SkiaShape()
                {
                    Type = ShapeType.Rectangle,
                    Margin = 16,
                    Padding = new Thickness(12, 10, 12, 12),
                    WidthRequest = 220,
                    HeightRequest = 128,
                    CornerRadius = 22,
                    BackgroundColor = Color.FromArgb("#A60B1220"),
                    StrokeWidth = 1,
                    StrokeColor = Color.FromArgb("#3311C5BF"),
                    VerticalOptions = LayoutOptions.Start,
                    HorizontalOptions = LayoutOptions.End,
                    Children =
                    {
                        new SkiaLabel("Audio Monitor")
                        {
                            FontSize = 12,
                            CharacterSpacing = 1,
                            TextColor = Color.FromArgb("#7DEAE5"),
                            UseCache = SkiaCacheType.Operations,
                            HorizontalOptions = LayoutOptions.Start,
                            VerticalOptions = LayoutOptions.Start,
                        },
                        new SkiaLabel()
                        {
                            Margin = new Thickness(0, 18, 0, 0),
                            FontSize = 12,
                            TextColor = Color.FromArgb("#A7B5C6"),
                            UseCache = SkiaCacheType.Operations,
                            HorizontalOptions = LayoutOptions.Start,
                            VerticalOptions = LayoutOptions.Start,
                        }
                        .ObserveProperty(() => Visualizer, nameof(Visualizer.VisualizerName), me =>
                        {
                            me.Text = Visualizer?.VisualizerName ?? "None";
                        }),
                        new AudioVisualizer()
                        {
                            Margin = new Thickness(0, 42, 0, 0),
                            HorizontalOptions = LayoutOptions.Fill,
                            VerticalOptions = LayoutOptions.Fill,
                        }
                        .Assign(out Visualizer)
                    }
                }
            };
        }

        public void AddAudioSample(AudioSample sample)
        {
            if (Visualizer != null && Visualizer.IsVisible)
            {
                Visualizer?.AddSample(sample);
            }
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
