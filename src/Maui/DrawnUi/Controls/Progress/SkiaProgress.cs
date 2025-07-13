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

        HorizontalOptions = LayoutOptions.Fill;
        MinimumWidthRequest = 100;
        Type = LayoutType.Column;
        UseCache = SkiaCacheType.ImageDoubleBuffered;

        Children = new List<SkiaControl>()
        {
            // Main track container
            new SkiaLayout
            {
                Tag = "Track",
                HeightRequest = TrackHeight,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Center,
                Children = new List<SkiaControl>()
                {
                    // Background track
                    new SkiaShape()
                    {
                        Tag = "BackgroundTrack",
                        BackgroundColor = TrackColor,
                        HeightRequest = TrackHeight,
                        CornerRadius = TrackHeight / 2,
                        HorizontalOptions = LayoutOptions.Fill,
                        UseCache = SkiaCacheType.Operations,
                        VerticalOptions = LayoutOptions.Center
                    },

                    // Progress trail
                    new ProgressTrail()
                    {
                        Tag = "ProgressTrail",
                        BackgroundColor = ProgressColor,
                        HeightRequest = TrackHeight,
                        CornerRadius = TrackHeight / 2,
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

    protected virtual void CreateCupertinoStyleContent()
    {
        // iOS progress bar styling - follows Apple HIG specifications
        SetDefaultContentSize(200, 4);

        HorizontalOptions = LayoutOptions.Fill;
        MinimumWidthRequest = 100;
        Type = LayoutType.Column;
        UseCache = SkiaCacheType.ImageDoubleBuffered;

        var iosTrackHeight = 4.0; // iOS standard progress bar height
        var iosTrackColor = Color.FromRgba(229, 229, 234, 255); // iOS system gray 5
        var iosProgressColor = Color.FromRgba(0, 122, 255, 255); // iOS system blue #007AFF

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

        // Set default colors
        TrackColor = iosTrackColor;
        ProgressColor = iosProgressColor;
        TrackHeight = iosTrackHeight;
    }

    protected virtual void CreateMaterialStyleContent()
    {
        // Material Design progress bar styling - follows Material Design 3 specifications
        SetDefaultContentSize(200, 4);

        HorizontalOptions = LayoutOptions.Fill;
        MinimumWidthRequest = 100;
        Type = LayoutType.Column;
        UseCache = SkiaCacheType.ImageDoubleBuffered;

        var materialTrackHeight = 4.0; // Material Design standard linear progress height
        var materialTrackColor = Color.FromRgba(232, 234, 237, 255); // Material surface variant
        var materialProgressColor = Color.FromRgba(103, 80, 164, 255); // Material primary purple

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

        // Set default colors
        TrackColor = materialTrackColor;
        ProgressColor = materialProgressColor;
        TrackHeight = materialTrackHeight;
    }

    protected virtual void CreateWindowsStyleContent()
    {
        // Windows Fluent Design progress bar styling
        SetDefaultContentSize(200, 6);

        HorizontalOptions = LayoutOptions.Fill;
        MinimumWidthRequest = 100;
        Type = LayoutType.Column;
        UseCache = SkiaCacheType.ImageDoubleBuffered;

        var windowsTrackHeight = 6.0; // Windows standard progress bar height
        var windowsTrackColor = Color.FromRgba(243, 242, 241, 255); // Fluent neutral background
        var windowsProgressColor = Color.FromRgba(0, 120, 212, 255); // Fluent accent blue #0078D4

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

        // Set default colors
        TrackColor = windowsTrackColor;
        ProgressColor = windowsProgressColor;
        TrackHeight = windowsTrackHeight;
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

    protected override void UpdateVisualState()
    {
        if (ProgressTrail is ProgressTrail trail && Track != null)
        {
            // Calculate progress width based on current value
            var totalWidth = Track.Width;
            if (totalWidth > 0)
            {
                var progressRatio = Math.Clamp((Value - Min) / (Max - Min), 0.0, 1.0);
                var progressWidth = totalWidth * progressRatio;
                
                trail.XPosEnd = progressWidth;
                trail.BackgroundColor = ProgressColor;
            }
        }

        // Update track colors
        var backgroundTrack = FindView<SkiaShape>("BackgroundTrack");
        if (backgroundTrack != null)
        {
            backgroundTrack.BackgroundColor = TrackColor;
            backgroundTrack.HeightRequest = TrackHeight;
        }

        if (ProgressTrail != null)
        {
            ProgressTrail.HeightRequest = TrackHeight;
        }
    }

    #endregion
}
