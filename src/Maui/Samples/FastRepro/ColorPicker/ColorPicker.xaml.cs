namespace Sandbox.Views.Controls;

public partial class ColorPicker
{
    private bool _isUpdatingFromCode = false;

    public ColorPicker()
    {
        InitializeComponent();
    }

    public static readonly BindableProperty SelectedColorProperty = BindableProperty.Create(
        nameof(SelectedColor),
        typeof(Color),
        typeof(ColorPicker),
        Colors.White,
        propertyChanged: SelectedColorChanged,
        defaultBindingMode: BindingMode.TwoWay);

    private static void SelectedColorChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is ColorPicker control)
        {
            // When SelectedColor is set from outside, update the picker position
            if (newvalue is Color color && oldvalue != newvalue && !control._isUpdatingFromCode)
            {
                control.SetupForSelectedColor(color);
            }
            control.Update();
        }
    }

    /// <summary>
    /// Selected color with 2-way binding support
    /// </summary>
    public Color SelectedColor
    {
        get { return (Color)GetValue(SelectedColorProperty); }
        set { SetValue(SelectedColorProperty, value); }
    }

    /// <summary>
    /// Sets up the picker position for the given color
    /// </summary>
    /// <param name="color"></param>
    public void SetupForSelectedColor(Color color)
    {
        if (Slider != null && ColorsPanel != null)
        {
            // Prevent circular updates while setting up
            _isUpdatingFromCode = true;

            try
            {
                //set horizontal slider thumb position
                Slider.SetSliderValueForColor(color);
                ClearColor = Slider.SelectedColor;

                //set panel indicator position
                var saturation = color.GetSaturation();
                var lumi = color.GetLuminosity();
                ColorsPanel.PointerRingPositionXOffsetRatio = saturation;
                ColorsPanel.PointerRingPositionYOffsetRatio = lumi;

                // Ensure indicator is updated after layout is ready
                if (ColorsPanel.IsLayoutReady)
                {
                    ColorsPanel.SetupIndicator();
                }
                else
                {
                    // Wait for layout to be ready, then update indicator
                    ColorsPanel.LayoutIsReady += (s, e) =>
                    {
                        ColorsPanel.SetupIndicator();
                    };
                }
            }
            finally
            {
                _isUpdatingFromCode = false;
            }
        }
    }

    private Color _clearColor;
    public Color ClearColor
    {
        get
        {
            return _clearColor;
        }
        set
        {
            if (_clearColor != value)
            {
                _clearColor = value;
                OnPropertyChanged();

                ColorsPanel.SelectionColors = new List<Color>()
                {
                    Colors.White,
                    value
                };
            }
        }
    }

    public void OnSliderValueChanged(object sender, double e)
    {
        // Only update if not already updating from code
        if (!_isUpdatingFromCode)
        {
            ClearColor = Slider.SelectedColor;
        }
    }

    private void ColorsPanelSelectionChanged(object sender, Color value)
    {
        // Prevent circular updates when user actually picks a color
        _isUpdatingFromCode = true;
        SelectedColor = value;
        _isUpdatingFromCode = false;
    }

}