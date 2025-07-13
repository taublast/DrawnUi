using DrawnUi.Features.Images;
using Microsoft.Maui.Storage;

namespace DrawnUi.Controls;


public class SkiaGif : AnimatedFramesRenderer
{
    public SkiaImage Display { get; protected set; }


    /// <summary>
    /// For standalone use
    /// </summary>
    public SkiaGif()
    {
        this.Display = new()
        {
            Tag="GifDisplay",
            LoadSourceOnFirstDraw = false,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };

        Display.SetParent(this);
    }

    protected virtual void LayoutDisplay()
    {
        Display.HorizontalOptions = this.NeedAutoWidth ? LayoutOptions.Start : LayoutOptions.Fill;
        Display.VerticalOptions = this.NeedAutoHeight ? LayoutOptions.Start : LayoutOptions.Fill;
    }

    protected override void InvalidateMeasure()
    {
        if (Display != null && !IsStandalone)
        {
            LayoutDisplay();
        }
        base.InvalidateMeasure();
    }

    /// <summary>
    /// For building custom controls
    /// </summary>
    /// <param name="display"></param>
    public SkiaGif(SkiaImage display)
    {
        IsStandalone = true;
        this.Display = display;
    }

    protected override SkiaControl GetAnimatorParent()
    {
        if (IsStandalone)
        {
            return Display;
        }

        return base.GetAnimatorParent();
    }

    protected override void RenderFrame(DrawingContext ctx)
    {
        DrawViews(ctx); //just draw our Display
    }

    /// <summary>
    /// invoked by internal animator
    /// </summary>
    /// <param name="value"></param>
    protected override void OnAnimatorUpdated(double value)
    {
        base.OnAnimatorUpdated(value);

        Seek(value);
    }

    /// <summary>
    /// called by Seek
    /// </summary>
    /// <param name="frame"></param>
    protected override void OnAnimatorSeeking(double time)
    {
        if (Animation != null)
        {
            var frame = Animation.GetFrameNumber(time);
            Animation.SeekFrame(frame);
            Display.SetBitmapInternal(Animation.Frame, true);
        }
        base.OnAnimatorSeeking(time);
    }

    protected override void OnAnimatorInitializing()
    {
        if (Animation != null)
        {
            ApplySpeed();

            Animator.mValue = 0;
            Animator.mMinValue = 0;
            Animator.mMaxValue = Animation.DurationMs;
            Animator.Distance = Animator.mMaxValue - Animator.mMinValue;
        }
    }

    protected override void ApplySpeed()
    {
        if (Animation == null)
            return;

        var speed = 1.0;
        if (SpeedRatio < 1)
            speed = Animation.DurationMs * (1 + SpeedRatio);
        else
            speed = Animation.DurationMs / SpeedRatio;
        Animator.Speed = speed;
    }

    public GifAnimation _animation;
    public GifAnimation Animation
    {
        get => _animation;

        set
        {
            if (_animation != value)
            {
                _animation = value;
                OnPropertyChanged();
            }
        }
    }

    private readonly object _lockSource = new();

    public void SetAnimation(GifAnimation animation, bool disposePrevious)
    {
        lock (_lockSource)
        {
            if (animation == null || animation == Animation || IsDisposed) return;

            var wasPlaying = IsPlaying;
            GifAnimation kill = null;

            if (wasPlaying)
            {
                kill = Animation;
                Stop();
            }

            Animation = animation;

            Debug.WriteLine($"[SkiaGif] Loaded animation: Duration:{Animation.DurationMs} Frames:  Fps:{Animation.TotalFrames}");

            InitializeAnimator(); //autoplay applied inside

            OnAnimatorSeeking(DefaultFrame);

            if (wasPlaying && !IsPlaying)
                Start();

            if (kill != null && disposePrevious)
            {
                kill.Dispose();
            }

            Invalidate();

            Monitor.PulseAll(_lockSource);
        }
    }

    public override void Start(int delayMs = 0)
    {
        if (this.Animation?.TotalFrames > 0)
            base.Start(delayMs);
    }

