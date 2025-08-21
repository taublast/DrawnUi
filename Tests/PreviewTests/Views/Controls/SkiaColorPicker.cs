using AppoMobi.Maui.Gestures;
using SkiaSharp.Views.Maui;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

//reusing code from https://github.com/nor0x/Maui.ColorPicker

namespace PreviewTests.Views.Controls;

public partial class SkiaColorPicker : SkiaLayout
{
    protected SkiaColorsPanel Panel { get; set; }

    protected SkiaShape Indicator { get; set; }

    public void SetupGradientPanel()
    {
        if (Panel == null)
        {
            Panel = new()
            {
                BackgroundColor = Colors.White, //need opaque
                ZIndex = -1,
                UseCache = SkiaCacheType.Image,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill
            };
            if (Views.All(x => x != Panel))
            {
                AddSubView(Panel);
            }
        }

        if (!_colorsSet)
        {
            _colorsSet = true;
            Panel.Direction = this.ColorFlowDirection;

            var primary = GetPrimaryLayerColors();
            var secondary = GetSecondaryLayerColors(ColorSpectrumStyle);

            //if (ColorSpectrumStyle == ColorSpectrumStyle.Shades)
            //{
            //    Panel.PrimaryColors = secondary;
            //    Panel.SecondaryColors = primary;
            //}
            //else
            {
                Panel.PrimaryColors = primary;
                Panel.SecondaryColors = secondary;
                Panel.ColorsBlendMode = this.ColorsBlendMode;
            }
        }
    }

    protected virtual void UpdateColors()
    {
        _colorsSet = false;
        SetupGradientPanel();
    }


    public virtual void SetupIndicator()
    {
        if (Indicator == null)
        {
            Indicator = new()
            {
                UseCache = SkiaCacheType.Operations,
                StrokeColor = Microsoft.Maui.Graphics.Colors.White,
                StrokeWidth = PointerRingBorderUnits,
                Type = ShapeType.Circle,
                LockRatio = 1,
                HeightRequest = PointerRingDiameterUnits
            };
            if (Views.All(x => x != Indicator))
            {
                AddSubView(Indicator);
            }
        }

        Indicator.StrokeWidth = PointerRingBorderUnits;

        Indicator.TranslationX = (DrawingRect.Width * PointerRingPositionXOffsetRatio) / RenderingScale - PointerRingDiameterUnits / 2f;
        Indicator.TranslationY = (DrawingRect.Height * PointerRingPositionYOffsetRatio) / RenderingScale - PointerRingDiameterUnits / 2f;
    }

    protected virtual void UpdateIndicator()
    {
        var x = ((float)DrawingRect.Left + DrawingRect.Width * (float)PointerRingPositionXOffsetRatio);
        var y = ((float)DrawingRect.Top + DrawingRect.Height * (float)PointerRingPositionYOffsetRatio);
        _lastTouchPoint = new SKPoint(x, y);

        SetupIndicator();
    }

    protected override void OnLayoutChanged()
    {
        base.OnLayoutChanged();

        SetupGradientPanel();
        SetupIndicator();
    }




    protected override int RenderViewsList(DrawingContext context, IEnumerable<SkiaControl> skiaControls)
    {
        var count = 0;
        foreach (var child in skiaControls)
        {
            if (child != null)
            {
                if (child == Indicator)
                {
                    //just get the pixels color under before drawing it
                    SKColor touchPointColor = SKColor.Empty;

                    using (var bitmap = SKBitmap.FromImage(Panel.RenderObject.Image))
                    {
                        var x = (int)Math.Round((child.TranslationX + child.Width / 2) * context.Scale);
                        var y = (int)Math.Round((child.TranslationY + child.Height / 2) * context.Scale);

                        x = Math.Clamp(x, 0, bitmap.Width - 1);
                        y = Math.Clamp(y, 0, bitmap.Height - 1);

                        touchPointColor = bitmap.GetPixel(x, y);
                        PickedColor = touchPointColor.ToMauiColor();
                    }

                }
                child.OptionalOnBeforeDrawing();
                if (child.CanDraw) //still visible 
                {
                    count++;
                    child.Render(context);
                }
            }
        }

        return count;
    }



