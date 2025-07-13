using DrawnUi.Views;
using Sandbox.Views;

namespace Sandbox
{
    public class SkiaStack : SkiaLayout
    {
        public SkiaStack()
        {
            Type = LayoutType.Column;
            HorizontalOptions = LayoutOptions.Fill;
            Spacing = 8;
        }
    }

    public class MainPageCodeLottie : BasePageCodeBehind, IDisposable
    {
        Canvas Canvas;

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                this.Content = null;
                Canvas?.Dispose();
            }

            base.Dispose(isDisposing);
        }

        private SkiaLottie Lottie;

        public override void Build()
        {
            Canvas?.Dispose();

            Canvas = new Canvas()
            {
                WidthRequest = 210,
                HorizontalOptions = LayoutOptions.Center,
                Gestures = GesturesMode.Lock,
                RenderingMode = RenderingModeType.Accelerated,
                VerticalOptions = LayoutOptions.Fill,
                HeightRequest = 250,
                BackgroundColor = Colors.DarkBlue,
                Content =
                    new SkiaLayout()
                        {
                            VerticalOptions = LayoutOptions.Fill,
                            HorizontalOptions = LayoutOptions.Fill,
                            Padding = 8,
                            Children = new List<SkiaControl>()
                            {
                                new SkiaStack()
                                {
                                    new SkiaLottie()
                                    {
                                        AutoPlay = false,
                                        WidthRequest = 110,
                                        LockRatio = 1,
                                        SpeedRatio = 0.6f,
                                        Repeat = 0,
                                        DefaultFrame = -1, // Frame to display when not playing. Default is 0. If you set to -1 that would mean "last frame".
                                        UseCache = SkiaCacheType.ImageDoubleBuffered,
                                        Source = $"Space/Lottie/crash.json"
                                    }.Assign(out Lottie),

                                    new SkiaButton()
                                    {
                                        Text = "Toggle Play", ControlStyle = PrebuiltControlStyle.Cupertino,
                                    }.OnTapped((me) =>
                                    {
                                        if (Lottie.IsPlaying)
                                        {
                                            Lottie.Stop();
                                        }
                                        else
                                        {
                                            Lottie.Start();
                                        }
                                    })
                                },
#if DEBUG
                                new SkiaLabelFps()
                                {
                                    Margin = new(0, 0, 4, 24),
                                    VerticalOptions = LayoutOptions.End,
                                    HorizontalOptions = LayoutOptions.End,
                                    Rotation = -45,
                                    BackgroundColor = Colors.DarkRed,
                                    TextColor = Colors.White,
                                    ZIndex = 110,
                                }
#endif
                            }
                        }
   
            };

            this.Content = Canvas;
        }
    }
}
