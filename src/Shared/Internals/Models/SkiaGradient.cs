using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace DrawnUi.Draw;

public partial class SkiaGradient : BindableObject, ICloneable, IComparable, IComparable<SkiaGradient>, IEquatable<SkiaGradient>
{
    public ISkiaControl Parent { get; set; }

    public object Clone()
    {
        return new SkiaGradient
        {
            Type = Type,
            TileMode = TileMode,
            Light = Light,
            Colors = Colors,
            ColorPositions = ColorPositions,
            StartXRatio = StartXRatio,
            StartYRatio = StartYRatio,
            EndXRatio = EndXRatio,
            EndYRatio = EndYRatio,
            Opacity = Opacity
        };
    }

    public static SkiaGradient FromBrush(GradientBrush gradientBrush)
    {
        SkiaGradient gradient = null;

        if (gradientBrush is LinearGradientBrush linear)
        {
            gradient = new SkiaGradient()
            {
                Type = GradientType.Linear,
                Colors = linear.GradientStops.OrderBy(x=>x.Offset).Select(x => x.Color).ToList(),
                ColorPositions = linear.GradientStops.OrderBy(x => x.Offset).Select(x => (double)x.Offset).ToList(),
                StartXRatio = (float)linear.StartPoint.X,
                StartYRatio = (float)linear.StartPoint.Y,
                EndXRatio = (float)linear.EndPoint.X,
                EndYRatio = (float)linear.EndPoint.Y,
            };
        }
        else
        if (gradientBrush is RadialGradientBrush radial)
        {
            gradient = new SkiaGradient()
            {
                Type = GradientType.Oval, //MAUI is using oval and not circle for this
                Colors = radial.GradientStops.OrderBy(x => x.Offset).Select(x => x.Color).ToList(),
                ColorPositions = radial.GradientStops.Select(x => (double)x.Offset).ToList(),
                StartXRatio = (float)radial.Center.X,
                StartYRatio = (float)radial.Center.Y
            };
        }
        return gradient;
    }

    public static (double X1, double Y1, double X2, double Y2) LinearGradientAngleToPoints(double direction)
    {
        //adapt to css style
        direction -= 90;

        //allow negative angles
        if (direction < 0)
            direction = 360 + direction;

        if (direction > 360)
            direction = 360;

        (double x, double y) pointOfAngle(double a)
        {
            return (x: Math.Cos(a), y: Math.Sin(a));
        }

        ;

        double degreesToRadians(double d)
        {
            return ((d * Math.PI) / 180);
        }

        var eps = Math.Pow(2, -52);
        var angle = (direction % 360);
        var startPoint = pointOfAngle(degreesToRadians(180 - angle));
        var endPoint = pointOfAngle(degreesToRadians(360 - angle));

        if (startPoint.x <= 0 || Math.Abs(startPoint.x) <= eps)
            startPoint.x = 0;

        if (startPoint.y <= 0 || Math.Abs(startPoint.y) <= eps)
            startPoint.y = 0;

        if (endPoint.x <= 0 || Math.Abs(endPoint.x) <= eps)
            endPoint.x = 0;

        if (endPoint.y <= 0 || Math.Abs(endPoint.y) <= eps)
            endPoint.y = 0;

        return (startPoint.x, startPoint.y, endPoint.x, endPoint.y);
    }


    /// <summary>
    /// Incremented whenever any gradient property changes. Consumers can cache shaders
    /// keyed on this version to avoid rebuilding native objects every frame.
    /// </summary>
    public int Version => _version;
    private int _version;

    public int CompareTo(object obj)
    {
        if (obj is null)
        {
            return 1;
        }

        if (obj is not SkiaGradient other)
        {
            return 0;
        }

        return CompareTo(other);
    }

    public int CompareTo(SkiaGradient other)
    {
        if (ReferenceEquals(this, other))
        {
            return 0;
        }

        if (other is null)
        {
            return 1;
        }

        var result = Type.CompareTo(other.Type);
        if (result != 0)
        {
            return result;
        }

        result = BlendMode.CompareTo(other.BlendMode);
        if (result != 0)
        {
            return result;
        }

        result = TileMode.CompareTo(other.TileMode);
        if (result != 0)
        {
            return result;
        }

        result = Light.CompareTo(other.Light);
        if (result != 0)
        {
            return result;
        }

        result = Opacity.CompareTo(other.Opacity);
        if (result != 0)
        {
            return result;
        }

        result = StartXRatio.CompareTo(other.StartXRatio);
        if (result != 0)
        {
            return result;
        }

        result = StartYRatio.CompareTo(other.StartYRatio);
        if (result != 0)
        {
            return result;
        }

        result = EndXRatio.CompareTo(other.EndXRatio);
        if (result != 0)
        {
            return result;
        }

        result = EndYRatio.CompareTo(other.EndYRatio);
        if (result != 0)
        {
            return result;
        }

        result = CompareColorLists(Colors, other.Colors);
        if (result != 0)
        {
            return result;
        }

        return CompareDoubleLists(ColorPositions, other.ColorPositions);
    }

