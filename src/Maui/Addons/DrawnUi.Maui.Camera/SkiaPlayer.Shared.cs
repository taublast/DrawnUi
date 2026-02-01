global using DrawnUi.Draw;
global using SkiaSharp;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AppoMobi.Specials;
using DrawnUi.Views;
using Color = Microsoft.Maui.Graphics.Color;

#if WINDOWS
using DrawnUi.Camera.Platforms.Windows;
#elif IOS || MACCATALYST
using AVFoundation;
using CoreMedia;
using Foundation;
#endif

namespace DrawnUi.Camera;

/// <summary>
/// SkiaPlayer control for video playback using platform-specific decoding.
/// Plays videos recorded by SkiaCamera with hardware acceleration.
///
/// Basic usage:
/// var player = new SkiaPlayer { Source = "path/to/video.mp4" };
/// await player.PlayAsync();
///
/// Features:
/// - Hardware-accelerated decoding (MediaCodec/AVFoundation/MediaFoundation)
/// - Frame-by-frame display like SkiaCamera preview
/// - Audio playback with sync
/// - Seek support
/// - Cross-platform MP4/H.264/AAC support
/// </summary>
public partial class SkiaPlayer : SkiaControl
{
    #region COMMON

    #region PROPERTIES

    /// <summary>
    /// Video source path (file path or URL)
    /// </summary>
    public static readonly BindableProperty SourceProperty = BindableProperty.Create(
        nameof(Source),
        typeof(string),
        typeof(SkiaPlayer),
        null,
        propertyChanged: OnSourceChanged);

    public string Source
    {
        get { return (string)GetValue(SourceProperty); }
        set { SetValue(SourceProperty, value); }
    }

