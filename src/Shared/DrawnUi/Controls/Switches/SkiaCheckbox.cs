namespace DrawnUi.Draw;

/// <summary>
/// Switch-like control, can include any content inside. It's aither you use default content (todo templates?..)
/// or can include any content inside, and properties will by applied by convention to a SkiaShape with Tag `Frame`, SkiaShape with Tag `Thumb`. At the same time you can override ApplyProperties() and apply them to your content yourself.
/// </summary>
public class SkiaCheckbox : SkiaToggle
{
    #region DEFAULT CONTENT

    // SVG checkmark paths for different styles
 
    protected static string SvgCupertinoCheck = "<svg width=\"800px\" height=\"800px\" viewBox=\"0 0 24 24\" fill=\"none\">\n<path d=\"M4 12.6111L8.92308 17.5L20 6.5\" stroke=\"#000000\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>\n</svg>";
    
    protected static string SvgMaterialCheck = "<svg width=\"800px\" height=\"800px\" viewBox=\"0 0 24 24\" fill=\"none\">\n<path d=\"M5 13L9 17L19 7\" stroke=\"#000000\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>\n</svg>";
    
    protected static string SvgWindowsCheck = "<svg width=\"800px\" height=\"800px\" viewBox=\"0 0 24 24\" fill=\"none\">\n<path d=\"M4 11.6L10 17.6L20 7.6\" stroke=\"#000000\" stroke-width=\"2.5\" stroke-linecap=\"square\" stroke-linejoin=\"round\"/>\n</svg>";

    protected override void CreateDefaultContent()
    {
        if (this.Views.Count == 0)
        {
            switch (UsingControlStyle)
            {
                case PrebuiltControlStyle.Cupertino:
                    CreateCupertinoStyleContent();
                    break;
                case PrebuiltControlStyle.Material:
                    CreateMaterialStyleContent();
                    break;
                case PrebuiltControlStyle.Material3:
                    CreateMaterial3StyleContent();
                    break;
                case PrebuiltControlStyle.Windows:
                    CreateWindowsStyleContent();
                    break;
                default:
                    CreateDefaultStyleContent();
                    break;
            }
            
            ApplyProperties();
        }
    }
    
    // Flat DrawnUI default palette (accent + neutral outline), shared with the other default controls.
    private static readonly Color DefaultAccentColor = Color.FromRgba(220, 20, 60, 255); // Crimson #DC143C
    private static readonly Color DefaultOutlineColor = Color.FromRgba(142, 149, 157, 255); // neutral #8E959D

    protected virtual void CreateDefaultStyleContent()
    {
        // Original DrawnUI checkbox: stroked rectangle, filled inner square when checked.
        // Flat palette: neutral outline off, accent stroke + accent inner square on.
        SetDefaultContentSize(22, 22);

        var frameOff = IsSet(ColorFrameOffProperty) ? ColorFrameOff : DefaultOutlineColor;
        var frameOn = IsSet(ColorFrameOnProperty) ? ColorFrameOn : DefaultAccentColor;
        var checkOn = IsSet(ColorCheckOnProperty) ? ColorCheckOn : DefaultAccentColor;

        this.AddSubView(new SkiaShape
        {
            Tag = "FrameOff",
            StrokeWidth = 1,
            Type = ShapeType.Rectangle,
            StrokeColor = frameOff,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        });

        this.AddSubView(new SkiaShape
        {
            Tag = "FrameOn",
            Type = ShapeType.Rectangle,
            StrokeColor = frameOn,
            StrokeWidth = 1,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = new List<SkiaControl>()
            {
                new SkiaShape()
                {
                    Tag = "ViewCheckOn",
                    UseCache = SkiaCacheType.Operations,
                    Type = ShapeType.Rectangle,
                    BackgroundColor = checkOn,
                    Margin = 3,
                    LockRatio = 1,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                }
            }
        });

        ColorFrameOff = frameOff;
        ColorFrameOn = frameOn;
        ColorCheckOn = checkOn;
    }
    
    protected virtual void CreateCupertinoStyleContent()
    {
        // iOS style uses rounded rectangle with checkmark
        SetDefaultContentSize(22, 22);
        
        // Frame Off
        this.AddSubView(new SkiaShape
        {
            Tag = "FrameOff",
            StrokeWidth = 1.5,
            Type = ShapeType.Rectangle,
            CornerRadius = 4, // iOS slightly rounded corners
            StrokeColor = Color.FromRgba(191, 191, 191, 255), // iOS light gray
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        });
        
        // Frame On
        var frameOn = new SkiaShape
        {
            Tag = "FrameOn",
            Type = ShapeType.Rectangle,
            CornerRadius = 4, // iOS slightly rounded corners
            BackgroundColor = Color.FromRgba(0, 122, 255, 255), // iOS blue
            StrokeWidth = 0,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };
        this.AddSubView(frameOn);
        
        // Checkmark
        var checkmark = new SkiaSvg
        {
            Tag = "ViewCheckOn",
            SvgString = SvgCupertinoCheck,
            TintColor = Colors.White, // White checkmark
            Margin = new Thickness(2),
            UseCache = SkiaCacheType.Operations,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };
        frameOn.AddSubView(checkmark);
        ViewCheckOn = checkmark;
        
        // Color overrides for iOS style
        SetStyleDefault(ColorFrameOffProperty, Color.FromRgba(191, 191, 191, 255));
        SetStyleDefault(ColorFrameOnProperty, Color.FromRgba(0, 122, 255, 255));
        SetStyleDefault(ColorCheckOnProperty, Colors.White);
    }
    
