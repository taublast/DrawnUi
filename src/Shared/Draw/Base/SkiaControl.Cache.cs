using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using HarfBuzzSharp;

namespace DrawnUi.Draw;

public partial class SkiaControl
{
    private readonly LimitedQueue<Action> _offscreenCacheRenderingQueue = new(1);

    public static readonly BindableProperty UseCacheProperty = BindableProperty.Create(nameof(UseCache),
        typeof(SkiaCacheType),
        typeof(SkiaControl),
        SkiaCacheType.None,
        propertyChanged: NeedDraw);

    /// <summary>
    /// Never reuse the rendering result. Actually true for ScrollLooped SkiaLayout viewport container to redraw its content several times for creating a looped aspect.
    /// </summary>
    public SkiaCacheType UseCache
    {
        get { return (SkiaCacheType)GetValue(UseCacheProperty); }
        set { SetValue(UseCacheProperty, value); }
    }

    public static readonly BindableProperty AllowCachingProperty = BindableProperty.Create(nameof(AllowCaching),
        typeof(bool),
        typeof(SkiaControl),
        true,
        propertyChanged: NeedDraw);

    /// <summary>
    /// Might want to set this to False for certain cases.
    /// </summary>
    public bool AllowCaching
    {
        get { return (bool)GetValue(AllowCachingProperty); }
        set { SetValue(AllowCachingProperty, value); }
    }

    /// <summary>
    /// Used by the UseCacheDoubleBuffering process. 
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public CachedObject RenderObjectPrevious
    {
        get { return _renderObjectPrevious; }
        set
        {
            RenderObjectNeedsUpdate = false;
            if (_renderObjectPrevious != value)
            {
                var kill = _renderObjectPrevious;
                _renderObjectPrevious = value;
                if (kill != null
                    && UsingCacheType != SkiaCacheType.Image
                    && UsingCacheType == SkiaCacheType.ImageComposite)
                {
                    //kill.Dispose();
                    DisposeObject(kill);
                }
            }
        }
    }

    CachedObject _renderObjectPrevious;


    /// <summary>
    /// The cached representation of the control. Will be used on redraws without calling Paint etc, until the control is requested to be updated.
    /// </summary>
    //[EditorBrowsable(EditorBrowsableState.Never)]
    public CachedObject RenderObject
    {
        get { return _renderObject; }
        set
        {
            RenderObjectNeedsUpdate = false;
            if (_renderObject != value)
            {
                //lock both RenderObjectPrevious and RenderObject
                lock (LockDraw)
                {
                    if (_renderObject != null) //if we already have something in actual cache then
                    {
                        if (UsesCacheDoubleBuffering
                            //|| UsingCacheType == SkiaCacheType.Image //to just reuse same surface
                            || UsingCacheType == SkiaCacheType.ImageComposite)
                        {
                            RenderObjectPrevious = _renderObject; //send it to back for special cases
                        }
                        else
                        {
                            DisposeObject(_renderObject);
                        }
                    }

                    _renderObject = value;
                    OnPropertyChanged();

                    if (value != null)
                        OnCacheCreated();
                    else
                        OnCacheDestroyed();

                    Monitor.PulseAll(LockDraw);
                }
            }
        }
    }

    CachedObject _renderObject;

    protected virtual void OnCacheCreated()
    {
        CreatedCache?.Invoke(this, RenderObject);
    }

    protected virtual void OnCacheDestroyed()
    {
    }

    /// <summary>
    /// Indended to prohibit background rendering, useful for streaming controls like camera, gif etc. SkiaBackdrop has it set to True as well.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public virtual bool CanUseCacheDoubleBuffering => true;

    /// <summary>
    /// Read-only computed flag for internal use.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool UsesCacheDoubleBuffering
    {
        get
        {
            return CanUseCacheDoubleBuffering
                   && Super.Multithreaded
                   //&& Parent is SkiaControl
                   || UsingCacheType == SkiaCacheType.ImageDoubleBuffered;
        }
    }

