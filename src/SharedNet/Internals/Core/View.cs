using DrawnUi.Views;

namespace DrawnUi.Draw
{
    public class Element : DrawnUi.Draw.BindableObject
    {
        public virtual Element Parent { get; set; }

        protected virtual void OnParentSet()
        {
        }

        protected virtual void OnChildRemoved(Element child, int oldLogicalIndex)
        {
        }

        protected virtual void OnChildAdded(Element child)
        {
        }
    }

    public class View : Element, DrawnUi.Draw.IView
    {
        public object Handler { get; set; }

        public event EventHandler SizeChanged;

        /// <summary>
        /// ToDo
        /// </summary>
        public bool IsEnabled { get; set; }

        public IList<object> Effects { get; } = new List<object>();

        public IList<object> Behaviors { get; } = new List<object>();

        public new virtual bool IsVisible { get; set; } = true;

        public virtual bool InputTransparent { get; set; }

        public virtual bool IsClippedToBounds { get; set; }


        public static readonly BindableProperty TranslationXProperty = BindableProperty.Create(nameof(TranslationX),
            typeof(double), typeof(SkiaControl),
            0.0);
        public double TranslationX
        {
            get { return (double)GetValue(TranslationXProperty); }
            set
            {
                SetValue(TranslationXProperty, value);
            }
        }

        public static readonly BindableProperty TranslationYProperty = BindableProperty.Create(nameof(TranslationY),
            typeof(double), typeof(SkiaControl),
            0.0);
        public double TranslationY
        {
            get { return (double)GetValue(TranslationYProperty); }
            set { SetValue(TranslationYProperty, value); }
        }

        public static readonly BindableProperty ZIndexProperty = BindableProperty.Create(nameof(ZIndex),
            typeof(int), typeof(SkiaControl),
            0);

        public int ZIndex
        {
            get { return (int)GetValue(ZIndexProperty); }
            set { SetValue(ZIndexProperty, value); }
        }

        public static readonly BindableProperty OpacityProperty = BindableProperty.Create(nameof(Opacity),
            typeof(double), typeof(SkiaControl),
            1.0);
        /// <summary>
        /// This is used after RenderingObject is created along with other transforms like translation etc, so it's fast to be applied over a cached drawing result.
        /// </summary>
        public double Opacity
        {
            get { return (double)GetValue(OpacityProperty); }
            set { SetValue(OpacityProperty, value); }
        }

        public virtual double Rotation { get; set; }

        public static readonly BindableProperty RotationXProperty = BindableProperty.Create(nameof(RotationX),
            typeof(double), typeof(SkiaControl),
            0.0);
        public double RotationX
        {
            get { return (double)GetValue(RotationXProperty); }
            set
            {
                SetValue(RotationXProperty, value);
            }
        }

        public static readonly BindableProperty RotationYProperty = BindableProperty.Create(nameof(RotationY),
            typeof(double), typeof(SkiaControl),
            0.0);
        public double RotationY
        {
            get { return (double)GetValue(RotationYProperty); }
            set { SetValue(RotationYProperty, value); }
        }

        public static readonly BindableProperty AnchorXProperty = BindableProperty.Create(nameof(AnchorX),
            typeof(double), typeof(SkiaControl),
            0.5);
        public double AnchorX
        {
            get { return (double)GetValue(AnchorXProperty); }
            set
            {
                SetValue(AnchorXProperty, value);
            }
        }

        public static readonly BindableProperty AnchorYProperty = BindableProperty.Create(nameof(AnchorY),
            typeof(double), typeof(SkiaControl),
            0.5);
        public double AnchorY
        {
            get { return (double)GetValue(AnchorYProperty); }
            set { SetValue(AnchorYProperty, value); }
        }

        public static readonly BindableProperty ScaleXProperty = BindableProperty.Create(nameof(ScaleX),
            typeof(double), typeof(SkiaControl),
            1.0);
        public double ScaleX
        {
            get { return (double)GetValue(ScaleXProperty); }
            set
            {
                SetValue(ScaleXProperty, value);
            }
        }

        public static readonly BindableProperty ScaleYProperty = BindableProperty.Create(nameof(ScaleY),
            typeof(double), typeof(SkiaControl),
            1.0);
        public double ScaleY
        {
            get { return (double)GetValue(ScaleYProperty); }
            set { SetValue(ScaleYProperty, value); }
        }

