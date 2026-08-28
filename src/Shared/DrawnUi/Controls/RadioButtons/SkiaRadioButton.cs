namespace DrawnUi.Controls;

/// <summary>
/// Switch-like control, can include any content inside. It's aither you use default content (todo templates?..)
/// or can include any content inside, and properties will by applied by convention to a SkiaShape with Tag `Frame`, SkiaShape with Tag `Thumb`. At the same time you can override ApplyProperties() and apply them to your content yourself.
/// </summary>
public class SkiaRadioButton : SkiaToggle, ISkiaRadioButton
{
    public SkiaRadioButton()
    {

    }

    public SkiaRadioButton(string text)
    {
        Text = text;
    }

    #region DEFAULT CONTENT

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

    // Flat DrawnUI default palette, shared with the other default controls.
    private static readonly Color DefaultAccentColor = Color.FromRgba(220, 20, 60, 255); // Crimson #DC143C
    private static readonly Color DefaultOutlineColor = Color.FromRgba(142, 149, 157, 255); // neutral #8E959D

    protected virtual void CreateDefaultStyleContent()
    {
        // Flat DrawnUI look: neutral ring when off, accent ring + filled accent dot when on.
        SetStyleDefault(UseCacheProperty, SkiaCacheType.Image);
        SetStyleDefault(MinimumHeightRequestProperty, 24d);

        // SkiaToggle's ColorThumb* default to White (invisible on light bg) — use the flat palette
        // unless the app explicitly set ring/dot colors via ColorFrameOff/ColorFrameOn.
        SetStyleDefault(ColorThumbOffProperty, IsSet(ColorFrameOffProperty) ? ColorFrameOff : DefaultOutlineColor);
        SetStyleDefault(ColorThumbOnProperty, IsSet(ColorFrameOnProperty) ? ColorFrameOn : DefaultAccentColor);

        Children = new List<SkiaControl>()
        {
            new SkiaLayout()
            {
                HeightRequest = 18,
                LockRatio = 1,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new SkiaShape()
                    {
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill,
                        StrokeColor = ColorThumbOff,
                        StrokeWidth = 2,
                        Type = ShapeType.Circle
                    },
                    new SkiaShape()
                    {
                        Tag = "On",
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill,
                        StrokeColor = ColorThumbOn,
                        StrokeWidth = 2,
                        Type = ShapeType.Circle,
                        Children = new List<SkiaControl>()
                        {
                            new SkiaShape()
                            {
                                HorizontalOptions = LayoutOptions.Fill,
                                VerticalOptions = LayoutOptions.Fill,
                                BackgroundColor = ColorThumbOn,
                                Margin = 4,
                                Type = ShapeType.Circle
                            }
                        }
                    },
                }
            },
            new SkiaRichLabel()
            {
                Tag = "Text",
                Margin = new(26, 0, 0, 0),
                FontSize = 14,
                MaxLines = 2,
                TextColor = Colors.Black,
                VerticalOptions = LayoutOptions.Center
            }
        };
    }

    /// <summary>
    /// Creates an iOS-style radio button: light gray ring when off,
    /// filled iOS-blue circle with a white inner dot when selected.
    /// </summary>
    protected virtual void CreateCupertinoStyleContent()
    {
        SetStyleDefault(UseCacheProperty, SkiaCacheType.Image);
        SetStyleDefault(MinimumHeightRequestProperty, 24d);

        SetStyleDefault(ColorThumbOffProperty, Color.FromRgba(191, 191, 191, 255)); // iOS light gray
        SetStyleDefault(ColorThumbOnProperty, Color.FromRgba(0, 122, 255, 255));    // iOS blue

        Children = new List<SkiaControl>()
        {
            new SkiaLayout()
            {
                HeightRequest = 20,
                LockRatio = 1,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new SkiaShape()
                    {
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill,
                        StrokeColor = ColorThumbOff,
                        StrokeWidth = 1.5,
                        Type = ShapeType.Circle
                    },
                    new SkiaShape()
                    {
                        Tag = "On",
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill,
                        BackgroundColor = ColorThumbOn,
                        Type = ShapeType.Circle,
                        Children = new List<SkiaControl>()
                        {
                            new SkiaShape()
                            {
                                HorizontalOptions = LayoutOptions.Fill,
                                VerticalOptions = LayoutOptions.Fill,
                                BackgroundColor = Colors.White,
                                Margin = 6.5,
                                Type = ShapeType.Circle
                            }
                        }
                    },
                }
            },
            new SkiaRichLabel()
            {
                Tag = "Text",
                Margin = new(28, 0, 0, 0),
                FontSize = 14,
                MaxLines = 2,
                TextColor = Colors.Black,
                VerticalOptions = LayoutOptions.Center
            }
        };
    }

    /// <summary>
    /// Creates a Material Design style radio button: mid-gray 2pt ring when off,
    /// Material-blue ring with a filled inner dot when selected.
    /// </summary>
    protected virtual void CreateMaterialStyleContent()
    {
        SetStyleDefault(ColorThumbOffProperty, Color.FromRgba(117, 117, 117, 255)); // Material mid-gray
        SetStyleDefault(ColorThumbOnProperty, Color.FromRgba(33, 150, 243, 255));   // Material blue

        BuildMaterialFamilyContent();
    }

    /// <summary>
    /// Creates a Material 3 (Material You) style radio button: on-surface-variant ring when off,
    /// primary #6750A4 ring with a filled inner dot when selected.
    /// </summary>
    protected virtual void CreateMaterial3StyleContent()
    {
        SetStyleDefault(ColorThumbOffProperty, Color.FromRgba(73, 69, 79, 255));   // M3 on-surface variant
        SetStyleDefault(ColorThumbOnProperty, Color.FromRgba(103, 80, 164, 255));  // M3 primary

        BuildMaterialFamilyContent();
    }

    /// <summary>
    /// Shared ring+dot composition for the Material and Material3 styles;
    /// colors come from <see cref="SkiaToggle.ColorThumbOff"/>/<see cref="SkiaToggle.ColorThumbOn"/>
    /// set by the caller.
    /// </summary>
    protected void BuildMaterialFamilyContent()
    {
        SetStyleDefault(UseCacheProperty, SkiaCacheType.Image);
        SetStyleDefault(MinimumHeightRequestProperty, 24d);

        Children = new List<SkiaControl>()
        {
            new SkiaLayout()
            {
                HeightRequest = 20,
                LockRatio = 1,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new SkiaShape()
                    {
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill,
                        StrokeColor = ColorThumbOff,
                        StrokeWidth = 2,
                        Type = ShapeType.Circle
                    },
                    new SkiaShape()
                    {
                        Tag = "On",
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill,
                        StrokeColor = ColorThumbOn,
                        StrokeWidth = 2,
                        Type = ShapeType.Circle,
                        Children = new List<SkiaControl>()
                        {
                            new SkiaShape()
                            {
                                HorizontalOptions = LayoutOptions.Fill,
                                VerticalOptions = LayoutOptions.Fill,
                                BackgroundColor = ColorThumbOn,
                                Margin = 5,
                                Type = ShapeType.Circle
                            }
                        }
                    },
                }
            },
            new SkiaRichLabel()
            {
                Tag = "Text",
                Margin = new(28, 0, 0, 0),
                FontSize = 14,
                MaxLines = 2,
                TextColor = Colors.Black,
                VerticalOptions = LayoutOptions.Center
            }
        };
    }

    /// <summary>
    /// Creates a Windows (Fluent) style radio button: thin gray ring when off,
    /// thick accent-colored ring leaving a white center when selected.
    /// </summary>
    protected virtual void CreateWindowsStyleContent()
    {
        SetStyleDefault(UseCacheProperty, SkiaCacheType.Image);
        SetStyleDefault(MinimumHeightRequestProperty, 24d);

        SetStyleDefault(ColorThumbOffProperty, Color.Parse("#767676"));            // Fluent neutral
        SetStyleDefault(ColorThumbOnProperty, Color.FromRgba(0, 120, 215, 255));   // Windows blue

        Children = new List<SkiaControl>()
        {
            new SkiaLayout()
            {
                HeightRequest = 20,
                LockRatio = 1,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new SkiaShape()
                    {
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill,
                        StrokeColor = ColorThumbOff,
                        StrokeWidth = 1,
                        Type = ShapeType.Circle
                    },
                    new SkiaShape()
                    {
                        Tag = "On",
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill,
                        StrokeColor = ColorThumbOn,
                        StrokeWidth = 5,
                        Type = ShapeType.Circle
                    },
                }
            },
            new SkiaRichLabel()
            {
                Tag = "Text",
                Margin = new(28, 0, 0, 0),
                FontSize = 14,
                MaxLines = 2,
                TextColor = Colors.Black,
                VerticalOptions = LayoutOptions.Center
            }
        };
    }

    #endregion

    protected override void OnLayoutChanged()
    {
        base.OnLayoutChanged();

        ApplyProperties();
    }

    public SkiaControl GroupParent
    {
        get
        {
            return Parent as SkiaControl;
        }
    }

    public virtual void ApplyOff()
    {
        if (ViewOn != null)
        {
            ViewOn.IsVisible = false;
        }
        Update();
    }

    public virtual void ApplyOn()
    {
        if (ViewOn != null)
        {
            ViewOn.IsVisible = true;
        }
        Update();
    }

    public SkiaControl ViewOff;
    public SkiaControl ViewOn;
    public SkiaLabel ViewText;

    protected virtual void FindViews()
    {
        ViewOn = FindView<SkiaControl>("On");
        ViewOff = FindView<SkiaControl>("Off");
        ViewText = FindView<SkiaLabel>("Text");
    }

    public override void OnChildrenChanged()
    {
        base.OnChildrenChanged();

        FindViews();
        ApplyProperties();
    }


    public override void ApplyProperties()
    {
        if (ViewText != null)
        {
            ViewText.Text = this.Text;
        }

        if (IsToggled)
        {
            ApplyOn();
        }
        else
        {
            ApplyOff();
        }        
    }

    public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
    {
        CheckHovered(args);

        if (args.Type == TouchActionResult.Tapped)
        {
            if (!IsToggled)
            {
                IsToggled = true;
                return this;
            }

            return null;
        }

        return base.ProcessGestures(args, apply);
    }

    protected override void NotifyWasToggled()
    {
        base.NotifyWasToggled();

        if (!_lockIsToggled)
        {
            Manager.ReportValueChange(this, IsToggled);
        }
    }

    protected virtual void OnGroupChanged()
    {
        UpdateGroup();
    }

    public virtual void UpdateGroup()
    {
        Manager.RemoveFromGroups(this);

        if (this.Parent is not SkiaControl control)
        {
            return;
        }

        if (!string.IsNullOrEmpty(GroupName))
        {
            Manager.AddToGroup(this, GroupName);
        }
        else
        {
            Manager.AddToGroup(this, control);
        }
    }

    public override void OnParentChanged(IDrawnBase newvalue, IDrawnBase oldvalue)
    {
        base.OnParentChanged(newvalue, oldvalue);

        UpdateGroup();
        ApplyProperties();
    }

    RadioButtons Manager => RadioButtons.All;

    bool _lockIsToggled;

    public void SetValueInternal(bool value)
    {
        _lockIsToggled = true;
        IsToggled = value;
        _lockIsToggled = false;
    }

    public bool GetValueInternal()
    {
        return IsToggled;
    }

    public static readonly BindableProperty GroupNameProperty = BindableProperty.Create(nameof(GroupName),
        typeof(string),
        typeof(SkiaRadioButton),
        string.Empty,
        propertyChanged: NeedUpdateGroup);

    private static void NeedUpdateGroup(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaRadioButton control)
        {
            control.OnGroupChanged();
        }
    }

    public string GroupName
    {
        get { return (string)GetValue(GroupNameProperty); }
        set { SetValue(GroupNameProperty, value); }
    }

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text),
        typeof(string),
        typeof(SkiaButton),
        string.Empty, propertyChanged: NeedUpdateProperties);

    /// <summary>
    /// Bind to your own content!
    /// </summary>
    public string Text
    {
        get { return (string)GetValue(TextProperty); }
        set { SetValue(TextProperty, value); }
    }
}