    public event EventHandler<CachedObject> CreatedCache;

    public void DestroyRenderingObject()
    {
        RenderObject = null;
    }

    /// <summary>
    /// Technical optional method for some custom logic. Actually used by SkiaMap only.
    /// </summary>
    /// <param name="ctx"></param>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="cache"></param>
    public void DrawRenderObject(DrawingContext ctx, float x, float y, CachedObject cache = null)
    {
        cache ??= RenderObject;
        if (cache == null)
            return;
        var destinationRect = new SKRect(x, y, x + cache.Bounds.Width, y + cache.Bounds.Height);
        var context = AddPaintArguments(ctx).WithDestination(destinationRect);
        DrawRenderObjectInternal(context, cache);
    }

    public bool IsRenderObjectValid(SKSize size, CachedObject cache = null)
    {
        cache ??= RenderObject;

        if (cache == null || !CompareSize(cache.Bounds.Size, size, 1))
            return false;

        return true;
    }

    /// <summary>
    /// Technical optional method for some custom logic. Will create as SKImage or SKPicture (asOperations=true).
    /// WIll not affect current RenderObject property.
    /// </summary>
    /// <param name="ctx"></param>
    /// <param name="area"></param>
    /// <param name="asOperations"></param>
    /// <returns></returns>
    public CachedObject CreateRenderedObject(DrawingContext ctx, SKRect area, bool asOperations)
    {
        var usingCacheType = asOperations ? SkiaCacheType.Operations : SkiaCacheType.Image;

        var renderObject = CreateRenderingObject(ctx, area, null, usingCacheType,
            (context) =>
            {
                PaintWithEffects(context.WithDestination(area));
            });

        return renderObject;
    }

    public void RasterizeToCache(DrawingContext ctx)
    {
        SkiaControl control = this;

        var destination = new SKRect(0, 0, float.PositiveInfinity, float.PositiveInfinity);

        var measuredSize = control.Measure(destination.Width, destination.Height, ctx.Scale);
        var size = measuredSize.Pixels;

        control.Arrange(
            new SKRect(0, 0, size.Width, size.Height),
            size.Width, size.Height, ctx.Scale);

        control.RenderObject = control.CreateRenderedObject(ctx, control.DrawingRect, false);
    }

    protected virtual bool CheckCachedObjectValid(CachedObject cache, SKRect recordingArea, SkiaDrawingContext context)
    {
        if (cache != null)
        {
            if (!CompareSize(cache.RecordingArea.Size, recordingArea.Size, 1))
            {
                CacheValidity = CacheValidityType.SizeMismatch;
                return false;
            }

            //check hardware context maybe changed
            if (UsingCacheType == SkiaCacheType.GPU && cache.Surface != null &&
                cache.Surface.Context != null &&
                context.Superview?.CanvasView is SkiaViewAccelerated hardware)
            {
                //hardware context might change if we returned from background..
                if (hardware.GRContext == null || cache.Surface.Context == null
                                               || (int)hardware.GRContext.Handle != (int)cache.Surface.Context.Handle)
                {
                    CacheValidity = CacheValidityType.GraphicContextMismatch;
                    return false;
                }
            }

            CacheValidity = CacheValidityType.Valid;
            return true;
        }

        CacheValidity = CacheValidityType.Missing;
        return false;
    }

    public virtual SkiaCacheType UsingCacheType
    {
        get
        {
            if (!AllowCaching || !Super.CacheEnabled)
            {
                return SkiaCacheType.None;
            }

            if (CanUseCacheDoubleBuffering && Super.Multithreaded)
            {
                //if (Parent is SkiaControl)
                {
                    if (UseCache == SkiaCacheType.None)
                        return SkiaCacheType.OperationsFull;
                }

                if (UseCache == SkiaCacheType.ImageDoubleBuffered || UseCache == SkiaCacheType.GPU)
                    return SkiaCacheType.Image;

                if (UseCache == SkiaCacheType.ImageComposite)
                    return SkiaCacheType.Operations;
            }

            if (UseCache == SkiaCacheType.GPU && !Super.GpuCacheEnabled)
                return SkiaCacheType.Image;

            //if (EffectPostRenderer != null 
            //    && (UseCache == SkiaCacheType.None || UseCache == SkiaCacheType.Operations))
            //    return SkiaCacheType.Image;

            if (UseCache == SkiaCacheType.None && CanUseCacheDoubleBuffering && Super.Multithreaded &&
                Parent is SkiaControl)
                return SkiaCacheType.Operations;

            return UseCache;
        }
    }

