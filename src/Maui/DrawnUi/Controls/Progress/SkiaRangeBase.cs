using System.Runtime.CompilerServices;

namespace DrawnUi.Draw;

/// <summary>
/// Base class for range-based controls like sliders and progress bars.
/// Provides common functionality for value ranges, track management, and platform styling.
/// </summary>
public abstract class SkiaRangeBase : SkiaLayout
{
    #region COMMON PROPERTIES

    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value),
        typeof(double),
        typeof(SkiaRangeBase),
        0.0,
        BindingMode.TwoWay,
        propertyChanged: OnValuePropertyChanged,
        coerceValue: CoerceValue);

    /// <summary>
    /// The current value of the range control
    /// </summary>
    public double Value
    {
        get { return (double)GetValue(ValueProperty); }
        set { SetValue(ValueProperty, value); }
    }

    public static readonly BindableProperty MinProperty =
        BindableProperty.Create(nameof(Min), typeof(double), typeof(SkiaRangeBase), 0.0);

    public double Min
    {
        get { return (double)GetValue(MinProperty); }
        set { SetValue(MinProperty, value); }
    }

    public static readonly BindableProperty MaxProperty =
        BindableProperty.Create(nameof(Max), typeof(double), typeof(SkiaRangeBase), 100.0);

    public double Max
    {
        get { return (double)GetValue(MaxProperty); }
        set { SetValue(MaxProperty, value); }
    }

    public static readonly BindableProperty StepProperty =
        BindableProperty.Create(nameof(Step), typeof(double), typeof(SkiaRangeBase), 0.0);

    public double Step
    {
        get { return (double)GetValue(StepProperty); }
        set { SetValue(StepProperty, value); }
    }

    #endregion

    #region VISUAL PROPERTIES

    public static readonly BindableProperty TrackColorProperty =
        BindableProperty.Create(nameof(TrackColor), typeof(Color), typeof(SkiaRangeBase), 
            new Color(0.8f, 0.8f, 0.8f));

    /// <summary>
    /// The color of the background track
    /// </summary>
    public Color TrackColor
    {
        get { return (Color)GetValue(TrackColorProperty); }
        set { SetValue(TrackColorProperty, value); }
    }

    public static readonly BindableProperty ProgressColorProperty =
        BindableProperty.Create(nameof(ProgressColor), typeof(Color), typeof(SkiaRangeBase),
            new Color(0f, 0.478f, 1f));

    /// <summary>
    /// The color of the progress/selected portion
    /// </summary>
    public Color ProgressColor
    {
        get { return (Color)GetValue(ProgressColorProperty); }
        set { SetValue(ProgressColorProperty, value); }
    }

    public static readonly BindableProperty TrackHeightProperty =
        BindableProperty.Create(nameof(TrackHeight), typeof(double), typeof(SkiaRangeBase), 4.0);

    /// <summary>
    /// The height of the track
    /// </summary>
    public double TrackHeight
    {
        get { return (double)GetValue(TrackHeightProperty); }
        set { SetValue(TrackHeightProperty, value); }
    }

    #endregion

    #region INTERNAL PROPERTIES

    protected SkiaControl Track;
    protected SkiaControl ProgressTrail;

    protected volatile bool lockInternal;

    public static readonly BindableProperty InvertProperty =
        BindableProperty.Create(nameof(Invert), typeof(bool), typeof(SkiaRangeBase), false);

    public bool Invert
    {
        get { return (bool)GetValue(InvertProperty); }
        set { SetValue(InvertProperty, value); }
    }

    #endregion

    #region VALUE CONVERSION

    protected double AdjustToStepValue(double value, double minValue, double stepValue)
    {
        if (stepValue <= 0) return value;

        double relativeValue = value - minValue;
        double adjustedStep = Math.Round(relativeValue / stepValue) * stepValue;
        return adjustedStep + minValue;
    }

    protected virtual double ValueFromPosition(double position, double totalLength)
    {
        if (totalLength <= 0) return Min;

        double ratio = position / totalLength;
        if (Invert)
        {
            return Max - ratio * (Max - Min);
        }
        else
        {
            return Min + ratio * (Max - Min);
        }
    }

    protected virtual double PositionFromValue(double value, double totalLength)
    {
        if (totalLength <= 0) return 0;

        double ratio;
        if (Invert)
        {
            ratio = (Max - value) / (Max - Min);
        }
        else
        {
            ratio = (value - Min) / (Max - Min);
        }

        return ratio * totalLength;
    }

    #endregion

    #region EVENTS

    public event EventHandler<double> ValueChanged;

    protected virtual void OnValueChanged()
    {
        ValueChanged?.Invoke(this, Value);
    }

    public override void OnDisposing()
    {
        base.OnDisposing();
        ValueChanged = null;
    }

    #endregion

    #region PROPERTY CHANGE HANDLING

    private static void OnValuePropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SkiaRangeBase range && newValue is double newValueDouble)
        {
            range.OnValueChanged();
        }
    }

    private static object CoerceValue(BindableObject bindable, object value)
    {
        var newValue = (double)value;

        if (bindable is SkiaRangeBase range)
        {
            var adjusted = range.AdjustToStepValue(newValue, range.Min, range.Step);
            return Math.Clamp(adjusted, range.Min, range.Max);
        }

        return value;
    }

    protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (lockInternal)
            return;

        if (propertyName.IsEither(nameof(Value), nameof(Min), nameof(Max), 
            nameof(TrackColor), nameof(ProgressColor), nameof(TrackHeight)))
        {
            UpdateVisualState();
        }
    }

    #endregion

    #region ABSTRACT METHODS

    /// <summary>
    /// Override to find and assign Track and ProgressTrail references
    /// </summary>
    protected abstract void FindViews();

    /// <summary>
    /// Override to update the visual appearance based on current properties
    /// </summary>
    protected abstract void UpdateVisualState();

    #endregion

    #region LAYOUT OVERRIDES

    public override void OnChildrenChanged()
    {
        base.OnChildrenChanged();
        FindViews();
    }

    protected override void OnLayoutChanged()
    {
        base.OnLayoutChanged();
        UpdateVisualState();
    }

    #endregion
}
