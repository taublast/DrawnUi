namespace DrawnUi.Draw;

/// <summary>
/// Button-like control, can include any content inside. It's either you use default content (todo templates?..)
/// or can include any content inside, and properties will by applied by convention to a SkiaLabel with Tag `MainLabel`, SkiaShape with Tag `MainFrame`. At the same time you can override ApplyProperties() and apply them to your content yourself.
/// </summary>
public partial class SkiaButton : SkiaLayout, ISkiaGestureListener
{
    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(
        nameof(CornerRadius),
        typeof(CornerRadius),
        typeof(SkiaButton),
        new CornerRadius(8),
        propertyChanged: NeedApplyProperties);

    [System.ComponentModel.TypeConverter(typeof(Microsoft.Maui.Converters.CornerRadiusTypeConverter))]
    public CornerRadius CornerRadius
    {
        get { return (CornerRadius)GetValue(CornerRadiusProperty); }
        set { SetValue(CornerRadiusProperty, value); }
    }

    public static readonly BindableProperty BevelProperty = BindableProperty.Create(
    nameof(Bevel),
    typeof(SkiaBevel),
    typeof(SkiaButton),
    null,
    propertyChanged: NeedApplyProperties);

    public SkiaBevel Bevel
    {
        get { return (SkiaBevel)GetValue(BevelProperty); }
        set { SetValue(BevelProperty, value); }
    }

    public static readonly BindableProperty BevelTypeProperty = BindableProperty.Create(
    nameof(BevelType),
    typeof(BevelType),
    typeof(SkiaButton),
    BevelType.None,
    propertyChanged: NeedApplyProperties);

    public BevelType BevelType
    {
        get { return (BevelType)GetValue(BevelTypeProperty); }
        set { SetValue(BevelTypeProperty, value); }
    }

    protected Color InitialBackgroundColor;
    protected Brush InitialBackground;

}