    public virtual CachedObject CreateRenderingObject(
        DrawingContext context,
        SKRect recordingArea,
        CachedObject reuseSurfaceFrom,
        SkiaCacheType usingCacheType,
        Action<DrawingContext> action)
    {
        if (recordingArea.Height == 0 || recordingArea.Width == 0 || IsDisposed || IsDisposing)
        {
            return null;
        }

        CachedObject renderObject = null;

        try
        {
            var recordArea = GetCacheArea(recordingArea);

            //if (UsingCacheType == SkiaCacheType.OperationsFull)
            //{
            //    recordArea = destination;
            //}

            NeedUpdate =
                false; //if some child changes this while rendering to cache we will erase resulting RenderObject

            //GRContext grContext = null;

            if (usingCacheType == SkiaCacheType.Operations || usingCacheType == SkiaCacheType.OperationsFull)
            {
                var cacheRecordingArea = GetCacheRecordingArea(recordingArea);

                using (var recorder = new SKPictureRecorder())
                {
                    var recordingContext = context.CreateForRecordingOperations(recorder, cacheRecordingArea);

                    action(recordingContext);

                    var skPicture = recorder.EndRecording();
                    renderObject = new(UsingCacheType, skPicture, context.Destination, cacheRecordingArea);
                }
            }
            else if (usingCacheType != SkiaCacheType.None)
            {
                var width = (int)recordArea.Width;
                var height = (int)recordArea.Height;

                bool needCreateSurface = !CheckCachedObjectValid(reuseSurfaceFrom, recordingArea, context.Context) ||
                                         usingCacheType == SkiaCacheType.GPU; //never reuse GPU surfaces

                SKSurface surface = null;

                if (!needCreateSurface)
                {
                    //reusing existing surface
                    surface = reuseSurfaceFrom.Surface;
                    if (surface == null)
                    {
                        return null; //would be unexpected
                    }

                    if (usingCacheType != SkiaCacheType.ImageComposite)
                        surface.Canvas.Clear();
                }
                else
                {
                    var kill = surface;

                    var cacheSurfaceInfo = new SKImageInfo(width, height);

                    if (usingCacheType == SkiaCacheType.GPU)
                    {
                        if (context.Context.Superview != null && context.Context.Superview?.CanvasView is
                                                                  SkiaViewAccelerated accelerated
                                                              && accelerated.GRContext != null)
                        {
                            //grContext = accelerated.GRContext;
                            //hardware accelerated
                            surface = SKSurface.Create(accelerated.GRContext,
                                true,
                                cacheSurfaceInfo);
                        }
                    }

                    if (surface == null) //fallback if gpu failed
                    {
                        //non-gpu
                        surface = SKSurface.Create(cacheSurfaceInfo);
                    }

                    if (kill!=null && kill != surface)
                        DisposeObject(kill);

                    // if (usingCacheType == SkiaCacheType.GPU)
                    //     surface.Canvas.Clear(SKColors.Red);
                }

                if (surface == null)
                {
                    return null; //would be totally unexpected
                }

                var recordingContext = context.CreateForRecordingImage(surface, recordArea.Size);

                recordingContext.Context.IsRecycled = !needCreateSurface;

                // Translate the canvas to start drawing at (0,0)

                recordingContext.Context.Canvas.Translate(-recordArea.Left, -recordArea.Top);

                //create empty NODE just for children to attach to
                if (Super.UseFrozenVisualLayers)
                {
                    VisualLayer = VisualLayer.CreateEmpty();
                }

                // Perform the drawing action
                action(recordingContext);

                //surface.Canvas.Flush();

                recordingContext.Context.Canvas.Translate(recordArea.Left, recordArea.Top);
                recordingContext.Context.Canvas.Flush();

                renderObject = new(usingCacheType, surface, recordArea, recordArea)
                {
                    SurfaceIsRecycled = recordingContext.Context.IsRecycled
                };

                if (Super.UseFrozenVisualLayers && VisualLayer != null)
                {
                    var childrenNodes = this.VisualLayer.Children;
                    renderObject.Children = childrenNodes;
                }
            }


            //else we landed here with no cache type at all..
        }
        catch (Exception e)
        {
            Super.Log(e);
        }

        return renderObject;
    }

