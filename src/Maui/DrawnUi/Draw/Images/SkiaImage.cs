using DrawnUi.Infrastructure.Xaml;

namespace DrawnUi.Draw;

public class SkiaImage : SkiaControl
{
    public SkiaImage()
    {
    }

    public SkiaImage(string source)
    {
        this.Source = source;
    }

    void CancelNextSource()
    {
        CancelLoading?.Cancel();
        LoadSource = null;
        var kill = ApplyNewSource;
        ApplyNewSource = null;
        if (kill != null)
        {
            if (SkiaImageManager.ReuseBitmaps)
                kill.ProtectBitmapFromDispose = true; //do not dispose shared cached image
            DisposeObject(kill);
        }
    }

    public override SKImage CachedImage
    {
        get
        {
            if (RenderObject == null && ScaledSource != null)
            {
                return ScaledSource.Image;
            }
            return base.CachedImage;
        }
    }

    public override void OnScaleChanged()
    {
        InvalidateImageFilter();
        InvalidateColorFilter();

        base.OnScaleChanged();
    }

    /// <summary>
    /// Set this to true for cases when your image is auto-sized and changing the source should re-measure it again.
    /// </summary>
    public bool HasUnstableSize { get; set; }

    /// <summary>
    /// Do not call this directly, use InstancedBitmap prop
    /// </summary>
    /// <param name="loaded"></param>
    protected virtual LoadedImageSource SetImage(LoadedImageSource loaded)
    {
        if (loaded == null)
        {
            Cleared?.Invoke(this, null);
            _needClearBitmap = true;
        }

        if (loaded == ApplyNewSource && loaded != null)
        {
            return loaded;
        }

        var killApplyNewSource = ApplyNewSource;
        ApplyNewSource = loaded;
        if (killApplyNewSource != null)
        {
            if (SkiaImageManager.ReuseBitmaps)
                killApplyNewSource.ProtectBitmapFromDispose = true; //do not dispose shared cached image
            DisposeObject(killApplyNewSource);
        }

        // see HasUnstableSize help for explanation
        if (NeedAutoSize && (HasUnstableSize
                             || MeasuredSize.Pixels.Width < 1 || MeasuredSize.Pixels.Height < 1))
        {
            Invalidate();
        }
        else
        {
            Update();
        }

        return loaded;
    }

    /// <summary>
    /// Will render on an off-screen surface and return result. Contains all the effects and other rendering properties applied, size will correspond to source.
    /// </summary>
    /// <returns></returns>
    public virtual SKImage GetRenderedSource()
    {
        if (LoadedSource != null)
        {
            var info = new SKImageInfo(LoadedSource.Width, LoadedSource.Height);
            using var surface = SKSurface.Create(info);
            if (surface != null)
            {
                var context = new SkiaDrawingContext()
                {
                    Canvas = surface.Canvas, Width = info.Width, Height = info.Height
                };
                var destination = new SKRect(0, 0, info.Width, info.Height);
                var ctx = new DrawingContext(context, destination, 1, null);
                Render(ctx);
                surface.Flush();
                return surface.Snapshot();
            }
        }

        return null;
    }

    //public override bool WillClipBounds => true;
    public CancellationTokenSource CancelLoading;

    public LoadedImageSource LoadedSource
    {
        get => _loadedSource;
        set
        {
            if (value != _loadedSource)
            {
                _loadedSource = value;
                OnPropertyChanged();
            }
        }
    }

    public static bool LogEnabled = false;

    static void TraceLog(string message)
    {
        if (LogEnabled)
        {
#if WINDOWS
            Trace.WriteLine(message);
#else
            Console.WriteLine(message);
#endif
        }
    }

    #region PROPERTIES

    public static readonly BindableProperty UseAssemblyProperty = BindableProperty.Create(
        nameof(UseAssembly),
        typeof(object),
        typeof(SkiaImage),
        null);

    public object UseAssembly
    {
        get { return GetValue(UseAssemblyProperty); }
        set { SetValue(UseAssemblyProperty, value); }
    }

    public static readonly BindableProperty AspectProperty = BindableProperty.Create(
        nameof(Aspect),
        typeof(TransformAspect),
        typeof(SkiaImage),
        TransformAspect.AspectCover,
        propertyChanged: NeedInvalidateMeasure);

    /// <summary>
    /// Apspect to render image with, default is AspectCover. 
    /// </summary>
    public TransformAspect Aspect
    {
        get { return (TransformAspect)GetValue(AspectProperty); }
        set { SetValue(AspectProperty, value); }
    }

    public static readonly BindableProperty VerticalAlignmentProperty = BindableProperty.Create(
        nameof(VerticalAlignment),
        typeof(DrawImageAlignment),
        typeof(SkiaImage),
        DrawImageAlignment.Center,
        propertyChanged: NeedDraw);

    public DrawImageAlignment VerticalAlignment
    {
        get { return (DrawImageAlignment)GetValue(VerticalAlignmentProperty); }
        set { SetValue(VerticalAlignmentProperty, value); }
    }

    public static readonly BindableProperty HorizontalAlignmentProperty = BindableProperty.Create(
        nameof(HorizontalAlignment),
        typeof(DrawImageAlignment),
        typeof(SkiaImage),
        DrawImageAlignment.Center,
        propertyChanged: NeedDraw);

    public DrawImageAlignment HorizontalAlignment
    {
        get { return (DrawImageAlignment)GetValue(HorizontalAlignmentProperty); }
        set { SetValue(HorizontalAlignmentProperty, value); }
    }

    //private static void OnSetSource(BindableObject bindable, object oldvalue, object newvalue)
    //{
    //	if (oldvalue != newvalue)
    //	{
    //		var me = bindable as SkiaImage;
    //		me?.SetSource(newvalue as string);
    //	}
    //}

    private static void OnSetSource(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (oldvalue != newvalue)
        {
            var me = bindable as SkiaImage;
            me?.SetSource(newvalue as ImageSource);
        }
    }

    public static readonly BindableProperty SourceProperty = BindableProperty.Create(
        nameof(Source),
        typeof(ImageSource),
        typeof(SkiaImage),
        defaultValue: null, propertyChanged: OnSetSource);

