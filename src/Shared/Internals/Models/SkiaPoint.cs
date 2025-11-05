namespace DrawnUi.Draw
{
    public class SkiaPoint : BindableObject
    {
        public SkiaPoint()
        {

        }

        public SkiaPoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public static readonly BindableProperty XProperty = BindableProperty.Create(
            nameof(X),
            typeof(double),
            typeof(SkiaPoint),
            0.0,
            propertyChanged: OnPointPropertyChanged);

        public double X
        {
            get => (double)GetValue(XProperty);
            set => SetValue(XProperty, value);
        }

        public static readonly BindableProperty YProperty = BindableProperty.Create(
            nameof(Y),
            typeof(double),
            typeof(SkiaPoint),
            0.0,
            propertyChanged: OnPointPropertyChanged);

        public double Y
        {
            get => (double)GetValue(YProperty);
            set => SetValue(YProperty, value);
        }

        private static void OnPointPropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is SkiaPoint point)
            {
                point.ParentShape?.Update();
            }
        }

        internal SkiaShape ParentShape { get; set; }
    }
}