    protected virtual void CreateMaterialStyleContent()
    {
        // Material style tends to use a container with a checkmark
        SetDefaultContentSize(24, 24);

        // Frame Off (outlined box)
        this.AddSubView(new SkiaShape
        {
            Tag = "FrameOff",
            StrokeWidth = 2,
            Type = ShapeType.Rectangle,
            CornerRadius = 2, // Material slight corner
            StrokeColor = Color.FromRgba(117, 117, 117, 255), // Material mid-gray
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        });

        // Frame On (filled box)
        var frameOn = new SkiaShape
        {
            Tag = "FrameOn",
            Type = ShapeType.Rectangle,
            CornerRadius = 2,
            BackgroundColor = Color.FromRgba(33, 150, 243, 255), // Material blue
            StrokeWidth = 0,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };
        this.AddSubView(frameOn);
        
        // Material style checkmark
        var checkmark = new SkiaSvg
        {
            Tag = "ViewCheckOn",
            SvgString = SvgMaterialCheck,
            TintColor = Colors.White,
            Margin = new Thickness(2),
            UseCache = SkiaCacheType.Operations,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };
        frameOn.AddSubView(checkmark);
        ViewCheckOn = checkmark;
        
        // Color overrides for Material style
        SetStyleDefault(ColorFrameOffProperty, Color.FromRgba(117, 117, 117, 255));
        SetStyleDefault(ColorFrameOnProperty, Color.FromRgba(33, 150, 243, 255));
        SetStyleDefault(ColorCheckOnProperty, Colors.White);
    }

    /// <summary>
    /// Creates a Material 3 (Material You) checkbox: 18pt box, 2pt on-surface-variant outline
    /// when off, filled primary #6750A4 with a white checkmark when checked.
    /// </summary>
    protected virtual void CreateMaterial3StyleContent()
    {
        SetDefaultContentSize(18, 18);

        // Frame Off (outlined box)
        this.AddSubView(new SkiaShape
        {
            Tag = "FrameOff",
            StrokeWidth = 2,
            Type = ShapeType.Rectangle,
            CornerRadius = 2,
            StrokeColor = Color.FromRgba(73, 69, 79, 255), // M3 on-surface variant #49454F
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        });

        // Frame On (filled box)
        var frameOn = new SkiaShape
        {
            Tag = "FrameOn",
            Type = ShapeType.Rectangle,
            CornerRadius = 2,
            BackgroundColor = Color.FromRgba(103, 80, 164, 255), // M3 primary #6750A4
            StrokeWidth = 0,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };
        this.AddSubView(frameOn);

        // Material style checkmark
        var checkmark = new SkiaSvg
        {
            Tag = "ViewCheckOn",
            SvgString = SvgMaterialCheck,
            TintColor = Colors.White,
            Margin = new Thickness(2),
            UseCache = SkiaCacheType.Operations,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };
        frameOn.AddSubView(checkmark);
        ViewCheckOn = checkmark;

        // Color overrides for Material 3 style
        SetStyleDefault(ColorFrameOffProperty, Color.FromRgba(73, 69, 79, 255));
        SetStyleDefault(ColorFrameOnProperty, Color.FromRgba(103, 80, 164, 255));
        SetStyleDefault(ColorCheckOnProperty, Colors.White);
    }
    
    protected virtual void CreateWindowsStyleContent()
    {
        // Windows style with squared corners
        SetDefaultContentSize(20, 20);
        
        // Frame Off
        this.AddSubView(new SkiaShape
        {
            Tag = "FrameOff",
            StrokeWidth = 1,
            Type = ShapeType.Rectangle,
            CornerRadius = 0,  
            StrokeColor = Color.Parse("#767676"),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        });
        
        // Frame On
        var frameOn = new SkiaShape
        {
            Tag = "FrameOn",
            Type = ShapeType.Rectangle,
            CornerRadius = 0,
            BackgroundColor = Color.FromRgba(0, 120, 215, 255), // Windows blue
            StrokeWidth = 0,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };
        this.AddSubView(frameOn);
        
        // Windows style checkmark is more bold
        var checkmark = new SkiaSvg
        {
            Tag = "ViewCheckOn",
            SvgString = SvgWindowsCheck,
            TintColor = Colors.White,
            Margin = new Thickness(1),
            UseCache = SkiaCacheType.Operations,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };
        frameOn.AddSubView(checkmark);
        ViewCheckOn = checkmark;
        
        // Color overrides for Windows style
        SetStyleDefault(ColorFrameOffProperty, Color.FromRgba(153, 153, 153, 255));
        SetStyleDefault(ColorFrameOnProperty, Color.FromRgba(0, 120, 215, 255));
        SetStyleDefault(ColorCheckOnProperty, Colors.White);
    }
    