    [System.ComponentModel.TypeConverter(typeof(FrameworkImageSourceConverter))]
    //[System.ComponentModel.TypeConverter(typeof(ImageSourceConverter))]
    public ImageSource Source
    {
        get { return (ImageSource)GetValue(SourceProperty); }
        set { SetValue(SourceProperty, value); }
    }

    public static readonly BindableProperty ImageBitmapProperty = BindableProperty.Create(
        nameof(ImageBitmap),
        typeof(LoadedImageSource),
        typeof(SkiaImage),
        defaultValue: null,
        propertyChanged: OnSetInstancedBitmap);

    /// <summary>
    /// this is the source loaded, doesn't reflect any effects or any other rendering properties 
    /// </summary>
    public LoadedImageSource ImageBitmap
    {
        get { return (LoadedImageSource)GetValue(ImageBitmapProperty); }
        set { SetValue(ImageBitmapProperty, value); }
    }

    public static readonly BindableProperty LoadSourceOnFirstDrawProperty = BindableProperty.Create(
        nameof(LoadSourceOnFirstDraw),
        typeof(bool),
        typeof(SkiaImage),
        false,
        propertyChanged: OnLoadSourceChanged);

    /// <summary>
    /// Should the source be loaded on the first draw, useful for the first fast rendering of the screen and loading images after,
    /// default is False.
    /// Set this to False if you need an off-screen loading and to True to make the screen load faster.
    /// While images are loaded in async manner this still has its impact.
    /// Useful to set True for for SkiaCarousel cells etc..
    /// </summary>
    public bool LoadSourceOnFirstDraw
    {
        get { return (bool)GetValue(LoadSourceOnFirstDrawProperty); }
        set { SetValue(LoadSourceOnFirstDrawProperty, value); }
    }

    public static readonly BindableProperty RescalingQualityProperty = BindableProperty.Create(
        nameof(RescalingQuality),
        typeof(SKFilterQuality),
        typeof(SkiaImage),
        SKFilterQuality.Medium,
        propertyChanged: NeedDraw);

    /// <summary>
    /// Default value is Medium.
    /// </summary>
    public SKFilterQuality RescalingQuality
    {
        get { return (SKFilterQuality)GetValue(RescalingQualityProperty); }
        set { SetValue(RescalingQualityProperty, value); }
    }


