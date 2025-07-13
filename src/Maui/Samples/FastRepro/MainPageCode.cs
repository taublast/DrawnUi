using DrawnUi.Views;
using DrawnUi.Controls;
using DrawnUi.Draw;
using Canvas = DrawnUi.Views.Canvas;
using System.Collections.ObjectModel;
using System.Diagnostics;
using AppoMobi.Specials;

namespace Sandbox
{
    public class MainPageCode : BasePageReloadable, IDisposable
    {
        Canvas Canvas;
        SkiaSpinner Spinner;
        SkiaLabel _selectedLabel;
        ObservableCollection<string> _spinnerItems;

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                this.Content = null;
                Canvas?.Dispose();
            }

            base.Dispose(isDisposing);
        }

        /// <summary>
        /// This will be called by HotReload
        /// </summary>
        public override void Build()
        {
            Canvas?.Dispose();


            Canvas = new Canvas()
            {
                RenderingMode = RenderingModeType.Accelerated,
                Gestures = GesturesMode.Enabled,
                VerticalOptions = LayoutOptions.Fill,
                HorizontalOptions = LayoutOptions.Fill,
                BackgroundColor = Colors.DarkSlateBlue,
                Content =
                    new SkiaStack()
                    {
                        Spacing = 0,
                        VerticalOptions = LayoutOptions.Fill,
                        UseCache = SkiaCacheType.ImageComposite,
                        Children =
                        {
                            new SkiaShape()
                            {
                                HeightRequest = 100,
                                HorizontalOptions = LayoutOptions.Fill,
                                BackgroundColor = Colors.Wheat
                            },
                            new SkiaScroll()
                            {
                                HorizontalOptions = LayoutOptions.Fill,
                                Content = new SkiaWrap()
                                {
                                    UseCache = SkiaCacheType.ImageComposite,
                                    Spacing = 100,
                                    Children =
                                    {
                                        new SkiaShape()
                                        {
                                            HeightRequest = 40,
                                            HorizontalOptions = LayoutOptions.Fill,
                                            BackgroundColor = Colors.White,
                                        }.OnTapped(me =>
                                        {
                                            me.Update();
                                            Debug.WriteLine("WHITE");
                                        }),
                                        new SkiaShape()
                                        {
                                            HeightRequest = 40,
                                            HorizontalOptions = LayoutOptions.Fill,
                                            BackgroundColor = Colors.Red,
                                        }.OnTapped(me =>
                                        {
                                            me.Update();
                                            Debug.WriteLine("RED");
                                        }),
                                        new SkiaShape()
                                        {
                                            HeightRequest = 40,
                                            HorizontalOptions = LayoutOptions.Fill,
                                            BackgroundColor = Colors.Green,
                                        }.OnTapped(me =>
                                        {
                                            me.Update();
                                            Debug.WriteLine("GREEN");
                                        }),
                                        new SkiaShape()
                                        {
                                            HeightRequest = 40,
                                            HorizontalOptions = LayoutOptions.Fill,
                                            BackgroundColor = Colors.Blue,
                                        }.OnTapped(me =>
                                        {
                                            me.Update();
                                            Debug.WriteLine("BLUE");
                                        }),
                                    }
                                },
                            }
                        }
                    }
            };

            this.Content = Canvas;
        }
    }
}
