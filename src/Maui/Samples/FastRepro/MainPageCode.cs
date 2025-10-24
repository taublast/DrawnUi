using System.Collections.ObjectModel;
using DrawnUi.Controls;
using DrawnUi.Views;
using Sandbox.Resources;
using Sandbox.Views.Controls;
using System.ComponentModel;
using AppoMobi.Specials;
using DrawnUi.Draw;

namespace Sandbox
{
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
                Gestures = GesturesMode.Enabled,
                VerticalOptions = LayoutOptions.Fill,
                HorizontalOptions = LayoutOptions.Fill,
                BackgroundColor = Colors.DarkSlateBlue,
                Content =
                    new SkiaStack()
                    {
                        Spacing = 0,
                        VerticalOptions = LayoutOptions.Fill,
                        Children =
                        {
                            new SkiaScroll()
                            {
                                BackgroundColor = Colors.White,
                                HorizontalOptions = LayoutOptions.Fill,
                                Content = new SkiaStack()
                                {
                                    MinimumWidthRequest = 200,
                                    UseCache = SkiaCacheType.Operations,
                                    Spacing = 0,
                                    Children =
                                    {
                                        new SkiaStack()
                                        {
                                            Padding = 10,
                                            Children =
                                            {
                                                new SkiaButton("Hello World")
                                                {
                                                    BackgroundColor = Colors.Red,
                                                    ControlStyle =
                                                        PrebuiltControlStyle.Windows,
                                                }
                                            }
                                        }
                                    }
                                },
                            }
                        }
                    }
            };

            Content = new Grid() //due to maui layout specifics we are forced to use a Grid as root wrapper
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
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
