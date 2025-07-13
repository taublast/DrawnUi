using AppoMobi.Maui.Gestures;
using Sandbox.Views;
using Canvas = DrawnUi.Views.Canvas;

namespace Sandbox
{
    public class MainPageCodeForVideo : BasePageCodeBehind, IDisposable
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


        private int _counter;
        private SkiaLabel _label;

        public override void Build()
        {
            Canvas?.Dispose();

            Canvas = new Canvas()
            {
                Gestures = GesturesMode.Enabled,
                BackgroundColor = Colors.LightGray,
                Content = new SkiaLayout()
                {
                    UseCache = SkiaCacheType.Image,
                    Children = new List<SkiaControl>()
                    {
                        new SkiaShape()
                        {
                            MinimumWidthRequest = 200,
                            Margin = 10,
                            StrokeColor = Colors.Black,
                            StrokeWidth = 1,
                            CornerRadius = 16,
                            BackgroundColor = Colors.CadetBlue,
                            Padding = new Thickness(16, 8),
                            Children = new List<SkiaControl>()
                            {
                                new SkiaLayout()
                                {
                                    Spacing = 12,
                                    HorizontalOptions = LayoutOptions.Center,
                                    Type = LayoutType.Row,
                                    Children = new List<SkiaControl>()
                                    {
                                        new SkiaSvg()
                                        {
                                            WidthRequest = 18,
                                            LockRatio = 1,
                                            SvgString =
                                                "<svg height=\"800px\" width=\"800px\" version=\"1.1\" id=\"Capa_1\" xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" \r\n\t viewBox=\"0 0 52.93 52.93\" xml:space=\"preserve\">\r\n<g>\r\n\t<circle style=\"fill:#010002;\" cx=\"26.465\" cy=\"25.59\" r=\"4.462\"/>\r\n\t<path style=\"fill:#010002;\" d=\"M52.791,32.256c-0.187-1.034-1.345-2.119-2.327-2.492l-2.645-1.004\r\n\t\tc-0.982-0.373-1.699-1.237-1.651-1.935c0.029-0.417,0.046-0.838,0.046-1.263c0-0.284-0.008-0.566-0.021-0.846\r\n\t\tc-0.023-0.467,0.719-1.193,1.677-1.624l2.39-1.074c0.958-0.432,2.121-1.565,2.194-2.613c0.064-0.929-0.047-2.196-0.648-3.765\r\n\t\tc-0.699-1.831-1.834-3.005-2.779-3.718c-0.839-0.633-2.423-0.595-3.381-0.163l-2.08,0.936c-0.958,0.431-2.274,0.119-3.025-0.616\r\n\t\tc-0.177-0.174-0.356-0.343-0.54-0.509c-0.778-0.705-1.17-2-0.796-2.983l0.819-2.162c0.373-0.982,0.368-2.594-0.322-3.385\r\n\t\tc-0.635-0.728-1.643-1.579-3.215-2.281c-1.764-0.788-3.346-0.811-4.483-0.639c-1.039,0.158-2.121,1.331-2.494,2.312l-0.946,2.491\r\n\t\tc-0.373,0.982-0.798,1.775-0.949,1.771c-0.092-0.004-0.183-0.005-0.274-0.005c-0.622,0-1.238,0.03-1.846,0.09\r\n\t\tc-1.016,0.1-2.176-0.507-2.607-1.465l-1.124-2.5c-0.431-0.959-1.538-2.21-2.589-2.227c-0.916-0.016-2.207,0.209-3.936,1.028\r\n\t\tc-1.874,0.889-2.971,1.742-3.611,2.437c-0.712,0.771-0.554,2.416-0.122,3.374l1.481,3.296c0.431,0.958,0.256,2.266-0.324,2.979\r\n\t\tc-0.579,0.714-1.786,1.033-2.768,0.661l-3.598-1.365c-0.982-0.373-2.65-0.476-3.406,0.256c-0.658,0.637-1.412,1.709-2.056,3.51\r\n\t\tc-0.696,1.954-0.867,3.332-0.83,4.276c0.042,1.05,1.317,2.101,2.3,2.474l4.392,1.667c0.982,0.373,1.782,1.244,1.839,1.941\r\n\t\tc0.055,0.699-0.635,1.61-1.593,2.042l-4.382,1.97c-0.958,0.431-2.211,1.539-2.227,2.589c-0.015,0.916,0.21,2.208,1.028,3.935\r\n\t\tc0.89,1.874,1.742,2.971,2.437,3.611c0.773,0.713,2.417,0.554,3.375,0.123l4.698-2.112c0.958-0.432,2.076-0.412,2.525,0.013\r\n\t\ts0.535,1.541,0.162,2.524L12.743,46.6c-0.373,0.982-0.476,2.65,0.256,3.404c0.638,0.659,1.709,1.414,3.51,2.057\r\n\t\tc1.954,0.697,3.333,0.868,4.277,0.831c1.05-0.042,2.1-1.318,2.473-2.3l1.693-4.46c0.373-0.982,1.058-1.742,1.531-1.719\r\n\t\tc0.284,0.014,0.57,0.021,0.857,0.021c0.134,0,0.266-0.001,0.398-0.005c0.219-0.007,0.747,0.762,1.178,1.721l1.963,4.364\r\n\t\tc0.431,0.958,1.605,1.986,2.653,2.038c1.121,0.056,2.669-0.062,4.43-0.734c1.685-0.645,2.659-1.604,3.219-2.442\r\n\t\tc0.584-0.873,0.388-2.517-0.044-3.475l-1.606-3.573c-0.431-0.958-0.169-2.191,0.527-2.824c0.693-0.633,2-0.9,2.981-0.526\r\n\t\tl3.432,1.303c0.982,0.373,2.64,0.489,3.478-0.145c0.738-0.56,1.591-1.49,2.281-3.034C53.057,35.248,53.015,33.497,52.791,32.256z\r\n\t\t M26.465,39.79c-7.844,0-14.201-6.357-14.201-14.2s6.357-14.2,14.201-14.2c7.842,0,14.2,6.357,14.2,14.2\r\n\t\tC40.666,33.433,34.307,39.79,26.465,39.79z\"/>\r\n</g>\r\n</svg>",
                                            TintColor = Colors.White
                                        },
                                        new SkiaLabel()
                                        {
                                            TranslationY = -1,
                                            TextColor = Colors.White,
                                            VerticalOptions = LayoutOptions.Center,
                                            Text = "Placeholder"
                                        }.Adapt((c) => { _label = c; })
                                    }
                                }
                            }
                        }.Adapt((c) =>
                        {
                            c.Shadows.Add(new() { Color = Colors.Black, Blur = 2, X = 2, Y = 2 });
                            c.OnGestures = (args, info) =>
                            {
                                if (args.Type == TouchActionResult.Down)
                                {
                                    c.TranslationX = 1;
                                    c.TranslationY = 1;

                                    var shadow = c.Shadows[0];
                                    shadow.X = 0;
                                    shadow.Y = 0;

                                    c.BackgroundColor = Colors.Blue;

                                    return c;
                                }

                                if (args.Type == TouchActionResult.Up)
                                {
                                    c.BackgroundColor = Colors.CadetBlue;


                                    c.TranslationX = 0;
                                    c.TranslationY = 0;

                                    var shadow = c.Shadows[0];
                                    shadow.X = 2;
                                    shadow.Y = 2;
                                }

                                if (args.Type == TouchActionResult.Tapped)
                                {
                                    _label.Text = $"Was tapped {_counter++} time(s)";

                                    return c;
                                }

                                return null;
                            };
                        })
                    }
                }
            };


            this.Content = Canvas;
        }
    }
}