    #endregion

    public virtual void ApplyOn()
    {
        if (FrameOn != null)
        {
            FrameOn.IsVisible = true;
            
            if (FrameOn is SkiaShape shape)
            {
                // Update the color based on control style
                switch (UsingControlStyle)
                {
                    case PrebuiltControlStyle.Cupertino:
                    case PrebuiltControlStyle.Material:
                    case PrebuiltControlStyle.Material3:
                    case PrebuiltControlStyle.Windows:
                        shape.BackgroundColor = ColorFrameOn;
                        break;
                    default:
                        shape.StrokeColor = ColorFrameOn;
                        break;
                }
            }
        }

        if (ViewCheckOn != null)
        {
            ViewCheckOn.IsVisible = true;
            
            // Apply appropriate styling based on ViewCheckOn type
            if (ViewCheckOn is SkiaShape shape)
            {
                if (shape.Type == ShapeType.Path)
                {
                    shape.StrokeColor = this.ColorCheckOn;
                }
                else
                {
                    shape.BackgroundColor = this.ColorCheckOn;
                }
            }
            else if (ViewCheckOn is SkiaSvg svg)
            {
                svg.TintColor = this.ColorCheckOn;
            }
        }

        if (FrameOff != null)
        {
            FrameOff.IsVisible = false;
        }
    }

    public virtual void ApplyOff()
    {
        if (FrameOff != null)
        {
            FrameOff.IsVisible = true;
            
            if (FrameOff is SkiaShape shape)
            {
                shape.StrokeColor = ColorFrameOff;
            }
        }

        if (FrameOn != null)
        {
            FrameOn.IsVisible = false;
        }
        
        if (ViewCheckOn != null)
        {
            ViewCheckOn.IsVisible = false;
        }
    }

    public SkiaControl FrameOff;
    public SkiaControl FrameOn;
    protected SkiaControl ViewCheckOn;

    public override void OnChildrenChanged()
    {
        base.OnChildrenChanged();

        FindViews();
    }

    protected virtual void FindViews()
    {
        FrameOn = FindView<SkiaControl>("FrameOn");
        FrameOff = FindView<SkiaControl>("FrameOff");
        
        // Try to find ViewCheckOn - could be either a SkiaShape or SkiaSvg depending on style
        ViewCheckOn = FindView<SkiaControl>("ViewCheckOn");
        
        // If we couldn't find it directly, it might be nested inside FrameOn
        if (ViewCheckOn == null && FrameOn != null)
        {
            if (FrameOn is SkiaLayout layout)
            {
                ViewCheckOn = layout.FindView<SkiaControl>("ViewCheckOn");
            }
        }
    }

    public override void ApplyProperties()
    {
        if (IsToggled)
        {
            ApplyOn();
        }
        else
        {
            ApplyOff();
        }
    }


    public static readonly BindableProperty ColorCheckOnProperty = BindableProperty.Create(
        nameof(ColorCheckOn),
        typeof(Color),
        typeof(SkiaToggle),
        Colors.Red, propertyChanged: NeedUpdateProperties);

    public Color ColorCheckOn
    {
        get { return (Color)GetValue(ColorCheckOnProperty); }
        set { SetValue(ColorCheckOnProperty, value); }
    }

    protected virtual bool CanAnimate()
    {
        return LayoutReady && IsAnimated && IsVisible; //todo add visibility in view tree
    }

    public static uint AnimationSpeed = 100;

    CancellationTokenSource cancelAnimation;

    protected override void OnToggledChanged()
    {
        cancelAnimation?.Cancel();
        cancelAnimation?.Dispose();
        cancelAnimation = new CancellationTokenSource();

        if (CanAnimate() && ViewCheckOn != null && FrameOff!=null && FrameOn !=null)
        {
            var msSpeed = AnimationSpeed;
            if (!IsToggled)
            {
                _ = Task.Run(async () =>
                {
                    FrameOff.IsVisible = true;
                    await ViewCheckOn.ScaleToAsync(0.0, 0.0, msSpeed, Easing.CubicOut, cancelAnimation);
                    ApplyOff();
                }, cancelAnimation.Token);
            }
            else
            {
                _ = Task.Run(async () =>
                {
                    ViewCheckOn.Scale = 0;
                    ViewCheckOn.IsVisible = true;
                    FrameOn.IsVisible = true;
                    await ViewCheckOn.ScaleToAsync(1.0, 1.0, msSpeed, Easing.CubicIn, cancelAnimation);
                    ApplyOn();
                }, cancelAnimation.Token);
            }

            NotifyWasToggled();

            IsInternalCall = false;
        }
        else
        {
            base.OnToggledChanged();
        }
    }


}



 
