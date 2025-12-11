using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using AppoMobi.Specials;
using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Infrastructure.Enums;
using DrawnUi.Views;
using Sandbox.Resources;


namespace Sandbox
{


    public class FinalStack : SkiaStack
    {
        public override ScaledSize MeasureLayout(MeasureRequest request, bool force)
        {
            var ret = base.MeasureLayout(request, force);
            return ret;
        }

        public override SKRect CalculateLayout(SKRect destination, float widthRequest, float heightRequest, float scale)
        {
            return base.CalculateLayout(destination, widthRequest, heightRequest, scale);
        }
    }

    public class DebugStack : SkiaStack
    {
        public override ScaledSize MeasureLayout(MeasureRequest request, bool force)
        {
            var ret = base.MeasureLayout(request, force);
            return ret;
        }

        public override SKRect CalculateLayout(SKRect destination, float widthRequest, float heightRequest, float scale)
        {
            return base.CalculateLayout(destination, widthRequest, heightRequest, scale);
        }
    }

    public class DebugLabel : SkiaLabel
    {
        public override SKRect CalculateLayout(SKRect destination, float widthRequest, float heightRequest, float scale)
        {
            return base.CalculateLayout(destination, widthRequest, heightRequest, scale);
        }

        protected override void AdaptCachedLayout(SKRect destination, float scale)
        {
            base.AdaptCachedLayout(destination, scale);
        }
    }

    public class MainPageCode : BasePageReloadable, IDisposable, INotifyPropertyChanged
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

        /// <summary>
        /// This will be called by HotReload
        /// </summary>
        public override void Build()
        {
            Canvas?.Dispose();

            BindingContext = this;

            Canvas = new Canvas()
            {
                RenderingMode = RenderingModeType.Accelerated,
                UpdateMode = UpdateMode.Dynamic,
                //Gestures = GesturesMode.Enabled,
                VerticalOptions = LayoutOptions.Fill,
                HorizontalOptions = LayoutOptions.Fill,
                Content =
                    new SkiaLayout()
                    {
                        VerticalOptions = LayoutOptions.Fill,
                        HorizontalOptions = LayoutOptions.Fill,
                        Children =
                        {
                            //new RenderSceneOptimized(),

                         new FinalStack()
                        {
                            BackgroundColor = Color.Parse("#66000000"),
                            HorizontalOptions = LayoutOptions.Center,
                            VerticalOptions = LayoutOptions.Center,
                            Spacing = 0,
                            Children =
                            {
                                //header 
                                new SkiaLayer()
                                {
                                    HeightRequest = 28,
                                    BackgroundColor = Color.Parse("#66000000"),
                                },

                                //overlay data
                                new DebugStack()
                                {
                                    HorizontalOptions = LayoutOptions.Fill,
                                    BackgroundColor = Colors.Blue,
                                    UseCache = SkiaCacheType.Operations,
                                    Spacing = 0,
                                    Padding = new(8, 4),
                                    Children =
                                    {
                                        //driver
                                        new DebugLabel()
                                        {
                                            Text = "FirstName LastName",
                                            UseCache = SkiaCacheType.Operations,
                                            MaxLines = 1,
                                            Margin = new(0, 0, 0, 4),
                                            FontSize = 12,
                                            HorizontalOptions = LayoutOptions.End,
                                        },

                                        //car
                                        new SkiaLabel("BMW X1 xDrive 20i")
                                        {
                                            MaxLines = 1,
                                            Margin = new(0, 0, 0, 4),
                                            FontSize = 12,
                                            HorizontalOptions = LayoutOptions.Start,
                                        },


                                    }
                                },

                                new SkiaLabel("Debugging End")
                                {
                                    MaxLines = 1,
                                    TextColor = Colors.GreenYellow,
                                    Margin = new(0, 0, 0, 4),
                                    FontSize = 12,
                                    HorizontalOptions = LayoutOptions.End,
                                },

                                new SkiaRichLabel("⏱️ Результаты замера") //MainShortStatus
                                    {
                                        IsParentIndependent = true,
                                        UseCache = SkiaCacheType.Image,
                                        MaxLines = 1,
                                        FontSize = 11,
                                        TextColor = Colors.White,
                                        Padding = new(0, 0, 0, 12),
                                        VerticalTextAlignment = TextAlignment.Center,
                                        HorizontalTextAlignment = DrawTextAlignment.Center,
                                        HeightRequest = 28,
                                        WidthRequest = 300,
                                        BackgroundColor = Color.Parse("#66000000")
                                    }
                            }},


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

            Content = new Grid() //due to maui layout specifics we are forced to use a Grid as root wrapper
            {
                Children = { Canvas }
            };
        }

        #region TUNE TONE

        private Color _ToneColor = Styles.TintContentColor;

        public Color ToneColor
        {
            get { return _ToneColor; }
            set
            {
                if (_ToneColor != value)
                {
                    _ToneColor = value;
                    OnPropertyChanged();
                    //Debug.WriteLine($"Tint {value}");
                }
            }
        }

        private double _ToneContrast = Styles.TintContentContrast;

        public double ToneContrast
        {
            get { return _ToneContrast; }
            set
            {
                if (_ToneContrast != value)
                {
                    _ToneContrast = value;
                    OnPropertyChanged();
                    //Super.Log($"[VAL1] {value}");
                }
            }
        }

        private double _ToneBrightness = Styles.TintContentLightness;

        public double ToneBrightness
        {
            get { return _ToneBrightness; }
            set
            {
                if (_ToneBrightness != value)
                {
                    _ToneBrightness = value;
                    OnPropertyChanged();
                    //Super.Log($"[VAL2] {value}");
                }
            }
        }


        private double _ToneAlpha = Styles.TintContentColorAlpha;

        public double ToneAlpha
        {
            get { return _ToneAlpha; }
            set
            {
                if (_ToneAlpha != value)
                {
                    _ToneAlpha = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _ToneSaturation = Styles.TintContentSaturation;

        public double ToneSaturation
        {
            get { return _ToneSaturation; }
            set
            {
                if (_ToneSaturation != value)
                {
                    _ToneSaturation = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion
    }
}
