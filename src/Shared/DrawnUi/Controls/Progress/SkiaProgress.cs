namespace DrawnUi.Draw;

/// <summary>
/// Linear progress bar control with platform-specific styling.
/// Shows progress from Min to Value within the Min-Max range.
/// </summary>
public class SkiaProgress : SkiaRangeBase
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

            UpdateVisualState();
        }
    }

    protected virtual void CreateDefaultStyleContent()
    {
        SetDefaultContentSize(200, 8);

        SetStyleDefault(HorizontalOptionsProperty, LayoutOptions.Fill);
        SetStyleDefault(MinimumWidthRequestProperty, 100d);
        Type = LayoutType.Column;
        SetStyleDefault(UseCacheProperty, SkiaCacheType.ImageDoubleBuffered);

        var trackHeight = ResolvedTrackHeight;
        var trackColor = ResolvedTrackColor;
        var progressColor = ResolvedProgressColor;

        Children = new List<SkiaControl>()
        {
            // Main track container
            new SkiaLayout
            {
                Tag = "Track",
                HeightRequest = trackHeight,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Center,
                Children = new List<SkiaControl>()
                {
                    // Background track
                    new SkiaShape()
                    {
                        Tag = "BackgroundTrack",
                        BackgroundColor = trackColor,
                        HeightRequest = trackHeight,
                        CornerRadius = trackHeight / 2,
                        HorizontalOptions = LayoutOptions.Fill,
                        UseCache = SkiaCacheType.Operations,
                        VerticalOptions = LayoutOptions.Center
                    },

                    // Progress trail
                    new ProgressTrail()
                    {
                        Tag = "ProgressTrail",
                        BackgroundColor = progressColor,
                        HeightRequest = trackHeight,
                        CornerRadius = trackHeight / 2,
                        HorizontalOptions = LayoutOptions.Start,
                        UseCache = SkiaCacheType.Operations,
                        VerticalOptions = LayoutOptions.Center,
                        XPos = 0,
                    }.Assign(out var progressTrail)
                }
            }.Assign(out var track)
        };

        Track = track;
        ProgressTrail = progressTrail;
    }

    /// <summary>
    /// Track color to render: an explicitly set <see cref="SkiaRangeBase.TrackColor"/> wins,
    /// otherwise the current style's palette.
    /// </summary>
    protected Color ResolvedTrackColor => IsSet(TrackColorProperty)
        ? TrackColor
        : UsingControlStyle switch
        {
            PrebuiltControlStyle.Cupertino => Color.FromRgba(229, 229, 234, 255), // iOS system gray 5
            PrebuiltControlStyle.Material => Color.FromRgba(232, 234, 237, 255), // Material surface variant
            PrebuiltControlStyle.Material3 => Color.FromRgba(230, 224, 233, 255), // M3 surface container highest
            PrebuiltControlStyle.Windows => Color.FromRgba(243, 242, 241, 255), // Fluent neutral background
            _ => Color.FromRgba(215, 219, 224, 255) // DrawnUI flat default neutral #D7DBE0
        };

    /// <summary>
    /// Progress color to render: an explicitly set <see cref="SkiaRangeBase.ProgressColor"/> wins,
    /// otherwise the current style's palette.
    /// </summary>
    protected Color ResolvedProgressColor => IsSet(ProgressColorProperty)
        ? ProgressColor
        : UsingControlStyle switch
        {
            PrebuiltControlStyle.Cupertino => Color.FromRgba(0, 122, 255, 255), // iOS system blue
            PrebuiltControlStyle.Material => Color.FromRgba(33, 150, 243, 255), // Material blue (M2 family)
            PrebuiltControlStyle.Material3 => Color.FromRgba(103, 80, 164, 255), // M3 primary
            PrebuiltControlStyle.Windows => Color.FromRgba(0, 120, 212, 255), // Fluent accent
            _ => Color.FromRgba(220, 20, 60, 255) // DrawnUI flat default accent Crimson #DC143C
        };

    /// <summary>
    /// Track height to render: an explicitly set <see cref="SkiaRangeBase.TrackHeight"/> wins,
    /// otherwise the current style's standard height.
    /// </summary>
    protected double ResolvedTrackHeight => IsSet(TrackHeightProperty)
        ? TrackHeight
        : UsingControlStyle switch
        {
            PrebuiltControlStyle.Cupertino => 4.0,
            PrebuiltControlStyle.Material => 4.0,
            PrebuiltControlStyle.Material3 => 4.0,
            PrebuiltControlStyle.Windows => 6.0,
            _ => 8.0 // DrawnUI flat default
        };

    protected virtual void CreateCupertinoStyleContent()
    {
        // iOS progress bar styling - follows Apple HIG specifications
        SetDefaultContentSize(200, 4);

        SetStyleDefault(HorizontalOptionsProperty, LayoutOptions.Fill);
        SetStyleDefault(MinimumWidthRequestProperty, 100d);
        Type = LayoutType.Column;
        SetStyleDefault(UseCacheProperty, SkiaCacheType.ImageDoubleBuffered);

        var iosTrackHeight = ResolvedTrackHeight;
        var iosTrackColor = ResolvedTrackColor;
        var iosProgressColor = ResolvedProgressColor;

        Children = new List<SkiaControl>()
        {
            new SkiaLayout
            {
                Tag = "Track",
                HeightRequest = iosTrackHeight,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Center,
                Children = new List<SkiaControl>()
                {
                    new SkiaShape()
                    {
                        Tag = "BackgroundTrack",
                        BackgroundColor = iosTrackColor,
                        HeightRequest = iosTrackHeight,
                        CornerRadius = iosTrackHeight / 2,
                        HorizontalOptions = LayoutOptions.Fill,
                        UseCache = SkiaCacheType.Operations,
                        VerticalOptions = LayoutOptions.Center
                    },

                    new ProgressTrail()
                    {
                        Tag = "ProgressTrail",
                        BackgroundColor = iosProgressColor,
                        HeightRequest = iosTrackHeight,
                        CornerRadius = iosTrackHeight / 2,
                        HorizontalOptions = LayoutOptions.Start,
                        UseCache = SkiaCacheType.Operations,
                        VerticalOptions = LayoutOptions.Center,
                        XPos = 0,
                    }.Assign(out var progressTrail)
                }
            }.Assign(out var track)
        };

        Track = track;
        ProgressTrail = progressTrail;
    }

    protected virtual void CreateMaterialStyleContent()
    {
        // Material Design progress bar styling - follows Material Design 3 specifications
        SetDefaultContentSize(200, 4);

        SetStyleDefault(HorizontalOptionsProperty, LayoutOptions.Fill);
        SetStyleDefault(MinimumWidthRequestProperty, 100d);
        Type = LayoutType.Column;
        SetStyleDefault(UseCacheProperty, SkiaCacheType.ImageDoubleBuffered);

        var materialTrackHeight = ResolvedTrackHeight;
        var materialTrackColor = ResolvedTrackColor;
        var materialProgressColor = ResolvedProgressColor;

        Children = new List<SkiaControl>()
        {
            new SkiaLayout
            {
                Tag = "Track",
                HeightRequest = materialTrackHeight,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Center,
                Children = new List<SkiaControl>()
                {
                    new SkiaShape()
                    {
                        Tag = "BackgroundTrack",
                        BackgroundColor = materialTrackColor,
                        HeightRequest = materialTrackHeight,
                        CornerRadius = 2, // Material Design 3 uses slight rounding
                        HorizontalOptions = LayoutOptions.Fill,
                        UseCache = SkiaCacheType.Operations,
                        VerticalOptions = LayoutOptions.Center
                    },

                    new ProgressTrail()
                    {
                        Tag = "ProgressTrail",
                        BackgroundColor = materialProgressColor,
                        HeightRequest = materialTrackHeight,
                        CornerRadius = 2, // Material Design 3 uses slight rounding
                        HorizontalOptions = LayoutOptions.Start,
                        UseCache = SkiaCacheType.Operations,
                        VerticalOptions = LayoutOptions.Center,
                        XPos = 0,
                    }.Assign(out var progressTrail)
                }
            }.Assign(out var track)
        };

        Track = track;
        ProgressTrail = progressTrail;
    }

    /// <summary>
    /// Creates a Material 3 (Material You) progress bar: rounded active indicator,
    /// a gap before the remaining track and a small stop-indicator dot at the track end.
    /// </summary>
    protected virtual void CreateMaterial3StyleContent()
    {
        SetDefaultContentSize(200, 4);

        SetStyleDefault(HorizontalOptionsProperty, LayoutOptions.Fill);
        SetStyleDefault(MinimumWidthRequestProperty, 100d);
        Type = LayoutType.Column;
        SetStyleDefault(UseCacheProperty, SkiaCacheType.ImageDoubleBuffered);

        var trackHeight = ResolvedTrackHeight;
        var trackColor = ResolvedTrackColor;
        var progressColor = ResolvedProgressColor;

        Children = new List<SkiaControl>()
        {
            new SkiaLayout
            {
                Tag = "Track",
                HeightRequest = trackHeight,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Center,
                Children = new List<SkiaControl>()
                {
                    new SkiaShape()
                    {
                        Tag = "BackgroundTrack",
                        BackgroundColor = trackColor,
                        HeightRequest = trackHeight,
                        CornerRadius = 2,
                        HorizontalOptions = LayoutOptions.Fill,
                        UseCache = SkiaCacheType.Operations,
                        VerticalOptions = LayoutOptions.Center
                    },

                    new ProgressTrail()
                    {
                        Tag = "ProgressTrail",
                        BackgroundColor = progressColor,
                        HeightRequest = trackHeight,
                        CornerRadius = 2,
                        HorizontalOptions = LayoutOptions.Start,
                        UseCache = SkiaCacheType.Operations,
                        VerticalOptions = LayoutOptions.Center,
                        XPos = 0,
                    }.Assign(out var progressTrail),

                    // M3 signature stop indicator: small dot at the track end
                    new SkiaShape()
                    {
                        Tag = "StopDot",
                        Type = ShapeType.Circle,
                        WidthRequest = 4,
                        LockRatio = 1,
                        BackgroundColor = progressColor,
                        HorizontalOptions = LayoutOptions.End,
                        VerticalOptions = LayoutOptions.Center,
                        UseCache = SkiaCacheType.Operations,
                    }
                }
            }.Assign(out var track)
        };

        Track = track;
        ProgressTrail = progressTrail;
    }

    protected virtual void CreateWindowsStyleContent()
    {
        // Windows Fluent Design progress bar styling
        SetDefaultContentSize(200, 6);

        SetStyleDefault(HorizontalOptionsProperty, LayoutOptions.Fill);
        SetStyleDefault(MinimumWidthRequestProperty, 100d);
        Type = LayoutType.Column;
        SetStyleDefault(UseCacheProperty, SkiaCacheType.ImageDoubleBuffered);

        var windowsTrackHeight = ResolvedTrackHeight;
        var windowsTrackColor = ResolvedTrackColor;
        var windowsProgressColor = ResolvedProgressColor;

        Children = new List<SkiaControl>()
        {
            new SkiaLayout
            {
                Tag = "Track",
                HeightRequest = windowsTrackHeight,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Center,
                Children = new List<SkiaControl>()
                {
                    new SkiaShape()
                    {
                        Tag = "BackgroundTrack",
                        BackgroundColor = windowsTrackColor,
                        HeightRequest = windowsTrackHeight,
                        CornerRadius = 3, // Fluent Design uses moderate rounding
                        HorizontalOptions = LayoutOptions.Fill,
                        UseCache = SkiaCacheType.Operations,
                        VerticalOptions = LayoutOptions.Center
                    },

                    new ProgressTrail()
                    {
                        Tag = "ProgressTrail",
                        BackgroundColor = windowsProgressColor,
                        HeightRequest = windowsTrackHeight,
                        CornerRadius = 3, // Fluent Design uses moderate rounding
                        HorizontalOptions = LayoutOptions.Start,
                        UseCache = SkiaCacheType.Operations,
                        VerticalOptions = LayoutOptions.Center,
                        XPos = 0,
                    }.Assign(out var progressTrail)
                }
            }.Assign(out var track)
        };

        Track = track;
        ProgressTrail = progressTrail;
    }

    #endregion

    #region IMPLEMENTATION

    protected override void FindViews()
    {
        if (Track == null)
            Track = FindView<SkiaLayout>("Track");

        if (ProgressTrail == null)
            ProgressTrail = FindView<ProgressTrail>("ProgressTrail");
    }

    /// <summary>
    /// Gap in points between the active indicator and the remaining track in the Material 3 style.
    /// </summary>
    protected const double MaterialProgressGap = 4.0;

    protected override void UpdateVisualState()
    {
        double progressWidth = -1;

        if (ProgressTrail is ProgressTrail trail && Track != null)
        {
            // Calculate progress width based on current value
            var totalWidth = Track.Width;
            if (totalWidth > 0)
            {
                var progressRatio = Math.Clamp((Value - Min) / (Max - Min), 0.0, 1.0);
                progressWidth = totalWidth * progressRatio;

                trail.XPosEnd = progressWidth;
                trail.BackgroundColor = ResolvedProgressColor;
            }
        }

        // Update track colors
        var backgroundTrack = FindView<SkiaShape>("BackgroundTrack");
        if (backgroundTrack != null)
        {
            backgroundTrack.BackgroundColor = ResolvedTrackColor;
            backgroundTrack.HeightRequest = ResolvedTrackHeight;

            if (UsingControlStyle == PrebuiltControlStyle.Material3 && progressWidth >= 0)
            {
                // M3 signature: remaining track starts after a gap from the active indicator
                backgroundTrack.Margin = new Thickness(progressWidth + MaterialProgressGap, 0, 0, 0);
            }
        }

        if (FindView<SkiaShape>("StopDot") is SkiaShape dot)
        {
            dot.BackgroundColor = ResolvedProgressColor;
        }

        if (ProgressTrail != null)
        {
            ProgressTrail.HeightRequest = ResolvedTrackHeight;
        }
    }

    #endregion
}