    public Action<DrawingContext, CachedObject> DelegateDrawCache { get; set; }


    public virtual void DrawRenderObjectInternal(
        DrawingContext ctx,
        CachedObject cache)
    {
        var destination = ctx.Destination;
        // New absolute offsets
        destination.Offset((float)(Left * ctx.Scale), (float)(Top * ctx.Scale));

        if (DelegateDrawCache != null)
        {
            DelegateDrawCache(ctx.WithDestination(destination), cache);
        }
        else
        {
            DrawRenderObject(ctx.WithDestination(destination), cache);
        }

        if (Super.UseFrozenVisualLayers && VisualLayer!=null && cache.Children != null)
        {
            this.VisualLayer.AttachFromCache(cache);
        }
    }


    public bool IsCacheImage
    {
        get
        {
            var cache = UsingCacheType;
            return cache == SkiaCacheType.Image
                   || cache == SkiaCacheType.GPU
                   || cache == SkiaCacheType.ImageComposite
                   || cache == SkiaCacheType.ImageDoubleBuffered;
        }
    }

    public bool IsCacheOperations
    {
        get
        {
            var cache = UsingCacheType;
            return cache == SkiaCacheType.Operations
                   || cache == SkiaCacheType.OperationsFull;
        }
    }


    protected virtual bool UseRenderingObject(DrawingContext context, SKRect recordArea)
    {
        lock (LockDraw) //prevent conflicts with erasing cache after we decided to use it
        {
            var cache = RenderObject;
            var cacheOffscreen = RenderObjectPrevious;

            if (RenderObjectPrevious != null && RenderObjectPreviousNeedsUpdate)
            {
                var kill = RenderObjectPrevious;
                RenderObjectPrevious = null;
                RenderObjectPreviousNeedsUpdate = false;

                if (kill != null)
                {
                    DisposeObject(kill);
                }
            }

            if (cache != null)
            {
                //CacheValidity will be set by CheckCachedObjectValid
                if (!UsesCacheDoubleBuffering && !CheckCachedObjectValid(cache, recordArea, context.Context))
                {
                    return false;
                }

                //draw existing front cache
                lock (LockDraw)
                {
                    DrawRenderObjectInternal(context, cache);
                    Monitor.PulseAll(LockDraw);
                }

                if (!UsesCacheDoubleBuffering || !NeedUpdateFrontCache)
                {
                    return true;
                }
            }
            else
            {
                CacheValidity = CacheValidityType.Missing;
            }

            if (UsesCacheDoubleBuffering)
            {
                lock (LockDraw)
                {
                    if (cache == null && cacheOffscreen != null)
                    {
                        DrawRenderObjectInternal(context, cacheOffscreen);
                    }

                    Monitor.PulseAll(LockDraw);
                }

                NeedUpdateFrontCache = false;

                var clone = AddPaintArguments(context);
                PushToOffscreenRendering(() =>
                {
                    //will be executed on background thread in parallel
                    var oldObject = RenderObjectPreparing;
                    RenderObjectPreparing = CreateRenderingObject(clone, recordArea, oldObject, UsingCacheType,
                        (ctx) => { PaintWithEffects(ctx); });
                    RenderObject = RenderObjectPreparing;
                    _renderObjectPreparing = null;

                    if (Parent != null && Parent.UpdateLocks < 1)
                    {
                        Parent?.UpdateByChild(this); //repaint us
                    }
                });

                return !NeedUpdateFrontCache;
            }

            return false;
        }
    }

