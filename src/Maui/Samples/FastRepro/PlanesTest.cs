using DrawnUi.Views;
using DrawnUi.Controls;
using DrawnUi.Draw;
using Canvas = DrawnUi.Views.Canvas;
using System.Collections.ObjectModel;
using AppoMobi.Specials;
using System.Diagnostics;
using System.Windows.Input;

namespace Sandbox
{
    /// <summary>
    /// Test page for 3-plane virtualization system in SkiaScroll + SkiaLayout
    /// Tests the configuration: Virtualisation="Managed", IsTemplated=true
    /// </summary>
    public class PlanesTest : BasePageReloadable, IDisposable
    {
        Canvas Canvas;
        SkiaScroll MainScroll;
        SkiaLayout VirtualizedLayout;
        ObservableCollection<TestDataItem> ItemsSource;
        SkiaLabel StatusLabel;
        SkiaLabel PlaneInfoLabel;
        SkiaButton AddItemsButton;
        SkiaButton ClearItemsButton;
        SkiaButton ScrollToTopButton;
        SkiaButton ScrollToBottomButton;
        SkiaButton StressTestButton;
        SkiaButton AutoScrollButton;

        // Test data generation
        private int _itemCounter = 0;
        private readonly Random _random = new Random();
        private bool _autoScrolling = false;
        private bool _stressTesting = false;
        private readonly string[] _sampleTexts = {
            "Short text",
            "This is a medium length text that spans multiple lines and tests how the virtualization handles varying content heights.",
            "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.",
            "Single line",
            "Multi-line content\nwith line breaks\nto test height\nvariations in cells",
            "Another test item with different content length to verify proper measurement and virtualization behavior."
        };

