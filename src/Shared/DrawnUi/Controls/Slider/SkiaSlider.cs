using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace DrawnUi.Draw;
 
public class SkiaSlider : SkiaLayout
{
    public SkiaSlider()
    {
        
        SetStyleDefault(UseCacheProperty, SkiaCacheType.ImageDoubleBuffered);
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
                // same geometry for both eras; palette resolves per style (M2 blue vs M3 primary)
                case PrebuiltControlStyle.Material3:
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

    protected virtual void FindViews()
    {
        if (Trail == null)
            Trail = FindView<SkiaLayout>("Trail");

        if (SelectedTrail == null)
            SelectedTrail = FindView<SliderTrail>("SelectedTrail");

        if (EndThumb == null)
            EndThumb = FindView<SliderThumb>("EndThumb");

        if (EnableRange && StartThumb == null)
            StartThumb = FindView<SliderThumb>("StartThumb");
    }

    /// <summary>
    /// Applies style-related property values (colors, sizes, trail geometry) to the current content,
    /// dispatching to the appearance updater matching <see cref="SkiaControl.UsingControlStyle"/>.
    /// </summary>
    public virtual void ApplyProperties()
    {
        switch (UsingControlStyle)
        {
            case PrebuiltControlStyle.Cupertino:
                UpdateCupertinoAppearance();
                break;
            case PrebuiltControlStyle.Material:
            case PrebuiltControlStyle.Material3:
                UpdateMaterialAppearance();
                break;
            case PrebuiltControlStyle.Windows:
                UpdateWindowsAppearance();
                break;
            default:
                UpdateDefaultAppearance();
                break;
        }
    }

    public SkiaControl Trail;
    private SliderTrail SelectedTrail;
    private SliderThumb StartThumb;
    private SliderThumb EndThumb;

    /// <summary>
    /// True when the content was built by one of this control's Create*StyleContent methods,
    /// as opposed to user-provided content (XAML subclass). Geometry helpers like
    /// <see cref="UpdateTrailGeometry"/> only run for style-built content so they never
    /// fight user bindings on custom trails.
    /// </summary>
    protected bool StyleContentCreated { get; private set; }

    // true when SliderHeight was not user-set before style content was built
    private bool _styleOwnsSliderHeight;

    // Flat DrawnUI default palette (shared across the default-styled controls).
    private static readonly Color DefaultAccentColor = Color.FromRgba(220, 20, 60, 255); // Crimson #DC143C
    private static readonly Color DefaultTrackColor = Color.FromRgba(215, 219, 224, 255); // neutral #D7DBE0

    protected virtual void CreateDefaultStyleContent()
    {
        StyleContentCreated = true;
        _styleOwnsSliderHeight = !IsSet(SliderHeightProperty);
        AvailableWidthAdjustment = 1.5;
        SetStyleDefault(HorizontalOptionsProperty, LayoutOptions.Fill);
        SetStyleDefault(MinimumWidthRequestProperty, 64d);
        SetStyleDefault(SliderHeightProperty, 35d);
        Type = LayoutType.Column;
        SetStyleDefault(UseCacheProperty, SkiaCacheType.ImageDoubleBuffered);

        // Flat "DrawnUI" look: neutral track, accent trail + thumb, white inner dot, soft shadow, no strokes.
        SliderThumb BuildDefaultThumb(string tag) => new SliderThumb()
        {
            Tag = tag,
            UseCache = SkiaCacheType.Image,
            Children = new List<SkiaControl>()
            {
                new SkiaShape()
                {
                    Margin = 5,
                    BackgroundColor = DefaultAccentColor,
                    HorizontalOptions = LayoutOptions.Fill,
                    Type = ShapeType.Circle,
                    VerticalOptions = LayoutOptions.Fill,
                    Shadows = new List<SkiaShadow>()
                    {
                        new SkiaShadow()
                        {
                            Blur = 3,
                            Opacity = 0.25,
                            X = 0,
                            Y = 1,
                            Color = Colors.Black
                        }
                    }
                },
                new SkiaShape()
                {
                    LockRatio = 1,
                    WidthRequest = 6,
                    BackgroundColor = Colors.White,
                    HorizontalOptions = LayoutOptions.Center,
                    Type = ShapeType.Circle,
                    VerticalOptions = LayoutOptions.Center,
                },
            }
        };

        var trailChildren = new List<SkiaControl>()
        {
            //unselected trail
            new SkiaShape()
            {
                BackgroundColor = DefaultTrackColor,
                HeightRequest = 6,
                CornerRadius = 3,
                HorizontalOptions = LayoutOptions.Fill,
                UseCache = SkiaCacheType.Operations,
                VerticalOptions = LayoutOptions.Center
            },
            //selected trail
            new SliderTrail()
                {
                    Tag = "SelectedTrail",
                    BackgroundColor = DefaultAccentColor,
                    HeightRequest = 6,
                    CornerRadius = 3,
                    HorizontalOptions = LayoutOptions.Start,
                    UseCache = SkiaCacheType.Operations,
                    VerticalOptions = LayoutOptions.Center,
                    SideOffset = 0,
                    XPos = 0,
                }.Assign(out SelectedTrail)
                .ObserveProperties(() => EndThumb, me => UpdateTrailGeometry(),
                    x => x.TranslationX, x => x.Width, x => x.WidthRequest)
                .ObserveProperties(this, me => UpdateTrailGeometry(),
                    x => x.StartThumbX),

            //end thumb
            BuildDefaultThumb("EndThumb").Assign(out EndThumb)
                .ObserveProperties(() => Trail, me =>
                {
                    me.HeightRequest = Trail.Height;
                    me.WidthRequest = Trail.Height;
                }, x => x.Height)
                .ObserveProperties(this, me => me.TranslationX = this.EndThumbX,
                    x => x.EndThumbX),
        };

        if (EnableRange)
        {
            StartThumb = BuildDefaultThumb("StartThumb")
            .ObserveProperties(() => Trail, me =>
            {
                me.HeightRequest = Trail.Height;
                me.WidthRequest = Trail.Height;
            }, x => x.Height)
            .ObserveProperties(this, me => me.TranslationX = StartThumbX,
                x => x.StartThumbX);

            trailChildren.Insert(trailChildren.Count - 1, StartThumb);
        }

        Children = new List<SkiaControl>()
        {
            new SkiaLayout
                {
                    Tag = "Trail",
                    HeightRequest = SliderHeight,
                    HorizontalOptions = LayoutOptions.Fill,
                    Children = trailChildren
                }
                .ObserveProperties(this, me => me.HeightRequest = SliderHeight,
                    x => x.SliderHeight)
                .Assign(out Trail)
        };
    }

    /// <summary>
    /// Creates a Cupertino (iOS) style slider following Apple's design guidelines.
    /// When <see cref="EnableRange"/> is true, a second (start) thumb is added automatically.
    /// </summary>
    protected virtual void CreateCupertinoStyleContent()
    {
        StyleContentCreated = true;
        _styleOwnsSliderHeight = !IsSet(SliderHeightProperty);
        AvailableWidthAdjustment = -1;
        SetStyleDefault(HorizontalOptionsProperty, LayoutOptions.Fill);
        SetStyleDefault(MinimumWidthRequestProperty, 64d);
        SetStyleDefault(SliderHeightProperty, EnableRange ? CupertinoThumbDiameter + 8 : CupertinoThumbDiameter);
        Type = LayoutType.Column;
        SetStyleDefault(UseCacheProperty, SkiaCacheType.ImageDoubleBuffered);

        var trailChildren = new List<SkiaControl>()
        {
            // Unselected track (gray)
            new SkiaShape()
            {
                BackgroundColor = TrackColor,
                HeightRequest = CupertinoTrackHeight,
                CornerRadius = CupertinoTrackHeight / 2,
                HorizontalOptions = LayoutOptions.Fill,
                UseCache = SkiaCacheType.Operations,
                VerticalOptions = LayoutOptions.Center
            },

            // Selected track (iOS blue); XPos follows StartThumbX in range mode
            new SliderTrail()
                {
                    Tag = "SelectedTrail",
                    BackgroundColor = TrackSelectedColor,
                    HeightRequest = CupertinoTrackHeight,
                    CornerRadius = CupertinoTrackHeight / 2,
                    HorizontalOptions = LayoutOptions.Start,
                    UseCache = SkiaCacheType.Operations,
                    VerticalOptions = LayoutOptions.Center,
                    SideOffset = 0,
                    XPos = 0,
                }.Assign(out SelectedTrail)
                .ObserveProperties(() => EndThumb, me => UpdateTrailGeometry(),
                    x => x.TranslationX, x => x.Width, x => x.WidthRequest)
                .ObserveProperties(this, me => UpdateTrailGeometry(),
                    x => x.StartThumbX),

            // End thumb
            new SliderThumb()
                {
                    Tag = "EndThumb",
                    UseCache = SkiaCacheType.Image,
                    Children = new List<SkiaControl>()
                    {
                        new SkiaShape()
                        {
                            BackgroundColor = ThumbColor,
                            HorizontalOptions = LayoutOptions.Fill,
                            StrokeColor = new Color(0.8f, 0.8f, 0.8f),
                            StrokeWidth = CupertinoThumbBorderWidth,
                            Type = ShapeType.Circle,
                            VerticalOptions = LayoutOptions.Fill,
                            Shadows = new List<SkiaShadow>()
                            {
                                new SkiaShadow()
                                {
                                    Blur = 3,
                                    Opacity = 0.2,
                                    X = 0,
                                    Y = 1,
                                    Color = Colors.Gray
                                }
                            }
                        }
                    }
                }.Assign(out EndThumb)
                .ObserveProperties(() => Trail, me =>
                {
                    me.HeightRequest = CupertinoThumbDiameter;
                    me.WidthRequest = CupertinoThumbDiameter;
                }, x => x.Height)
                .ObserveProperties(this, me => me.TranslationX = this.EndThumbX,
                    x => x.EndThumbX),
        };

        if (EnableRange)
        {
            // Extra vertical space so thumb drop-shadows are not clipped by the track container (see SliderHeight default above).
            var thumbSize = CupertinoThumbDiameter + 4;

            // Adjust end-thumb for shadow room; trail edges follow thumb centers via UpdateTrailGeometry.
            EndThumb.Top     = 2;
            EndThumb.Left    = 6;
            EndThumb.Padding = new Thickness(2);

            StartThumb = new SliderThumb()
            {
                Tag     = "StartThumb",
                UseCache = SkiaCacheType.Image,
                Left    = -2,
                Top     = 2,
                Padding = new Thickness(2),
                Children = new List<SkiaControl>()
                {
                    new SkiaShape()
                    {
                        BackgroundColor = ThumbColor,
                        HorizontalOptions = LayoutOptions.Fill,
                        StrokeColor = new Color(0.8f, 0.8f, 0.8f),
                        StrokeWidth = CupertinoThumbBorderWidth,
                        Type = ShapeType.Circle,
                        VerticalOptions = LayoutOptions.Fill,
                        Shadows = new List<SkiaShadow>()
                        {
                            new SkiaShadow()
                            {
                                Blur = 3,
                                Opacity = 0.2,
                                X = 0,
                                Y = 1,
                                Color = Colors.Gray
                            }
                        }
                    }
                }
            }
            .ObserveProperties(() => Trail, me =>
            {
                me.HeightRequest = thumbSize;
                me.WidthRequest  = thumbSize;
            }, x => x.Height)
            .ObserveProperties(this, me => me.TranslationX = StartThumbX,
                x => x.StartThumbX);

            trailChildren.Insert(trailChildren.Count - 1, StartThumb);
        }

        Children = new List<SkiaControl>()
        {
            new SkiaLayout
                {
                    Tag = "Trail",
                    HeightRequest = SliderHeight,
                    HorizontalOptions = LayoutOptions.Fill,
                    Children = trailChildren
                }
                .ObserveProperties(this, me => me.HeightRequest = SliderHeight,
                    x => x.SliderHeight)
                .Assign(out Trail)
        };
    }

    // Style palettes, aligned with SkiaProgress/SkiaSwitch platform styles.
    // Material (M2 era) = blue family like SkiaSwitch/SkiaCheckbox/SkiaButton Material;
    // Material3 = Material You primary.
    private static readonly Color Material2PrimaryColor = Color.FromRgba(33, 150, 243, 255);  // Material blue
    private static readonly Color Material2TrackColor = Color.FromRgba(232, 234, 237, 255);   // Material surface variant
    private static readonly Color Material3PrimaryColor = Color.FromRgba(103, 80, 164, 255);  // M3 primary #6750A4
    private static readonly Color Material3TrackColor = Color.FromRgba(230, 224, 233, 255);   // M3 surface container highest
    private const double MaterialTrackHeight = 4.0;
    private const double MaterialThumbSize = 20.0;

    /// <summary>
    /// Accent color of the current Material-family style: M2 blue for Material, primary for Material3.
    /// </summary>
    protected Color MaterialPalettePrimary => UsingControlStyle == PrebuiltControlStyle.Material3
        ? Material3PrimaryColor
        : Material2PrimaryColor;

    /// <summary>
    /// Track color of the current Material-family style.
    /// </summary>
    protected Color MaterialPaletteTrack => UsingControlStyle == PrebuiltControlStyle.Material3
        ? Material3TrackColor
        : Material2TrackColor;

    private static readonly Color WindowsAccentColor = Color.FromRgba(0, 120, 212, 255);     // Fluent accent #0078D4
    private static readonly Color WindowsTrackColor = Color.FromRgba(198, 198, 198, 255);    // Fluent rail gray
    private const double WindowsTrackHeight = 4.0;
    private const double WindowsThumbSize = 20.0;
    private const double WindowsThumbDotSize = 10.0;

    /// <summary>
    /// Creates a Material Design 3 style slider: 4pt rounded track, 20pt filled primary-color thumb.
    /// When <see cref="EnableRange"/> is true, a second (start) thumb is added automatically.
    /// <see cref="ThumbColor"/>, <see cref="TrackColor"/> and <see cref="TrackSelectedColor"/> override
    /// the Material palette when explicitly set.
    /// </summary>
    protected virtual void CreateMaterialStyleContent()
    {
        StyleContentCreated = true;
        _styleOwnsSliderHeight = !IsSet(SliderHeightProperty);
        AvailableWidthAdjustment = -1;
        SetStyleDefault(HorizontalOptionsProperty, LayoutOptions.Fill);
        SetStyleDefault(MinimumWidthRequestProperty, 64d);
        SetStyleDefault(SliderHeightProperty, EnableRange ? MaterialThumbSize + 8 : MaterialThumbSize);
        Type = LayoutType.Column;
        SetStyleDefault(UseCacheProperty, SkiaCacheType.ImageDoubleBuffered);

        var trackColor = IsSet(TrackColorProperty) ? TrackColor : MaterialPaletteTrack;
        var selectedColor = IsSet(TrackSelectedColorProperty) ? TrackSelectedColor : MaterialPalettePrimary;
        var thumbColor = IsSet(ThumbColorProperty) ? ThumbColor : MaterialPalettePrimary;

        SkiaShape CreateThumbShape() => new SkiaShape()
        {
            BackgroundColor = thumbColor,
            HorizontalOptions = LayoutOptions.Fill,
            Type = ShapeType.Circle,
            VerticalOptions = LayoutOptions.Fill,
            Shadows = new List<SkiaShadow>()
            {
                new SkiaShadow() { Blur = 2, Opacity = 0.3, X = 0, Y = 1, Color = Colors.Black }
            }
        };

        var trailChildren = new List<SkiaControl>()
        {
            // Unselected track
            new SkiaShape()
            {
                BackgroundColor = trackColor,
                HeightRequest = MaterialTrackHeight,
                CornerRadius = MaterialTrackHeight / 2,
                HorizontalOptions = LayoutOptions.Fill,
                UseCache = SkiaCacheType.Operations,
                VerticalOptions = LayoutOptions.Center
            },

            // Selected track
            new SliderTrail()
                {
                    Tag = "SelectedTrail",
                    BackgroundColor = selectedColor,
                    HeightRequest = MaterialTrackHeight,
                    CornerRadius = MaterialTrackHeight / 2,
                    HorizontalOptions = LayoutOptions.Start,
                    UseCache = SkiaCacheType.Operations,
                    VerticalOptions = LayoutOptions.Center,
                    SideOffset = 0,
                    XPos = 0,
                }.Assign(out SelectedTrail)
                .ObserveProperties(() => EndThumb, me => UpdateTrailGeometry(),
                    x => x.TranslationX, x => x.Width, x => x.WidthRequest)
                .ObserveProperties(this, me => UpdateTrailGeometry(),
                    x => x.StartThumbX),

            // End thumb
            new SliderThumb()
                {
                    Tag = "EndThumb",
                    UseCache = SkiaCacheType.Image,
                    Children = new List<SkiaControl>() { CreateThumbShape() }
                }.Assign(out EndThumb)
                .ObserveProperties(() => Trail, me =>
                {
                    me.HeightRequest = MaterialThumbSize;
                    me.WidthRequest = MaterialThumbSize;
                }, x => x.Height)
                .ObserveProperties(this, me => me.TranslationX = this.EndThumbX,
                    x => x.EndThumbX),
        };

        if (EnableRange)
        {
            // Extra vertical space so thumb drop-shadows are not clipped by the track container (see SliderHeight default above).
            EndThumb.Top = 4;

            StartThumb = new SliderThumb()
            {
                Tag = "StartThumb",
                UseCache = SkiaCacheType.Image,
                Top = 4,
                Children = new List<SkiaControl>() { CreateThumbShape() }
            }
            .ObserveProperties(() => Trail, me =>
            {
                me.HeightRequest = MaterialThumbSize;
                me.WidthRequest = MaterialThumbSize;
            }, x => x.Height)
            .ObserveProperties(this, me => me.TranslationX = StartThumbX,
                x => x.StartThumbX);

            trailChildren.Insert(trailChildren.Count - 1, StartThumb);
        }

        Children = new List<SkiaControl>()
        {
            new SkiaLayout
                {
                    Tag = "Trail",
                    HeightRequest = SliderHeight,
                    HorizontalOptions = LayoutOptions.Fill,
                    Children = trailChildren
                }
                .ObserveProperties(this, me => me.HeightRequest = SliderHeight,
                    x => x.SliderHeight)
                .Assign(out Trail)
        };
    }

    /// <summary>
    /// Creates a Windows (Fluent) style slider: 4pt rounded track, 20pt white thumb with an accent inner dot.
    /// When <see cref="EnableRange"/> is true, a second (start) thumb is added automatically.
    /// <see cref="ThumbColor"/> overrides the inner dot color; <see cref="TrackColor"/> and
    /// <see cref="TrackSelectedColor"/> override the Fluent palette when explicitly set.
    /// </summary>
    protected virtual void CreateWindowsStyleContent()
    {
        StyleContentCreated = true;
        _styleOwnsSliderHeight = !IsSet(SliderHeightProperty);
        AvailableWidthAdjustment = -1;
        SetStyleDefault(HorizontalOptionsProperty, LayoutOptions.Fill);
        SetStyleDefault(MinimumWidthRequestProperty, 64d);
        SetStyleDefault(SliderHeightProperty, EnableRange ? WindowsThumbSize + 8 : WindowsThumbSize);
        Type = LayoutType.Column;
        SetStyleDefault(UseCacheProperty, SkiaCacheType.ImageDoubleBuffered);

        var trackColor = IsSet(TrackColorProperty) ? TrackColor : WindowsTrackColor;
        var selectedColor = IsSet(TrackSelectedColorProperty) ? TrackSelectedColor : WindowsAccentColor;
        var dotColor = IsSet(ThumbColorProperty) ? ThumbColor : WindowsAccentColor;

        SkiaShape CreateThumbShape() => new SkiaShape()
        {
            BackgroundColor = Colors.White,
            HorizontalOptions = LayoutOptions.Fill,
            StrokeColor = Color.FromRgba(229, 229, 229, 255),
            StrokeWidth = 1,
            Type = ShapeType.Circle,
            VerticalOptions = LayoutOptions.Fill,
            Shadows = new List<SkiaShadow>()
            {
                new SkiaShadow() { Blur = 2, Opacity = 0.25, X = 0, Y = 1, Color = Colors.Black }
            },
            Children = new List<SkiaControl>()
            {
                new SkiaShape()
                {
                    BackgroundColor = dotColor,
                    LockRatio = 1,
                    WidthRequest = WindowsThumbDotSize,
                    HorizontalOptions = LayoutOptions.Center,
                    Type = ShapeType.Circle,
                    VerticalOptions = LayoutOptions.Center,
                }
            }
        };

        var trailChildren = new List<SkiaControl>()
        {
            // Unselected track
            new SkiaShape()
            {
                BackgroundColor = trackColor,
                HeightRequest = WindowsTrackHeight,
                CornerRadius = WindowsTrackHeight / 2,
                HorizontalOptions = LayoutOptions.Fill,
                UseCache = SkiaCacheType.Operations,
                VerticalOptions = LayoutOptions.Center
            },

            // Selected track
            new SliderTrail()
                {
                    Tag = "SelectedTrail",
                    BackgroundColor = selectedColor,
                    HeightRequest = WindowsTrackHeight,
                    CornerRadius = WindowsTrackHeight / 2,
                    HorizontalOptions = LayoutOptions.Start,
                    UseCache = SkiaCacheType.Operations,
                    VerticalOptions = LayoutOptions.Center,
                    SideOffset = 0,
                    XPos = 0,
                }.Assign(out SelectedTrail)
                .ObserveProperties(() => EndThumb, me => UpdateTrailGeometry(),
                    x => x.TranslationX, x => x.Width, x => x.WidthRequest)
                .ObserveProperties(this, me => UpdateTrailGeometry(),
                    x => x.StartThumbX),

            // End thumb
            new SliderThumb()
                {
                    Tag = "EndThumb",
                    UseCache = SkiaCacheType.Image,
                    Children = new List<SkiaControl>() { CreateThumbShape() }
                }.Assign(out EndThumb)
                .ObserveProperties(() => Trail, me =>
                {
                    me.HeightRequest = WindowsThumbSize;
                    me.WidthRequest = WindowsThumbSize;
                }, x => x.Height)
                .ObserveProperties(this, me => me.TranslationX = this.EndThumbX,
                    x => x.EndThumbX),
        };

        if (EnableRange)
        {
            // Extra vertical space so thumb drop-shadows are not clipped by the track container (see SliderHeight default above).
            EndThumb.Top = 4;

            StartThumb = new SliderThumb()
            {
                Tag = "StartThumb",
                UseCache = SkiaCacheType.Image,
                Top = 4,
                Children = new List<SkiaControl>() { CreateThumbShape() }
            }
            .ObserveProperties(() => Trail, me =>
            {
                me.HeightRequest = WindowsThumbSize;
                me.WidthRequest = WindowsThumbSize;
            }, x => x.Height)
            .ObserveProperties(this, me => me.TranslationX = StartThumbX,
                x => x.StartThumbX);

            trailChildren.Insert(trailChildren.Count - 1, StartThumb);
        }

        Children = new List<SkiaControl>()
        {
            new SkiaLayout
                {
                    Tag = "Trail",
                    HeightRequest = SliderHeight,
                    HorizontalOptions = LayoutOptions.Fill,
                    Children = trailChildren
                }
                .ObserveProperties(this, me => me.HeightRequest = SliderHeight,
                    x => x.SliderHeight)
                .Assign(out Trail)
        };
    }

    /// <summary>
    /// Applies colors to the Material style content, falling back to the Material palette
    /// for properties not explicitly set.
    /// </summary>
    protected virtual void UpdateMaterialAppearance()
    {
        if (Trail == null || SelectedTrail == null || EndThumb == null)
            return;

        var unselectedTrack = Trail.Children.FirstOrDefault(c => c is SkiaShape && c != SelectedTrail) as SkiaShape;
        if (unselectedTrack != null)
        {
            unselectedTrack.BackgroundColor = IsSet(TrackColorProperty) ? TrackColor : MaterialPaletteTrack;
        }

        SelectedTrail.BackgroundColor = IsSet(TrackSelectedColorProperty) ? TrackSelectedColor : MaterialPalettePrimary;

        var thumbColor = IsSet(ThumbColorProperty) ? ThumbColor : MaterialPalettePrimary;
        foreach (var thumb in new[] { EndThumb, StartThumb })
        {
            if (thumb?.Children.FirstOrDefault(c => c is SkiaShape) is SkiaShape shape)
            {
                shape.BackgroundColor = thumbColor;
            }
        }

        UpdateTrailGeometry();
    }

    /// <summary>
    /// Applies colors to the Windows style content, falling back to the Fluent palette
    /// for properties not explicitly set. <see cref="ThumbColor"/> drives the inner accent dot.
    /// </summary>
    protected virtual void UpdateWindowsAppearance()
    {
        if (Trail == null || SelectedTrail == null || EndThumb == null)
            return;

        var unselectedTrack = Trail.Children.FirstOrDefault(c => c is SkiaShape && c != SelectedTrail) as SkiaShape;
        if (unselectedTrack != null)
        {
            unselectedTrack.BackgroundColor = IsSet(TrackColorProperty) ? TrackColor : WindowsTrackColor;
        }

        SelectedTrail.BackgroundColor = IsSet(TrackSelectedColorProperty) ? TrackSelectedColor : WindowsAccentColor;

        var dotColor = IsSet(ThumbColorProperty) ? ThumbColor : WindowsAccentColor;
        foreach (var thumb in new[] { EndThumb, StartThumb })
        {
            if (thumb?.Children.FirstOrDefault(c => c is SkiaShape) is SkiaShape outer &&
                outer.Children.FirstOrDefault(c => c is SkiaShape) is SkiaShape dot)
            {
                dot.BackgroundColor = dotColor;
            }
        }

        UpdateTrailGeometry();
    }

    bool UpdateStyleProperties(string propertyName)
    {
        // Handle style-affecting property changes for any control style
        if (propertyName.IsEither(
                nameof(CupertinoTrackHeight),
                nameof(CupertinoThumbDiameter),
                nameof(ThumbColor),
                nameof(TrackColor),
                nameof(TrackSelectedColor),
                nameof(CupertinoThumbBorderWidth),
                nameof(TrailStartOffset),
                nameof(TrailEndOffset)))
        {
            ApplyProperties();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the horizontal offset of the thumb visual center relative to the thumb's translated position,
    /// accounting for its Left placement and current width (falls back to WidthRequest before layout).
    /// </summary>
    protected virtual double GetThumbCenterOffset(SliderThumb thumb)
    {
        if (thumb == null)
            return 0;

        var width = thumb.Width > 0 ? thumb.Width : thumb.WidthRequest;
        if (width < 0)
            width = 0;

        return thumb.Left + width / 2;
    }

    /// <summary>
    /// Anchors the selected trail edges under the thumb centers:
    /// end edge at EndThumb center + <see cref="TrailEndOffset"/>, and in range mode
    /// start edge at StartThumb center + <see cref="TrailStartOffset"/>.
    /// This way the trail never peeks out from under the thumb and scales with any thumb size.
    /// Only applies to content built by Create*StyleContent, never to user-provided (XAML) content.
    /// </summary>
    protected virtual void UpdateTrailGeometry()
    {
        if (!StyleContentCreated || SelectedTrail == null || EndThumb == null)
            return;

        SelectedTrail.XPosEnd = EndThumb.TranslationX + GetThumbCenterOffset(EndThumb) + TrailEndOffset;

        if (EnableRange && StartThumb != null)
        {
            SelectedTrail.XPos = StartThumbX + GetThumbCenterOffset(StartThumb) + TrailStartOffset;
        }
    }

    /// <summary>
    /// Applies <see cref="ThumbColor"/>, <see cref="TrackColor"/> and <see cref="TrackSelectedColor"/>
    /// to the default style content. Each color is applied only when explicitly set,
    /// keeping the stock default look otherwise.
    /// </summary>
    protected virtual void UpdateDefaultAppearance()
    {
        if (Trail == null || SelectedTrail == null || EndThumb == null)
            return;

        if (IsSet(TrackColorProperty))
        {
            var unselectedTrack = Trail.Children.FirstOrDefault(c => c is SkiaShape && c != SelectedTrail) as SkiaShape;
            if (unselectedTrack != null)
            {
                unselectedTrack.BackgroundColor = TrackColor;
                unselectedTrack.StrokeColor = Darken(TrackColor);
            }
        }

        if (IsSet(TrackSelectedColorProperty))
        {
            SelectedTrail.BackgroundColor = TrackSelectedColor;
            SelectedTrail.StrokeColor = Darken(TrackSelectedColor);
        }

        if (IsSet(ThumbColorProperty))
        {
            ApplyThumbColor(EndThumb);
            ApplyThumbColor(StartThumb);
        }

        UpdateTrailGeometry();
    }

    /// <summary>
    /// Returns a darker shade of the given color, used to derive strokes and shadows
    /// that must follow a user-set fill color.
    /// </summary>
    protected static Color Darken(Color color, float ratio = 0.7f)
    {
        return new Color(color.Red * ratio, color.Green * ratio, color.Blue * ratio, color.Alpha);
    }

    /// <summary>
    /// Applies <see cref="ThumbColor"/> to the thumb's main shape, deriving matching
    /// darker shades for its stroke, shadow and any accent children strokes.
    /// </summary>
    protected void ApplyThumbColor(SliderThumb thumb)
    {
        if (thumb == null)
            return;

        var darker = Darken(ThumbColor);

        var shape = thumb.Children.FirstOrDefault(c => c is SkiaShape) as SkiaShape;
        if (shape != null)
        {
            shape.BackgroundColor = ThumbColor;
            shape.StrokeColor = darker;
            foreach (var shadow in shape.Shadows)
            {
                shadow.Color = darker;
            }
        }

        // Accent children (e.g. the inner dot) keep their fill but their stroke follows too
        foreach (var accent in thumb.Children.OfType<SkiaShape>().Skip(1))
        {
            accent.StrokeColor = darker;
        }
    }

    /// <summary>
    /// Updates the appearance of the Cupertino slider based on the current property values
    /// </summary>
    private void UpdateCupertinoAppearance()
    {
        // Only proceed if views have been created
        if (Trail == null || SelectedTrail == null || EndThumb == null)
            return;

        // Find and update the track
        var unselectedTrack = Trail.Children.FirstOrDefault(c => c is SkiaShape && c != SelectedTrail) as SkiaShape;
        if (unselectedTrack != null)
        {
            unselectedTrack.HeightRequest = CupertinoTrackHeight;
            unselectedTrack.CornerRadius = CupertinoTrackHeight / 2;
            unselectedTrack.BackgroundColor = TrackColor;
        }

        // Update the selected trail
        SelectedTrail.HeightRequest = CupertinoTrackHeight;
        SelectedTrail.CornerRadius = CupertinoTrackHeight / 2;
        SelectedTrail.BackgroundColor = TrackSelectedColor;

        // Update end thumb size and border
        EndThumb.HeightRequest = CupertinoThumbDiameter;
        EndThumb.WidthRequest = CupertinoThumbDiameter;

        var thumbShape = EndThumb.Children.FirstOrDefault(c => c is SkiaShape) as SkiaShape;
        if (thumbShape != null)
        {
            thumbShape.StrokeWidth = CupertinoThumbBorderWidth;
            thumbShape.BackgroundColor = ThumbColor;
        }

        // Update start thumb if range mode is active
        if (StartThumb != null)
        {
            StartThumb.HeightRequest = CupertinoThumbDiameter;
            StartThumb.WidthRequest = CupertinoThumbDiameter;

            var startThumbShape = StartThumb.Children.FirstOrDefault(c => c is SkiaShape) as SkiaShape;
            if (startThumbShape != null)
            {
                startThumbShape.StrokeWidth = CupertinoThumbBorderWidth;
                startThumbShape.BackgroundColor = ThumbColor;
            }
        }

        // Update the overall slider height (range mode needs extra room for shadows)
        if (_styleOwnsSliderHeight)
        {
            SliderHeight = EnableRange ? CupertinoThumbDiameter + 8 : CupertinoThumbDiameter;
        }
        Trail.HeightRequest = SliderHeight;

        UpdateTrailGeometry();

        Invalidate();
    }

    public static readonly BindableProperty ThumbColorProperty =
        BindableProperty.Create(nameof(ThumbColor), typeof(Color), typeof(SkiaSlider),
            new Color(1f, 1f, 1f));

    /// <summary>
    /// The color of the thumb. Applies to any control style; in the default style it is applied
    /// only when explicitly set, preserving the stock look otherwise.
    /// </summary>
    public Color ThumbColor
    {
        get { return (Color)GetValue(ThumbColorProperty); }
        set { SetValue(ThumbColorProperty, value); }
    }

    public static readonly BindableProperty TrailStartOffsetProperty =
        BindableProperty.Create(nameof(TrailStartOffset), typeof(double), typeof(SkiaSlider), 0.0);

    /// <summary>
    /// Fine-tune offset in points for where the selected trail starts relative to the start thumb center
    /// (range mode only). Positive moves the trail start to the right. Default 0 = exactly under the thumb center.
    /// Use when a custom thumb size or asymmetric thumb visual needs the trail edge adjusted.
    /// </summary>
    public double TrailStartOffset
    {
        get { return (double)GetValue(TrailStartOffsetProperty); }
        set { SetValue(TrailStartOffsetProperty, value); }
    }

    public static readonly BindableProperty TrailEndOffsetProperty =
        BindableProperty.Create(nameof(TrailEndOffset), typeof(double), typeof(SkiaSlider), 0.0);

    /// <summary>
    /// Fine-tune offset in points for where the selected trail ends relative to the end thumb center.
    /// Positive extends the trail to the right. Default 0 = exactly under the thumb center.
    /// Use when a custom thumb size or asymmetric thumb visual needs the trail edge adjusted.
    /// </summary>
    public double TrailEndOffset
    {
        get { return (double)GetValue(TrailEndOffsetProperty); }
        set { SetValue(TrailEndOffsetProperty, value); }
    }

    #region CUPERTINO STYLE PROPERTIES

    public static readonly BindableProperty CupertinoTrackHeightProperty =
        BindableProperty.Create(nameof(CupertinoTrackHeight), typeof(double), typeof(SkiaSlider), 2.0);

    /// <summary>
    /// The height of the iOS slider track in points (default is 2pt per iOS guidelines)
    /// </summary>
    public double CupertinoTrackHeight
    {
        get { return (double)GetValue(CupertinoTrackHeightProperty); }
        set { SetValue(CupertinoTrackHeightProperty, value); }
    }

    public static readonly BindableProperty CupertinoThumbDiameterProperty =
        BindableProperty.Create(nameof(CupertinoThumbDiameter), typeof(double), typeof(SkiaSlider), 28.0);

    /// <summary>
    /// The diameter of the iOS slider thumb in points (default is 28pt per iOS guidelines)
    /// </summary>
    public double CupertinoThumbDiameter
    {
        get { return (double)GetValue(CupertinoThumbDiameterProperty); }
        set { SetValue(CupertinoThumbDiameterProperty, value); }
    }

    public static readonly BindableProperty TrackColorProperty =
        BindableProperty.Create(nameof(TrackColor), typeof(Color), typeof(SkiaSlider), new Color(0.8f, 0.8f, 0.8f));

    /// <summary>
    /// The color of the unselected track (default light gray). Applies to any control style;
    /// in the default style it is applied only when explicitly set.
    /// </summary>
    public Color TrackColor
    {
        get { return (Color)GetValue(TrackColorProperty); }
        set { SetValue(TrackColorProperty, value); }
    }

    public static readonly BindableProperty TrackSelectedColorProperty =
        BindableProperty.Create(nameof(TrackSelectedColor), typeof(Color), typeof(SkiaSlider),
            new Color(0f, 0.478f, 1f));

    /// <summary>
    /// The color of the selected track (default iOS blue #007AFF). Applies to any control style;
    /// in the default style it is applied only when explicitly set.
    /// </summary>
    public Color TrackSelectedColor
    {
        get { return (Color)GetValue(TrackSelectedColorProperty); }
        set { SetValue(TrackSelectedColorProperty, value); }
    }

    public static readonly BindableProperty CupertinoThumbBorderWidthProperty =
        BindableProperty.Create(nameof(CupertinoThumbBorderWidth), typeof(double), typeof(SkiaSlider), 0.5);

    /// <summary>
    /// The width of the border around the iOS slider thumb (default 0.5pt)
    /// </summary>
    public double CupertinoThumbBorderWidth
    {
        get { return (double)GetValue(CupertinoThumbBorderWidthProperty); }
        set { SetValue(CupertinoThumbBorderWidthProperty, value); }
    }

    #endregion

    #endregion

    #region GESTURES

    protected bool IsUserPanning { get; set; }

    /// <summary>
    /// enlarge hotspot by pts
    /// </summary>
    public double moreHotspotSize = 10.0;

    protected double lastTouchX;

    /// <summary>
    /// track touched area type
    /// </summary>
    protected RangeZone touchArea;

    protected Vector2 _panningStartOffsetPts;

    protected override void OnLayoutChanged()
    {
        base.OnLayoutChanged();

        FindViews();
    }

    public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
    {
        CheckHovered(args);

        //Super.Log($"[Touch] SLIDER got {args.Type}");

        bool passedToChildren = false;

        ISkiaGestureListener PassToChildren()
        {
            passedToChildren = true;

            return base.ProcessGestures(args, apply);
        }

        var consumedDefault = BlockGesturesBelow ? this : null;

        ISkiaGestureListener consumed = null;

        //pass Released always to children first
        if (args.Type == TouchActionResult.Up
            || !IsUserPanning || !RespondsToGestures)
        {
            if (args.Event.NumberOfTouches < 2)
                TouchBusy = false;

            consumed = PassToChildren();
            if (consumed == this)
            {
                //BlockGesturesBelow fired
                consumed = null;
            }
            if (consumed != null && args.Type != TouchActionResult.Up)
            {
                return consumed;
            }
        }

        if (!RespondsToGestures)
        {
            if (consumed == null) return consumedDefault;

            return consumed;
        }


        void ResetPan()
        {
            IsUserPanning = false;
            _panningStartOffsetPts = new(args.Event.Location.X, args.Event.Location.Y);
        }

        switch (args.Type)
        {
            case TouchActionResult.Down:

                if (args.Event.NumberOfTouches < 2)
                {
                    ResetPan();
                }

                var thisOffset = TranslateInputCoords(apply.ChildOffset);

                var x = args.Event.Location.X + thisOffset.X;
                var y = args.Event.Location.Y + thisOffset.Y;

                var relativeX = x - LastDrawnAt.Left; //inside this control
                var relativeY = y - LastDrawnAt.Top; //inside this control

                var locationX = relativeX / RenderingScale; //from pix to pts
                var locationY = relativeY / RenderingScale; //from pix to pts

                if (EnableRange && locationX >= StartThumbX - moreHotspotSize &&
                    locationX <= StartThumbX + SliderHeight + moreHotspotSize)
                {
                    touchArea = RangeZone.Start;
                }
                else if (locationX >= EndThumbX - moreHotspotSize &&
                         locationX <= EndThumbX + SliderHeight + moreHotspotSize)
                {
                    touchArea = RangeZone.End;
                }
                else
                {
                    touchArea = RangeZone.Unknown;
                }

                bool onTrail = true;
                if (Trail != null)
                {
                    onTrail = CheckChildGestureHit(Trail, args, apply); 
                }

                if (onTrail || touchArea == RangeZone.Start || touchArea == RangeZone.End)
                    IsPressed = true;

                if (touchArea == RangeZone.Unknown && ClickOnTrailEnabled)
                {
                    //clicked on trail maybe

                    if (onTrail)
                    {
                        var halfThumb = SliderHeight / 2f;

                        if (EnableRange)
                        {
                            var half = (Width + AvailableWidthAdjustment) / 2.0;

                            if (locationX > half)
                            {
                                MoveEndThumbHere(locationX - halfThumb);
                            }
                            else if (locationX <= half)
                            {
                                MoveStartThumbHere(locationX - halfThumb);
                            }
                        }
                        else
                        {
                            MoveEndThumbHere(locationX - halfThumb);
                        }
                    }
                }

                if (touchArea == RangeZone.Start)
                {
                    consumed = this;
                    lastTouchX = StartThumbX;
                }
                else if (touchArea == RangeZone.End)
                {
                    consumed = this;
                    lastTouchX = EndThumbX;
                }

                break;

            case TouchActionResult.Panning when args.Event.NumberOfTouches == 1:

                //filter correct direction so we could scroll below the control in another direction:
                if (!IsUserPanning && IgnoreWrongDirection)
                {
                    //first panning gesture..
                    var panDirection = GetDirectionType(_panningStartOffsetPts,
                        new Vector2(_panningStartOffsetPts.X + args.Event.Distance.Total.X,
                            _panningStartOffsetPts.Y + args.Event.Distance.Total.Y), 0.8f);

                    if (Orientation == OrientationType.Vertical && panDirection != DirectionType.Vertical)
                    {
                        return consumedDefault;
                    }

                    if (Orientation == OrientationType.Horizontal && panDirection != DirectionType.Horizontal)
                    {
                        return consumedDefault;
                    }
                }


                IsUserPanning = true;


                //synch this
                if (touchArea == RangeZone.Start)
                    lastTouchX = StartThumbX;
                else if (touchArea == RangeZone.End)
                    lastTouchX = EndThumbX;


                if (touchArea != RangeZone.Unknown)
                {
                    TouchBusy = true;

                    if (touchArea == RangeZone.Start)
                    {
                        var maybe = lastTouchX + args.Event.Distance.Delta.X / RenderingScale; //args.TotalDistance.X;
                        SetStartOffsetClamped(maybe);
                    }
                    else if (touchArea == RangeZone.End)
                    {
                        var maybe = lastTouchX + args.Event.Distance.Delta.X / RenderingScale;
                        SetEndOffsetClamped(maybe);

                        //Super.Log($"[Touch] SLIDER zone END {maybe}");
                    }

                    RecalculateValues();
                }

                consumed = this;
                break;

            case TouchActionResult.Up when args.Event.NumberOfTouches < 2:
                IsUserPanning = false;
                IsPressed = false;
                break;
        }

        if (consumed != null || IsUserPanning) // || args.Event.NumberOfTouches > 1)
        {
            if (consumed == null && args.Type != TouchActionResult.Up)
            {
                return this;
            }

            if (consumed == null) return consumedDefault;
            return consumed;
        }

        if (!passedToChildren)
        {
            return PassToChildren();
        }

        return consumedDefault;
    }

    private bool TouchBusy;

    #endregion

    #region PROPERTIES

    public static readonly BindableProperty ClickOnTrailEnabledProperty =
        BindableProperty.Create(nameof(ClickOnTrailEnabled), typeof(bool),
            typeof(SkiaSlider), true);

    public bool ClickOnTrailEnabled
    {
        get { return (bool)GetValue(ClickOnTrailEnabledProperty); }
        set { SetValue(ClickOnTrailEnabledProperty, value); }
    }

    public static readonly BindableProperty EnableRangeProperty =
        BindableProperty.Create(nameof(EnableRange), typeof(bool), typeof(SkiaSlider), false);

    public bool EnableRange
    {
        get { return (bool)GetValue(EnableRangeProperty); }
        set { SetValue(EnableRangeProperty, value); }
    }

    public static readonly BindableProperty RespondsToGesturesProperty = BindableProperty.Create(
        nameof(RespondsToGestures),
        typeof(bool),
        typeof(SkiaSlider),
        true);

    /// <summary>
    /// Can be open/closed by gestures along with code-behind, default is true
    /// </summary>
    public bool RespondsToGestures
    {
        get { return (bool)GetValue(RespondsToGesturesProperty); }
        set { SetValue(RespondsToGesturesProperty, value); }
    }

    public static readonly BindableProperty SliderHeightProperty =
        BindableProperty.Create(nameof(SliderHeight), typeof(double), typeof(SkiaSlider), 22.0); //, BindingMode.TwoWay

    public double SliderHeight
    {
        get { return (double)GetValue(SliderHeightProperty); }
        set { SetValue(SliderHeightProperty, value); }
    }

    public static readonly BindableProperty MinProperty =
        BindableProperty.Create(nameof(Min), typeof(double), typeof(SkiaSlider), 0.0); //, BindingMode.TwoWay

    public double Min
    {
        get { return (double)GetValue(MinProperty); }
        set { SetValue(MinProperty, value); }
    }

    public static readonly BindableProperty MaxProperty =
        BindableProperty.Create(nameof(Max), typeof(double), typeof(SkiaSlider), 100.0);

    public double Max
    {
        get { return (double)GetValue(MaxProperty); }
        set { SetValue(MaxProperty, value); }
    }

    public static readonly BindableProperty StartProperty =
        BindableProperty.Create(nameof(Start), typeof(double), typeof(SkiaSlider), 0.0, BindingMode.TwoWay,
            propertyChanged: OnStartPropertyChanged,
            coerceValue: CoerceValue);

    /// <summary>
    /// Enabled for ranged
    /// </summary>
    public double Start
    {
        get { return (double)GetValue(StartProperty); }
        set { SetValue(StartProperty, value); }
    }

    private static void OnStartPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SkiaSlider slider && newValue is double newStartValue)
        {
            slider.OnStartChanged();
        }
    }

    private static object CoerceValue(BindableObject bindable, object value)
    {
        var newValue = (double)value;

        if (bindable is SkiaSlider slider)
        {
            var adjusted = slider.AdjustToStepValue(newValue, slider.Min, slider.Step);
            if (slider.Width >= 0)
            {
                return Math.Clamp(adjusted, slider.Min, slider.Max);
            }

            return adjusted;
        }

        return value;
    }

    public static readonly BindableProperty EndProperty = BindableProperty.Create(
        nameof(End),
        typeof(double),
        typeof(SkiaSlider),
        100.0,
        BindingMode.TwoWay,
        propertyChanged: OnEndPropertyChanged,
        coerceValue: CoerceValue);

    /// <summary>
    /// For non-ranged this is your main value
    /// </summary>
    public double End
    {
        get { return (double)GetValue(EndProperty); }
        set { SetValue(EndProperty, value); }
    }

    private static void OnEndPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SkiaSlider slider && newValue is double newEndValue)
        {
            slider.OnEndChanged();
        }
    }

    public static readonly BindableProperty StartThumbXProperty =
        BindableProperty.Create(nameof(StartThumbX), typeof(double), typeof(SkiaSlider), 0.0);

    public double StartThumbX
    {
        get { return (double)GetValue(StartThumbXProperty); }
        set { SetValue(StartThumbXProperty, value); }
    }

    public static readonly BindableProperty EndThumbXProperty =
        BindableProperty.Create(nameof(EndThumbX), typeof(double), typeof(SkiaSlider), 0.0);

    public double EndThumbX
    {
        get { return (double)GetValue(EndThumbXProperty); }
        set { SetValue(EndThumbXProperty, value); }
    }

    public static readonly BindableProperty StepProperty =
        BindableProperty.Create(nameof(Step), typeof(double), typeof(SkiaSlider), 1.0); //, BindingMode.TwoWay

    public double Step
    {
        get { return (double)GetValue(StepProperty); }
        set { SetValue(StepProperty, value); }
    }

    public static readonly BindableProperty RangeMinProperty =
        BindableProperty.Create(nameof(RangeMin), typeof(double), typeof(SkiaSlider), 0.0); //, BindingMode.TwoWay

    public double RangeMin
    {
        get { return (double)GetValue(RangeMinProperty); }
        set { SetValue(RangeMinProperty, value); }
    }

    public static readonly BindableProperty ValueStringFormatProperty =
        BindableProperty.Create(nameof(ValueStringFormat), typeof(string), typeof(SkiaSlider),
            "### ### ##0.##"); //, BindingMode.TwoWay

    public string ValueStringFormat
    {
        get { return (string)GetValue(ValueStringFormatProperty); }
        set { SetValue(ValueStringFormatProperty, value); }
    }

    public static readonly BindableProperty MinMaxStringFormatProperty =
        BindableProperty.Create(nameof(MinMaxStringFormat), typeof(string), typeof(SkiaSlider),
            "### ### ##0.##"); //, BindingMode.TwoWay

    public string MinMaxStringFormat
    {
        get { return (string)GetValue(MinMaxStringFormatProperty); }
        set { SetValue(MinMaxStringFormatProperty, value); }
    }

    public static readonly BindableProperty AvailableWidthAdjustmentProperty =
        BindableProperty.Create(nameof(AvailableWidthAdjustment), typeof(double), typeof(SkiaSlider), 0.0);

    /// <summary>
    /// Padding for the Thumb to go, to be able to leave some space around for shadows etc.
    /// </summary>
    public double AvailableWidthAdjustment
    {
        get { return (double)GetValue(AvailableWidthAdjustmentProperty); }
        set { SetValue(AvailableWidthAdjustmentProperty, value); }
    }

    public static readonly BindableProperty OrientationProperty = BindableProperty.Create(nameof(Orientation),
        typeof(OrientationType), typeof(SkiaSlider),
        OrientationType.Horizontal,
        propertyChanged: NeedDraw);

    /// <summary>
    /// <summary>Gets or sets the orientation. This is a bindable property.</summary>
    /// </summary>
    public OrientationType Orientation
    {
        get { return (OrientationType)GetValue(OrientationProperty); }
        set { SetValue(OrientationProperty, value); }
    }

    public static readonly BindableProperty IgnoreWrongDirectionProperty = BindableProperty.Create(
        nameof(IgnoreWrongDirection),
        typeof(bool),
        typeof(SkiaSlider),
        true);

    /// <summary>
    /// Will ignore gestures of the wrong direction, like if this Orientation is Horizontal will ignore gestures with vertical direction velocity
    /// </summary>
    public bool IgnoreWrongDirection
    {
        get { return (bool)GetValue(IgnoreWrongDirectionProperty); }
        set { SetValue(IgnoreWrongDirectionProperty, value); }
    }

    #endregion

    #region ENGINE

    protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        // EnableRange may be set after ControlStyle in an object initializer, which causes
        // NeedInitialize to fire before EnableRange=true. Detect this and force a rebuild.
        if (propertyName == nameof(EnableRange) && DefaultContentCreated && StyleContentCreated)
        {
            Trail = null;
            SelectedTrail = null;
            EndThumb = null;
            StartThumb = null;
            ClearChildren();
            DefaultContentCreated = false;
        }

        if (UpdateStyleProperties(propertyName))
            return;

        if (propertyName.IsEither(nameof(Min),
                nameof(MinMaxStringFormat), nameof(Max)))
        {
            var mask = "{0:" + MinMaxStringFormat + "}";
            MinDesc = string.Format(mask, Min).Trim();
            MaxDesc = string.Format(mask, Max).Trim();
        }

        if (lockInternal)
            return;

        if (propertyName.IsEither(nameof(Width),
                nameof(Min), nameof(Max), nameof(StartThumbX),
                nameof(EndThumbX), nameof(Step), nameof(Start),
                nameof(End), nameof(AvailableWidthAdjustment)))
        {
            if (Width > -1)
            {
                if (EnableRange)
                {
                    StepValue = (Max - Min) / (Width + AvailableWidthAdjustment - SliderHeight);
                }
                else
                {
                    StepValue = (Max - Min) / (Width + AvailableWidthAdjustment * 2 - SliderHeight);
                }

                //if (!TouchBusy)
                //{
                //    var mask = "{0:" + ValueStringFormat + "}";
                //    if (EnableRange)
                //    {
                //        SetStartOffsetClamped((Start - Min) / StepValue - AvailableWidthAdjustment);
                //        StartDesc = string.Format(mask, Start).Trim();
                //    }
                //    SetEndOffsetClamped((End - Min) / StepValue);
                //    EndDesc = string.Format(mask, End).Trim();
                //}
                if (!TouchBusy)
                {
                    var mask = "{0:" + ValueStringFormat + "}";
                    if (EnableRange)
                    {
                        var maybe = PositionFromValue(Start) + AvailableWidthAdjustment;
                        SetStartOffsetClamped(maybe);
                        StartDesc = string.Format(mask, Start).Trim();
                    }

                    var maybeEnd = PositionFromValue(End) - AvailableWidthAdjustment;
                    SetEndOffsetClamped(maybeEnd);
                    EndDesc = string.Format(mask, End).Trim();
                }
            }
        }
    }

    public virtual void OnEndChanged()
    {
        EndChanged?.Invoke(this, End);
    }

    public virtual void OnStartChanged()
    {
        StartChanged?.Invoke(this, Start);
    }

    public override void OnDisposing()
    {
        base.OnDisposing();

        StartChanged = null;
        EndChanged = null;
    }

    public event EventHandler<double> StartChanged;
    public event EventHandler<double> EndChanged;

    protected double AdjustToStepValue(double value, double minValue, double stepValue)
    {
        if (stepValue <= 0) return value;

        double relativeValue = value - minValue;
        double adjustedStep = Math.Round(relativeValue / stepValue) * stepValue;
        return adjustedStep + minValue;
    }

    protected virtual void RecalculateValues()
    {
        lockInternal = true;

        var mask = "{0:" + ValueStringFormat + "}";
        ConvertOffsetsToValues();
        if (EnableRange)
        {
            StartDesc = string.Format(mask, Start).Trim();
        }

        EndDesc = string.Format(mask, End).Trim();

        lockInternal = false;
    }

    protected void MoveEndThumbHere(double x)
    {
        if (!ClickOnTrailEnabled)
            return;

        touchArea = RangeZone.End;

        lockInternal = true;
        SetEndOffsetClamped(x);
        lockInternal = false;

        RecalculateValues();
    }

    protected void MoveStartThumbHere(double x)
    {
        if (!ClickOnTrailEnabled)
            return;

        touchArea = RangeZone.Start;

        lockInternal = true;

        SetStartOffsetClamped(x);

        lockInternal = false;

        RecalculateValues();
    }

    private volatile bool lockInternal;
    private string _StartDesc;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public string StartDesc
    {
        get { return _StartDesc; }
        set
        {
            if (_StartDesc != value)
            {
                _StartDesc = value;
                OnPropertyChanged();
            }
        }
    }

    private string _EndDesc;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public string EndDesc
    {
        get { return _EndDesc; }
        set
        {
            if (_EndDesc != value)
            {
                _EndDesc = value;
                OnPropertyChanged();
            }
        }
    }

    private string _MinDesc;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public string MinDesc
    {
        get { return _MinDesc; }
        set
        {
            if (_MinDesc != value)
            {
                _MinDesc = value;
                OnPropertyChanged();
            }
        }
    }

    private string _MaxDesc;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public string MaxDesc
    {
        get { return _MaxDesc; }
        set
        {
            if (_MaxDesc != value)
            {
                _MaxDesc = value;
                OnPropertyChanged();
            }
        }
    }

    private double _StepValue;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public double StepValue
    {
        get { return _StepValue; }
        set
        {
            if (_StepValue != value)
            {
                _StepValue = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _IsPressed;

    public bool IsPressed
    {
        get { return _IsPressed; }
        set
        {
            if (_IsPressed != value)
            {
                _IsPressed = value;
                OnPropertyChanged();
            }
        }
    }

    #endregion

    //protected virtual void ConvertOffsetsToValues()
    //{
    //    if (EnableRange)
    //    {
    //        Start = StepValue * (this.StartThumbX + AvailableWidthAdjustment) + Min;
    //        End = StepValue * (this.EndThumbX - AvailableWidthAdjustment) + Min;
    //    }
    //    else
    //    {
    //        End = StepValue * (this.EndThumbX + AvailableWidthAdjustment) + Min;
    //    }
    //}

    //void SetStartOffsetClamped(double maybe)
    //{
    //    if (maybe < -AvailableWidthAdjustment)
    //    {
    //        StartThumbX = -AvailableWidthAdjustment;
    //    }
    //    else
    //    if (EnableRange && maybe > (EndThumbX - RangeMin / StepValue))
    //    {
    //        StartThumbX = EndThumbX - RangeMin / StepValue;
    //    }
    //    else
    //    {
    //        StartThumbX = maybe;
    //    }
    //}

    //void SetEndOffsetClamped(double maybe)
    //{
    //    if (maybe < -AvailableWidthAdjustment)
    //    {
    //        EndThumbX = -AvailableWidthAdjustment;
    //    }
    //    else
    //    if (maybe > (Width + AvailableWidthAdjustment) - SliderHeight)
    //    {
    //        EndThumbX = (Width + AvailableWidthAdjustment) - SliderHeight;
    //    }
    //    else
    //    if (EnableRange && maybe < (StartThumbX + RangeMin / StepValue))
    //    {
    //        EndThumbX = StartThumbX + RangeMin / StepValue;
    //    }
    //    else
    //    {
    //        EndThumbX = maybe;
    //    }
    //}

    void SetStartOffsetClamped(double maybe)
    {
        double minPosition = GetStartThumbMinPosition();
        double maxPosition = GetStartThumbMaxPosition();

        if (maybe < minPosition)
        {
            StartThumbX = minPosition;
        }
        else if (maybe > maxPosition)
        {
            StartThumbX = maxPosition;
        }
        else
        {
            StartThumbX = maybe;
        }
    }

    void SetEndOffsetClamped(double maybe)
    {
        double minPosition = GetEndThumbMinPosition();
        double maxPosition = GetEndThumbMaxPosition();

        if (maybe < minPosition)
        {
            EndThumbX = minPosition;
        }
        else if (maybe > maxPosition)
        {
            EndThumbX = maxPosition;
        }
        else
        {
            EndThumbX = maybe;
        }
    }

    protected virtual void ConvertOffsetsToValues()
    {
        if (EnableRange)
        {
            Start = AdjustToStepValue(ValueFromPosition(this.StartThumbX + AvailableWidthAdjustment), Min, Step);
            End = AdjustToStepValue(ValueFromPosition(this.EndThumbX - AvailableWidthAdjustment), Min, Step);
        }
        else
        {
            End = AdjustToStepValue(ValueFromPosition(this.EndThumbX + AvailableWidthAdjustment), Min, Step);
        }
    }

    #region Invert

    private double ValueFromPosition(double position)
    {
        double totalLength = Width + AvailableWidthAdjustment * 2 - SliderHeight;
        if (totalLength <= 0) return Min; // Avoid division by zero

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

    private double PositionFromValue(double value)
    {
        double totalLength = Width + AvailableWidthAdjustment - SliderHeight;
        if (totalLength <= 0) return 0; // Avoid division by zero

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

    private double GetStartThumbMinPosition()
    {
        if (Invert)
        {
            if (EnableRange)
            {
                return EndThumbX + RangeMin / StepValue;
            }
            else
            {
                return -AvailableWidthAdjustment;
            }
        }
        else
        {
            return -AvailableWidthAdjustment;
        }
    }

    private double GetStartThumbMaxPosition()
    {
        if (Invert)
        {
            return (Width + AvailableWidthAdjustment) - SliderHeight;
        }
        else
        {
            if (EnableRange)
            {
                return EndThumbX - RangeMin / StepValue;
            }
            else
            {
                return (Width + AvailableWidthAdjustment) - SliderHeight;
            }
        }
    }

    private double GetEndThumbMinPosition()
    {
        if (Invert)
        {
            return -AvailableWidthAdjustment;
        }
        else
        {
            if (EnableRange)
            {
                return StartThumbX + RangeMin / StepValue;
            }
            else
            {
                return -AvailableWidthAdjustment;
            }
        }
    }

    private double GetEndThumbMaxPosition()
    {
        if (Invert)
        {
            if (EnableRange)
            {
                return StartThumbX - RangeMin / StepValue;
            }
            else
            {
                return (Width + AvailableWidthAdjustment) - SliderHeight;
            }
        }
        else
        {
            return (Width + AvailableWidthAdjustment) - SliderHeight;
        }
    }

    #endregion
}


/*
public class SkiaSlider : SkiaRangeBase
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
                  //case PrebuiltControlStyle.Material:
                  //    CreateMaterialStyleContent();
                  //    break;
                  //case PrebuiltControlStyle.Windows:
                  //    CreateWindowsStyleContent();
                  //    break;
                  default:
                      CreateDefaultStyleContent();
                      break;
              }

              ApplyProperties();
          }
      }

      protected override void FindViews()
      {
          // Find slider-specific views
          if (Trail == null)
              Trail = FindView<SkiaLayout>("Trail");

          if (SelectedTrail == null)
              SelectedTrail = FindView<SliderTrail>("SelectedTrail");

          if (EndThumb == null)
              EndThumb = FindView<SliderThumb>("EndThumb");

          // Set base class references
          Track = Trail;
          ProgressTrail = SelectedTrail;
      }

      protected override void UpdateVisualState()
      {
          // Update slider-specific visual state
          if (SelectedTrail != null && EndThumb != null)
          {
              SelectedTrail.XPosEnd = EndThumb.TranslationX;
              SelectedTrail.BackgroundColor = ProgressColor;
          }
      }

      public virtual void ApplyProperties()
      {
          if (UsingControlStyle == PrebuiltControlStyle.Cupertino)
          {
              UpdateCupertinoAppearance();
          }
      }

      public SkiaControl Trail;
      private SliderTrail SelectedTrail;
      private SliderThumb EndThumb;

      protected virtual void CreateDefaultStyleContent()
      {
          //SetDefaultContentSize(64, 35);


          AvailableWidthAdjustment = 1.5;
          HorizontalOptions = LayoutOptions.Fill;
          MinimumWidthRequest = 64;
          SliderHeight = 35;
          Type = LayoutType.Column;
          UseCache = SkiaCacheType.ImageDoubleBuffered;

          Children = new List<SkiaControl>()
          {
              //main grid
              new SkiaLayout
                  {
                      Tag = "Trail",
                      HeightRequest = SliderHeight,
                      HorizontalOptions = LayoutOptions.Fill,
                      Children = new List<SkiaControl>()
                      {
                          //unselected trail
                          new SkiaShape()
                          {
                              BackgroundColor = Colors.DarkGray,
                              HeightRequest = 8,
                              CornerRadius = 6,
                              HorizontalOptions = LayoutOptions.Fill,
                              StrokeColor = Colors.Grey,
                              StrokeWidth = 2,
                              UseCache = SkiaCacheType.Operations,
                              VerticalOptions = LayoutOptions.Center
                          },
                          //selected trail
                          new SliderTrail()
                              {
                                  Tag = "SelectedTrail",
                                  BackgroundColor = Colors.Red,
                                  HeightRequest = 10,
                                  CornerRadius = 6,
                                  HorizontalOptions = LayoutOptions.Start,
                                  StrokeBlendMode = SKBlendMode.Color,
                                  StrokeColor = Colors.DarkRed,
                                  StrokeWidth = 2,
                                  UseCache = SkiaCacheType.Operations,
                                  VerticalOptions = LayoutOptions.Center,
                                  ModifyXPosEnd = 20,
                                  SideOffset = -4,
                                  XPos = 0,
                              }.Assign(out SelectedTrail)
                              //{Binding Source={x:Reference EndThumb}, Path=TranslationX}"
                              .Observe(() => EndThumb, (me, prop) =>
                              {
                                  if (prop.IsEither(nameof(BindingContext), nameof(TranslationX)))
                                  {
                                      me.XPosEnd = EndThumb.TranslationX;
                                  }
                              }),

                          //thumb
                          new SliderThumb()
                              {
                                  Tag = "EndThumb",
                                  UseCache = SkiaCacheType.Image,
                                  Children = new List<SkiaControl>()
                                  {
                                      //thumb circle
                                      new SkiaShape()
                                      {
                                          Margin = 4,
                                          BackgroundColor = Colors.Red,
                                          HorizontalOptions = LayoutOptions.Fill,
                                          StrokeColor = Colors.DarkRed,
                                          StrokeWidth = 1,
                                          Type = ShapeType.Circle,
                                          VerticalOptions = LayoutOptions.Fill,
                                          Shadows = new List<SkiaShadow>()
                                          {
                                              new SkiaShadow()
                                              {
                                                  Blur = 2,
                                                  Opacity = 0.5,
                                                  X = 1,
                                                  Y = 1,
                                                  Color = Colors.DarkRed
                                              }
                                          }
                                      },
                                      //point inside circle
                                      new SkiaShape()
                                      {
                                          LockRatio = 1,
                                          WidthRequest = 6,
                                          BackgroundColor = Colors.White,
                                          HorizontalOptions = LayoutOptions.Center,
                                          StrokeColor = Colors.DarkRed,
                                          Type = ShapeType.Circle,
                                          VerticalOptions = LayoutOptions.Center,
                                      },
                                  }
                              }.Assign(out EndThumb)
                              //HeightRequest="{Binding Source={x:Reference SliderContainer}, Path=Height}"
                              //WidthRequest="{Binding Source={x:Reference SliderContainer}, Path=Height}"
                              .Observe(() => Trail, (me, prop) =>
                              {
                                  if (prop.IsEither(nameof(BindingContext), nameof(Height)))
                                  {
                                      me.HeightRequest = Trail.Height;
                                      me.WidthRequest = Trail.Height;
                                  }
                              })
                              //TranslationX="{Binding Source={x:Reference This}, Path=EndThumbX}"
                              .Observe(this, (me, prop) =>
                              {
                                  if (prop.IsEither(nameof(BindingContext), nameof(EndThumbX)))
                                  {
                                      me.TranslationX = this.EndThumbX;
                                  }
                              }),
                      }
                  }
                  //same as {Binding Source={x:Reference This}, Path=SliderHeight}"
                  .Observe(this, (me, prop) =>
                  {
                      if (prop.IsEither(nameof(BindingContext), nameof(SliderHeight)))
                      {
                          me.HeightRequest = SliderHeight;
                      }
                  })
                  .Assign(out Trail)
          };
      }

      /// <summary>
      /// Creates a Cupertino (iOS) style slider following Apple's design guidelines
      /// </summary>
      protected virtual void CreateCupertinoStyleContent()
      {
          // Set default properties
          AvailableWidthAdjustment = -1;
          HorizontalOptions = LayoutOptions.Fill;
          MinimumWidthRequest = 64;
          SliderHeight = CupertinoThumbDiameter; // Set slider height to match thumb diameter
          Type = LayoutType.Column;
          UseCache = SkiaCacheType.ImageDoubleBuffered;

          Children = new List<SkiaControl>()
          {
              // Main track container
              new SkiaLayout
                  {
                      Tag = "Trail",
                      HeightRequest = SliderHeight,
                      HorizontalOptions = LayoutOptions.Fill,
                      Children = new List<SkiaControl>()
                      {
                          // Unselected track (gray part)
                          new SkiaShape()
                          {
                              BackgroundColor = TrackColor,
                              HeightRequest = CupertinoTrackHeight,
                              CornerRadius = CupertinoTrackHeight / 2, // Round corners (pill shape)
                              HorizontalOptions = LayoutOptions.Fill,
                              UseCache = SkiaCacheType.Operations,
                              VerticalOptions = LayoutOptions.Center
                          },

                          // Selected track (iOS blue part)
                          new SliderTrail()
                              {
                                  Tag = "SelectedTrail",
                                  BackgroundColor = TrackSelectedColor,
                                  HeightRequest = CupertinoTrackHeight,
                                  CornerRadius = CupertinoTrackHeight / 2, // Round corners
                                  HorizontalOptions = LayoutOptions.Start,
                                  UseCache = SkiaCacheType.Operations,
                                  VerticalOptions = LayoutOptions.Center,
                                  ModifyXPosEnd = CupertinoThumbDiameter / 2,
                                  SideOffset = -4,
                                  XPos = 0,
                              }.Assign(out SelectedTrail)
                              .Observe(() => EndThumb, (me, prop) =>
                              {
                                  if (prop.IsEither(nameof(BindingContext), nameof(TranslationX)))
                                  {
                                      me.XPosEnd = EndThumb.TranslationX;
                                  }
                              }),

                          // iOS thumb (circular)
                          new SliderThumb()
                              {
                                  Tag = "EndThumb",
                                  UseCache = SkiaCacheType.Image,
                                  Children = new List<SkiaControl>()
                                  {
                                      // Main thumb circle
                                      new SkiaShape()
                                      {
                                          BackgroundColor = ThumbColor,
                                          HorizontalOptions = LayoutOptions.Fill,
                                          StrokeColor = new Color(0.8f, 0.8f, 0.8f), // Light gray border
                                          StrokeWidth = CupertinoThumbBorderWidth,
                                          Type = ShapeType.Circle,
                                          VerticalOptions = LayoutOptions.Fill,
                                          Shadows = new List<SkiaShadow>()
                                          {
                                              new SkiaShadow()
                                              {
                                                  Blur = 3,
                                                  Opacity = 0.2,
                                                  X = 0,
                                                  Y = 1,
                                                  Color = Colors.Gray
                                              }
                                          }
                                      }
                                  }
                              }.Assign(out EndThumb)
                              .Observe(() => Trail, (me, prop) =>
                              {
                                  if (prop.IsEither(nameof(BindingContext), nameof(Height)))
                                  {
                                      me.HeightRequest = CupertinoThumbDiameter;
                                      me.WidthRequest = CupertinoThumbDiameter;
                                  }
                              })
                              .Observe(this, (me, prop) =>
                              {
                                  if (prop.IsEither(nameof(BindingContext), nameof(EndThumbX)))
                                  {
                                      me.TranslationX = this.EndThumbX;
                                  }
                              }),
                      }
                  }
                  .Observe(this, (me, prop) =>
                  {
                      if (prop.IsEither(nameof(BindingContext), nameof(SliderHeight)))
                      {
                          me.HeightRequest = SliderHeight;
                      }
                  })
                  .Assign(out Trail)
          };
      }

      bool UpdateCupertinoProperties(string propertyName)
      {
          // Handle Cupertino style property changes
          if (UsingControlStyle == PrebuiltControlStyle.Cupertino &&
              propertyName.IsEither(
                  nameof(CupertinoTrackHeight),
                  nameof(CupertinoThumbDiameter),
                  nameof(ThumbColor),
                  nameof(TrackColor),
                  nameof(TrackSelectedColor),
                  nameof(CupertinoThumbBorderWidth)))
          {
              UpdateCupertinoAppearance();
              return true;
          }

          return false;
      }

      /// <summary>
      /// Updates the appearance of the Cupertino slider based on the current property values
      /// </summary>
      private void UpdateCupertinoAppearance()
      {
          // Only proceed if views have been created
          if (Trail == null || SelectedTrail == null || EndThumb == null)
              return;

          // Find and update the track
          var unselectedTrack = Trail.Children.FirstOrDefault(c => c is SkiaShape && c != SelectedTrail) as SkiaShape;
          if (unselectedTrack != null)
          {
              unselectedTrack.HeightRequest = CupertinoTrackHeight;
              unselectedTrack.CornerRadius = CupertinoTrackHeight / 2;
              unselectedTrack.BackgroundColor = TrackColor;
          }

          // Update the selected trail
          SelectedTrail.HeightRequest = CupertinoTrackHeight;
          SelectedTrail.CornerRadius = CupertinoTrackHeight / 2;
          SelectedTrail.BackgroundColor = TrackSelectedColor;
          SelectedTrail.ModifyXPosEnd = CupertinoThumbDiameter / 2;

          // Update the thumb size and border
          EndThumb.HeightRequest = CupertinoThumbDiameter;
          EndThumb.WidthRequest = CupertinoThumbDiameter;

          var thumbShape = EndThumb.Children.FirstOrDefault(c => c is SkiaShape) as SkiaShape;
          if (thumbShape != null)
          {
              thumbShape.StrokeWidth = CupertinoThumbBorderWidth;
              thumbShape.BackgroundColor = ThumbColor;
          }

          // Update the overall slider height to match the thumb
          SliderHeight = CupertinoThumbDiameter;
          Trail.HeightRequest = SliderHeight;

          Invalidate();
      }

      public static readonly BindableProperty ThumbColorProperty =
          BindableProperty.Create(nameof(ThumbColor), typeof(Color), typeof(SkiaSlider),
              new Color(1f, 1f, 1f));

      public Color ThumbColor
      {
          get { return (Color)GetValue(ThumbColorProperty); }
          set { SetValue(ThumbColorProperty, value); }
      }

      #region CUPERTINO STYLE PROPERTIES

      public static readonly BindableProperty CupertinoTrackHeightProperty =
          BindableProperty.Create(nameof(CupertinoTrackHeight), typeof(double), typeof(SkiaSlider), 2.0);

      /// <summary>
      /// The height of the iOS slider track in points (default is 2pt per iOS guidelines)
      /// </summary>
      public double CupertinoTrackHeight
      {
          get { return (double)GetValue(CupertinoTrackHeightProperty); }
          set { SetValue(CupertinoTrackHeightProperty, value); }
      }

      public static readonly BindableProperty CupertinoThumbDiameterProperty =
          BindableProperty.Create(nameof(CupertinoThumbDiameter), typeof(double), typeof(SkiaSlider), 28.0);

      /// <summary>
      /// The diameter of the iOS slider thumb in points (default is 28pt per iOS guidelines)
      /// </summary>
      public double CupertinoThumbDiameter
      {
          get { return (double)GetValue(CupertinoThumbDiameterProperty); }
          set { SetValue(CupertinoThumbDiameterProperty, value); }
      }

      /// <summary>
      /// The color of the selected track. Maps to base ProgressColor property for backward compatibility.
      /// </summary>
      public Color TrackSelectedColor
      {
          get { return ProgressColor; }
          set { ProgressColor = value; }
      }

      // TrackColor property is now inherited from SkiaRangeBase

      public static readonly BindableProperty CupertinoThumbBorderWidthProperty =
          BindableProperty.Create(nameof(CupertinoThumbBorderWidth), typeof(double), typeof(SkiaSlider), 0.5);

      /// <summary>
      /// The width of the border around the iOS slider thumb (default 0.5pt)
      /// </summary>
      public double CupertinoThumbBorderWidth
      {
          get { return (double)GetValue(CupertinoThumbBorderWidthProperty); }
          set { SetValue(CupertinoThumbBorderWidthProperty, value); }
      }

      #endregion

      #endregion

      #region GESTURES

      protected bool IsUserPanning { get; set; }

      /// <summary>
      /// enlarge hotspot by pts
      /// </summary>
      public double moreHotspotSize = 10.0;

      protected double lastTouchX;

      /// <summary>
      /// track touched area type
      /// </summary>
      protected RangeZone touchArea;

      protected Vector2 _panningStartOffsetPts;

      protected override void OnLayoutChanged()
      {
          base.OnLayoutChanged();

          FindViews();
      }


      public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
      {
          //Super.Log($"[Touch] SLIDER got {args.Type}");

          bool passedToChildren = false;

          ISkiaGestureListener PassToChildren()
          {
              passedToChildren = true;

              return base.ProcessGestures(args, apply);
          }

          var consumedDefault = BlockGesturesBelow ? this : null;

          ISkiaGestureListener consumed = null;

          //pass Released always to children first
          if (args.Type == TouchActionResult.Up
              || !IsUserPanning || !RespondsToGestures)
          {
              if (args.Event.NumberOfTouches < 2)
                  TouchBusy = false;

              consumed = PassToChildren();
              if (consumed == this)
              {
                  //BlockGesturesBelow fired
                  consumed = null;
              }
              if (consumed != null && args.Type != TouchActionResult.Up)
              {
                  return consumed;
              }
          }

          if (!RespondsToGestures)
          {
              if (consumed == null) return consumedDefault;

              return consumed;
          }


          void ResetPan()
          {
              IsUserPanning = false;
              _panningStartOffsetPts = new(args.Event.Location.X, args.Event.Location.Y);
          }

          switch (args.Type)
          {
              case TouchActionResult.Down:

                  if (args.Event.NumberOfTouches < 2)
                  {
                      ResetPan();
                  }

                  var thisOffset = TranslateInputCoords(apply.ChildOffset);

                  var x = args.Event.Location.X + thisOffset.X;
                  var y = args.Event.Location.Y + thisOffset.Y;

                  var relativeX = x - LastDrawnAt.Left; //inside this control
                  var relativeY = y - LastDrawnAt.Top; //inside this control

                  var locationX = relativeX / RenderingScale; //from pix to pts
                  var locationY = relativeY / RenderingScale; //from pix to pts

                  if (EnableRange && locationX >= StartThumbX - moreHotspotSize &&
                      locationX <= StartThumbX + SliderHeight + moreHotspotSize)
                  {
                      touchArea = RangeZone.Start;
                  }
                  else if (locationX >= EndThumbX - moreHotspotSize &&
                           locationX <= EndThumbX + SliderHeight + moreHotspotSize)
                  {
                      touchArea = RangeZone.End;
                  }
                  else
                  {
                      touchArea = RangeZone.Unknown;
                  }

                  bool onTrail = true;
                  if (Trail != null)
                  {
                      var trailOffset = Trail.TranslateInputCoords(thisOffset);
                      onTrail = Trail.HitIsInside(args.Event.Location.X + trailOffset.X,
                          args.Event.Location.Y + trailOffset.Y);
                  }

                  if (onTrail || touchArea == RangeZone.Start || touchArea == RangeZone.End)
                      IsPressed = true;

                  if (touchArea == RangeZone.Unknown && ClickOnTrailEnabled)
                  {
                      //clicked on trail maybe

                      if (onTrail)
                      {
                          var halfThumb = SliderHeight / 2f;

                          if (EnableRange)
                          {
                              var half = (Width + AvailableWidthAdjustment) / 2.0;

                              if (locationX > half)
                              {
                                  MoveEndThumbHere(locationX - halfThumb);
                              }
                              else if (locationX <= half)
                              {
                                  MoveStartThumbHere(locationX - halfThumb);
                              }
                          }
                          else
                          {
                              MoveEndThumbHere(locationX - halfThumb);
                          }
                      }
                  }

                  if (touchArea == RangeZone.Start)
                  {
                      consumed = this;
                      lastTouchX = StartThumbX;
                  }
                  else if (touchArea == RangeZone.End)
                  {
                      consumed = this;
                      lastTouchX = EndThumbX;
                  }

                  break;

              case TouchActionResult.Panning when args.Event.NumberOfTouches == 1:

                  //filter correct direction so we could scroll below the control in another direction:
                  if (!IsUserPanning && IgnoreWrongDirection)
                  {
                      //first panning gesture..
                      var panDirection = GetDirectionType(_panningStartOffsetPts,
                          new Vector2(_panningStartOffsetPts.X + args.Event.Distance.Total.X,
                              _panningStartOffsetPts.Y + args.Event.Distance.Total.Y), 0.8f);

                      if (Orientation == OrientationType.Vertical && panDirection != DirectionType.Vertical)
                      {
                          return consumedDefault;
                      }

                      if (Orientation == OrientationType.Horizontal && panDirection != DirectionType.Horizontal)
                      {
                          return consumedDefault;
                      }
                  }


                  IsUserPanning = true;


                  //synch this
                  if (touchArea == RangeZone.Start)
                      lastTouchX = StartThumbX;
                  else if (touchArea == RangeZone.End)
                      lastTouchX = EndThumbX;


                  if (touchArea != RangeZone.Unknown)
                  {
                      TouchBusy = true;

                      if (touchArea == RangeZone.Start)
                      {
                          var maybe = lastTouchX + args.Event.Distance.Delta.X / RenderingScale; //args.TotalDistance.X;
                          SetStartOffsetClamped(maybe);
                      }
                      else if (touchArea == RangeZone.End)
                      {
                          var maybe = lastTouchX + args.Event.Distance.Delta.X / RenderingScale;
                          SetEndOffsetClamped(maybe);

                          //Super.Log($"[Touch] SLIDER zone END {maybe}");
                      }

                      RecalculateValues();
                  }

                  consumed = this;
                  break;

              case TouchActionResult.Up when args.Event.NumberOfTouches < 2:
                  IsUserPanning = false;
                  IsPressed = false;
                  break;
          }

          if (consumed != null || IsUserPanning) // || args.Event.NumberOfTouches > 1)
          {
              if (consumed == null && args.Type != TouchActionResult.Up)
              {
                  return this;
              }

              if (consumed == null) return consumedDefault;
              return consumed;
          }

          if (!passedToChildren)
          {
              return PassToChildren();
          }

          return consumedDefault;
      }

      private bool TouchBusy;

      #endregion

      #region PROPERTIES

      public static readonly BindableProperty ClickOnTrailEnabledProperty =
          BindableProperty.Create(nameof(ClickOnTrailEnabled), typeof(bool),
              typeof(SkiaSlider), true);

      public bool ClickOnTrailEnabled
      {
          get { return (bool)GetValue(ClickOnTrailEnabledProperty); }
          set { SetValue(ClickOnTrailEnabledProperty, value); }
      }

      public static readonly BindableProperty EnableRangeProperty =
          BindableProperty.Create(nameof(EnableRange), typeof(bool), typeof(SkiaSlider), false);

      public bool EnableRange
      {
          get { return (bool)GetValue(EnableRangeProperty); }
          set { SetValue(EnableRangeProperty, value); }
      }

      public static readonly BindableProperty RespondsToGesturesProperty = BindableProperty.Create(
          nameof(RespondsToGestures),
          typeof(bool),
          typeof(SkiaSlider),
          true);

      /// <summary>
      /// Can be open/closed by gestures along with code-behind, default is true
      /// </summary>
      public bool RespondsToGestures
      {
          get { return (bool)GetValue(RespondsToGesturesProperty); }
          set { SetValue(RespondsToGesturesProperty, value); }
      }

      public static readonly BindableProperty SliderHeightProperty =
          BindableProperty.Create(nameof(SliderHeight), typeof(double), typeof(SkiaSlider), 22.0); //, BindingMode.TwoWay

      public double SliderHeight
      {
          get { return (double)GetValue(SliderHeightProperty); }
          set { SetValue(SliderHeightProperty, value); }
      }

      // Min and Max properties are now inherited from SkiaRangeBase

      public static readonly BindableProperty StartProperty =
          BindableProperty.Create(nameof(Start), typeof(double), typeof(SkiaSlider), 0.0, BindingMode.TwoWay,
              propertyChanged: OnStartPropertyChanged,
              coerceValue: CoerceValue);

      /// <summary>
      /// Enabled for ranged
      /// </summary>
      public double Start
      {
          get { return (double)GetValue(StartProperty); }
          set { SetValue(StartProperty, value); }
      }

      private static void OnStartPropertyChanged(BindableObject bindable, object oldValue, object newValue)
      {
          if (bindable is SkiaSlider slider && newValue is double newStartValue)
          {
              slider.OnStartChanged();
          }
      }

      private static object CoerceValue(BindableObject bindable, object value)
      {
          var newValue = (double)value;

          if (bindable is SkiaSlider slider)
          {
              var adjusted = slider.AdjustToStepValue(newValue, slider.Min, slider.Step);
              if (slider.Width >= 0)
              {
                  return Math.Clamp(adjusted, slider.Min, slider.Max);
              }

              return adjusted;
          }

          return value;
      }

      public static readonly BindableProperty EndProperty = BindableProperty.Create(
          nameof(End),
          typeof(double),
          typeof(SkiaSlider),
          100.0,
          BindingMode.TwoWay,
          propertyChanged: OnEndPropertyChanged,
          coerceValue: CoerceValue);

      /// <summary>
      /// For non-ranged this is your main value
      /// </summary>
      public double End
      {
          get { return (double)GetValue(EndProperty); }
          set { SetValue(EndProperty, value); }
      }

      private static void OnEndPropertyChanged(BindableObject bindable, object oldValue, object newValue)
      {
          if (bindable is SkiaSlider slider && newValue is double newEndValue)
          {
              slider.Value = newEndValue;
          }
      }

      protected override void OnPropertyChanging(string propertyName = null)
      {
          base.OnPropertyChanging(propertyName);
      }

      public static readonly BindableProperty StartThumbXProperty =
          BindableProperty.Create(nameof(StartThumbX), typeof(double), typeof(SkiaSlider), 0.0);

      public double StartThumbX
      {
          get { return (double)GetValue(StartThumbXProperty); }
          set { SetValue(StartThumbXProperty, value); }
      }

      public static readonly BindableProperty EndThumbXProperty =
          BindableProperty.Create(nameof(EndThumbX), typeof(double), typeof(SkiaSlider), 0.0);

      public double EndThumbX
      {
          get { return (double)GetValue(EndThumbXProperty); }
          set { SetValue(EndThumbXProperty, value); }
      }

      // Step property is now inherited from SkiaRangeBase

      public static readonly BindableProperty RangeMinProperty =
          BindableProperty.Create(nameof(RangeMin), typeof(double), typeof(SkiaSlider), 0.0); //, BindingMode.TwoWay

      public double RangeMin
      {
          get { return (double)GetValue(RangeMinProperty); }
          set { SetValue(RangeMinProperty, value); }
      }

      public static readonly BindableProperty ValueStringFormatProperty =
          BindableProperty.Create(nameof(ValueStringFormat), typeof(string), typeof(SkiaSlider),
              "### ### ##0.##"); //, BindingMode.TwoWay

      public string ValueStringFormat
      {
          get { return (string)GetValue(ValueStringFormatProperty); }
          set { SetValue(ValueStringFormatProperty, value); }
      }

      public static readonly BindableProperty MinMaxStringFormatProperty =
          BindableProperty.Create(nameof(MinMaxStringFormat), typeof(string), typeof(SkiaSlider),
              "### ### ##0.##"); //, BindingMode.TwoWay

      public string MinMaxStringFormat
      {
          get { return (string)GetValue(MinMaxStringFormatProperty); }
          set { SetValue(MinMaxStringFormatProperty, value); }
      }

      public static readonly BindableProperty AvailableWidthAdjustmentProperty =
          BindableProperty.Create(nameof(AvailableWidthAdjustment), typeof(double), typeof(SkiaSlider), 0.0);

      /// <summary>
      /// Padding for the Thumb to go, to be able to leave some space around for shadows etc.
      /// </summary>
      public double AvailableWidthAdjustment
      {
          get { return (double)GetValue(AvailableWidthAdjustmentProperty); }
          set { SetValue(AvailableWidthAdjustmentProperty, value); }
      }

      public static readonly BindableProperty OrientationProperty = BindableProperty.Create(nameof(Orientation),
          typeof(OrientationType), typeof(SkiaSlider),
          OrientationType.Horizontal,
          propertyChanged: NeedDraw);

      /// <summary>
      /// <summary>Gets or sets the orientation. This is a bindable property.</summary>
      /// </summary>
      public OrientationType Orientation
      {
          get { return (OrientationType)GetValue(OrientationProperty); }
          set { SetValue(OrientationProperty, value); }
      }

      public static readonly BindableProperty IgnoreWrongDirectionProperty = BindableProperty.Create(
          nameof(IgnoreWrongDirection),
          typeof(bool),
          typeof(SkiaSlider),
          true);

      // Invert property is now inherited from SkiaRangeBase

      /// <summary>
      /// Will ignore gestures of the wrong direction, like if this Orientation is Horizontal will ignore gestures with vertical direction velocity
      /// </summary>
      public bool IgnoreWrongDirection
      {
          get { return (bool)GetValue(IgnoreWrongDirectionProperty); }
          set { SetValue(IgnoreWrongDirectionProperty, value); }
      }

      #endregion

      #region ENGINE

      protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
      {
          base.OnPropertyChanged(propertyName);

          if (UpdateCupertinoProperties(propertyName))
              return;

          if (propertyName.IsEither(nameof(Min),
                  nameof(MinMaxStringFormat), nameof(Max)))
          {
              var mask = "{0:" + MinMaxStringFormat + "}";
              MinDesc = string.Format(mask, Min).Trim();
              MaxDesc = string.Format(mask, Max).Trim();
          }

          if (lockInternal)
              return;

          if (propertyName == nameof(Value))
          {
              End = Value;
              return;
          }

          if (propertyName.IsEither(nameof(Width),
                  nameof(Min), nameof(Max), nameof(StartThumbX),
                  nameof(EndThumbX), nameof(Step), nameof(Start),
                  nameof(End), nameof(AvailableWidthAdjustment)))
          {
              if (Width > -1)
              {
                  if (EnableRange)
                  {
                      StepValue = (Max - Min) / (Width + AvailableWidthAdjustment - SliderHeight);
                  }
                  else
                  {
                      StepValue = (Max - Min) / (Width + AvailableWidthAdjustment * 2 - SliderHeight);
                  }

                  //if (!TouchBusy)
                  //{
                  //    var mask = "{0:" + ValueStringFormat + "}";
                  //    if (EnableRange)
                  //    {
                  //        SetStartOffsetClamped((Start - Min) / StepValue - AvailableWidthAdjustment);
                  //        StartDesc = string.Format(mask, Start).Trim();
                  //    }
                  //    SetEndOffsetClamped((End - Min) / StepValue);
                  //    EndDesc = string.Format(mask, End).Trim();
                  //}
                  if (!TouchBusy)
                  {
                      var mask = "{0:" + ValueStringFormat + "}";
                      if (EnableRange)
                      {
                          StartThumbX = PositionFromValue(Start) + AvailableWidthAdjustment;
                          StartDesc = string.Format(mask, Start).Trim();
                      }

                      EndThumbX = PositionFromValue(End) - AvailableWidthAdjustment;
                      EndDesc = string.Format(mask, End).Trim();
                  }
              }
          }
      }

      public virtual void OnEndChanged()
      {
          EndChanged?.Invoke(this, End);
      }

      public virtual void OnStartChanged()
      {
          StartChanged?.Invoke(this, Start);
      }

      public override void OnDisposing()
      {
          base.OnDisposing();

          StartChanged = null;
          EndChanged = null;
      }

      public event EventHandler<double> StartChanged;
      public event EventHandler<double> EndChanged;

      // AdjustToStepValue method is now inherited from SkiaRangeBase

      protected virtual void RecalculateValues()
      {
          lockInternal = true;

          var mask = "{0:" + ValueStringFormat + "}";
          ConvertOffsetsToValues();
          if (EnableRange)
          {
              StartDesc = string.Format(mask, Start).Trim();
          }

          EndDesc = string.Format(mask, End).Trim();

          lockInternal = false;
      }

      protected void MoveEndThumbHere(double x)
      {
          if (!ClickOnTrailEnabled)
              return;

          touchArea = RangeZone.End;

          lockInternal = true;
          SetEndOffsetClamped(x);
          lockInternal = false;

          RecalculateValues();
      }

      protected void MoveStartThumbHere(double x)
      {
          if (!ClickOnTrailEnabled)
              return;

          touchArea = RangeZone.Start;

          lockInternal = true;

          SetStartOffsetClamped(x);

          lockInternal = false;

          RecalculateValues();
      }

      //private volatile bool lockInternal;

      private string _StartDesc;

      [EditorBrowsable(EditorBrowsableState.Never)]
      public string StartDesc
      {
          get { return _StartDesc; }
          set
          {
              if (_StartDesc != value)
              {
                  _StartDesc = value;
                  OnPropertyChanged();
              }
          }
      }

      private string _EndDesc;

      [EditorBrowsable(EditorBrowsableState.Never)]
      public string EndDesc
      {
          get { return _EndDesc; }
          set
          {
              if (_EndDesc != value)
              {
                  _EndDesc = value;
                  OnPropertyChanged();
              }
          }
      }

      private string _MinDesc;

      [EditorBrowsable(EditorBrowsableState.Never)]
      public string MinDesc
      {
          get { return _MinDesc; }
          set
          {
              if (_MinDesc != value)
              {
                  _MinDesc = value;
                  OnPropertyChanged();
              }
          }
      }

      private string _MaxDesc;

      [EditorBrowsable(EditorBrowsableState.Never)]
      public string MaxDesc
      {
          get { return _MaxDesc; }
          set
          {
              if (_MaxDesc != value)
              {
                  _MaxDesc = value;
                  OnPropertyChanged();
              }
          }
      }

      private double _StepValue;

      [EditorBrowsable(EditorBrowsableState.Never)]
      public double StepValue
      {
          get { return _StepValue; }
          set
          {
              if (_StepValue != value)
              {
                  _StepValue = value;
                  OnPropertyChanged();
              }
          }
      }

      private bool _IsPressed;

      public bool IsPressed
      {
          get { return _IsPressed; }
          set
          {
              if (_IsPressed != value)
              {
                  _IsPressed = value;
                  OnPropertyChanged();
              }
          }
      }

      #endregion

      void SetStartOffsetClamped(double maybe)
      {
          double minPosition = GetStartThumbMinPosition();
          double maxPosition = GetStartThumbMaxPosition();

          if (maybe < minPosition)
          {
              StartThumbX = minPosition;
          }
          else if (maybe > maxPosition)
          {
              StartThumbX = maxPosition;
          }
          else
          {
              StartThumbX = maybe;
          }
      }

      void SetEndOffsetClamped(double maybe)
      {
          double minPosition = GetEndThumbMinPosition();
          double maxPosition = GetEndThumbMaxPosition();

          if (maybe < minPosition)
          {
              EndThumbX = minPosition;
          }
          else if (maybe > maxPosition)
          {
              EndThumbX = maxPosition;
          }
          else
          {
              EndThumbX = maybe;
          }
      }

      protected virtual void ConvertOffsetsToValues()
      {
          if (EnableRange)
          {
              Start = AdjustToStepValue(ValueFromPosition(this.StartThumbX + AvailableWidthAdjustment), Min, Step);
              End = AdjustToStepValue(ValueFromPosition(this.EndThumbX - AvailableWidthAdjustment), Min, Step);
          }
          else
          {
              End = AdjustToStepValue(ValueFromPosition(this.EndThumbX + AvailableWidthAdjustment), Min, Step);
          }
      }

      #region Invert

      private double ValueFromPosition(double position)
      {
          double totalLength = Width + AvailableWidthAdjustment * 2 - SliderHeight;
          return base.ValueFromPosition(position, totalLength);
      }

      private double PositionFromValue(double value)
      {
          double totalLength = Width + AvailableWidthAdjustment - SliderHeight;
          return base.PositionFromValue(value, totalLength);
      }

      private double GetStartThumbMinPosition()
      {
          if (Invert)
          {
              if (EnableRange)
              {
                  return EndThumbX + RangeMin / StepValue;
              }
              else
              {
                  return -AvailableWidthAdjustment;
              }
          }
          else
          {
              return -AvailableWidthAdjustment;
          }
      }

      private double GetStartThumbMaxPosition()
      {
          if (Invert)
          {
              return (Width + AvailableWidthAdjustment) - SliderHeight;
          }
          else
          {
              if (EnableRange)
              {
                  return EndThumbX - RangeMin / StepValue;
              }
              else
              {
                  return (Width + AvailableWidthAdjustment) - SliderHeight;
              }
          }
      }

      private double GetEndThumbMinPosition()
      {
          if (Invert)
          {
              return -AvailableWidthAdjustment;
          }
          else
          {
              if (EnableRange)
              {
                  return StartThumbX + RangeMin / StepValue;
              }
              else
              {
                  return -AvailableWidthAdjustment;
              }
          }
      }

      private double GetEndThumbMaxPosition()
      {
          if (Invert)
          {
              if (EnableRange)
              {
                  return StartThumbX - RangeMin / StepValue;
              }
              else
              {
                  return (Width + AvailableWidthAdjustment) - SliderHeight;
              }
          }
          else
          {
              return (Width + AvailableWidthAdjustment) - SliderHeight;
          }
      }

      #endregion
  }

  */
