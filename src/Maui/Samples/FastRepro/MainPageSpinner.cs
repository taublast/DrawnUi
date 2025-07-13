using DrawnUi.Views;
using DrawnUi.Controls;
using DrawnUi.Draw;
using Canvas = DrawnUi.Views.Canvas;
using System.Collections.ObjectModel;
using AppoMobi.Specials;

namespace Sandbox
{
    public class MainPageSpinner : BasePageReloadable, IDisposable
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

            var wheelSizePts = 320.0;

            // Initialize spinner items
            _spinnerItems = new ObservableCollection<string>
            {
                "Alice",
                "Bob",
                "Charlie",
                "Diana",
                "Edward",
                "Fiona",
                "George",
                "Hannah",
                "Ivan",
                "Julia",
                "Kevin",
                "Luna"
            };

            // Create the spinner
            Spinner = new SkiaSpinner
            {
                //BackgroundColor = Colors.Aquamarine,
                Velocity = 2.0,
                ItemsSource = _spinnerItems,
                InverseVisualRotation = true,
                SidePosition = SidePosition.Left,
                WidthRequest = wheelSizePts,
                HeightRequest = wheelSizePts,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };

            // Create selected item label
            _selectedLabel = new SkiaLabel
            {
                //UseCache = SkiaCacheType.Operations,
                FontSize = 18,
                TextColor = Colors.White,
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 20, 0, 0)
            }.Observe(Spinner, (me, prop) =>
            {
                if (prop.IsEither(nameof(BindingContext), nameof(SkiaSpinner.SelectedIndex)))
                {
                    var itemName = "None";
                    if (Spinner.SelectedIndex >= 0)
                    {
                        itemName = $"{_spinnerItems[Spinner.SelectedIndex]} [{Spinner.SelectedIndex}]";
                    }

                    me.Text = $"Selected: {itemName}";
                }
            });

            // Subscribe to selection changes
            Spinner.SelectedIndexChanged += OnSpinnerSelectionChanged;

            Canvas = new Canvas()
            {
                RenderingMode = RenderingModeType.Accelerated,
                VerticalOptions = LayoutOptions.Fill,
                HorizontalOptions = LayoutOptions.Fill,
                BackgroundColor = Colors.DarkSlateBlue,
                Gestures = GesturesMode.Enabled,
                Content =
                    new SkiaLayer()
                    {
                        VerticalOptions = LayoutOptions.Fill,
                        Children =
                        {
            
                            new SkiaLayout()
                            {
                                Type = LayoutType.Column,
                                //UseCache = SkiaCacheType.ImageComposite,
                                HorizontalOptions = LayoutOptions.Fill,
                                VerticalOptions = LayoutOptions.Fill,
                                Spacing = 20,
                                Padding = new (20),
                                Children =
                                {
                                    // Title
                                    new SkiaLabel()
                                    {
                                        Text = "SkiaSpinner Demo",
                                        UseCache = SkiaCacheType.Operations,
                                        FontSize = 24,
                                        FontWeight = FontWeights.Bold,
                                        TextColor = Colors.White,
                                        HorizontalOptions = LayoutOptions.Center,
                                        Margin = new Thickness(0, 20, 0, 0)
                                    },

                                    // Instructions
                                    new SkiaLabel()
                                    {
                                        UseCache = SkiaCacheType.Operations,
                                        Text = "Pan to spin the wheel, or tap the buttons below",
                                        FontSize = 14,
                                        TextColor = Colors.LightGray,
                                        HorizontalOptions = LayoutOptions.Center,
                                        HorizontalTextAlignment = DrawTextAlignment.Center
                                    },

                                    // Spinner container with triangle indicator
                                    new SkiaLayout()
                                    {
                                        HorizontalOptions = LayoutOptions.Center,
                                        VerticalOptions = LayoutOptions.Fill,
                                        Children =
                                        {
                                            Spinner,
                                                // Selected item indicator
                                            new SkiaShape()
                                                {
                                                    Tag = "Arrow",
                                                    UseCache = SkiaCacheType.Operations,
                                                    Type = ShapeType.Polygon,
                                                    BackgroundColor = Colors.Red,
                                                    StrokeColor = Colors.White,
                                                    StrokeWidth = 1.5,
                                                    WidthRequest = 24,
                                                    HeightRequest = 16,
                                                    VerticalOptions = LayoutOptions.Center,
                                                    TranslationX = -10,
                                                    ZIndex = 10 // Ensure it appears on top
                                                }
                                                .WithPoints("1.0, 0.5; 0.0, 0.0; 0.0, 1.0;") // Triangle pointing right
                                                //.WithPoints( "0.0, 0.5; 1.0, 0.0; 1.0, 1.0;") // Triangle pointing left
                                        }
                                    },

                                    // Selected item display
                                    _selectedLabel,

                                    // Control buttons
                                    new SkiaLayout()
                                    {
                                        UseCache = SkiaCacheType.Operations,
                                        Type = LayoutType.Row,
                                        HorizontalOptions = LayoutOptions.Center,
                                        Spacing = 15,
                                        Children =
                                        {
                                            new SkiaButton()
                                            {
                                                UseCache = SkiaCacheType.Image,
                                                Text = "Spin Random",
                                                BackgroundColor = Colors.Orange,
                                                TextColor = Colors.White,
                                                CornerRadius = 8,
                                            }.OnTapped(me => { OnButtonTapped("spin"); }),
                                            new SkiaButton()
                                            {
                                                Text = "Add Item",
                                                UseCache = SkiaCacheType.Image,
                                                BackgroundColor = Colors.Green,
                                                TextColor = Colors.White,
                                                CornerRadius = 8,
                                            }.OnTapped(me => { OnButtonTapped("add"); }),
                                            new SkiaButton()
                                            {
                                                Text = "Remove Item",
                                                UseCache = SkiaCacheType.Image,
                                                BackgroundColor = Colors.Red,
                                                TextColor = Colors.White,
                                                CornerRadius = 8,
                                            }.OnTapped(me => { OnButtonTapped("remove"); }),
                                        }
                                    }
                                }
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

        void OnSpinnerSelectionChanged(object sender, int selectedIndex)
        {
            if (selectedIndex >= 0 && selectedIndex < _spinnerItems.Count)
            {
                _selectedLabel.Text = $"Selected: {_spinnerItems[selectedIndex]}";
            }
            else
            {
                _selectedLabel.Text = "Selected: None";
            }
        }

        void OnButtonTapped(object parameter)
        {
            var action = parameter?.ToString();

            switch (action)
            {
                case "spin":
                    Spinner.SpinToRandom();
                    break;

                case "add":
                    if (_spinnerItems.Count < 150)
                    {
                        var newName = $"Person {_spinnerItems.Count + 1}";
                        _spinnerItems.Add(newName);
                    }

                    break;

                case "remove":
                    if (_spinnerItems.Count > 2)
                    {
                        _spinnerItems.RemoveAt(_spinnerItems.Count - 1);
                    }

                    break;
            }
        }
    }
}