        public ICommand CommandChildTapped
        {
            get
            {
                return new Command((o) =>
                {
                    if (o is SkiaControl control)
                    {
                        if (control.BindingContext is TestDataItem item)
                        {
                            var index = item.Id;
                            Debug.WriteLine($"[TAPPED] child {item.Id}");
                            //try hide NEXT item
                            if (index < ItemsSource.Count-1)
                            {
                                var nextItem = ItemsSource[index];
                                nextItem.Hide = !nextItem.Hide;
                                Debug.WriteLine($"[CHANGED] child {index+1} HIDE {item.Hide}");
                            }
                        }
                    }
                });
            }
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

        public override void Build()
        {
            Canvas?.Dispose();

            // Initialize test data
            ItemsSource = new ObservableCollection<TestDataItem>();
            GenerateInitialData(50); // Start with 50 items

            this.ItemsSource[1].Hide = true;

            // Create status labels
            StatusLabel = new SkiaLabel
            {
                Text = $"Items: {ItemsSource.Count} | 3-Plane Virtualization Test",
                FontSize = 14,
                TextColor = Colors.White,
                HorizontalOptions = LayoutOptions.Fill,
                Margin = new Thickness(16, 8)
            };

            PlaneInfoLabel = new SkiaLabel
            {
                Text = "Plane Info: Monitoring...",
                FontSize = 12,
                TextColor = Colors.LightGray,
                HorizontalOptions = LayoutOptions.Fill,
                Margin = new Thickness(16, 4)
            };

            // Create the virtualized layout with exact configuration for 3-plane system
            VirtualizedLayout = new SkiaLayout
            {
                Tag = "VirtualizedLayout",
                Type = LayoutType.Column,
                Spacing = 0,
                HorizontalOptions = LayoutOptions.Fill,
                
                // KEY CONFIGURATION FOR 3-PLANE VIRTUALIZATION
                MeasureItemsStrategy = MeasuringStrategy.MeasureVisible, //disabled will be measurefirst
                Virtualisation = VirtualisationType.Managed,
                RecyclingTemplate = RecyclingTemplate.Enabled,
                
                ItemsSource = ItemsSource,
                CommandChildTapped = CommandChildTapped
            };

            // Set up the item template
            VirtualizedLayout.ItemTemplate = new DataTemplate(() =>
            {
                SkiaLayout lazyParent=null;

                var cell = new SkiaLayout
                {
                    Type = LayoutType.Row,
                    Margin = 2,
                    UseCache = SkiaCacheType.Image,
                    Spacing = 12,
                    Padding = new Thickness(12, 8),
                    BackgroundColor = Color.FromArgb("#2A2A2A"),
                    HorizontalOptions = LayoutOptions.Fill,
                    Children =
                    {
                        // Index indicator - Avatar circle with data binding
                        new SkiaShape
                        {
                            Type = ShapeType.Circle,
                            WidthRequest = 32,
                            HeightRequest = 32,
                            BackgroundColor = Color.FromArgb("#4A90E2"),
                            VerticalOptions = LayoutOptions.Start,
                        }
                        .ObserveBindingContext<SkiaShape, TestDataItem>((me, item, prop) =>
                        {
                            if (prop.IsEither(nameof(BindingContext)))
                            {
                                // Change color based on item ID to make binding visible
                                var colors = new[] { "#4A90E2", "#E24A4A", "#4AE24A", "#E2E24A", "#E24AE2" };
                                me.BackgroundColor = Color.FromArgb(colors[item.Id % colors.Length]);
                            }
                        }),
                        
                        // Content column - UNCOMMENT for UNEVEN ROWS

                 
                        new SkiaLayout
                        {
                            Type = LayoutType.Column,
                            Spacing = 4,
                            HorizontalOptions = LayoutOptions.Fill,
                            Children =
                            {
                                new SkiaLabel
                                {
                                    FontSize = 16,
                                    FontWeight = FontWeights.Bold,
                                    TextColor = Colors.White,
                                    HorizontalOptions = LayoutOptions.Fill
                                }.ObserveSelf((label, prop) =>
                                {
                                    if (prop.IsEither(nameof(BindingContext), "Title"))
                                    {
                                        if (label.BindingContext is TestDataItem item)
                                            label.Text = $"{item.Id} {item.Title}";
                                    }
                                }),

                                //comment this out to test even rows:

                                new SkiaLabel
                                {
                                    FontSize = 14,
                                    TextColor = Colors.LightGray,
                                    HorizontalOptions = LayoutOptions.Fill,
                                    LineBreakMode = LineBreakMode.WordWrap
                                }.ObserveSelf((label, prop) =>
                                {
                                    if (prop.IsEither(nameof(BindingContext), "Description"))
                                    {
                                        if (label.BindingContext is TestDataItem item)
                                            label.Text = item.Description;
                                    }
                                }),
                                
                                new SkiaLabel
                                {
                                    FontSize = 12,
                                    TextColor = Colors.Gray,
                                    HorizontalOptions = LayoutOptions.Fill
                                }.ObserveSelf((label, prop) =>
                                {
                                    if (prop.IsEither(nameof(BindingContext), "Metadata"))
                                    {
                                        if (label.BindingContext is TestDataItem item)
                                            label.Text = item.Metadata;
                                    }
                                })

                              
                            }
                        }
            

                    }
                }
                .ObserveSelf((me, prop) =>
                {
                    if (prop.IsEither(nameof(Parent)))
                    {
                        if (me.Parent!=null)
                            lazyParent = me.Parent as SkiaLayout;
                    }
                })
                .ObserveBindingContext<SkiaLayout, TestDataItem>((me, item, prop) =>
                {
                    if (prop == nameof(BindingContext))
                    {
                        //setup cell initially
                        me.NeedMeasure = true;
                        me.IsVisible = !item.Hide;
                        if (!me.IsVisible)
                        {
                            var stop = 1;
                        }
                    }
                    else
                    if (prop == nameof(TestDataItem.Hide))
                    {
                        me.IsVisible = !item.Hide;
                        if (!me.IsVisible)
                        {
                            var stop = 1;
                        }
                        if (lazyParent != null)
                        {
                            lazyParent.TrackChildAsDirty(me);
                            me.Parent.InvalidateByChild(me);
                        }
                        else
                        {
                            //this cell was never drawn because was initially invisible
                            var scroll = (VirtualizedLayout.Parent as SkiaScroll);
                            scroll.Invalidate();
                        }
                    }
                });

                return cell;
            });

            // Create the scroll container that will use 3-plane virtualization
            MainScroll = new SkiaScroll
            {
                Orientation = ScrollOrientation.Vertical,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                BackgroundColor = Color.FromArgb("#1A1A1A"),
                
                // This will trigger UseVirtual = true when combined with layout configuration
                Content = VirtualizedLayout
            };

            // Control buttons
            var buttonStyle = new Action<SkiaButton>(btn =>
            {
                btn.BackgroundColor = Color.FromArgb("#4A90E2");
                btn.TextColor = Colors.White;
                btn.CornerRadius = 6;
                btn.FontSize = 14;
            });

            AddItemsButton = new SkiaButton { Text = "Add 25 Items" };
            buttonStyle(AddItemsButton);
            AddItemsButton.Tapped += OnAddItemsTapped;

            ClearItemsButton = new SkiaButton { Text = "Clear All" };
            buttonStyle(ClearItemsButton);
            ClearItemsButton.Tapped += OnClearItemsTapped;

            ScrollToTopButton = new SkiaButton { Text = "Scroll Top" };
            buttonStyle(ScrollToTopButton);
            ScrollToTopButton.Tapped += OnScrollToTopTapped;

            ScrollToBottomButton = new SkiaButton { Text = "Scroll Bottom" };
            buttonStyle(ScrollToBottomButton);
            ScrollToBottomButton.Tapped += OnScrollToBottomTapped;

            StressTestButton = new SkiaButton { Text = "Stress Test" };
            buttonStyle(StressTestButton);
            StressTestButton.BackgroundColor = Color.FromArgb("#E74C3C");
            StressTestButton.Tapped += OnStressTestTapped;

            AutoScrollButton = new SkiaButton { Text = "Auto Scroll" };
            buttonStyle(AutoScrollButton);
            AutoScrollButton.BackgroundColor = Color.FromArgb("#9B59B6");
            AutoScrollButton.Tapped += OnAutoScrollTapped;

            Canvas = new Canvas()
            {
                RenderingMode = RenderingModeType.Accelerated,
                VerticalOptions = LayoutOptions.Fill,
                HorizontalOptions = LayoutOptions.Fill,
                BackgroundColor = Color.FromArgb("#0D1117"),
                Gestures = GesturesMode.Enabled,
                Content = new SkiaLayer()
                {
                    VerticalOptions = LayoutOptions.Fill,
                    Children =
                    {
                        new SkiaStack()
                        {
                            Type = LayoutType.Column,
                            VerticalOptions = LayoutOptions.Fill,
                            Children =
                            {
                                // Header with title and status
                                new SkiaStack()
                                {
                                    UseCache = SkiaCacheType.Operations,
                                    Type = LayoutType.Column,
                                    BackgroundColor = Color.FromArgb("#161B22"),
                                    Padding = new Thickness(0, 0, 0, 8),
                                    Children =
                                    {
                                        new SkiaLabel()
                                        {
                                            Text = "3-Plane Virtualization Test",
                                            FontSize = 20,
                                            FontWeight = FontWeights.Bold,
                                            TextColor = Colors.White,
                                            HorizontalOptions = LayoutOptions.Center,
                                            Margin = new Thickness(16, 16, 16, 8)
                                        },
                                        StatusLabel,
                                        PlaneInfoLabel
                                    }
                                },
                                
                                // Control buttons row 1
                                new SkiaWrap()
                                {
                                    UseCache = SkiaCacheType.Image,
                                    Spacing = 8,
                                    Padding = new Thickness(16, 8),
                                    BackgroundColor = Color.FromArgb("#161B22"),
                                    Children = { AddItemsButton, ClearItemsButton, ScrollToTopButton, ScrollToBottomButton }
                                },

                                // Control buttons row 2
                                new SkiaLayout()
                                {
                                    UseCache = SkiaCacheType.Image,
                                    Type = LayoutType.Row,
                                    Spacing = 8,
                                    Padding = new Thickness(16, 4, 16, 8),
                                    BackgroundColor = Color.FromArgb("#161B22"),
                                    Children = { StressTestButton, AutoScrollButton }
                                },
                                
                                // Main scrollable content

                                MainScroll
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

            // Start monitoring
            StartPlaneMonitoring();
        }

        private void GenerateInitialData(int count)
        {
            for (int i = 0; i < count; i++)
            {
                ItemsSource.Add(CreateTestItem());
            }
        }

        private TestDataItem CreateTestItem()
        {
            _itemCounter++;
            var textIndex = _random.Next(_sampleTexts.Length);
            
            return new TestDataItem
            {
                Id = _itemCounter,
                Title = $"Item #{_itemCounter}",
                Description = _sampleTexts[textIndex],
                Metadata = $"Created: {DateTime.Now:HH:mm:ss} | Height: Variable | Index: {_itemCounter - 1}"
            };
        }

        private void OnAddItemsTapped(object sender, EventArgs e)
        {
            for (int i = 0; i < 25; i++)
            {
                ItemsSource.Add(CreateTestItem());
            }
            UpdateStatus();
        }

        private void OnClearItemsTapped(object sender, EventArgs e)
        {
            ItemsSource.Clear();
            _itemCounter = 0;
            UpdateStatus();
        }

        private void OnScrollToTopTapped(object sender, EventArgs e)
        {
            MainScroll?.ScrollToTop(1.0f);
        }

        private void OnScrollToBottomTapped(object sender, EventArgs e)
        {
            MainScroll?.ScrollToBottom(1.0f);
        }

        private void OnStressTestTapped(object sender, EventArgs e)
        {
            if (_stressTesting)
            {
                _stressTesting = false;
                StressTestButton.Text = "Stress Test";
                StressTestButton.BackgroundColor = Color.FromArgb("#E74C3C");
                return;
            }

            _stressTesting = true;
            StressTestButton.Text = "Stop Stress";
            StressTestButton.BackgroundColor = Color.FromArgb("#95A5A6");

            // Start stress test - rapidly add items and scroll
            Tasks.StartTimerAsync(TimeSpan.FromMilliseconds(100), async () =>
            {
                if (!_stressTesting) return false;

                // Add items in batches
                for (int i = 0; i < 10; i++)
                {
                    ItemsSource.Add(CreateTestItem());
                }

                // Random scroll position
                if (_random.Next(0, 3) == 0)
                {
                    var scrollPosition = _random.NextDouble();
                    if (scrollPosition < 0.3)
                        MainScroll?.ScrollToTop(0.5f);
                    else if (scrollPosition > 0.7)
                        MainScroll?.ScrollToBottom(0.5f);
                }

                return _stressTesting;
            });
        }

        private void OnAutoScrollTapped(object sender, EventArgs e)
        {
            if (_autoScrolling)
            {
                _autoScrolling = false;
                AutoScrollButton.Text = "Auto Scroll";
                AutoScrollButton.BackgroundColor = Color.FromArgb("#9B59B6");
                return;
            }

            _autoScrolling = true;
            AutoScrollButton.Text = "Stop Auto";
            AutoScrollButton.BackgroundColor = Color.FromArgb("#95A5A6");

            var scrollDirection = 1; // 1 for down, -1 for up
            var scrollStep = 0;

            Tasks.StartTimerAsync(TimeSpan.FromMilliseconds(50), async () =>
            {
                if (!_autoScrolling) return false;

                scrollStep++;

                // Change direction every 100 steps
                if (scrollStep % 100 == 0)
                {
                    scrollDirection *= -1;
                }

                // Smooth scrolling
                var currentOffset = MainScroll?.ViewportOffsetY ?? 0;
                var newOffset = currentOffset + (scrollDirection * 5);

                MainScroll?.ScrollTo(MainScroll.ViewportOffsetX, newOffset, 0);

                return _autoScrolling;
            });
        }

        private void UpdateStatus()
        {
            var useVirtual = MainScroll?.UseVirtual ?? false;
            var strategy = VirtualizedLayout?.MeasureItemsStrategy.ToString() ?? "Unknown";
            var virtualization = VirtualizedLayout?.Virtualisation.ToString() ?? "Unknown";

            StatusLabel.Text = $"Items: {ItemsSource.Count} | UseVirtual: {useVirtual} | Strategy: {strategy} | Type: {virtualization}";
        }

        private void StartPlaneMonitoring()
        {
            // Monitor plane states periodically
            Tasks.StartTimerAsync(TimeSpan.FromMilliseconds(500), async () =>
            {
                try
                {
                    if (MainScroll?.UseVirtual == true)
                    {
                        // Access plane information if available
                        var planeInfo = "3-Plane System Active";

                        if (VirtualizedLayout != null)
                        {
                            planeInfo += $" Measured: {VirtualizedLayout.LastMeasuredIndex + 1}/{ItemsSource.Count}";
                            planeInfo += $" | Visible: {VirtualizedLayout.FirstVisibleIndex}-{VirtualizedLayout.LastVisibleIndex}";
                        }

                        PlaneInfoLabel.Text = planeInfo;
                    }
                    else
                    {
                        PlaneInfoLabel.Text = "3-Plane System: Not Active (check configuration)";
                    }

                    UpdateStatus();
                }
                catch (Exception ex)
                {
                    PlaneInfoLabel.Text = $"Monitoring Error: {ex.Message}";
                }

                return true; // Continue timer
            });
        }
    }

    /// <summary>
    /// Test data model for virtualization testing
    /// </summary>
    public class TestDataItem : BindableObject
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Metadata { get; set; }

        private bool hide;
        public bool Hide    
        {
            get => hide;
            set
            {
                if (value == hide)
                {
                    return;
                }

                hide = value;
                OnPropertyChanged();
            }
        }
    }
}
