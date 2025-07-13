namespace DrawnUi.Controls
{
    [ContentProperty("ItemTemplate")]
    public class SkiaWheelPicker : SkiaLayout
    {
        public SkiaWheelScroll Scroller { get; private set; }

        protected SkiaWheelStack Wheel;

        public SkiaWheelPicker()
        {
        }

        protected virtual void AttachScroller(SkiaWheelScroll scroller)
        {
            if (scroller != null)
            {
                Scroller = scroller;
                AddSubView(scroller);
                Scroller.SelectedIndexChanged += ScrollerOnIndexChanged;
            }
        }

        private void ScrollerOnIndexChanged(object sender, int e)
        {
            UpdateIndexFromWheel();
        }

        protected override void CreateDefaultContent()
        {
            if (this.Views.Count == 0)
            {
                var scroller = new SkiaWheelScroll()
                {
                    //todo properties
                    LinesColor = this.LinesColor,
                    Loop = false,
                    OpacityFadeStrength = 1.5f,
                    Margin = new(0, 10, 0, 10),
                    Bounces = true,
                    FrictionScrolled = 0.8f,
                    HorizontalOptions = LayoutOptions.Fill,
                    Orientation = ScrollOrientation.Vertical,
                    SnapToChildren = SnapToChildrenType.Center,
                    TrackIndexPosition = RelativePositionType.Center,
                    VerticalOptions = LayoutOptions.Fill,
                    ZIndex = 1,
                    Content =
                        new SkiaWheelStack()
                        {
                            Spacing = 1,
                            WidthRequest = -1,
                            BackgroundColor = Colors.Transparent,
                            VerticalOptions = LayoutOptions.Fill,
                            HorizontalOptions = LayoutOptions.Fill,
                            ItemsSource = ItemsSource,
                            ItemTemplate = this.ItemTemplate
                        }.Assign(out Wheel)
                };

                SetBackgroundView(this.BackgroundView, true);

                AttachScroller(scroller);

                SyncItemsSource();
            }
        }

        protected virtual void ApplyProperties()
        {
            if (Scroller != null)
            {
                Scroller.VisibleItemCount = VisibleItems;
                Scroller.LinesColor = this.LinesColor;
                //todo LinesThickness
            }
        }


        // Redirect ItemsSource property to inner wheel  
        public override void ApplyItemsSource()
        {
            SyncItemsSource();
        }

        // Redirect ItemsSource property to inner wheel  
        public override void OnItemSourceChanged()
        {
            SyncItemsSource();
        }

        // Redirect ItemsSource property to inner wheel  
        protected override void ItemsSourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            SyncItemsSource();
        }

        /// <summary>
        /// Redirect ItemsSource property to inner wheel shape
        /// </summary>
        void SyncItemsSource()
        {
            if (Wheel != null)
            {
                Wheel.ItemsSource = this.ItemsSource;
                Wheel.ItemTemplate = this.ItemTemplate ?? CreateDefaultTemplate();
            }

            UpdateWheelFromIndex();
        }

        protected int ItemsCount
        {
            get
            {
                if (ItemsSource == null)
                {
                    return 0;
                }

                return ItemsSource.Count;
            }
        }

        protected void UpdateIndexFromWheel()
        {
            if (ItemsCount == 0 || Scroller == null)
            {
                return;
            }

            _isUpdatingFromIndex = true;

            var index = Scroller.SelectedIndex;

            Debug.WriteLine($"UpdateIndexFromWheel: {index}");

            if (SelectedIndex != index)
            {
                SelectedIndex = index;
            }

            _isUpdatingFromIndex = false;
        }

        protected void UpdateWheelFromIndex()
        {
            if (ItemsCount == 0 || Scroller == null)
            {
                return;
            }

            Scroller.SelectedIndex = SelectedIndex;
        }

        #region EVENTS

        public event EventHandler<int> SelectedIndexChanged;

        #endregion

        #region BINDABLE PROPERTIES

        public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(nameof(SelectedIndex),
            typeof(int),
            typeof(SkiaWheelPicker),
            -1,
            BindingMode.TwoWay,
            null, propertyChanged: (b, o, n) =>
            {
                if (b is SkiaWheelPicker control && !control._isUpdatingFromIndex)
                {
                    control.UpdateWheelFromIndex();
                    control.SelectedIndexChanged?.Invoke(control, (int)n);
                }
            });

        public int SelectedIndex
        {
            get { return (int)GetValue(SelectedIndexProperty); }
            set { SetValue(SelectedIndexProperty, value); }
        }

        public static readonly BindableProperty VisibleItemsProperty = BindableProperty.Create(nameof(VisibleItems),
            typeof(int),
            typeof(SkiaWheelPicker),
            7,
            propertyChanged: NeedApplyProperties);

        public int VisibleItems
        {
            get { return (int)GetValue(VisibleItemsProperty); }
            set { SetValue(VisibleItemsProperty, value); }
        }

        public static readonly BindableProperty BackgroundViewProperty = BindableProperty.Create(
            nameof(BackgroundView),
            typeof(SkiaControl),
            typeof(SkiaWheelPicker),
            null, propertyChanged: (b, o, n) =>
            {
                if (b is SkiaWheelPicker control)
                {
                    control.SetBackgroundView((SkiaControl)n);
                }
            });

        public SkiaControl BackgroundView
        {
            get { return (SkiaControl)GetValue(BackgroundViewProperty); }
            set { SetValue(BackgroundViewProperty, value); }
        }

        public static readonly BindableProperty LinesColorProperty = BindableProperty.Create(
            nameof(LinesColor),
            typeof(Color),
            typeof(SkiaWheelPicker),
            defaultValue: Colors.White, propertyChanged: NeedApplyProperties);

        public Color LinesColor
        {
            get { return (Color)GetValue(LinesColorProperty); }
            set { SetValue(LinesColorProperty, value); }
        }

        public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
            nameof(TextColor),
            typeof(Color),
            typeof(SkiaWheelPicker),
            defaultValue: Colors.Grey, propertyChanged: NeedInvalidateScroller);

        public Color TextColor
        {
            get { return (Color)GetValue(TextColorProperty); }
            set { SetValue(TextColorProperty, value); }
        }

        public static readonly BindableProperty TextSelectedColorProperty = BindableProperty.Create(
            nameof(TextSelectedColor),
            typeof(Color),
            typeof(SkiaWheelPicker),
            defaultValue: Colors.Red, propertyChanged: NeedInvalidateScroller);

        public Color TextSelectedColor
        {
            get { return (Color)GetValue(TextSelectedColorProperty); }
            set { SetValue(TextSelectedColorProperty, value); }
        }

        private static void NeedApplyProperties(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is SkiaWheelPicker control)
            {
                control.ApplyProperties();
            }
        }

        private static void NeedInvalidateScroller(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is SkiaWheelPicker control)
            {
                control.Scroller?.Invalidate();
            }
        }

        #endregion

        #region INTERNALS

        bool _isUpdatingFromIndex;

        public void SetBackgroundView(SkiaControl view, bool force = false)
        {
            if (DefaultContentCreated || force)
            {
                var oldContent = Views.Except(new[] { Scroller }).FirstOrDefault();
                if (view != oldContent)
                {
                    if (oldContent != null)
                    {
                        RemoveSubView(oldContent);
                    }

                    if (view != null)
                    {
                        view.ZIndex = 1;
                        AddSubView(view);
                    }
                }
            }
        }

        protected virtual DataTemplate CreateDefaultTemplate()
        {
            return new DataTemplate(() =>
            {
                var cell = new SkiaWheelPickerCell(this.TextColor);

                return cell;
            });
        }

        #endregion
    }
}