    private static void OnLoadSourceChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaImage control)
        {
            control.LoadSourceIfNeeded();
        }
    }

    public static readonly BindableProperty PreviewBase64Property = BindableProperty.Create(nameof(PreviewBase64),
        typeof(string), typeof(SkiaImage), defaultValue: string.Empty
    );

    /// <summary>
    /// If setting in code-behind must be set BEFORE you change the Source
    /// </summary>
    public string PreviewBase64
    {
        get { return (string)GetValue(PreviewBase64Property); }
        set { SetValue(PreviewBase64Property, value); }
    }

    public override void OnWillDisposeWithChildren()
    {
        CancelLoading?.Cancel();

        Cleared = null;
        Error = null;
        Success = null;

        base.OnWillDisposeWithChildren();
    }

    public void SetSource(Func<CancellationToken, Task<Stream>> getStream)
    {
        var source = new StreamImageSource { Stream = getStream };
        SetImageSource(source);
    }

    public void SetSource(ImageSource source)
    {
        //until we implement 2-threads rendering this is needed for ImageDoubleBuffered cache rendering
        if (IsDisposing || IsDisposed)
            return;

        TraceLog($"[SkiaImage] Creating Source from {source}");
        SetImageSource(source);
    }

    public void SetSource(string source)
    {
        //until we implement 2-threads rendering this is needed for ImageDoubleBuffered cache rendering
        if (IsDisposing || IsDisposed)
            return;

        if (string.IsNullOrEmpty(source))
        {
            SetImageSource(null);
        }
        else
        {
            TraceLog($"[SkiaImage] Creating Source from {source}");
            //lastImageSource = source;
            var imageSource = FrameworkImageSourceConverter.FromInvariantString(source);
            //if (imageSource is UriImageSource uri)
            //{
            //	uri.CachingEnabled = true;
            //	uri.CacheValidity = TimeSpan.FromDays(1);
            //}
            SetImageSource(imageSource);
        }
    }

    public void SetFromBase64(string input)
    {
        var pixelArray = Convert.FromBase64String(input);

        var bitmap = SKBitmap.Decode(pixelArray);

        ImageBitmap = new LoadedImageSource(bitmap) { ProtectBitmapFromDispose = SkiaImageManager.ReuseBitmaps };
        //SetImage(new InstancedBitmap(bitmap));
    }

    private LoadedImageSource _applyNewSource;

    protected LoadedImageSource ApplyNewSource
    {
        get { return _applyNewSource; }
        set { _applyNewSource = value; }
    }

    private static void OnSetInstancedBitmap(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaImage control)
        {
            control.SetImage(newvalue as LoadedImageSource);
        }
    }

    /// <summary>
    /// Use only if you know what to do, this internally just sets the new bitmap without any invalidations and not forcing an update.
    /// You would want to set InstancedBitmap prop for a usual approach.
    /// </summary>
    /// <param name="bitmap"></param>
    public LoadedImageSource SetBitmapInternal(SKBitmap bitmap, bool protectFromDispose = false)
    {
        return SetImage(new LoadedImageSource(bitmap)
        {
            ProtectFromDispose = protectFromDispose, ProtectBitmapFromDispose = SkiaImageManager.ReuseBitmaps
        });
    }

    public LoadedImageSource SetImageInternal(SKImage image, bool protectFromDispose = false)
    {
        return SetImage(new LoadedImageSource(image)
        {
            ProtectFromDispose = protectFromDispose, ProtectBitmapFromDispose = SkiaImageManager.ReuseBitmaps
        });
    }

    private bool _IsLoading;

    public bool IsLoading
    {
        get { return _IsLoading; }
        set
        {
            if (_IsLoading != value)
            {
                _IsLoading = value;
                OnPropertyChanged();
            }
        }
    }

    public Action LoadSource { get; protected set; }

    public virtual void StopLoading()
    {
        CancelLoading?.Cancel();
        LoadSource = null;
    }

    public virtual void ReloadSource()
    {
        if (LastSource != null)
            SetImageSource(LastSource);
    }

    public event EventHandler Cleared;

    protected virtual async Task<SKBitmap> LoadImageManagedAsync(ImageSource source, CancellationTokenSource cancel,
        LoadPriority priority = LoadPriority.Normal)
    {
        return await SkiaImageManager.Instance.LoadImageManagedAsync(source, cancel, priority);
    }

    public virtual void SetImageSource(ImageSource source)
    {
        //until we implement 2-threads rendering this is needed for ImageDoubleBuffered cache rendering
        if (IsDisposing || IsDisposed)
            return;

        StopLoading();

        if (source == null)
        {
            TraceLog($"[SkiaImage] source is null");
            ClearBitmap();
            OnSuccess(null);
        }
        else
        {
            if (EraseChangedContent || Source == null)
            {
                ClearBitmap(); //erase old image..
            }

            CancelNextSource();

            if (!string.IsNullOrEmpty(PreviewBase64))
            {
                SetFromBase64(PreviewBase64);
                //Debug.WriteLine($"[SkiaImage] preview set");
            }

            if (!source.IsEmpty)
            {
                //maybe image is already cached?
                string uri = SkiaImageManager.GetUriFromImageSource(source);
                if (uri != null)
                {
                    var cachedBitmap = SkiaImageManager.Instance.GetFromCache(uri);
                    if (cachedBitmap != null)
                    {
                        SetImageInternal(SKImage.FromBitmap(cachedBitmap), SkiaImageManager.ReuseBitmaps);
                        //ImageBitmap = new LoadedImageSource(cachedBitmap)
                        //{
                        //    ProtectBitmapFromDispose = SkiaImageManager.ReuseBitmaps
                        //};
                        OnSuccess(uri);
                        return;
                    }
                }

                string url = FrameworkImageSourceConverter.ConvertToString(source);
                var cancel = new CancellationTokenSource();
                CancelLoading = cancel;
                IsLoading = true;

                if (!SkiaImageManager.LoadLocalAsync)
                {
                    //maybe its local image?
                    if (source is StreamImageSource || source is FileImageSource)
                    {
                        //load in sync mode
                        SKBitmap bitmap = null;
                        try
                        {
                            bitmap = SkiaImageManager.Instance.LoadImageManagedAsync(source, cancel).GetAwaiter()
                                .GetResult();
                        }
                        catch (Exception e)
                        {
                            Super.Log(e);
                        }
                        if (bitmap != null)
                        {
                            //ImageBitmap = new LoadedImageSource(bitmap)
                            //{
                            //    ProtectBitmapFromDispose = SkiaImageManager.ReuseBitmaps
                            //};
                            SetImageInternal(SKImage.FromBitmap(bitmap), SkiaImageManager.ReuseBitmaps);
                            OnSuccess(uri);
                        }
                        else
                        {
                            OnError(url);
                        }
                        return;
                    }
                }

                //okay will load async then
                LastSource = source;

                //async loadinc
                LoadSource = async () =>
                {
                    try
                    {
                        SKBitmap bitmap = null;
                        SkiaImageManager.Instance.CanReload -= OnCanReloadSource;

                        //while (bitmap == null && RetriesLeft > -1) TODO
                        {
                            if (cancel.Token.IsCancellationRequested)
                            {
                                //Debug.WriteLine($"[SkiaImage] {Id} loading canceled for {url} - ({retries})");
                                cancel?.Cancel();
                                IsLoading = false;
                                if (LogEnabled)
                                    TraceLog($"[SkiaImage] Canceled loading {source}");
                                return;
                            }

                            async Task LoadAction()
                            {
                                try
                                {
                                    //bitmap = await SkiaImageManager.LoadImageOnPlatformAsync(source, cancel.Token);
                                    bitmap = await LoadImageManagedAsync(source, cancel);

                                    if (IsDisposing || IsDisposed)
                                    {
                                        cancel?.Cancel();
                                        IsLoading = false;
                                        TraceLog($"[SkiaImage] Canceled disposed image {source}");
                                        return;
                                    }

                                    if (cancel.Token.IsCancellationRequested)
                                    {
                                        //Debug.WriteLine($"[SkiaImage] {Id} loading canceled for {url}");
                                        cancel?.Cancel();
                                        IsLoading = false;
                                        TraceLog($"[SkiaImage] Canceled already loaded {source}");
                                        if (bitmap != null && !SkiaImageManager.ReuseBitmaps)
                                        {
                                            DisposeObject(bitmap);
                                        }
                                        return;
                                    }

                                    if (bitmap != null)
                                    {
                                        CancelLoading?.Cancel();
                                        ImageBitmap = new LoadedImageSource(bitmap)
                                        {
                                            ProtectBitmapFromDispose = SkiaImageManager.ReuseBitmaps
                                        }; //at the end will use SetImage(new InstancedBitmap(bitmap));

                                        //TraceLog($"[SkiaImage] Loaded {source}");
                                        OnSuccess(uri);
                                        return;
                                    }

                                    TraceLog($"[SkiaImage] Error loading {url} as {source} for tag {Tag} ");

                                    //ClearBitmap(); //erase old image anyway even if EraseChangedContent is false
                                }
                                catch (TaskCanceledException)
                                {
                                }
                                catch (Exception e)
                                {
                                    Super.Log(e);
                                }
                                finally
                                {
                                    IsLoading = false;
                                }
                            }


                            await LoadAction();
                        }

                        OnError(url);
                    }
                    catch (Exception e)
                    {
                        TraceLog(e.Message);
                        OnError(url);
                    }
                };

                if (!LoadSourceOnFirstDraw)
                {
                    var action = LoadSource;
                    if (action != null)
                    {
                        if (action != null)
                        {
                            LoadSource = null;
                            Tasks.StartDelayedAsync(TimeSpan.FromMilliseconds(1),
                                async () => { await Task.Run(action); });
                        }
                    }
                }
                else
                {
                    Update();
                }
            }
            else
            {
                TraceLog($"[SkiaImage] Source already loaded {source}");

                OnSuccess(null);
            }
        }
    }

    /// <summary>
    /// Last source that we where loading. Td be reused for reload..
    /// </summary>
    public ImageSource LastSource { get; protected set; }

    public ImageSource ImageSource { get; protected set; }

    private static void NeedChangeColorFIlter(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaImage control)
        {
            control.InvalidateColorFilter();
            NeedDraw(bindable, oldvalue, newvalue);
        }
    }

    private static void NeedChangeImageFilter(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaImage control)
        {
            control.InvalidateImageFilter();
            NeedDraw(bindable, oldvalue, newvalue);
        }
    }

    public static readonly BindableProperty AddEffectProperty = BindableProperty.Create(
        nameof(AddEffect),
        typeof(SkiaImageEffect),
        typeof(SkiaImage),
        SkiaImageEffect.None,
        propertyChanged: NeedChangeColorFIlter);

    public SkiaImageEffect AddEffect
    {
        get { return (SkiaImageEffect)GetValue(AddEffectProperty); }
        set { SetValue(AddEffectProperty, value); }
    }

    public static readonly BindableProperty ColorTintProperty = BindableProperty.Create(
        nameof(ColorTint),
        typeof(Color),
        typeof(SkiaImage),
        Colors.Transparent,
        propertyChanged: NeedChangeColorFIlter);

    public Color ColorTint
    {
        get { return (Color)GetValue(ColorTintProperty); }
        set { SetValue(ColorTintProperty, value); }
    }

    public static readonly BindableProperty ZoomXProperty = BindableProperty.Create(
        nameof(ZoomX),
        typeof(double),
        typeof(SkiaImage),
        1.0,
        propertyChanged: NeedDraw);

    public double ZoomX
    {
        get { return (double)GetValue(ZoomXProperty); }
        set { SetValue(ZoomXProperty, value); }
    }

    public static readonly BindableProperty ZoomYProperty = BindableProperty.Create(
        nameof(ZoomY),
        typeof(double),
        typeof(SkiaImage),
        1.0,
        propertyChanged: NeedDraw);

    public double ZoomY
    {
        get { return (double)GetValue(ZoomYProperty); }
        set { SetValue(ZoomYProperty, value); }
    }

    public static readonly BindableProperty DarkenProperty = BindableProperty.Create(
        nameof(Darken),
        typeof(double),
        typeof(SkiaImage),
        5.0,
        propertyChanged: NeedChangeColorFIlter);

    public double Darken
    {
        get { return (double)GetValue(DarkenProperty); }
        set { SetValue(DarkenProperty, value); }
    }

    public static readonly BindableProperty LightenProperty = BindableProperty.Create(
        nameof(Lighten),
        typeof(double),
        typeof(SkiaImage),
        5.0,
        propertyChanged: NeedDraw);

    public double Lighten
    {
        get { return (double)GetValue(LightenProperty); }
        set { SetValue(LightenProperty, value); }
    }

    public static readonly BindableProperty ContrastProperty = BindableProperty.Create(
        nameof(Contrast),
        typeof(double),
        typeof(SkiaImage),
        1.0,
        propertyChanged: NeedChangeColorFIlter);

    public double Contrast
    {
        get { return (double)GetValue(ContrastProperty); }
        set { SetValue(ContrastProperty, value); }
    }

    public static readonly BindableProperty BrightnessProperty = BindableProperty.Create(
        nameof(Brightness),
        typeof(double),
        typeof(SkiaImage),
        1.0,
        propertyChanged: NeedChangeColorFIlter);

    public double Brightness
    {
        get { return (double)GetValue(BrightnessProperty); }
        set { SetValue(BrightnessProperty, value); }
    }

    public static readonly BindableProperty GammaProperty = BindableProperty.Create(
        nameof(Gamma),
        typeof(double),
        typeof(SkiaImage),
        1.0,
        propertyChanged: NeedChangeColorFIlter);

    public double Gamma
    {
        get { return (double)GetValue(GammaProperty); }
        set { SetValue(GammaProperty, value); }
    }

    public static readonly BindableProperty BlurProperty = BindableProperty.Create(
        nameof(Blur),
        typeof(double),
        typeof(SkiaImage),
        0.0,
        propertyChanged: NeedChangeImageFilter);

    public double Blur
    {
        get { return (double)GetValue(BlurProperty); }
        set { SetValue(BlurProperty, value); }
    }

    public static readonly BindableProperty SaturationProperty = BindableProperty.Create(
        nameof(Saturation),
        typeof(double),
        typeof(SkiaImage),
        0.0,
        propertyChanged: NeedChangeColorFIlter);

    public double Saturation
    {
        get { return (double)GetValue(SaturationProperty); }
        set { SetValue(SaturationProperty, value); }
    }

    public static readonly BindableProperty InflateAmountProperty = BindableProperty.Create(
        nameof(InflateAmount),
        typeof(double),
        typeof(SkiaImage),
        0.0,
        propertyChanged: NeedDraw);

    public double InflateAmount
    {
        get { return (double)GetValue(InflateAmountProperty); }
        set { SetValue(InflateAmountProperty, value); }
    }

    public static readonly BindableProperty UseGradientProperty = BindableProperty.Create(
        nameof(UseGradient),
        typeof(bool),
        typeof(SkiaImage),
        false,
        propertyChanged: NeedDraw);

    public bool UseGradient
    {
        get { return (bool)GetValue(UseGradientProperty); }
        set { SetValue(UseGradientProperty, value); }
    }

    public static readonly BindableProperty EraseChangedContentProperty = BindableProperty.Create(
        nameof(EraseChangedContent),
        typeof(bool),
        typeof(SkiaImage),
        false,
        propertyChanged: NeedDraw
    );

    /// <summary>
    /// Should we erase the existing image when another Source is set but wasn't applied yet (not loaded yet)
    /// </summary>
    public bool EraseChangedContent
    {
        get { return (bool)GetValue(EraseChangedContentProperty); }
        set { SetValue(EraseChangedContentProperty, value); }
    }

    public static readonly BindableProperty StartColorProperty = BindableProperty.Create(
        nameof(StartColor),
        typeof(Color),
        typeof(SkiaImage),
        Colors.DarkGray,
        propertyChanged: NeedDraw);

    public Color StartColor
    {
        get { return (Color)GetValue(StartColorProperty); }
        set { SetValue(StartColorProperty, value); }
    }

    public static readonly BindableProperty EndColorProperty = BindableProperty.Create(
        nameof(EndColor),
        typeof(Color),
        typeof(SkiaImage),
        Colors.Gray,
        propertyChanged: NeedDraw);

    public Color EndColor
    {
        get { return (Color)GetValue(EndColorProperty); }
        set { SetValue(EndColorProperty, value); }
    }

    public static readonly BindableProperty EffectBlendModeProperty = BindableProperty.Create(
        nameof(EffectBlendMode),
        typeof(SKBlendMode),
        typeof(SkiaImage),
        SKBlendMode.SrcIn,
        propertyChanged: NeedChangeColorFIlter);

    public SKBlendMode EffectBlendMode
    {
        get { return (SKBlendMode)GetValue(EffectBlendModeProperty); }
        set { SetValue(EffectBlendModeProperty, value); }
    }

    public static readonly BindableProperty HorizontalOffsetProperty = BindableProperty.Create(
        nameof(HorizontalOffset),
        typeof(double),
        typeof(SkiaImage),
        0.0,
        propertyChanged: NeedDraw);

    public double HorizontalOffset
    {
        get { return (double)GetValue(HorizontalOffsetProperty); }
        set { SetValue(HorizontalOffsetProperty, value); }
    }

    public static readonly BindableProperty VerticalOffsetProperty = BindableProperty.Create(
        nameof(VerticalOffset),
        typeof(double),
        typeof(SkiaImage),
        0.0,
        propertyChanged: NeedDraw);

    public double VerticalOffset
    {
        get { return (double)GetValue(VerticalOffsetProperty); }
        set { SetValue(VerticalOffsetProperty, value); }
    }

    public static readonly BindableProperty DrawWhenEmptyProperty = BindableProperty.Create(nameof(Tag),
        typeof(bool),
        typeof(SkiaImage),
        true, propertyChanged: NeedInvalidateMeasure);

    public bool DrawWhenEmpty
    {
        get { return (bool)GetValue(DrawWhenEmptyProperty); }
        set { SetValue(DrawWhenEmptyProperty, value); }
    }

    public static readonly BindableProperty SpriteHeightProperty = BindableProperty.Create(
        nameof(SpriteHeight),
        typeof(double),
        typeof(SkiaImage),
        0.0);

    public double SpriteHeight
    {
        get { return (double)GetValue(SpriteHeightProperty); }
        set { SetValue(SpriteHeightProperty, value); }
    }

    public static readonly BindableProperty SpriteWidthProperty = BindableProperty.Create(
        nameof(SpriteWidth),
        typeof(double),
        typeof(SkiaImage),
        0.0);

    public double SpriteWidth
    {
        get { return (double)GetValue(SpriteWidthProperty); }
        set { SetValue(SpriteWidthProperty, value); }
    }

    public static readonly BindableProperty SpriteIndexProperty = BindableProperty.Create(
        nameof(SpriteIndex),
        typeof(int),
        typeof(SkiaImage),
        -1);

    public int SpriteIndex
    {
        get { return (int)GetValue(SpriteIndexProperty); }
        set { SetValue(SpriteIndexProperty, value); }
    }

    #endregion

    public override bool CanDraw
    {
        get
        {
            if (Source == null)
            {
                return DrawWhenEmpty && base.CanDraw;
            }

            return base.CanDraw;
        }
    }

    bool _needClearBitmap;

    public virtual void ClearBitmap()
    {
        ImageBitmap = null;
        _needClearBitmap = true;
    }

    public virtual void OnError(string source)
    {
        HasError = true;
        Error?.Invoke(this, new ContentLoadedEventArgs(source));
    }

    public virtual void OnSuccess(string source)
    {
        HasError = false;
        Success?.Invoke(this, new ContentLoadedEventArgs(source));
    }

    protected virtual void SubscribeToCanReload()
    {
        SkiaImageManager.Instance.CanReload -= OnCanReloadSource;
        SkiaImageManager.Instance.CanReload += OnCanReloadSource;
    }

    private void OnCanReloadSource(object sender, EventArgs e)
    {
        ReloadSource();
    }

    public bool HasError
    {
        get => _hasError;
        protected set
        {
            if (value == _hasError) return;
            _hasError = value;
            OnPropertyChanged();
        }
    }

    public event EventHandler<ContentLoadedEventArgs> Error;
    public event EventHandler<ContentLoadedEventArgs> Success;

    #region RENDERiNG

    /// <summary>
    /// Reusing this
    /// </summary>
    protected SKPaint ImagePaint;

    /// <summary>
    /// Reusing this
    /// </summary>
    public SKImageFilter PaintImageFilter;

    //will reuse
    SKPath _preparedClipBounds = null;

    /// <summary>
    /// Reusing this
    /// </summary>
    protected SKColorFilter PaintColorFilter;

    protected bool NeedInvalidateImageFilter { get; set; }

    public virtual void InvalidateImageFilter()
    {
        NeedInvalidateImageFilter = true;
    }

    protected bool NeedInvalidateColorFilter { get; set; }

    public virtual void InvalidateColorFilter()
    {
        NeedInvalidateColorFilter = true;
    }

    protected override void Paint(DrawingContext ctx)
    {
        base.Paint(ctx);

        var source = LoadedSource;

        if (ImagePaint == null)
        {
            ImagePaint = new()
            {
                IsAntialias = true, IsDither = IsDistorted,
                //FilterQuality = SKFilterQuality.Medium
            };
        }

        if (NeedInvalidateImageFilter)
        {
            NeedInvalidateImageFilter = false;
            //var d = PaintImageFilter;
            PaintImageFilter = null;
            //d?.Dispose(); //might be used in double buffered!
        }

        if (NeedInvalidateColorFilter)
        {
            NeedInvalidateColorFilter = false;
            //var d = PaintColorFilter;
            PaintColorFilter = null;
            //d?.Dispose(); //might be used in double buffered!
        }

        if (source != null && !CheckIsGhost())
        {
            ImagePaint.ImageFilter = PaintImageFilter;
            ImagePaint.ColorFilter = PaintColorFilter;

            //ImageFilter
            if (PaintImageFilter == null && Blur > 0)
            {
                PaintImageFilter = SKImageFilter.CreateBlur((float)Blur, (float)Blur, SKShaderTileMode.Mirror);
            }

            ImagePaint.ImageFilter = PaintImageFilter;


            //ColorFilter
            if (PaintColorFilter == null)
            {
                PaintColorFilter = AddEffect switch
                {
                    SkiaImageEffect.Tint when ColorTint != Colors.Transparent
                        => SkiaImageEffects.Tint(ColorTint, EffectBlendMode),

                    SkiaImageEffect.Darken when Darken != 0.0
                        => SkiaImageEffects.Darken((float)Darken),

                    SkiaImageEffect.BlackAndWhite
                        => SkiaImageEffects.Grayscale(),

                    SkiaImageEffect.Pastel
                        => SkiaImageEffects.Pastel(),

                    SkiaImageEffect.Lighten when Lighten != 0.0
                        => SkiaImageEffects.Lighten((float)Lighten),

                    SkiaImageEffect.Sepia
                        => SkiaImageEffects.Sepia(),

                    SkiaImageEffect.InvertColors
                        => SkiaImageEffects.InvertColors(),

                    SkiaImageEffect.Gamma when Gamma >= 0
                        => SkiaImageEffects.Gamma((float)Gamma),

                    SkiaImageEffect.Contrast when Contrast >= 1.0
                        => SkiaImageEffects.Contrast((float)Contrast),

                    SkiaImageEffect.Saturation when Saturation >= 0
                        => SkiaImageEffects.Saturation((float)Saturation),

                    SkiaImageEffect.Brightness when Brightness >= 1.0
                        => SkiaImageEffects.Brightness((float)Brightness),

                    SkiaImageEffect.TSL when BackgroundColor != Colors.Transparent
                        => SkiaImageEffects.TintSL(BackgroundColor, (float)Saturation, (float)Brightness,
                            this.EffectBlendMode),

                    SkiaImageEffect.HSL when BackgroundColor != Colors.Transparent
                        => SkiaImageEffects.HSL((float)Gamma, (float)Saturation, (float)Brightness,
                            this.EffectBlendMode),

                    _ => null
                };
            }

            ImagePaint.ColorFilter = PaintColorFilter;

            TransformAspect stretch = Aspect;
            DrawImageAlignment horizontal = HorizontalAlignment;
            DrawImageAlignment vertical = VerticalAlignment;

            if (ClipSource)
            {
                _preparedClipBounds ??= new SKPath();
                _preparedClipBounds.Reset();
                _preparedClipBounds.AddRect(ctx.Destination);
                var restore = ctx.Context.Canvas.Save();
                ClipSmart(ctx.Context.Canvas, _preparedClipBounds);

                DrawSource(ctx, source, stretch, horizontal, vertical, ImagePaint);

                ctx.Context.Canvas.RestoreToCount(restore);
            }
            else
            {
                DrawSource(ctx, source, stretch, horizontal, vertical, ImagePaint);
            }
        }
    }

    public bool ClipSource = true;
    object lockDraw = new();
    private bool _hasError;
    private RescaledBitmap _scaledSource;
    private LoadedImageSource _loadedSource;

    public virtual void LoadSourceIfNeeded()
    {
        if (LoadSourceOnFirstDraw)
        {
            var action = LoadSource;
            if (action != null)
            {
                LoadSource = null;
                Tasks.StartDelayedAsync(TimeSpan.FromMilliseconds(1), async () => { await Task.Run(action); });
            }
        }
    }

    public override void Invalidate()
    {
        base.Invalidate();

        Update();
    }

    /// <summary>
    /// returns whether should abort pipeline
    /// </summary>
    /// <returns></returns>
    protected virtual bool SwapSources(float scale)
    {
        LoadSourceIfNeeded();

        var apply = ApplyNewSource;

        if (apply != null && apply != LoadedSource || _needClearBitmap)
        {
            ApplyNewSource = null;
            var kill = LoadedSource;
            LoadedSource = apply; //eventually clears bitmap
            _needClearBitmap = false;
            if (apply != null)
            {
                var source = LoadedSource;
                {
                    if (kill != null)
                    {
                        if (SkiaImageManager.ReuseBitmaps)
                            kill.Bitmap = null;
                        DisposeObject(kill);
                    }

                    if (NeedAutoSize)
                    {
                        Invalidate(); //resize on next frame
                        if (LoadedSource == null)
                            return true;
                    }

                    if (DrawingRect == SKRect.Empty || source == null)
                    {
                        NeedMeasure = true;
                    }
                    else
                    {
                        //fast insert new image into presized rect
                        SetAspectScale(source.Width, source.Height, DrawingRect, this.Aspect, scale);
                    }
                }
            }

            Update(); //gamechanger for doublebuffering and other complicated cases
        }

        return false;
    }

    protected override void Draw(DrawingContext context)
    {
        //until we implement 2-threads rendering this is needed for ImageDoubleBuffered cache rendering
        if (IsDisposing || IsDisposed)
            return;

        if (SwapSources(context.Scale))
            return;

        DrawUsingRenderObject(context, SizeRequest.Width, SizeRequest.Height);
    }

    protected override void OnLayoutChanged()
    {
        base.OnLayoutChanged();

        if (DrawingRect != SKRect.Empty && LoadSource != null)
        {
            SetAspectScale(LoadedSource.Width, LoadedSource.Height, DrawingRect, this.Aspect, RenderingScale);
        }
    }

    public override void OnDisposing()
    {
        LastSource = null;
        CancelLoading?.Cancel();
        ClearBitmap();
        ImagePaint?.Dispose();
        PaintColorFilter?.Dispose();
        PaintImageFilter?.Dispose();
        _preparedClipBounds?.Dispose();
        LoadedSource?.Dispose();
        ImagePaint = null;
        PaintColorFilter = null;
        PaintImageFilter = null;
        LoadedSource = null;
        ScaledSource?.Dispose();
        ScaledSource = null;

        base.OnDisposing();
    }

    public SKPoint AspectScale { get; protected set; }

    public RescaledBitmap ScaledSource
    {
        get => _scaledSource;
        protected set { _scaledSource = value; }
    }

    public class RescaledBitmap : IDisposable
    {
        public SKBitmap Bitmap { get; set; }

        public SKImage _image;

        public SKImage Image
        {
            get
            {
                if (_image == null && Bitmap != null)
                {
                    _image = SKImage.FromBitmap(Bitmap);
                }
                return _image;
            }
        }

        public SKFilterQuality Quality { get; set; }
        public Guid Source { get; set; }

        public void Dispose()
        {
            Bitmap?.Dispose();
            _image?.Dispose();
        }
    }
 
 
    /// <summary>
    /// Gets sampling options based on quality level
    /// </summary>
    /// <param name="quality">Quality level from user selection</param>
    /// <param name="isUpscaling">Whether the operation is upscaling</param>
    /// <returns>Appropriate SKSamplingOptions</returns>
    public static SKSamplingOptions GetSamplingOptions(SKFilterQuality quality, bool isUpscaling)
    {
        return quality switch
        {
            SKFilterQuality.None => new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None),

            SKFilterQuality.Low => new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None),

            SKFilterQuality.Medium => new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Nearest),

            SKFilterQuality.High => isUpscaling
                ? new SKSamplingOptions(SKCubicResampler.Mitchell)  // Bicubic for upscaling
                : new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), // Better for downscaling

            _ => new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None)
        };
    }

    /// <summary>
    /// Updated DrawSource with professional-quality resizing
    /// </summary>
    protected virtual void DrawSource(
        DrawingContext ctx,
        LoadedImageSource source,
        TransformAspect stretch,
        DrawImageAlignment horizontal = DrawImageAlignment.Center,
        DrawImageAlignment vertical = DrawImageAlignment.Center,
        SKPaint paint = null)
    {
        try
        {
            if (AspectScale == SKPoint.Empty)
            {
                throw new ApplicationException("AspectScale is not set");
            }

            var dest = ctx.Destination;
            var scale = ctx.Scale;

            var aspectScaleX = AspectScale.X * (float)(ZoomX);
            var aspectScaleY = AspectScale.Y * (float)(ZoomY);

            SKRect display = CalculateDisplayRect(dest,
                aspectScaleX * source.Width, aspectScaleY * source.Height,
                horizontal, vertical);

            display.Inflate(new SKSize((float)InflateAmount, (float)InflateAmount));
            display.Offset((float)Math.Round(scale * HorizontalOffset), (float)Math.Round(scale * VerticalOffset));

            TextureScale = new(dest.Width / display.Width, dest.Height / display.Height);

            if (this.RescalingQuality != SKFilterQuality.None)
            {
                var targetWidth = (int)Math.Round(display.Width);
                var targetHeight = (int)Math.Round(display.Height);

                // Avoid rescaling if target size is very close to source size
                var sizeRatio = Math.Abs(targetWidth - source.Width) / (float)source.Width;
                if (sizeRatio < 0.05f) // Less than 5% difference
                {
                    // Skip rescaling for minimal size changes
                    if (source.Bitmap != null)
                    {
                        ctx.Context.Canvas.DrawBitmap(source.Bitmap, display, paint);
                    }
                    else if (source.Image != null)
                    {
                        ctx.Context.Canvas.DrawImage(source.Image, display, paint);
                    }
                    return;
                }

                if (ScaledSource == null
                    || ScaledSource.Source != source.Id
                    || ScaledSource.Quality != this.RescalingQuality 
                    || ScaledSource.Bitmap.Width != targetWidth
                    || ScaledSource.Bitmap.Height != targetHeight)
                {
                    SKBitmap bitmapToResize = null;
                    bool needsDispose = false;

                    if (source.Bitmap != null)
                    {
                        bitmapToResize = source.Bitmap;
                    }
                    else if (source.Image != null)
                    {
                        bitmapToResize = SKBitmap.FromImage(source.Image);
                        needsDispose = true;
                    }

                    if (bitmapToResize != null)
                    {
                        var targetSize = new SKSizeI(targetWidth, targetHeight);
                        bool isUpscaling = targetWidth > source.Width || targetHeight > source.Height;

                        SKBitmap resizedBmp = bitmapToResize.Resize(targetSize,
                            GetSamplingOptions(RescalingQuality, isUpscaling));

                        var kill = ScaledSource;
                        ScaledSource = new() { Source = source.Id, Bitmap = resizedBmp, Quality = RescalingQuality };
                        kill?.Dispose();

                        if (needsDispose)
                        {
                            bitmapToResize.Dispose();
                        }
                    }
                }

                if (ScaledSource != null)
                {
                    ctx.Context.Canvas.DrawBitmap(ScaledSource.Bitmap, display, paint);
                }
            }
            else
            {
                if (source.Bitmap != null)
                {
                    ctx.Context.Canvas.DrawBitmap(source.Bitmap, display, paint);
                }
                else if (source.Image != null)
                {
                    ctx.Context.Canvas.DrawImage(source.Image, display, paint);
                }
            }
        }
        catch (Exception e)
        {
            Trace.WriteLine(e);
        }
    }

    public SKPoint TextureScale { get; protected set; }
    public ScaledRect SourceImageSize { get; protected set; }

    protected void SetAspectScale(int pxWidth, int pxHeight, SKRect dest, TransformAspect stretch, float scale)
    {
        var scaled = RescaleAspect(pxWidth, pxHeight, dest, stretch);

        SourceImageSize = ScaledRect.FromPixels(new SKRect(0, 0, pxWidth, pxHeight), scale);

        SourceWidth = SourceImageSize.Units.Width;
        SourceHeight = SourceImageSize.Units.Height;

        AspectScale = new SKPoint(scaled.X, scaled.Y);
    }

    protected override ScaledSize SetMeasuredAsEmpty(float scale)
    {
        AspectScale = SKPoint.Empty;

        return base.SetMeasuredAsEmpty(scale);
    }

    //public override void Arrange(SKRect destination, float widthRequest, float heightRequest, float scale)
    //{
    //    if (IsDisposed || !CanDraw)
    //        return;

    //    if (NeedMeasure)
    //    {
    //        var adjustedDestination = CalculateLayout(destination, widthRequest, heightRequest, scale);
    //        Measure(adjustedDestination.Width, adjustedDestination.Height, scale);
    //    }

    //    base.Arrange(destination, MeasuredSize.Units.Width, MeasuredSize.Units.Height, scale);
    //}

    public override ScaledSize OnMeasuring(float widthRequest, float heightRequest, float dscale)
    {
        //background measuring or invisible or self measure from draw because layout will never pass -1
        if (IsMeasuring || !CanDraw)
        {
            return MeasuredSize;
        }

        if (SwapSources(dscale))
            return MeasuredSize;

        var request = CreateMeasureRequest(widthRequest, heightRequest, dscale);
        if (request.IsSame && !NeedAutoSize)
        {
            return MeasuredSize;
        }

        if (request.WidthRequest == 0 || request.HeightRequest == 0)
        {
            InvalidateCacheWithPrevious();

            return SetMeasuredAsEmpty(request.Scale);
        }

        var widthConstraint = request.WidthRequest;
        var heightConstraint = request.HeightRequest;

        if ((float.IsInfinity(widthConstraint) || float.IsInfinity(heightConstraint)) && LoadedSource != null)
        {
            var aspect = LoadedSource.Width / (double)LoadedSource.Height;
            if (widthConstraint > 0)
            {
                heightConstraint = (float)(widthConstraint / aspect);
            }
            else if (heightConstraint > 0)
            {
                widthConstraint = (float)(heightConstraint * aspect);
            }
        }

        if (widthConstraint < 0 || heightConstraint < 0)
        {
            //set auto-size from image using newly image dimensions
            //this is the case of one dimension being Fill or explicit and the other being Auto (-1)
            if (LoadedSource != null && (widthConstraint > 0 || heightConstraint > 0))
            {
                var aspect = LoadedSource.Width / (double)LoadedSource.Height;
                if (widthConstraint > 0)
                {
                    heightConstraint = (float)(widthConstraint / aspect);
                }
                else if (heightConstraint > 0)
                {
                    widthConstraint = (float)(heightConstraint * aspect);
                }
                //Invalidate(); //remeasure us - disabled works superfine so far!!!
            }
            else
            {
                //not setting NeedMeasure=false; to force remeasurement on next frame
                AspectScale = SKPoint.Empty;

                return ScaledSize.Default;
                //return ScaledSize.FromPixels(0, 0, request.Scale);
            }
        }

        if (widthConstraint == 0 || heightConstraint == 0)
        {
            InvalidateCacheWithPrevious();

            return SetMeasuredAsEmpty(request.Scale);
        }

        var constraints = GetMeasuringConstraints(new(widthConstraint, heightConstraint, request.Scale));

        //we measured no children, simulated !
        ContentSize = ScaledSize.FromPixels(constraints.Content.Width, constraints.Content.Height, request.Scale);

        //return SetMeasuredAdaptToContentSize(constraints, request.Scale);

        var width = AdaptWidthConstraintToContentRequest(constraints, ContentSize.Pixels.Width,
            HorizontalOptions.Expands);
        var height =
            AdaptHeightConstraintToContentRequest(constraints, ContentSize.Pixels.Height, VerticalOptions.Expands);

        if (LoadedSource != null)
        {
            try
            {
                SetAspectScale(LoadedSource.Width, LoadedSource.Height, constraints.Content, this.Aspect,
                    request.Scale);

                if (NeedAutoHeight)
                {
                    var newValue = SourceImageSize.Pixels.Height * this.AspectScale.Y;
                    height = AdaptHeightConstraintToContentRequest(constraints, newValue, VerticalOptions.Expands);
                }

                if (NeedAutoWidth)
                {
                    var newValue = SourceImageSize.Pixels.Width * this.AspectScale.X;
                    width = AdaptWidthConstraintToContentRequest(constraints, newValue, HorizontalOptions.Expands);
                }
            }
            catch (Exception e)
            {
                Super.Log(e);
                AspectScale = SKPoint.Empty;
            }
        }
        else
        {
            AspectScale = SKPoint.Empty;
        }

        return SetMeasured(width, height, false, false, request.Scale);
    }

    #endregion

    /// <summary>
    /// From current set Source in points
    /// </summary>
    public float SourceWidth { get; protected set; }

    /// <summary>
    /// From current set Source in points
    /// </summary>
    public float SourceHeight { get; protected set; }

    public static SKRect CalculateDisplayRect(SKRect dest,
        float destWidth, float destHeight,
        DrawImageAlignment horizontal, DrawImageAlignment vertical)
    {
        float x = 0;
        float y = 0;

        switch (horizontal)
        {
            case DrawImageAlignment.Center:
                x = (dest.Width - destWidth) / 2.0f;
                break;

            case DrawImageAlignment.Start:
                break;

            case DrawImageAlignment.End:
                x = dest.Width - destWidth;
                break;
        }

        switch (vertical)
        {
            case DrawImageAlignment.Center:
                y = (dest.Height - destHeight) / 2.0f;
                break;

            case DrawImageAlignment.Start:
                break;

            case DrawImageAlignment.End:
                y = dest.Height - destHeight;
                break;
        }

        x += dest.Left;
        y += dest.Top;

        return new SKRect(x, y, x + destWidth, y + destHeight);
    }
}