    public bool Equals(SkiaGradient other)
    {
        return CompareTo(other) == 0;
    }

    public override bool Equals(object obj)
    {
        return obj is SkiaGradient other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Type);
        hash.Add(BlendMode);
        hash.Add(TileMode);
        hash.Add(Light);
        hash.Add(Opacity);
        hash.Add(StartXRatio);
        hash.Add(StartYRatio);
        hash.Add(EndXRatio);
        hash.Add(EndYRatio);

        if (Colors != null)
        {
            foreach (var color in Colors)
            {
                hash.Add(color);
            }
        }

        if (ColorPositions != null)
        {
            foreach (var position in ColorPositions)
            {
                hash.Add(position);
            }
        }

        return hash.ToHashCode();
    }

    public static bool operator ==(SkiaGradient left, SkiaGradient right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(SkiaGradient left, SkiaGradient right)
    {
        return !Equals(left, right);
    }

    private static int CompareColorLists(IList<Color> left, IList<Color> right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return right is null ? 0 : -1;
        }

        if (right is null)
        {
            return 1;
        }

        var countResult = left.Count.CompareTo(right.Count);
        if (countResult != 0)
        {
            return countResult;
        }

        for (int i = 0; i < left.Count; i++)
        {
            countResult = CompareColor(left[i], right[i]);
            if (countResult != 0)
            {
                return countResult;
            }
        }

        return 0;
    }

    private static int CompareDoubleLists(IList<double> left, IList<double> right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return right is null ? 0 : -1;
        }

        if (right is null)
        {
            return 1;
        }

        var countResult = left.Count.CompareTo(right.Count);
        if (countResult != 0)
        {
            return countResult;
        }

        for (int i = 0; i < left.Count; i++)
        {
            countResult = left[i].CompareTo(right[i]);
            if (countResult != 0)
            {
                return countResult;
            }
        }

        return 0;
    }

    private static int CompareColor(Color left, Color right)
    {
        var result = left.Red.CompareTo(right.Red);
        if (result != 0)
        {
            return result;
        }

        result = left.Green.CompareTo(right.Green);
        if (result != 0)
        {
            return result;
        }

        result = left.Blue.CompareTo(right.Blue);
        if (result != 0)
        {
            return result;
        }

        return left.Alpha.CompareTo(right.Alpha);
    }

    private static void RedrawCanvas(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaGradient gradient)
        {
            gradient._version++;
            gradient.Parent?.Update();
        }
    }

    public static readonly BindableProperty AngleProperty = BindableProperty.Create(
        nameof(Angle),
        typeof(double?),
        typeof(SkiaGradient),
        null,
        propertyChanged: AnglePropertyChanged);

    public double? Angle
    {
        get => (double?)GetValue(AngleProperty);
        set => SetValue(AngleProperty, value);
    }

    private static void AnglePropertyChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaGradient gradient && newvalue is double angle)
        {
            var (x1, y1, x2, y2) = LinearGradientAngleToPoints(angle);
            gradient.StartXRatio = (float)x1;
            gradient.StartYRatio = (float)y1;
            gradient.EndXRatio = (float)x2;
            gradient.EndYRatio = (float)y2;
        }
    }

    public static readonly BindableProperty TypeProperty = BindableProperty.Create(nameof(Type), typeof(GradientType), typeof(SkiaGradient),
        GradientType.Linear,
        propertyChanged: RedrawCanvas);
    public GradientType Type
    {
        get { return (GradientType)GetValue(TypeProperty); }
        set { SetValue(TypeProperty, value); }
    }

    public static readonly BindableProperty BlendModeProperty = BindableProperty.Create(nameof(BlendMode),
        typeof(SKBlendMode), typeof(SkiaGradient),
        SKBlendMode.SrcOver,
        propertyChanged: RedrawCanvas);
    public SKBlendMode BlendMode
    {
        get { return (SKBlendMode)GetValue(BlendModeProperty); }
        set { SetValue(BlendModeProperty, value); }
    }

    public static readonly BindableProperty TileModeProperty = BindableProperty.Create(nameof(TileMode), typeof(SKShaderTileMode), typeof(SkiaGradient),
        SKShaderTileMode.Clamp,
        propertyChanged: RedrawCanvas);
    public SKShaderTileMode TileMode
    {
        get { return (SKShaderTileMode)GetValue(TileModeProperty); }
        set { SetValue(TileModeProperty, value); }
    }


    public static readonly BindableProperty LightProperty = BindableProperty.Create(nameof(Light), typeof(double), typeof(SkiaGradient), 1.0,
        propertyChanged: RedrawCanvas);
    public double Light
    {
        get { return (double)GetValue(LightProperty); }
        set { SetValue(LightProperty, value); }
    }

    public static readonly BindableProperty OpacityProperty = BindableProperty.Create(nameof(Opacity),
        typeof(float), typeof(SkiaGradient),
        1.0f,
        propertyChanged: RedrawCanvas);
    public float Opacity
    {
        get { return (float)GetValue(OpacityProperty); }
        set { SetValue(OpacityProperty, value); }
    }

    public static readonly BindableProperty StartXRatioProperty = BindableProperty.Create(nameof(StartXRatio), typeof(float), typeof(SkiaGradient), 0.0f,
        propertyChanged: RedrawCanvas);
    public float StartXRatio
    {
        get { return (float)GetValue(StartXRatioProperty); }
        set { SetValue(StartXRatioProperty, value); }
    }

    public static readonly BindableProperty StartYRatioProperty = BindableProperty.Create(nameof(StartYRatio), typeof(float), typeof(SkiaGradient), 0.0f,
        propertyChanged: RedrawCanvas);
    public float StartYRatio
    {
        get { return (float)GetValue(StartYRatioProperty); }
        set { SetValue(StartYRatioProperty, value); }
    }

    public static readonly BindableProperty EndXRatioProperty = BindableProperty.Create(nameof(EndXRatio), typeof(float), typeof(SkiaGradient), 0.0f,
        propertyChanged: RedrawCanvas);
    public float EndXRatio
    {
        get { return (float)GetValue(EndXRatioProperty); }
        set { SetValue(EndXRatioProperty, value); }
    }

    public static readonly BindableProperty EndYRatioProperty = BindableProperty.Create(nameof(EndYRatio), typeof(float), typeof(SkiaGradient), 1.0f,
        propertyChanged: RedrawCanvas);
    public float EndYRatio
    {
        get { return (float)GetValue(EndYRatioProperty); }
        set { SetValue(EndYRatioProperty, value); }
    }

    #region COLORS


    public static readonly BindableProperty ColorsProperty = BindableProperty.Create(
        nameof(Colors),
        typeof(IList<Color>),
        typeof(SkiaGradient),
        defaultValueCreator: (instance) =>
        {
            var created = new ObservableAttachedItemsCollection<Color>();
            created.CollectionChanged += ((SkiaGradient)instance).OnSkiaPropertyColorCollectionChanged;
            return created;
        },
        validateValue: (bo, v) => v is IList<Color>,
        propertyChanged: ColorsPropertyChanged,
        coerceValue: CoerceColors);


    public IList<Color> Colors
    {
        get => (IList<Color>)GetValue(ColorsProperty);
        set => SetValue(ColorsProperty, value);
    }

    private static object CoerceColors(BindableObject bindable, object value)
    {
        if (!(value is ReadOnlyCollection<Color> readonlyCollection))
        {
            return value;
        }

        return new ReadOnlyCollection<Color>(
            readonlyCollection.Select(s => s)
                .ToList());
    }

    private static void ColorsPropertyChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaGradient gradient)
        {
            if (oldvalue is INotifyCollectionChanged oldCollection)
            {
                oldCollection.CollectionChanged -= gradient.OnSkiaPropertyColorCollectionChanged;
            }
            if (newvalue is INotifyCollectionChanged newCollection)
            {
                newCollection.CollectionChanged += gradient.OnSkiaPropertyColorCollectionChanged;
            }

            gradient._version++;
            gradient.Parent?.Update();
        }
    }

    private void OnSkiaPropertyColorCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        _version++;
        this.Parent?.Update();
    }

    #endregion

    #region COLOR POSITIONS


    public static readonly BindableProperty ColorPositionsProperty = BindableProperty.Create(
        nameof(ColorPositions),
        typeof(IList<double>),
        typeof(SkiaGradient),
        defaultValueCreator: (instance) =>
        {
            var created = new ObservableAttachedItemsCollection<double>();
            created.CollectionChanged += ((SkiaGradient)instance).OnColorPositionsCollectionChanged;
            return created;
        },
        validateValue: (bo, v) => v is IList<double>,
        propertyChanged: ColorPositionsPropertyChanged,
        coerceValue: CoerceColorPositions);


    public IList<double> ColorPositions
    {
        get => (IList<double>)GetValue(ColorPositionsProperty);
        set => SetValue(ColorPositionsProperty, value);
    }

    private static object CoerceColorPositions(BindableObject bindable, object value)
    {
        if (!(value is ReadOnlyCollection<double> readonlyCollection))
        {
            return value;
        }

        return new ReadOnlyCollection<double>(
            readonlyCollection.Select(s => s)
                .ToList());
    }

    private static void ColorPositionsPropertyChanged(BindableObject bindable, object oldvalue, object newvalue)
    {

        if (bindable is SkiaGradient gradient)
        {
            if (oldvalue is INotifyCollectionChanged oldCollection)
            {
                oldCollection.CollectionChanged -= gradient.OnColorPositionsCollectionChanged;
            }
            if (newvalue is INotifyCollectionChanged newCollection)
            {
                newCollection.CollectionChanged += gradient.OnColorPositionsCollectionChanged;
            }

            gradient._version++;
            gradient.Parent?.Update();
        }

    }

    private void OnColorPositionsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        _version++;
        this.Parent?.Update();
    }

    #endregion

}
