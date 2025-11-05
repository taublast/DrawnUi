using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using AppoMobi.Specials;
using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Infrastructure.Enums;
using DrawnUi.Views;
using Sandbox.Resources;
using Sandbox.Views.Controls;

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
                            new RenderSceneOptimized(),
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