    #region GESTURES

    public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
    {

        if (args.Type == TouchActionResult.Down || args.Type == TouchActionResult.Panning)
        {
            var point = TranslateInputOffsetToPixels(args.Event.Location, apply.ChildOffset);

            //e.Location.X / canvasSize.Width;
            PointerRingPositionXOffsetRatio = (point.X - DrawingRect.Left) / DrawingRect.Width;
            //e.Location.Y / canvasSize.Height;
            PointerRingPositionYOffsetRatio = (point.Y - DrawingRect.Top) / DrawingRect.Height;

            return this;
        }

        return base.ProcessGestures(args, apply);
    }

    private SKPoint _lastTouchPoint = new SKPoint();
    private bool _checkPointerInitPositionDone = false;



    #endregion

    /// <summary>
    /// Occurs when the Picked Color changes
    /// </summary>
    public event EventHandler<Color> PickedColorChanged;

    public static readonly BindableProperty PickedColorProperty
        = BindableProperty.Create(
            nameof(PickedColor),
            typeof(Color),
            typeof(SkiaColorPicker), Microsoft.Maui.Graphics.Colors.Transparent,
            propertyChanged: (bindable, value, newValue) =>
            {
                if (bindable is SkiaColorPicker control)
                {
                    control.PickedColorChanged?.Invoke(control, (Color)newValue);
                }
            });

    /// <summary>
    /// Get the current Picked Color
    /// </summary>
    public Color PickedColor
    {
        get { return (Color)GetValue(PickedColorProperty); }
        private set { SetValue(PickedColorProperty, value); }
    }

    public static readonly BindableProperty ColorSpectrumStyleProperty
     = BindableProperty.Create(
         nameof(ColorSpectrumStyle),
         typeof(ColorSpectrumStyle),
         typeof(SkiaColorPicker),
         ColorSpectrumStyle.TintToHueToShadeStyle,
         BindingMode.Default, null,
         propertyChanged: (bindable, value, newValue) =>
         {
             if (newValue != null)
                 ((SkiaColorPicker)bindable).UpdateColors();
             else
                 ((SkiaColorPicker)bindable).ColorSpectrumStyle = default;
         });

    /// <summary>
    /// Set the Color Spectrum Gradient Style
    /// </summary>
    public ColorSpectrumStyle ColorSpectrumStyle
    {
        get { return (ColorSpectrumStyle)GetValue(ColorSpectrumStyleProperty); }
        set { SetValue(ColorSpectrumStyleProperty, value); }
    }

    #region COLORS

    static List<Color> defaultColors = new()
    {
        new Color(255, 0, 0), // Red
        new Color(255, 255, 0), // Yellow
        new Color(0, 255, 0), // Green (Lime)
        new Color(0, 255, 255), // Aqua
        new Color(0, 0, 255), // Blue
        new Color(255, 0, 255), // Fuchsia
        new Color(255, 0, 0), // Red
    };

    public static readonly BindableProperty ColorsBlendModeProperty = BindableProperty.Create(nameof(ColorsBlendMode),
        typeof(SKBlendMode), typeof(SkiaColorPicker),
        SKBlendMode.SrcOver,
        propertyChanged: (bindable, value, newValue) =>
        {
            ((SkiaColorPicker)bindable).UpdateColors();
        });
    public SKBlendMode ColorsBlendMode
    {
        get { return (SKBlendMode)GetValue(ColorsBlendModeProperty); }
        set { SetValue(ColorsBlendModeProperty, value); }
    }

    public static readonly BindableProperty SelectionColorsProperty = BindableProperty.Create(
        nameof(SelectionColors),
        typeof(IList<Color>),
        typeof(SkiaColorPicker),
        defaultValueCreator: (instance) =>
        {
            var created = new ObservableCollection<Color>();
            if (instance is SkiaColorPicker control)
            {
                created.CollectionChanged += control.OnPropertyColorsCollectionChanged;
            }
            return created;
        },
        validateValue: (bo, v) => v is IList<Color>,
        propertyChanged: SelectionColorsPropertyChanged,
        coerceValue: CoerceSelectionColors);

