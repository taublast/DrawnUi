


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
        SkiaSpinner Spinner;
        SkiaLabel _selectedLabel;
        ObservableCollection<string> _spinnerItems;

        // ColorPicker test properties
        private Color _testColorPickerSelected = Styles.TintContentColor;

        /// <summary>
        /// Selected color from ColorPicker with 2-way binding
        /// </summary>
        public Color TestColorPickerSelected
        {
            get => _testColorPickerSelected;
            set
            {
                if (_testColorPickerSelected != value)
                {
                    _testColorPickerSelected = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TestColorPickerSelectedHex));
                }
            }
        }

        /// <summary>
        /// Hex representation of selected color
        /// </summary>
        public string TestColorPickerSelectedHex => TestColorPickerSelected.GetHexDesc();

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

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
                                    Spacing = 0,
                                    Children =
                                    {


                                        //new SkiaSlider()
                                        //{
                                        //    ControlStyle = PrebuiltControlStyle.Cupertino,
                                        //    End=0.5,
                                        //    Min = 0,
                                        //    Max=3,
                                        //    MinMaxStringFormat="P0",
                                        //    Step=0.01,
                                        //    HorizontalOptions = LayoutOptions.Fill,
                                        //},

                                        // ColorPicker test section
                                        new SkiaLayout()
                                        {
                                            Type = LayoutType.Column,
                                            Tag="Stack",
                                            HorizontalOptions = LayoutOptions.Fill,
                                            BackgroundColor = Colors.YellowGreen,
                                            Spacing = 10,
                                            Children =
                                            {
                                                // Info label
                                                new SkiaLabel()
                                                {
                                                    Text = "ColorPicker Test - Initial: TintContentColor",
                                                    FontSize = 14,
                                                    TextColor = Colors.Black,
                                                    HorizontalOptions = LayoutOptions.Center
                                                },

                                                // ColorPicker
                                                new SkiaLayer()
                                                {
                                                    HeightRequest=100,
                                                    WidthRequest = 280,
                                                    BlockGesturesBelow = true,
                                                    Children =
                                                    {
                                                        new ColorPicker()
                                                        {
                                                            UseCache = SkiaCacheType.Image,
                                                        }
                                                        // 2-way binding: page property ↔ ColorPicker.SelectedColor
                                                        .Observe(this, (me, prop) =>
                                                        {
                                                            bool attached = prop == nameof(BindingContext);
                                                            if (attached || prop == nameof(TestColorPickerSelected))
                                                            {
                                                                me.SelectedColor = TestColorPickerSelected;
                                                            }
                                                        })
                                                        .ObserveSelf((me, prop) =>
                                                        {
                                                            if (prop.IsEither(nameof(BindingContext), nameof(ColorPicker.SelectedColor)))
                                                            {
                                                                TestColorPickerSelected = me.SelectedColor;
                                                            }
                                                        })
                                                    }
                                                },

                                                // Selected color display
                                                new SkiaLayout()
                                                {
                                                    Type = LayoutType.Row,
                                                    Spacing = 10,
                                                    HorizontalOptions = LayoutOptions.Center,
                                                    Children =
                                                    {
                                                        new SkiaLabel()
                                                        {
                                                            Text = "Selected Color:",
                                                            FontSize = 12,
                                                            TextColor = Colors.Black,
                                                            VerticalOptions = LayoutOptions.Center
                                                        },
                                                        new SkiaControl()
                                                        {
                                                            WidthRequest = 30,
                                                            HeightRequest = 20,
                                                        }
                                                        .Observe(this, (me, prop) =>
                                                        {
                                                            bool attached = prop == nameof(BindingContext);
                                                            if (attached || prop == nameof(TestColorPickerSelected))
                                                            {
                                                                me.BackgroundColor = TestColorPickerSelected;
                                                            }
                                                        }),
                                                        new SkiaLabel()
                                                        {
                                                            FontSize = 10,
                                                            TextColor = Colors.DarkGray,
                                                            VerticalOptions = LayoutOptions.Center
                                                        }
                                                        .Observe(this, (me, prop) =>
                                                        {
                                                            bool attached = prop == nameof(BindingContext);
                                                            if (attached || prop == nameof(TestColorPickerSelected))
                                                            {
                                                                me.Text = TestColorPickerSelectedHex;
                                                            }
                                                        })
                                                    }
                                                }
                                            }
                                        }

 
                                    }
                                },
                            }
                        }
                    }
            };

            this.Content = Canvas;
        }

        #region TUNE TONE

        private Color _ToneColor = Styles.TintContentColor;
        public Color ToneColor
        {
            get
            {
                return _ToneColor;
            }
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
            get
            {
                return _ToneContrast;
            }
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
            get
            {
                return _ToneBrightness;
            }
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
            get
            {
                return _ToneAlpha;
            }
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
            get
            {
                return _ToneSaturation;
            }
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
