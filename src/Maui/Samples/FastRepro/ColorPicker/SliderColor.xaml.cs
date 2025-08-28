namespace Sandbox.Views.Controls;

public partial class SliderColor : SkiaSlider
{
    public SliderColor()
    {
        InitializeComponent();
    }

    public override void OnEndChanged()
    {
        SelectedColor = GetColorForValue(End);

        base.OnEndChanged();
    }

    public Color GetColorForValue(double sliderValue)
    {
        var colors = this.ColorPanel.PrimaryColors;
        if (colors.Count == 0)
        {
            colors = SkiaColorsPanel.DefaultPrimaryColors;
        }
        int numberOfColors = colors.Count;

        if (numberOfColors > 1)
        {
            sliderValue = Math.Clamp(sliderValue, 0.0, 1.0);

            double segmentSize = 1.0 / (numberOfColors - 1);
            int segmentIndex = (int)(sliderValue / segmentSize);

            // Handle edge case where sliderValue is 1.0
            if (segmentIndex >= numberOfColors - 1)
            {
                segmentIndex = numberOfColors - 2;
            }

            double segmentStartValue = segmentSize * segmentIndex;
            double fractionIntoSegment = (sliderValue - segmentStartValue) / segmentSize;

            Color startColor = colors[segmentIndex];
            Color endColor = colors[segmentIndex + 1];

            return InterpolateColor(startColor, endColor, fractionIntoSegment);
        }

        return Colors.Transparent;

    }

    private static Color InterpolateColor(Color start, Color end, double fraction)
    {
        float r = (float)(start.Red + (end.Red - start.Red) * fraction);
        float g = (float)(start.Green + (end.Green - start.Green) * fraction);
        float b = (float)(start.Blue + (end.Blue - start.Blue) * fraction);
        return new Color(r, g, b);
    }

    public static readonly BindableProperty SelectedColorProperty = BindableProperty.Create(
        nameof(SelectedColor),
        typeof(Color),
        typeof(SliderColor),
        Colors.Transparent,
        propertyChanged: SelectedColorChanged,
        defaultBindingMode: BindingMode.OneWayToSource);

    private static void SelectedColorChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SliderColor control)
        {
            control.Update();
        }
    }

    /// <summary>
    /// Resulting color, read-only
    /// </summary>
    public Color SelectedColor
    {
        get { return (Color)GetValue(SelectedColorProperty); }
        set { SetValue(SelectedColorProperty, value); }
    }


    public static readonly BindableProperty DefaultColorProperty = BindableProperty.Create(
        nameof(DefaultColor),
        typeof(Color),
        typeof(SliderColor),
        Colors.Transparent,
        propertyChanged: DefaultColorChanged,
        defaultBindingMode: BindingMode.OneWayToSource);

    /// <summary>
    /// Default color, set this at startup at will
    /// </summary>
    public Color DefaultColor
    {
        get { return (Color)GetValue(DefaultColorProperty); }
        set { SetValue(DefaultColorProperty, value); }
    }

    private static void DefaultColorChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SliderColor control)
        {
            control.ApplyDefaultColor();
        }
    }

    public virtual void ApplyDefaultColor()
    {
        SetSliderValueForColor(DefaultColor);
    }

    public void SetSliderValueForColor(Color color)
    {
        if (color != Colors.Transparent)
        {
            var hue = color.GetHue();
            var selectedColor = Color.FromHsv(hue, 1, 1);

            End = GetValueForColor(selectedColor);
        }
    }

    protected override void OnLayoutReady()
    {
        base.OnLayoutReady();

        SetSliderValueForColor(DefaultColor);
    }

    public double GetValueForColor(Color color)
    {
        var hue = color.GetHue();

        var sliderValue = (Max - Min) * hue;

        return sliderValue;
    }

    private static double FindClosestFraction(Color start, Color end, Color target)
    {
        double bestFraction = 0;
        double bestDistance = double.MaxValue;
        for (double fraction = 0; fraction <= 1; fraction += 0.01) // This can be fine-tuned for more precision
        {
            Color interpolatedColor = InterpolateColor(start, end, fraction);
            double currentDistance = ColorDistance(interpolatedColor, target);
            if (currentDistance < bestDistance)
            {
                bestDistance = currentDistance;
                bestFraction = fraction;
            }
        }
        return bestFraction;
    }

    private static double ColorDistance(Color c1, Color c2)
    {
        return Math.Sqrt(Math.Pow(c1.Red - c2.Red, 2) +
                         Math.Pow(c1.Green - c2.Green, 2) +
                         Math.Pow(c1.Blue - c2.Blue, 2));
    }



}