    private static void OnSourceChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaPlayer player)
        {
            player.OnSourceChanged((string)newvalue);
        }
    }

    /// <summary>
    /// Whether video is currently playing
    /// </summary>
    public static readonly BindableProperty IsPlayingProperty = BindableProperty.Create(
        nameof(IsPlaying),
        typeof(bool),
        typeof(SkiaPlayer),
        false,
        propertyChanged: OnIsPlayingChanged);

    public bool IsPlaying
    {
        get { return (bool)GetValue(IsPlayingProperty); }
        set { SetValue(IsPlayingProperty, value); }
    }

    private static void OnIsPlayingChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaPlayer player)
        {
            player.OnIsPlayingChanged((bool)newvalue);
        }
    }

    /// <summary>
    /// Current playback position
    /// </summary>
    public static readonly BindableProperty PositionProperty = BindableProperty.Create(
        nameof(Position),
        typeof(TimeSpan),
        typeof(SkiaPlayer),
        TimeSpan.Zero,
        propertyChanged: OnPositionChanged);

    public TimeSpan Position
    {
        get { return (TimeSpan)GetValue(PositionProperty); }
        set { SetValue(PositionProperty, value); }
    }

    private static void OnPositionChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaPlayer player)
        {
            player.OnPositionChanged((TimeSpan)newvalue);
        }
    }

    /// <summary>
    /// Video duration (read-only)
    /// </summary>
    public static readonly BindableProperty DurationProperty = BindableProperty.Create(
        nameof(Duration),
        typeof(TimeSpan),
        typeof(SkiaPlayer),
        TimeSpan.Zero);

    public TimeSpan Duration
    {
        get { return (TimeSpan)GetValue(DurationProperty); }
        private set { SetValue(DurationProperty, value); }
    }

    /// <summary>
    /// Audio volume (0.0 to 1.0)
    /// </summary>
    public static readonly BindableProperty VolumeProperty = BindableProperty.Create(
        nameof(Volume),
        typeof(double),
        typeof(SkiaPlayer),
        1.0,
        propertyChanged: OnVolumeChanged);

    public double Volume
    {
        get { return (double)GetValue(VolumeProperty); }
        set { SetValue(VolumeProperty, value); }
    }

    private static void OnVolumeChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaPlayer player)
        {
            player.OnVolumeChanged((double)newvalue);
        }
    }

    /// <summary>
    /// The SkiaImage control that displays the video frames
    /// </summary>
    public SkiaImage Display { get; protected set; }

    /// <summary>
    /// Whether to loop playback
    /// </summary>
    public static readonly BindableProperty IsLoopingProperty = BindableProperty.Create(
        nameof(IsLooping),
        typeof(bool),
        typeof(SkiaPlayer),
        false);

    public bool IsLooping
    {
        get { return (bool)GetValue(IsLoopingProperty); }
        set { SetValue(IsLoopingProperty, value); }
    }

    /// <summary>
    /// Whether video is loaded and ready to play
    /// </summary>
    public static readonly BindableProperty IsVideoLoadedProperty = BindableProperty.Create(
        nameof(IsVideoLoaded),
        typeof(bool),
        typeof(SkiaPlayer),
        false);

    public bool IsVideoLoaded
    {
        get { return (bool)GetValue(IsVideoLoadedProperty); }
        private set { SetValue(IsVideoLoadedProperty, value); }
    }

    #endregion

    #region EVENTS

    /// <summary>
    /// Fired when playback position changes
    /// </summary>
    public event EventHandler<TimeSpan> PositionChanged;

    /// <summary>
    /// Fired when video ends
    /// </summary>
    public event EventHandler PlaybackEnded;

    /// <summary>
    /// Fired when video loading completes
    /// </summary>
    public event EventHandler VideoLoaded;

    #endregion

    #region METHODS

    public override ScaledSize OnMeasuring(float widthConstraint, float heightConstraint, float scale)
    {
        if (Display == null)
        {
            Display = CreateDisplay();
            Display.IsParentIndependent = true;
            Display.SetParent(this);
        }

        return base.OnMeasuring(widthConstraint, heightConstraint, scale);
    }

    protected virtual SkiaImage CreateDisplay()
    {
        return new SkiaImage()
        {
            LoadSourceOnFirstDraw = true,
#if IOS || ANDROID
            RescalingQuality = SKFilterQuality.None, //reduce power consumption
#endif
            CacheRescaledSource = false,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };
    }

    /// <summary>
    /// Load video from source
    /// </summary>
    public async Task LoadAsync()
    {
        if (string.IsNullOrEmpty(Source))
            return;

#if ONPLATFORM
        try
        {
            IsVideoLoaded = false;
            await LoadVideoPlatformAsync(Source);
            IsVideoLoaded = true;
            VideoLoaded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SkiaPlayer] Load failed: {ex.Message}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Start playback
    /// </summary>
    public async Task PlayAsync()
    {
        if (!IsVideoLoaded)
            await LoadAsync();

        IsPlaying = true;
        PlayPlatformAsync();
    }

    /// <summary>
    /// Pause playback
    /// </summary>
    public async Task PauseAsync()
    {
        IsPlaying = false;
        PausePlatformAsync();
    }

    /// <summary>
    /// Stop playback and reset position
    /// </summary>
    public async Task StopAsync()
    {
        IsPlaying = false;
        Position = TimeSpan.Zero;
        StopPlatformAsync();
    }

    /// <summary>
    /// Seek to position
    /// </summary>
    public async Task SeekAsync(TimeSpan position)
    {
        SeekPlatformAsync(position);
        Position = position;
    }

#endregion

    #region PLATFORM METHODS

    partial void OnSourceChanged(string source);
    partial void OnIsPlayingChanged(bool isPlaying);
    partial void OnPositionChanged(TimeSpan position);
    partial void OnVolumeChanged(double volume);
    partial void PlayPlatformAsync();
    partial void PausePlatformAsync();
    partial void StopPlatformAsync();
    partial void SeekPlatformAsync(TimeSpan position);

    #endregion

    #region LIFECYCLE

    protected override void OnParentSet()
    {
        base.OnParentSet();

        if (Parent != null && !string.IsNullOrEmpty(Source) && !IsVideoLoaded)
        {
            Task.Run(() => LoadAsync());
        }
    }

    #endregion

#endregion
}