    private readonly SemaphoreSlim _semaphoreLoadFile = new(1, 1);

    private static void ApplySourceProperty(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaGif control)
            control.ReloadSource();
    }

    private object lockSource = new();

    /// <summary>
    /// Happens when loaded fine from `Source`. Will pass source as string.
    /// </summary>
    public event EventHandler<string> Success;

    /// <summary>
    /// Happens when loaded with error from `Source`. Will pass exception.
    /// </summary>
    public event EventHandler<Exception> Error;

    public virtual void ReloadSource()
    {
        if (string.IsNullOrEmpty(Source))
        {
            return;
        }

        lock (lockSource)
        {
            var type = GetSourceType(Source);

            switch (type)
            {
                case SourceType.Url:
                    Tasks.StartDelayedAsync(TimeSpan.FromMilliseconds(1), async () =>
                    {
                        var a = await LoadSource(Source);
                        if (a != null)
                        {
                            Success?.Invoke(this, Source);
                            SetAnimation(a, true);
                        }
                        else
                        {
                            Error?.Invoke(this, new Exception($"Failed to load source {Source}"));
                        }
                    });
                    break;
                default:
                    GifAnimation animation = new();
                    try
                    {
                        if (Source.SafeContainsInLower(SkiaImageManager.NativeFilePrefix))
                        {
                            var fullFilename = Source.Replace(SkiaImageManager.NativeFilePrefix, "");
                            using var stream = new FileStream(fullFilename, FileMode.Open);
                            animation.LoadFromStream(stream);
                        }
                        else
                        {
                            using var stream = FileSystem.OpenAppPackageFileAsync(Source).GetAwaiter().GetResult();
                            using var reader = new StreamReader(stream);
                            animation.LoadFromStream(stream);
                        }
                        if (animation.TotalFrames <1)
                        {
                            animation = null;
                        }
                        if (animation != null)
                        {
                            Success?.Invoke(this, Source);
                            SetAnimation(animation, true);
                        }
                        else
                        {
                            Error?.Invoke(this, new Exception($"Failed to load source {Source}"));
                        }
                    }
                    catch (Exception e)
                    {
                        Super.Log(e);
                        Error?.Invoke(this, new Exception($"Failed to load source {Source}"));
                    }
                    break;
            }
        }


    }



    /// <summary>
    /// This is not replacing current animation, only pre-loading! Use SetAnimation after that if needed.
    /// </summary>
    /// <param name="fileName"></param>
    /// <returns></returns>
    public async Task<GifAnimation> LoadSource(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return null;

        await _semaphoreLoadFile.WaitAsync();

        try
        {
            GifAnimation animation = new();
            {
                if (Uri.TryCreate(fileName, UriKind.Absolute, out var uri) && uri.Scheme != "file")
                {
                    using HttpClient client = Super.Services.CreateHttpClient();
                    using var dataStream = await client.GetStreamAsync(uri);
                    animation.LoadFromStream(dataStream);
                }
                else
                {
                    if (fileName.SafeContainsInLower(SkiaImageManager.NativeFilePrefix))
                    {
                        var fullFilename = fileName.Replace(SkiaImageManager.NativeFilePrefix, "");
                        using var stream = new FileStream(fullFilename, FileMode.Open);
                        animation.LoadFromStream(stream);
                    }
                    else
                    {
                        using var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
                        using var reader = new StreamReader(stream);
                        animation.LoadFromStream(stream);
                    }

                }
            }

            if (animation.TotalFrames > 0)
            {
                return animation;
            }

            return null;
        }
        catch (Exception e)
        {
            Trace.WriteLine($"[SkiaLottie] LoadSource failed to load animation {fileName}");
            Trace.WriteLine(e);
            return null;
        }
        finally
        {
            _semaphoreLoadFile.Release();
        }
    }


    public static readonly BindableProperty SourceProperty = BindableProperty.Create(nameof(Source),
        typeof(string),
        typeof(SkiaGif),
        string.Empty,
        propertyChanged: ApplySourceProperty);


    public string Source
    {
        get => (string)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

}