    public IList<Color> SelectionColors
    {
        get => (IList<Color>)GetValue(SelectionColorsProperty);
        set => SetValue(SelectionColorsProperty, value);
    }

    private static object CoerceSelectionColors(BindableObject bindable, object value)
    {
        if (!(value is ReadOnlyCollection<Color> readonlyCollection))
        {
            return value;
        }

        return new ReadOnlyCollection<Color>(
            readonlyCollection.Select(s => s)
                .ToList());
    }

    private static void SelectionColorsPropertyChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaColorPicker control)
        {
            if (oldvalue is INotifyCollectionChanged oldCollection)
            {
                oldCollection.CollectionChanged -= control.OnPropertyColorsCollectionChanged;
            }
            if (newvalue is INotifyCollectionChanged newCollection)
            {
                newCollection.CollectionChanged -= control.OnPropertyColorsCollectionChanged;
                newCollection.CollectionChanged += control.OnPropertyColorsCollectionChanged;
            }

            control.UpdateColors();
        }
    }

    private void OnPropertyColorsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateColors();
        this.Update();
    }

    #endregion



    public static readonly BindableProperty ColorFlowDirectionProperty
        = BindableProperty.Create(
            nameof(ColorFlowDirection),
            typeof(ColorFlowDirection),
            typeof(SkiaColorPicker),
            ColorFlowDirection.Vertical,
            BindingMode.Default, null,
            propertyChanged: (bindable, value, newValue) =>
            {
                if (newValue != null)
                    ((SkiaColorPicker)bindable).UpdateColors();
                else
                    ((SkiaColorPicker)bindable).ColorFlowDirection = default;
            });

    /// <summary>
    /// Sets the Color List flow Direction
    /// Horizontal or Verical
    /// </summary>
    public ColorFlowDirection ColorFlowDirection
    {
        get { return (ColorFlowDirection)GetValue(ColorFlowDirectionProperty); }
        set { SetValue(ColorFlowDirectionProperty, value); }
    }


    public static readonly BindableProperty PointerRingDiameterUnitsProperty
        = BindableProperty.Create(
            nameof(PointerRingDiameterUnits),
            typeof(double),
            typeof(SkiaColorPicker),
            16.0,
            BindingMode.Default,
            validateValue: (bindable, value) =>
            {
                return (((double)value > -1) && ((double)value <= 1));
            },
            propertyChanged: (bindable, value, newValue) =>
            {
                if (newValue != null)
                    ((SkiaColorPicker)bindable).UpdateIndicator();
                else
                    ((SkiaColorPicker)bindable).PointerRingDiameterUnits = default;
            });

    /// <summary>
    /// Sets the Picker Pointer Ring Diameter
    /// In device-independent Points
    /// </summary>
    public double PointerRingDiameterUnits
    {
        get { return (double)GetValue(PointerRingDiameterUnitsProperty); }
        set { SetValue(PointerRingDiameterUnitsProperty, value); }
    }

    public static readonly BindableProperty PointerRingBorderUnitsProperty
        = BindableProperty.Create(
            nameof(PointerRingBorderUnits),
            typeof(double),
            typeof(SkiaColorPicker),
            3.0,
            BindingMode.Default,
            validateValue: (bindable, value) =>
            {
                return (((double)value > -1) && ((double)value <= 1));
            },
            propertyChanged: (bindable, value, newValue) =>
            {
                if (newValue != null)
                    ((SkiaColorPicker)bindable).UpdateIndicator();
                else
                    ((SkiaColorPicker)bindable).PointerRingBorderUnits = default;
            });

    /// <summary>
    /// Sets the Picker Pointer Ring Border Size
    /// Value must be between 0-1
    /// Calculated against pixel size of Picker Pointer
    /// </summary>
    public double PointerRingBorderUnits
    {
        get { return (double)GetValue(PointerRingBorderUnitsProperty); }
        set { SetValue(PointerRingBorderUnitsProperty, value); }
    }

    public static readonly BindableProperty PointerRingPositionXOffsetRatioProperty
        = BindableProperty.Create(
            nameof(PointerRingPositionXOffsetRatio),
            typeof(double),
            typeof(SkiaColorPicker),
            1.0,
            BindingMode.OneTime,
            coerceValue: (b, v) =>
            {
                var value = (double)v;
                return Math.Clamp(value, 0, 1);
            },
            propertyChanged: (bindable, value, newValue) =>
            {
                if (newValue != null)
                {
                    ((SkiaColorPicker)bindable).UpdateIndicator();
                }
                else
                    ((SkiaColorPicker)bindable).ColorFlowDirection = default;
            });

    /// <summary>
    /// Sets the Picker Pointer X position
    /// Value must be between 0-1
    /// Calculated against the View Canvas Width value
    /// </summary>
    public double PointerRingPositionXOffsetRatio
    {
        get { return (double)GetValue(PointerRingPositionXOffsetRatioProperty); }
        set { SetValue(PointerRingPositionXOffsetRatioProperty, value); }
    }

    public static readonly BindableProperty PointerRingPositionYOffsetRatioProperty
        = BindableProperty.Create(
            nameof(PointerRingPositionYOffsetRatio),
            typeof(double),
            typeof(SkiaColorPicker),
            0.0,
            BindingMode.OneTime,
            coerceValue: (b, v) =>
            {
                var value = (double)v;
                return Math.Clamp(value, 0, 1);
            },
            propertyChanged: (bindable, value, newValue) =>
            {
                if (newValue != null)
                {
                    ((SkiaColorPicker)bindable).UpdateIndicator();
                }
                else
                    ((SkiaColorPicker)bindable).ColorFlowDirection = default;
            });

    private bool _colorsSet;

    /// <summary>
    /// Sets the Picker Pointer Y position
    /// Value must be between 0-1
    /// Calculated against the View Canvas Width value
    /// </summary>
    public double PointerRingPositionYOffsetRatio
    {
        get { return (double)GetValue(PointerRingPositionYOffsetRatioProperty); }
        set { SetValue(PointerRingPositionYOffsetRatioProperty, value); }
    }

    protected virtual List<Color> GetPrimaryLayerColors()
    {
        var colors = new System.Collections.Generic.List<Color>();
        if (SelectionColors.Count > 1)
        {
            foreach (var color in SelectionColors)
                colors.Add(color);
        }
        else
        {
            foreach (var color in defaultColors)
                colors.Add(color);
        }
        return colors;
    }

    protected virtual List<Color> GetSecondaryLayerColors(ColorSpectrumStyle colorSpectrumStyle)
    {
        switch (colorSpectrumStyle)
        {
        case ColorSpectrumStyle.HueOnlyStyle:
        return new()
        {
                Colors.Transparent
        };
        case ColorSpectrumStyle.Shades:
        return new()
            {
                Colors.White,
                Colors.Black
            };
        case ColorSpectrumStyle.HueToShadeStyle:
        return new()
        {
                Colors.Transparent,
                Colors.Black
        };
        case ColorSpectrumStyle.ShadeToHueStyle:
        return new()
        {
            Colors.Black,
                Colors.Transparent
        };
        case ColorSpectrumStyle.HueToTintStyle:
        return new()
        {
                Colors.Transparent,
                Colors.White
        };
        case ColorSpectrumStyle.TintToHueStyle:
        return new()
        {
                Colors.White,
                Colors.Transparent
        };
        case ColorSpectrumStyle.TintToHueToShadeStyle:
        return new()
        {
                Colors.White,
                Colors.Transparent,
                Colors.Black
        };
        case ColorSpectrumStyle.ShadeToHueToTintStyle:
        return new()
        {
                Colors.Black,
                Colors.Transparent,
                Colors.White
        };
        default:
        return new()
        {
                Colors.Transparent,
                Colors.Black
        };
        }
    }


}

public enum ColorSpectrumStyle
{
    Unset,
    HueOnlyStyle,
    HueToShadeStyle,
    ShadeToHueStyle,
    HueToTintStyle,
    TintToHueStyle,
    TintToHueToShadeStyle,
    ShadeToHueToTintStyle,
    Shades
}

public enum ColorFlowDirection
{
    Horizontal,
    Vertical
}
