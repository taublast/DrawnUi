namespace DrawnUi.Draw
{
    public class SkiaPoint : BindableObject, IComparable, IComparable<SkiaPoint>, IEquatable<SkiaPoint>
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

        public int CompareTo(object obj)
        {
            if (obj is null)
            {
                return 1;
            }

            if (obj is not SkiaPoint other)
            {
                return 0;
            }

            return CompareTo(other);
        }

        public int CompareTo(SkiaPoint other)
        {
            if (ReferenceEquals(this, other))
            {
                return 0;
            }

            if (other is null)
            {
                return 1;
            }

            var result = X.CompareTo(other.X);
            if (result != 0)
            {
                return result;
            }

            return Y.CompareTo(other.Y);
        }

        public bool Equals(SkiaPoint other)
        {
            return CompareTo(other) == 0;
        }

        public override bool Equals(object obj)
        {
            return obj is SkiaPoint other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        public static bool operator ==(SkiaPoint left, SkiaPoint right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(SkiaPoint left, SkiaPoint right)
        {
            return !Equals(left, right);
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
