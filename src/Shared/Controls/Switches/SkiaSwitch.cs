namespace DrawnUi.Draw;

/// <summary>
/// Switch-like control, can include any content inside. It's aither you use default content (todo templates?..)
/// or can include any content inside, and properties will by applied by convention to a SkiaShape with Tag `Frame`, SkiaShape with Tag `Thumb`. At the same time you can override ApplyProperties() and apply them to your content yourself.
/// </summary>
public class SkiaSwitch : SkiaToggle
{
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

    protected virtual void CreateDefaultStyleContent()
    {
        SetDefaultContentSize(50, 32);

        var shape = new SkiaShape
        {
            Tag = "Frame",
            Type = ShapeType.Rectangle,
            CornerRadius = 20,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };
        this.AddSubView(shape);

        this.AddSubView(new SkiaShape()
        {
            UseCache = SkiaCacheType.Operations,
            Type = ShapeType.Circle,
            Margin = 2,
            LockRatio = -1,
            Tag = "Thumb",
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Fill,
        });

        var hotspot = new SkiaHotspot() { TransformView = this.Thumb, };
        hotspot.Tapped += (s, e) => { IsToggled = !IsToggled; };
        this.AddSubView(hotspot);
    }

    protected virtual void CreateCupertinoStyleContent()
    {
        SetDefaultContentSize(51, 31);

        ExpandDirtyRegion = new Thickness(3, 6);

        Children = new List<SkiaControl>()
        {
            new SkiaShape
            {
                Tag = "Frame",
                Type = ShapeType.Rectangle,
                CornerRadius = 100,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
            },
            new SkiaShape()
            {
                Type = ShapeType.Circle,
                Margin = 2,
                LockRatio = -1,
                Tag = "Thumb",
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Fill,
                Shadows = new List<SkiaShadow>()
                {
                    new SkiaShadow()
                    {
                        Blur = 3,
                        Opacity = 0.1,
                        X = 0,
                        Y = 3,
                        Color = Colors.Black
                    }
                }
            },
            new SkiaHotspot() { TransformView = this.Thumb, }.Adapt(me =>
            {
                me.Tapped += (s, e) => { IsToggled = !IsToggled; };
            })
        };

        // iOS default colors
        ColorFrameOff = Color.FromRgba(229, 229, 229, 255);
        ColorFrameOn = Color.FromRgba(48, 209, 88, 255);
        ColorThumbOff = Colors.White;
        ColorThumbOn = Colors.White;
    }

    protected virtual void CreateMaterialStyleContent()
    {
        SetDefaultContentSize(46, 28);

        ExpandDirtyRegion = new(5, 6);

        Children = new List<SkiaControl>()
        {
            new SkiaShape
            {
                Tag = "Frame",
                Type = ShapeType.Rectangle,
                CornerRadius = 7,
                HeightRequest = 15,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Center,
                UseCache = SkiaCacheType.Operations
            },
            new SkiaShape()
            {
                Type = ShapeType.Circle,
                Margin = 0,
                LockRatio = -1,
                Tag = "Thumb",
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Fill,
                Shadows = new List<SkiaShadow>()
                {
                    new SkiaShadow()
                    {
                        Blur = 3,
                        Opacity = 0.1,
                        X = 1,
                        Y = 1,
                        Color = Colors.Black
                    }
                }
            },
            new SkiaHotspot() { TransformView = this.Thumb, }.Adapt(me =>
            {
                me.Tapped += (s, e) => { IsToggled = !IsToggled; };
            })
        };

        // Material design default colors
        ColorFrameOff = Color.FromRgba(158, 158, 158, 255);
        ColorFrameOn = Color.FromRgba(33, 150, 243, 255);
        ColorThumbOff = Colors.White;
        ColorThumbOn = Colors.White;
    }

    protected virtual void CreateWindowsStyleContent()
    {
        SetDefaultContentSize(48, 22);

        // Windows style is more squared
        var shape = new SkiaShape
        {
            Tag = "Frame",
            Type = ShapeType.Rectangle,
            CornerRadius = 12,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            UseCache = SkiaCacheType.Operations,
            StrokeColor = Color.Parse("#767676"),
            StrokeWidth = 2.5
        };
        this.AddSubView(shape);

        this.AddSubView(new SkiaShape()
        {
            Margin = 5.5,
            UseCache = SkiaCacheType.Operations,
            Type = ShapeType.Circle,
            LockRatio = -1,
            Tag = "Thumb",
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Fill,
        });

        var hotspot = new SkiaHotspot() { TransformView = this.Thumb, };
        hotspot.Tapped += (s, e) => { IsToggled = !IsToggled; };
        this.AddSubView(hotspot);

        // Windows default colors
        ColorFrameOff = Color.Parse("#767676");
        ColorFrameOn = Color.FromRgba(0, 120, 215, 255);
        ColorThumbOff = Color.Parse("#767676");
        ColorThumbOn = Colors.White;
    }

    #endregion

    public virtual void ApplyOn()
    {
        if (Thumb != null)
        {
            Thumb.TranslationX = GetThumbPosForOn();
            Thumb.BackgroundColor = this.ColorThumbOn;
            Track.BackgroundColor = this.ColorFrameOn;

            if (ControlStyle == PrebuiltControlStyle.Windows)
            {
                Track.StrokeColor = ColorFrameOn;
            }
        }
    }

    public virtual void ApplyOff()
    {
        if (Thumb != null)
        {
            Thumb.TranslationX = GetThumbPosForOff();
            Thumb.BackgroundColor = this.ColorThumbOff;

            if (ControlStyle == PrebuiltControlStyle.Windows)
            {
                Track.BackgroundColor = Colors.Transparent;
                Track.StrokeColor = ColorFrameOff;
            }
            else
            {
                Track.BackgroundColor = this.ColorFrameOff;
            }
        }
    }

    public SkiaShape Track;
    public SkiaShape Thumb;

    protected virtual void FindViews()
    {
        Track = FindView<SkiaShape>("Frame");
        Thumb = FindView<SkiaShape>("Thumb");
    }

    public override void OnChildrenChanged()
    {
        base.OnChildrenChanged();

        FindViews();
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

    #region ANIMATE

    protected virtual double GetThumbPosForOn()
    {
        var x = Track.Width + Track.Margins.Right + Track.Margins.Left
                - Thumb.Width - Thumb.Margins.Right - Thumb.Margins.Left;
        return x;
    }

    protected virtual double GetThumbPosForOff()
    {
        return 0;
    }

    public static uint AnimationSpeed = 200;

    protected virtual bool CanAnimate()
    {
        return LayoutReady && IsAnimated && IsVisible; //todo add visibility in view tree
    }

    CancellationTokenSource cancelAnimation;

    protected override void OnToggledChanged()
    {
        cancelAnimation?.Cancel();
        cancelAnimation?.Dispose();
        cancelAnimation = new CancellationTokenSource();

        if (CanAnimate() && Thumb != null)
        {
            var easing = Easing.CubicOut;
            var msSpeed = AnimationSpeed;
            var pos = 0.0;
            if (!IsToggled)
            {
                Task.Run(async () =>
                {
                    await Thumb.TranslateToAsync(pos, 0, msSpeed, easing, cancelAnimation);
                    ApplyOff();
                }, cancelAnimation.Token);
            }
            else
            {
                pos = GetThumbPosForOn();
                Task.Run(async () =>
                {
                    await Thumb.TranslateToAsync(pos, 0, msSpeed, easing, cancelAnimation);
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

    #endregion
}
