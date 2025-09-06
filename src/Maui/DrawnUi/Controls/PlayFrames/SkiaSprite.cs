using DrawnUi.Features.Images;
using Microsoft.Maui.Storage;
using System.Collections.Concurrent;

namespace DrawnUi.Controls;

/// <summary>
/// Renders animated sprite sheets by subclassing AnimatedFramesRenderer
/// </summary>
public class SkiaSprite : AnimatedFramesRenderer
{
    /// <summary>
    /// Cache for loaded spritesheets to avoid reloading the same image multiple times
    /// </summary>
    public static ConcurrentDictionary<string, SKBitmap> CachedSpriteSheets = new();

    /// <summary>
    /// Internal SkiaImage control used to display the current frame
    /// </summary>
    public SkiaImage Display { get; protected set; }

    /// <summary>
    /// For standalone use
    /// </summary>
    public SkiaSprite()
    {
        this.Display = new()
        {
            //UseCache = SkiaCacheType.Image,
            LoadSourceOnFirstDraw = false,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };

        Display.SetParent(this);
    }

    protected override void Paint(DrawingContext ctx)
    {

        if (Columns != _lastColumns || Rows != _lastRows)
        {
            RecalculateFrames();
        }

        base.Paint(ctx);
    }

    /// <summary>
    /// Creates a named animation from a frame sequence
    /// </summary>
    /// <param name="name">Name of the animation</param>
    /// <param name="frameSequence">Array of frame indices</param>
    /// <returns>The frame sequence array</returns>
    public static int[] CreateAnimationSequence(string name, int[] frameSequence)
    {
        // Store the animation sequence in the animation dictionary
        RegisteredAnimations[name] = frameSequence;
        return frameSequence;
    }

    /// <summary>
    /// Dictionary of named animation sequences that can be reused
    /// </summary>
    public static ConcurrentDictionary<string, int[]> RegisteredAnimations = new();

    /// <summary>
    /// For building custom controls
    /// </summary>
    /// <param name="display">Existing SkiaImage to use for display</param>
    public SkiaSprite(SkiaImage display)
    {
        IsStandalone = true;
        this.Display = display;
    }

    /// <summary>
    /// Updates the layout options for the Display based on auto-sizing settings
    /// </summary>
    protected virtual void LayoutDisplay()
    {
        Display.HorizontalOptions = this.NeedAutoWidth ? LayoutOptions.Start : LayoutOptions.Fill;
        Display.VerticalOptions = this.NeedAutoHeight ? LayoutOptions.Start : LayoutOptions.Fill;
    }

    /// <summary>
    /// Updates the layout of the Display when measurement needs to be invalidated
    /// </summary>
    protected override void InvalidateMeasure()
    {
        if (Display != null && !IsStandalone)
        {
            LayoutDisplay();
        }

        base.InvalidateMeasure();
    }

    /// <summary>
    /// Gets the parent control for animator operations
    /// </summary>
    /// <returns>The control to use as animator parent</returns>
    protected override SkiaControl GetAnimatorParent()
    {
        if (IsStandalone)
        {
            return Display;
        }

        return base.GetAnimatorParent();
    }

    /// <summary>
    /// Renders the current frame of the animation
    /// </summary>
    /// <param name="ctx">Drawing context</param>
    protected override void RenderFrame(DrawingContext ctx)
    {
        DrawViews(ctx); // Just draw our Display
    }

    /// <summary>
    /// Invoked by internal animator
    /// </summary>
    /// <param name="value">Current animation time value</param>
    protected override void OnAnimatorUpdated(double value)
    {
        base.OnAnimatorUpdated(value);

        Seek(value);
    }

    /// <summary>
    /// Called by Seek
    /// </summary>
    /// <param name="time">Time position to seek to</param>
    protected override void OnAnimatorSeeking(double time)
    {
        if (SpriteSheet != null)
        {
            var frame = GetFrameNumberFromTime(time);
            if (frame != CurrentFrame)
            {
                SetCurrentFrame(frame);
            }
        }

        base.OnAnimatorSeeking(time);
    }

    /// <summary>
    /// Gets frame number from time based on current frame rate
    /// </summary>
    /// <param name="msTime">Time in milliseconds</param>
    /// <returns>Frame number</returns>
    protected int GetFrameNumberFromTime(double msTime)
    {
        if (msTime < 0)
        {
            msTime = DurationMs + msTime;
        }

        msTime %= DurationMs;

        int frame = (int)(msTime / FrameDurationMs);

        if (FrameSequence != null && FrameSequence.Length > 0)
        {
            int sequenceIndex = frame % FrameSequence.Length;
            return FrameSequence[sequenceIndex];
        }

        return Math.Min(frame, TotalFrames - 1);
    }