    public CacheValidityType CacheValidity { get; protected set; }

    public enum CacheValidityType
    {
        Valid,
        Missing,
        SizeMismatch,
        GraphicContextMismatch
    }

    public Action GetOffscreenRenderingAction()
    {
        var action = _offscreenCacheRenderingQueue.Pop();
        return action;
    }

    public void PushToOffscreenRendering(Action action, CancellationToken cancel = default)
    {
        if (Super.OffscreenRenderingAtCanvasLevel)
        {
            _offscreenCacheRenderingQueue.Push(action);
            Superview?.PushToOffscreenRendering(this, cancel);
        }
        else
        {
            _offscreenCacheRenderingQueue.Push(action);
            if (!_processingOffscrenRendering)
            {
                _processingOffscrenRendering = true;
                Task.Run(async () => { await ProcessOffscreenCacheRenderingAsync(); }, cancel).ConfigureAwait(false);
            }
        }
    }


    private bool _processingOffscrenRendering = false;
    protected SemaphoreSlim semaphoreOffsecreenProcess = new(1);

    public async Task ProcessOffscreenCacheRenderingAsync()
    {
        await semaphoreOffsecreenProcess.WaitAsync();

        if (_offscreenCacheRenderingQueue.Count == 0)
            return;

        _processingOffscrenRendering = true;

        try
        {
            Action action = _offscreenCacheRenderingQueue.Pop();
            while (!IsDisposed && !IsDisposing && action != null)
            {
                try
                {
                    action.Invoke();

                    if (_offscreenCacheRenderingQueue.Count > 0)
                        action = _offscreenCacheRenderingQueue.Pop();
                    else
                        break;
                }
                catch (Exception e)
                {
                    Super.Log(e);
                }
            }

            //if (NeedUpdate || RenderObjectNeedsUpdate) //someone changed us while rendering inner content
            //{
            //    Update(); //kick
            //}
        }
        finally
        {
            _processingOffscrenRendering = false;
            semaphoreOffsecreenProcess.Release();
        }
    }


    /// <summary>
    /// Used by the UseCacheDoubleBuffering process. This is the new cache beign created in background. It will be copied to RenderObject when ready.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public CachedObject RenderObjectPreparing
    {
        get { return _renderObjectPreparing; }
        set
        {
            RenderObjectNeedsUpdate = false;
            if (_renderObjectPreparing != value)
            {
                _renderObjectPreparing = value;
            }
        }
    }

    CachedObject _renderObjectPreparing;


    /// <summary>
    /// Used by ImageDoubleBuffering cache
    /// </summary>
    protected bool NeedUpdateFrontCache
    {
        get => _needUpdateFrontCache;
        set => _needUpdateFrontCache = value;
    }


