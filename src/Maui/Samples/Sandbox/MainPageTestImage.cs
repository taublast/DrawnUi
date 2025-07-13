using Sandbox.Views;
using Canvas = DrawnUi.Views.Canvas;

namespace Sandbox
{
    public class MainPageTestImage : BasePageCodeBehind, IDisposable
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

        public override void Build()
        {
            Canvas?.Dispose();

            Canvas = new Canvas()
            {
                Gestures = GesturesMode.Enabled,
                VerticalOptions = LayoutOptions.Fill,
                HorizontalOptions = LayoutOptions.Fill,
                BackgroundColor = Colors.LightGray,
                Content = new SkiaLayout()
                {
                    VerticalOptions = LayoutOptions.Fill,
                    HorizontalOptions = LayoutOptions.Fill,
                    Children = new List<SkiaControl>()
                    {
                        new SkiaLayout()
                        {
                            Children = new List<SkiaControl>()
                            {
                                new SkiaShape()
                                {
                                    VerticalOptions = LayoutOptions.Center,
                                    HorizontalOptions = LayoutOptions.Center,
                                    WidthRequest = 150,
                                    HeightRequest = 150,
                                    Type = ShapeType.Polygon,
                                    StrokeColor = Colors.Black,
                                    StrokeWidth = 3,
                                    BackgroundColor = Colors.Red,
                                    Points = new List<SkiaPoint>()
                                    {
                                        new(0.1f, 0.1f), new(0.9f, 0.1f), new(0.1f, 0.5f),
                                    },
                                    Children = new List<SkiaControl>()
                                    {
                                        new SkiaImage()
                                        {
                                            Source = "cap.png",
                                            Aspect = TransformAspect.AspectFit,
                                            VerticalOptions = LayoutOptions.Center,
                                            HorizontalOptions = LayoutOptions.Center
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };


            this.Content = Canvas;
        }
    }
}