    /// <summary>
    /// Initializes the animator with the correct timing values
    /// </summary>
    protected override void OnAnimatorInitializing()
    {
        if (SpriteSheet != null)
        {
            ApplySpeed();

            Animator.mValue = 0;
            Animator.mMinValue = 0;
            Animator.mMaxValue = DurationMs;
            Animator.Distance = Animator.mMaxValue - Animator.mMinValue;
        }
    }

    /// <summary>
    /// Applies the current speed ratio to the animator
    /// </summary>
    protected override void ApplySpeed()
    {
        if (SpriteSheet == null)
            return;

        var speed = 1.0;
        if (SpeedRatio < 1)
            speed = DurationMs * (1 + SpeedRatio);
        else
            speed = DurationMs / SpeedRatio;
        Animator.Speed = speed;
    }

    /// <summary>
    /// Checks if the animator can start
    /// </summary>
    /// <returns>True if animator can start</returns>
    protected override bool CheckCanStartAnimator()
    {
        return SpriteSheet != null && TotalFrames > 0;
    }

    /// <summary>
    /// Starts the animation with optional delay
    /// </summary>
    /// <param name="delayMs">Delay in milliseconds before starting</param>
    public override void Start(int delayMs = 0)
    {
        if (SpriteSheet != null && TotalFrames > 0)
            base.Start(delayMs);
    }

    /// <summary>
    /// Lock object for thread-safe operations on source
    /// </summary>
    private readonly object _lockSource = new();

    /// <summary>
    /// Semaphore for limiting concurrent file loading operations
    /// </summary>
    private readonly SemaphoreSlim _semaphoreLoadFile = new(1, 1);

