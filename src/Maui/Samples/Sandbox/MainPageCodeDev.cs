using DrawnUi.Views;
using Sandbox.Views;

namespace Sandbox
{
    public class MainPageCodeDev : BasePageCodeBehind, IDisposable
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

        //public class MyDataTemplateSelector : DataTemplateSelector
        //{
        //    protected override DataTemplate? OnSelectTemplate(object item, BindableObject container)
        //    {
        //        return new DataTemplate(() =>
        //        {
        //            return new SkiaLabel()
        //            {
        //                HeightRequest = 32, BackgroundColor = Colors.Red, HorizontalOptions = LayoutOptions.Fill
        //            };
        //        });
        //    }
        //}

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
                BackgroundColor = Colors.Green,

                //Content = new SkiaLayout()
                //{
                //    Type = LayoutType.Column,
                //    Spacing = 8,
                //    BackgroundColor = Colors.White,
                //    HorizontalOptions = LayoutOptions.Fill,
                //    ItemsSource = itemsSource,
                //    ItemTemplate = selector
                //}

                Content =
                    new SkiaLayout() //wrapper
                        {
                            VerticalOptions = LayoutOptions.Fill,
                            HorizontalOptions = LayoutOptions.Fill,
                            BackgroundColor = Colors.Gray
                        }
                        .WithChildren(

                            new SkiaShape()
                            {
                                CornerRadius = 6,
                                BevelType = BevelType.Bevel,
                                Bevel = new SkiaBevel()
                                {
                                    Depth = 4,
                                    LightColor = Colors.White,
                                    ShadowColor = Color.Parse("#333333"),
                                    Opacity = 0.75
                                },
                                HorizontalOptions = LayoutOptions.Center,
                                VerticalOptions = LayoutOptions.Center,
                                WidthRequest = 100,
                                HeightRequest = 40,
                                BackgroundColor = Colors.Yellow
                            },

                            //new SkiaShape()
                            //{
                            //    Type = ShapeType.Circle,
                            //    BevelType = BevelType.Bevel,
                            //    Bevel = new SkiaBevel()
                            //    {
                            //        Depth = 4,
                            //        LightColor = Colors.White,
                            //        ShadowColor = Colors.Black,
                            //        Opacity = 0.75
                            //    },
                            //    HorizontalOptions = LayoutOptions.Center,
                            //    VerticalOptions = LayoutOptions.Center,
                            //    WidthRequest = 100,
                            //    HeightRequest = 100,
                            //    BackgroundColor = Colors.Yellow
                            //},

                            //new SkiaLayout()
                            //{
                            //    Tag = "1",
                            //    BackgroundColor = Colors.Yellow,
                            //    VerticalOptions = LayoutOptions.Fill,
                            //    HorizontalOptions = LayoutOptions.Fill,
                            //    //Children = new List<SkiaControl>()
                            //    //{
                            //    //    //new SkiaRichLabel()
                            //    //    //{
                            //    //    //    Margin=16,
                            //    //    //    FontFamily="FontText",
                            //    //    //    FontSize=15,
                            //    //    //    Text="`CODE` xx",
                            //    //    //    TextColor=Colors.White,
                            //    //    //}.CenterX(),
                            //    //}
                            //},
                            //new SkiaLabel()
                            //{
                            //    Margin = new(8),
                            //    VerticalOptions = LayoutOptions.Center,
                            //    ZIndex = 100,
                            //    FontSize = 8,
                            //    TextColor = Colors.White,
                            //    BackgroundColor = Colors.Black,
                            //}.Adapt((c) =>
                            //{
                            //    c.SetBinding(SkiaLabel.TextProperty, static (WheelScroll x) => x.DebugWheel, source: wheel);
                            //}),
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
                        )
            };

            this.Content = Canvas;
        }
    }
}