    private bool _RenderObjectPreviousNeedsUpdate;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool RenderObjectPreviousNeedsUpdate
    {
        get { return _RenderObjectPreviousNeedsUpdate; }
        set
        {
            if (_RenderObjectPreviousNeedsUpdate != value)
            {
                _RenderObjectPreviousNeedsUpdate = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Should delete RenderObject when starting new frame rendering
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool RenderObjectNeedsUpdate
    {
        get { return _renderObjectNeedsUpdate; }

        set
        {
            if (_renderObjectNeedsUpdate != value)
            {
                _renderObjectNeedsUpdate = value;
            }
        }
    }

    bool _renderObjectNeedsUpdate = true;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void InvalidateCache()
    {
        RenderObjectNeedsUpdate = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void InvalidateCacheWithPrevious()
    {
        InvalidateCache();

        if (UsingCacheType == SkiaCacheType.ImageComposite)
        {
            RenderObjectPreviousNeedsUpdate = true;
        }
    }


    /// <summary>
    /// Used for the Operations cache type to record inside the changed area, if your control is not inside the DrawingRect due to transforms/translations. This is NOT changing the rendering object 
    /// </summary>
    protected virtual SKRect GetCacheRecordingArea(SKRect drawingRect)
    {
        return drawingRect;
    }

    /// <summary>
    /// Normally cache is recorded inside DrawingRect, but you might want to expand this to include shadows around, for example.
    /// </summary>
    protected virtual SKRect GetCacheArea(SKRect value)
    {
        var dirty = value;
        if (ExpandDirtyRegion != Thickness.Zero)
        {
            dirty = new(
                value.Left - (float)Math.Round(ExpandDirtyRegion.Left * RenderingScale),
                value.Top - (float)Math.Round(ExpandDirtyRegion.Top * RenderingScale),
                value.Right + (float)Math.Round(ExpandDirtyRegion.Right * RenderingScale),
                value.Bottom + (float)Math.Round(ExpandDirtyRegion.Bottom * RenderingScale)
            );
        }

        return dirty;
    }

    /// <summary>
    /// Returns true if had drawn.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="widthRequest"></param>
    /// <param name="heightRequest"></param>
    /// <param name="destination"></param>
    /// <param name="scale"></param>
    /// <returns></returns>
    public virtual bool DrawUsingRenderObject(DrawingContext context,
        float widthRequest, float heightRequest)
    {
        if (IsDisposed || IsDisposing || !IsVisible)
            return false;

        Arrange(context.Destination, widthRequest, heightRequest, context.Scale);

        bool willDraw = !CheckIsGhost();
        if (willDraw)
        {
            if (UsingCacheType != SkiaCacheType.None)
            {
                var destination = DrawingRect;
                var recordArea = destination;
                if (UsingCacheType == SkiaCacheType.OperationsFull)
                {
                    recordArea = context.Context.Canvas.LocalClipBounds;
                }

                //paint from cache
                var clone = AddPaintArguments(context).WithDestination(destination);
                if (!UseRenderingObject(clone, recordArea))
                {
                    //record to cache and paint 
                    if (UsesCacheDoubleBuffering)
                    {
                        //use cloned struct in another thread 
                        PushToOffscreenRendering(() =>
                        {
                            //will be executed on background thread in parallel
                            var oldObject = RenderObjectPreparing;
                            RenderObjectPreparing = CreateRenderingObject(clone, recordArea, oldObject, UsingCacheType,
                                (ctx) => { PaintWithEffects(ctx); });
                            RenderObject = RenderObjectPreparing;
                            _renderObjectPreparing = null;

                            if (Parent != null && Parent.UpdateLocks < 1)
                            {
                                Parent?.UpdateByChild(this); //repaint us
                            }
                        });
                    }
                    else
                    {
                        CreateRenderingObjectAndPaint(clone, recordArea,
                            (ctx) => { PaintWithEffects(ctx.WithDestination(DrawingRect)); });
                    }
                }
            }
            else
            {
                // NO CACHE, DIRECT PAINT
                DrawDirectInternal(context, DrawingRect);
            }
        }

        FinalizeDrawingWithRenderObject(context); //NeedUpdate will go false

        return willDraw;
    }

    public virtual VisualLayer? PrepareNode(DrawingContext context, float widthRequest, float heightRequest)
    {
        //this will measure too if needed including deeper
        Arrange(context.Destination, widthRequest, heightRequest, context.Scale);

        bool willDraw = !CheckIsGhost();
        if (willDraw)
        {
            CreateTransformationMatrix(context.Context, DrawingRect);
            var node = CreateRenderedNode(DrawingRect, context.Scale);

            //note UsesCacheDoubleBuffering is going deprecated with 2 passes rendering tree
            //and new logic for ImageCacheComposite needs to be implemented for nodes
            if (UsingCacheType != SkiaCacheType.None)
            {
                var destination = DrawingRect;
                var recordArea = destination;
                if (UsingCacheType == SkiaCacheType.OperationsFull)
                {
                    recordArea = context.Context.Canvas.LocalClipBounds;
                }

                var clone = AddPaintArguments(context).WithDestination(destination);
                CachedObject cache = null;
                if (RenderObject == null || !CheckCachedObjectValid(RenderObject, recordArea, clone.Context))
                {
                    cache = CreateRenderingObject(clone, recordArea, RenderObjectPrevious, UsingCacheType,
                        (ctx) => { PaintWithEffects(ctx); });
                }

                node.Cache = cache;
            }

            // todo doubling children nodes for some reason
            foreach (var child in Views)
            {
                if (child.CanDraw)
                {
                    var childNode = child.PrepareNode(context, SizeRequest.Width, SizeRequest.Height);
                    if (childNode != null)
                    {
                        node.Children.Add(childNode);
                    }
                }
            }

            return node;
        }

        return null;
    }

    /// <summary>
    ///  Not using cache
    /// </summary>
    /// <param name="context">context.Destination can be bigger than drawingRect</param>
    /// <param name="drawingRect">normally equal to DrawingRect</param>
    public virtual void DrawDirectInternal(DrawingContext context, SKRect drawingRect)
    {
        // NO CACHE, DIRECT PAINT
        var destination = context.Destination;

        var clone = AddPaintArguments(context).WithDestination(drawingRect);
        DrawWithClipAndTransforms(clone, drawingRect, true, true, (ctx) =>
        {
            PaintWithEffects(ctx);

            if (EffectPostRenderer != null)
            {
                EffectPostRenderer.Render(ctx.WithDestination(destination));
            }
        });
    }

    /// <summary>
    /// This is NOT calling FinalizeDraw()!
    /// parameter 'area' Usually is equal to DrawingRect
    /// </summary>
    /// <param name="context"></param>
    /// <param name="recordArea"></param>
    /// <param name="action"></param>
    protected void CreateRenderingObjectAndPaint(
        DrawingContext context,
        SKRect recordingArea,
        Action<DrawingContext> action)
    {
        if (recordingArea.Width <= 0 || recordingArea.Height <= 0 || float.IsInfinity(recordingArea.Height) ||
            float.IsInfinity(recordingArea.Width) || IsDisposed || IsDisposing)
        {
            return;
        }

        if (RenderObject != null && !UsesCacheDoubleBuffering)
        {
            //we might come here with an existing RenderingObject if UseRenderingObject returned False
            RenderObject = null;
        }

        RenderObjectNeedsUpdate = false;

        var usingCacheType = UsingCacheType;

        CachedObject oldObject = null; //reusing this
        if (UsesCacheDoubleBuffering)
        {
            oldObject = RenderObject;
        }
        else if (usingCacheType == SkiaCacheType.Image
                 || usingCacheType == SkiaCacheType.ImageComposite)
        {
            oldObject = RenderObjectPrevious;
        }
        else if (usingCacheType == SkiaCacheType.OperationsFull)
        {
            var debug = 1;
        }

        var created = CreateRenderingObject(context, recordingArea, oldObject, UsingCacheType, action);

        if (created == null)
        {
            return;
        }

        if (oldObject != null)
        {
            if (created.SurfaceIsRecycled)
            {
                oldObject.Surface = null;
            }

            if (!UsesCacheDoubleBuffering && usingCacheType != SkiaCacheType.ImageComposite)
            {
                DisposeObject(oldObject);
            }
        }

        var notValid = RenderObjectNeedsUpdate;
        RenderObject = created;

        if (RenderObject != null)
        {
            DrawRenderObjectInternal(context.WithDestination(RenderObject.RecordingArea), RenderObject);
        }
        else
        {
            notValid = true;
        }

        if (NeedUpdate || notValid) //someone changed us while rendering inner content
        {
            InvalidateCache();
            //Update();
        }
    }
}