        public static readonly BindableProperty WidthRequestProperty = BindableProperty.Create(nameof(WidthRequest),
            typeof(double), typeof(SkiaControl),
            -1.0);
        public double WidthRequest
        {
            get { return (double)GetValue(WidthRequestProperty); }
            set { SetValue(WidthRequestProperty, value); }
        }

        public static readonly BindableProperty HeightRequestProperty = BindableProperty.Create(nameof(HeightRequest),
            typeof(double), typeof(SkiaControl),
            -1.0);
        public double HeightRequest
        {
            get { return (double)GetValue(HeightRequestProperty); }
            set { SetValue(HeightRequestProperty, value); }
        }

        public static readonly BindableProperty MaximumHeightRequestProperty = BindableProperty.Create(nameof(MaximumHeightRequest),
            typeof(double), typeof(SkiaControl),
            double.PositiveInfinity);
        public double MaximumHeightRequest
        {
            get { return (double)GetValue(MaximumHeightRequestProperty); }
            set { SetValue(MaximumHeightRequestProperty, value); }
        }

        public static readonly BindableProperty MaximumWidthRequestProperty = BindableProperty.Create(nameof(MaximumWidthRequest),
            typeof(double), typeof(SkiaControl),
            double.PositiveInfinity);
        public double MaximumWidthRequest
        {
            get { return (double)GetValue(MaximumWidthRequestProperty); }
            set { SetValue(MaximumWidthRequestProperty, value); }
        }


        public static readonly BindableProperty MinimumWidthRequestProperty = BindableProperty.Create(nameof(MinimumWidthRequest),
            typeof(double), typeof(SkiaControl),
            -1.0);
        public double MinimumWidthRequest
        {
            get { return (double)GetValue(MinimumWidthRequestProperty); }
            set { SetValue(MinimumWidthRequestProperty, value); }
        }

        public static readonly BindableProperty MinimumHeightRequestProperty = BindableProperty.Create(nameof(MinimumHeightRequest),
            typeof(double), typeof(SkiaControl),
            -1.0);
        public double MinimumHeightRequest
        {
            get { return (double)GetValue(MinimumHeightRequestProperty); }
            set { SetValue(MinimumHeightRequestProperty, value); }
        }

        public virtual double X { get; set; }

        public virtual double Y { get; set; }

        public virtual double Width { get; set; } = -1;

        public virtual double Height { get; set; } = -1;

        public static readonly BindableProperty ShadowProperty = BindableProperty.Create(nameof(Shadow), typeof(Shadow), typeof(SkiaControl),
            default(Shadow));
        public Shadow Shadow
        {
            get { return (Shadow)GetValue(ShadowProperty); }
            set { SetValue(ShadowProperty, value); }
        }

        public virtual Geometry Clip { get; set; }

        public virtual Brush Background { get; set; }

        public virtual Style Style { get; set; }

        public virtual Thickness Padding { get; set; }

        public virtual Thickness Margin { get; set; }

        public static readonly BindableProperty VerticalOptionsProperty = BindableProperty.Create(nameof(VerticalOptions),
            typeof(LayoutOptions),
            typeof(SkiaControl),
            LayoutOptions.Start);
        public LayoutOptions VerticalOptions
        {
            get { return (LayoutOptions)GetValue(VerticalOptionsProperty); }
            set { SetValue(VerticalOptionsProperty, value); }
        }

        public static readonly BindableProperty HorizontalOptionsProperty = BindableProperty.Create(nameof(HorizontalOptions),
            typeof(LayoutOptions),
            typeof(SkiaControl),
            LayoutOptions.Start);
        public LayoutOptions HorizontalOptions
        {
            get { return (LayoutOptions)GetValue(HorizontalOptionsProperty); }
            set { SetValue(HorizontalOptionsProperty, value); }
        }

        public static readonly BindableProperty BackgroundColorProperty = BindableProperty.Create(nameof(BackgroundColor), typeof(Color), typeof(SkiaControl),
            Colors.Transparent);
        public Color BackgroundColor
        {
            get { return (Color)GetValue(BackgroundColorProperty); }
            set { SetValue(BackgroundColorProperty, value); }
        }

        public virtual void Update()
        {
        }

        protected void RaiseSizeChanged()
        {
            SizeChanged?.Invoke(this, EventArgs.Empty);
        }

        protected virtual SizeRequest OnMeasure(double widthConstraint, double heightConstraint)
        {
            return new SizeRequest(new Size(widthConstraint, heightConstraint), new Size(0, 0));
        }

        public virtual void DisconnectHandlers()
        {
        }
    }
}