    /// <summary>
    /// Property changed handler for Source property
    /// </summary>
    private static void ApplySourceProperty(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaSprite control)
            control.ReloadSource();
    }

    /// <summary>
    /// Property changed handler for AnimationName property
    /// </summary>
    private static void ApplyAnimationNameProperty(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaSprite control && newvalue is string animationName)
        {
            if (RegisteredAnimations.TryGetValue(animationName, out var sequence))
            {
                control.FrameSequence = sequence;
            }
            else
            {
                Super.Log($"[SkiaSprite] Animation sequence '{animationName}' not found");
            }
        }
    }

    /// <summary>
    /// Lock object for thread-safe source loading
    /// </summary>
    private object lockSource = new();

    /// <summary>
    /// Reloads the sprite sheet from the Source property
    /// </summary>
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
                        var bitmap = await LoadSourceAsync(Source);
                        if (bitmap != null)
                        {
                            SetSpriteSheet(bitmap, true);
                        }
                    });
                    break;
                default:
                    Tasks.StartDelayedAsync(TimeSpan.FromMilliseconds(1), async () =>
                    {
                        var bitmap = await LoadLocalImageAsync(Source);
                        if (bitmap != null)
                        {
                            SetSpriteSheet(bitmap, true);
                        }
                    });
                    break;
            }
        }
    }

    /// <summary>
    /// Loads a spritesheet image from a source URL, using cache when available
    /// </summary>
    /// <param name="source">URL of the spritesheet</param>
    /// <returns>SKBitmap of the loaded spritesheet</returns>
    public async Task<SKBitmap> LoadSourceAsync(string source)
    {
        if (string.IsNullOrEmpty(source))
            return null;

        // Check if the spritesheet is already in the cache
        if (CachedSpriteSheets.TryGetValue(source, out var cachedBitmap))
        {
            Debug.WriteLine($"[SkiaSprite] Loaded {source} from cache");
            return cachedBitmap;
        }

        await _semaphoreLoadFile.WaitAsync();

        try
        {
            // Check cache again in case another thread loaded it while we were waiting
            if (CachedSpriteSheets.TryGetValue(source, out cachedBitmap))
            {
                Debug.WriteLine($"[SkiaSprite] Loaded {source} from cache after semaphore wait");
                return cachedBitmap;
            }

            SKBitmap bitmap = null;

            if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.Scheme != "file")
            {
                using HttpClient client = Super.Services.CreateHttpClient();
                var data = await client.GetByteArrayAsync(uri);
                bitmap = SKBitmap.Decode(data);
            }
            else
            {
                bitmap = await LoadLocalImageAsync(source);
            }

            if (bitmap != null)
            {
                // Add the loaded bitmap to the cache
                CachedSpriteSheets.TryAdd(source, bitmap);
                Debug.WriteLine($"[SkiaSprite] Added {source} to cache");
            }

            return bitmap;
        }
        catch (Exception e)
        {
            Super.Log($"[SkiaSprite] LoadSource failed to load spritesheet {source}");
            return null;
        }
        finally
        {
            _semaphoreLoadFile.Release();
        }
    }

    /// <summary>
    /// Loads a spritesheet image from a local or embedded resource
    /// </summary>
    /// <param name="source">Path to the image</param>
    /// <returns>SKBitmap of the loaded spritesheet</returns>
    public async Task<SKBitmap> LoadLocalImageAsync(string source)
    {
        try
        {
            // Check if the spritesheet is already in the cache
            if (CachedSpriteSheets.TryGetValue(source, out var cachedBitmap))
            {
                Debug.WriteLine($"[SkiaSprite] Loaded {source} from cache");
                return cachedBitmap;
            }

            SKBitmap bitmap = null;

            if (source.SafeContainsInLower(SkiaImageManager.NativeFilePrefix))
            {
                var fullFilename = source.Replace(SkiaImageManager.NativeFilePrefix, "");
                using var stream = new FileStream(fullFilename, FileMode.Open);
                bitmap = SKBitmap.Decode(stream);
            }
            else
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync(source);
                bitmap = SKBitmap.Decode(stream);
            }

            if (bitmap != null)
            {
                // Add the loaded bitmap to the cache
                CachedSpriteSheets.TryAdd(source, bitmap);
                Debug.WriteLine($"[SkiaSprite] Added {source} to cache");
            }

            return bitmap;
        }
        catch (Exception e)
        {
            Super.Log($"[SkiaSprite] LoadLocalImage failed to load spritesheet {source}");
            return null;
        }
    }

    /// <summary>
    /// Sets the sprite sheet bitmap and initializes the animation
    /// </summary>
    /// <param name="bitmap">Spritesheet bitmap</param>
    /// <param name="disposePrevious">If true, disposes previous bitmap</param>
    public void SetSpriteSheet(SKBitmap bitmap, bool disposePrevious)
    {
        lock (_lockSource)
        {
            if (bitmap == null || bitmap == SpriteSheet || IsDisposed) return;

            var wasPlaying = IsPlaying;
            SKBitmap kill = null;

            if (wasPlaying)
            {
                kill = SpriteSheet;
                Stop();
            }

            // Important: Don't set to our own SpriteSheetReference property
            // so that we're not responsible for disposing the cached bitmap
            _spriteSheet = bitmap;
            OnPropertyChanged(nameof(SpriteSheet));
            RecalculateFrames();

            if (TotalFrames > 0)
            {
                Debug.WriteLine(
                    $"[SkiaSprite] Loaded spritesheet: Frames: {TotalFrames}, Duration: {DurationMs}ms, FPS: {FramesPerSecond}");

                InitializeAnimator(); // Autoplay applied inside
                SetCurrentFrame(DefaultFrame);

                if (wasPlaying && !IsPlaying)
                    Start();

                // Only dispose if we know it's not in the cache
                if (kill != null && disposePrevious && !IsInCache(kill))
                {
                    kill.Dispose();
                }

                Invalidate();
            }

            Monitor.PulseAll(_lockSource);
        }
    }

    /// <summary>
    /// Checks if a bitmap is in the cache
    /// </summary>
    /// <param name="bitmap">Bitmap to check</param>
    /// <returns>True if the bitmap is in the cache</returns>
    private bool IsInCache(SKBitmap bitmap)
    {
        return CachedSpriteSheets.Values.Contains(bitmap);
    }

    /// <summary>
    /// Recalculates frame information based on current properties
    /// </summary>
    protected void RecalculateFrames()
    {
        if (SpriteSheet == null) return;

        // Calculate frames based on sprite sheet layout and dimensions
        int framesX = Math.Max(1, Columns);
        int framesY = Math.Max(1, Rows);

        // Total frames in the spritesheet (not necessarily the animation length)
        int totalFramesInSheet = Math.Min(framesX * framesY, MaxFrames > 0 ? MaxFrames : int.MaxValue);

        FrameWidth = SpriteSheet.Width / framesX;
        FrameHeight = SpriteSheet.Height / framesY;

        // If we have a frame sequence defined, use that for the animation length
        if (FrameSequence != null && FrameSequence.Length > 0)
        {
            TotalFrames = FrameSequence.Length;
        }
        else
        {
            TotalFrames = totalFramesInSheet;
        }

        // Calculate duration based on FPS
        FrameDurationMs = 1000 / FramesPerSecond;
        DurationMs = TotalFrames * FrameDurationMs;

        _lastColumns=Columns;
        _lastRows = Rows;
    }

    private bool isSettingFrame;

    /// <summary>
    /// Sets the current frame and updates the display
    /// </summary>
    /// <param name="frameNumber">Frame number to display</param>
    protected void SetCurrentFrame(int frameNumber)
    {
        if (SpriteSheet == null || TotalFrames == 0 || isSettingFrame) return;

        isSettingFrame = true;

        try
        {
            frameNumber = Math.Max(0, Math.Min(frameNumber, TotalFrames - 1));

            // If using a frame sequence, convert to actual spritesheet frame
            int actualFrame;
            if (FrameSequence != null && FrameSequence.Length > 0)
            {
                // Ensure we don't go beyond the bounds of our sequence array
                int sequenceIndex = Math.Min(frameNumber, FrameSequence.Length - 1);
                actualFrame = FrameSequence[sequenceIndex];
            }
            else
            {
                actualFrame = frameNumber;
            }

            CurrentFrame = frameNumber;

            // Calculate frame position in the spritesheet
            int framesX = Math.Max(1, Columns);
            int row = actualFrame / framesX;
            int col = actualFrame % framesX;

            // Extract frame from spritesheet
            int x = col * FrameWidth;
            int y = row * FrameHeight;

            // Create a new bitmap for the frame if needed
            if (_currentFrameBitmap == null ||
                _currentFrameBitmap.Width != FrameWidth ||
                _currentFrameBitmap.Height != FrameHeight)
            {
                _currentFrameBitmap?.Dispose();
                _currentFrameBitmap = new SKBitmap(FrameWidth, FrameHeight);
            }

            // Extract the frame from the sprite sheet
            using (var canvas = new SKCanvas(_currentFrameBitmap))
            {
                canvas.Clear();
                var srcRect = new SKRect(x, y, x + FrameWidth, y + FrameHeight);
                canvas.DrawBitmap(SpriteSheet, srcRect, new SKRect(0, 0, FrameWidth, FrameHeight));
            }

            Display.SetBitmapInternal(_currentFrameBitmap, true);
        }
        finally
        {
            isSettingFrame = false;
        }
    }

    public override void Update()
    {
        base.Update();
    }

    /// <summary>
    /// Current bitmap for the active frame
    /// Current bitmap for the active frame
    /// </summary>
    private SKBitmap _currentFrameBitmap;

    /// <summary>
    /// The full sprite sheet bitmap
    /// </summary>
    private SKBitmap _spriteSheet;

    private int _lastColumns;
    private int _lastRows;

    /// <summary>
    /// The full sprite sheet bitmap
    /// </summary>
    public SKBitmap SpriteSheet
    {
        get => _spriteSheet;
        protected set
        {
            if (_spriteSheet != value)
            {
                _spriteSheet = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Clears the spritesheet cache completely
    /// </summary>
    public static void ClearCache()
    {
        foreach (var bitmap in CachedSpriteSheets.Values)
        {
            bitmap.Dispose();
        }

        CachedSpriteSheets.Clear();
    }

    /// <summary>
    /// Removes a specific item from the spritesheet cache
    /// </summary>
    /// <param name="key">The source key to remove</param>
    public static void RemoveFromCache(string key)
    {
        if (CachedSpriteSheets.TryRemove(key, out var bitmap))
        {
            bitmap.Dispose();
        }
    }

    /// <summary>
    /// Handles cleanup when the control is being disposed
    /// </summary>
    public override void OnDisposing()
    {
        lock (_lockSource)
        {
            base.OnDisposing();

            // Only dispose SpriteSheet if it's not in the cache
            if (SpriteSheet != null && !IsInCache(SpriteSheet))
            {
                SpriteSheet.Dispose();
            }

            SpriteSheet = null;

            if (_currentFrameBitmap != null)
            {
                _currentFrameBitmap.Dispose();
                _currentFrameBitmap = null;
            }

            Monitor.PulseAll(_lockSource);
        }
    }

    #region Properties

    /// <summary>
    /// Bindable property for FrameSequence
    /// </summary>
    public static readonly BindableProperty FrameSequenceProperty = BindableProperty.Create(
        nameof(FrameSequence),
        typeof(int[]),
        typeof(SkiaSprite),
        null,
        propertyChanged: (b, o, n) => ((SkiaSprite)b).RecalculateFrames());

    /// <summary>
    /// Bindable property for AnimationName
    /// </summary>
    public static readonly BindableProperty AnimationNameProperty = BindableProperty.Create(
        nameof(AnimationName),
        typeof(string),
        typeof(SkiaSprite),
        null,
        propertyChanged: ApplyAnimationNameProperty);

    /// <summary>
    /// Optional custom sequence of frames to play from the spritesheet.
    /// If set, this overrides the default sequential frame playback.
    /// </summary>
    public int[] FrameSequence
    {
        get => (int[])GetValue(FrameSequenceProperty);
        set => SetValue(FrameSequenceProperty, value);
    }

    /// <summary>
    /// Name of a predefined animation sequence to use
    /// </summary>
    public string AnimationName
    {
        get => (string)GetValue(AnimationNameProperty);
        set => SetValue(AnimationNameProperty, value);
    }

    /// <summary>
    /// Bindable property for Source
    /// </summary>
    public static readonly BindableProperty SourceProperty = BindableProperty.Create(
        nameof(Source),
        typeof(string),
        typeof(SkiaSprite),
        string.Empty,
        propertyChanged: ApplySourceProperty);

    /// <summary>
    /// Source of the spritesheet image
    /// </summary>
    public string Source
    {
        get => (string)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>
    /// Bindable property for Columns
    /// </summary>
    public static readonly BindableProperty ColumnsProperty = BindableProperty.Create(
        nameof(Columns),
        typeof(int),
        typeof(SkiaSprite),
        1,
        propertyChanged: NeedDraw);

    /// <summary>
    /// Number of columns in the spritesheet
    /// </summary>
    public int Columns
    {
        get => (int)GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <summary>
    /// Bindable property for Rows
    /// </summary>
    public static readonly BindableProperty RowsProperty = BindableProperty.Create(
        nameof(Rows),
        typeof(int),
        typeof(SkiaSprite),
        1,
        propertyChanged: NeedDraw);

    /// <summary>
    /// Number of rows in the spritesheet
    /// </summary>
    public int Rows
    {
        get => (int)GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    /// <summary>
    /// Bindable property for MaxFrames
    /// </summary>
    public static readonly BindableProperty MaxFramesProperty = BindableProperty.Create(
        nameof(MaxFrames),
        typeof(int),
        typeof(SkiaSprite),
        0,
        propertyChanged: (b, o, n) => ((SkiaSprite)b).RecalculateFrames());

    /// <summary>
    /// Maximum number of frames to use from the spritesheet (0 means use all)
    /// </summary>
    public int MaxFrames
    {
        get => (int)GetValue(MaxFramesProperty);
        set => SetValue(MaxFramesProperty, value);
    }

    /// <summary>
    /// Bindable property for FramesPerSecond
    /// </summary>
    public static readonly BindableProperty FramesPerSecondProperty = BindableProperty.Create(
        nameof(FramesPerSecond),
        typeof(double),
        typeof(SkiaSprite),
        24.0,
        propertyChanged: (b, o, n) => ((SkiaSprite)b).RecalculateFrames());

    /// <summary>
    /// Frames per second for the animation
    /// </summary>
    public double FramesPerSecond
    {
        get => (double)GetValue(FramesPerSecondProperty);
        set => SetValue(FramesPerSecondProperty, value);
    }

    /// <summary>
    /// Bindable property for CurrentFrame
    /// </summary>
    public static readonly BindableProperty CurrentFrameProperty = BindableProperty.Create(
        nameof(CurrentFrame),
        typeof(int),
        typeof(SkiaSprite),
        0,
        propertyChanged: NeedDraw);

    /// <summary>
    /// Current frame being displayed
    /// </summary>
    public int CurrentFrame
    {
        get => (int)GetValue(CurrentFrameProperty);
        set => SetValue(CurrentFrameProperty, value);
    }

    /// <summary>
    /// Width of each frame in pixels
    /// </summary>
    public int FrameWidth { get; protected set; }

    /// <summary>
    /// Height of each frame in pixels
    /// </summary>
    public int FrameHeight { get; protected set; }

    /// <summary>
    /// Total number of frames in the spritesheet
    /// </summary>
    public int TotalFrames { get; protected set; }

    /// <summary>
    /// Duration of the animation in milliseconds
    /// </summary>
    public double DurationMs { get; protected set; }

    /// <summary>
    /// Duration of each frame in milliseconds
    /// </summary>
    public double FrameDurationMs { get; protected set; }

    #endregion
}
