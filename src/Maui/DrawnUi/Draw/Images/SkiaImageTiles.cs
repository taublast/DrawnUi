namespace DrawnUi.Draw;

public class SkiaImageTiles : SkiaImage
{
    public override bool WillClipBounds => true;


    #region PROPERTIES

    public static readonly BindableProperty TileAspectProperty = BindableProperty.Create(
        nameof(TileAspect),
        typeof(TransformAspect),
        typeof(SkiaImageTiles),
        TransformAspect.AspectCover,
        propertyChanged: NeedInvalidateMeasure);

    /// <summary>
    /// Apspect to render image with, default is AspectFitFill. 
    /// </summary>
    public TransformAspect TileAspect
    {
        get { return (TransformAspect)GetValue(TileAspectProperty); }
        set { SetValue(TileAspectProperty, value); }
    }

    public static readonly BindableProperty TileWidthProperty = BindableProperty.Create(
        nameof(TileWidth),
        typeof(double),
        typeof(SkiaImageTiles),
        0.0,
        propertyChanged: NeedInvalidateMeasure);

    public double TileWidth
    {
        get { return (double)GetValue(TileWidthProperty); }
        set { SetValue(TileWidthProperty, value); }
    }

    public static readonly BindableProperty TileHeightProperty = BindableProperty.Create(
        nameof(TileHeight),
        typeof(double),
        typeof(SkiaImageTiles),
        0.0,
        propertyChanged: NeedInvalidateMeasure);

    public double TileHeight
    {
        get { return (double)GetValue(TileHeightProperty); }
        set { SetValue(TileHeightProperty, value); }
    }

    public static readonly BindableProperty TileOffsetYProperty = BindableProperty.Create(
        nameof(TileOffsetY),
        typeof(double),
        typeof(SkiaImageTiles),
        0.0,
        propertyChanged: NeedDraw);

    public double TileOffsetY
    {
        get { return (double)GetValue(TileOffsetYProperty); }
        set { SetValue(TileOffsetYProperty, value); }
    }

    public static readonly BindableProperty TileOffsetXProperty = BindableProperty.Create(
        nameof(TileOffsetX),
        typeof(double),
        typeof(SkiaImageTiles),
        0.0,
        propertyChanged: NeedDraw);

    public double TileOffsetX
    {
        get { return (double)GetValue(TileOffsetXProperty); }
        set { SetValue(TileOffsetXProperty, value); }
    }

    public static readonly BindableProperty TileCacheTypeProperty = BindableProperty.Create(nameof(TileCacheType),
      typeof(SkiaCacheType),
      typeof(SkiaControl),
      SkiaCacheType.Image,
       propertyChanged: (bindable, old, changed) =>
       {
           if (bindable is SkiaImageTiles control && control.Tile != null)
           {
               control.Tile.UseCache = control.TileCacheType;
               control.Update();
           }
       });
    public SkiaCacheType TileCacheType
    {
        get { return (SkiaCacheType)GetValue(TileCacheTypeProperty); }
        set { SetValue(TileCacheTypeProperty, value); }
    }


    #endregion

    protected override void OnMeasured()
    {
        base.OnMeasured();

        SetupTiles();
    }

    /// <summary>
    /// Cached image that will be used as tile
    /// </summary>
    protected SkiaImage Tile { get; set; }

    /// <summary>
    /// Whether tiles are setup for rendering
    /// </summary>
    protected bool DrawTiles { get; set; }

    /// <summary>
    /// Source was loaded, we can create tile
    /// </summary>
    public override void OnSuccess(string source)
    {
        base.OnSuccess(source);

        SetupTiles();
    }

    protected virtual SkiaImage CreateTile(double width, double height, LoadedImageSource source)
    {
        if (source != null)
        {
            source.ProtectBitmapFromDispose = SkiaImageManager.ReuseBitmaps;
        }

        var tile = new SkiaImage()
        {
            Aspect = this.TileAspect,
            WidthRequest = width,
            HeightRequest = height,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            UseCache = TileCacheType,
            ImageBitmap = source
        };

        return tile;
    }

    protected virtual void SetupTiles()
    {
        var image = ApplyNewSource;
        if (image == null)
        {
            image = LoadedSource;
        }

        if (TileWidth > 0 && TileHeight > 0 && image != null)
        {
            Tile = CreateTile(TileWidth, TileHeight, image);
            DrawTiles = true;
        }
        else
        {
            DrawTiles = Tile != null;
        }

    }

    protected override void OnPropertyChanged(string propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName.IsEither(nameof(ZoomX), nameof(ZoomY),
                nameof(TileWidth), nameof(TileHeight),
        nameof(RenderingScale), nameof(VerticalOffset), nameof(HorizontalOffset)))
        {
            NeedRecalculate = true;
        }
    }

    bool NeedRecalculate { get; set; }

    protected void CalculateTileSize(LoadedImageSource source, SKRect dest,
        float scale, DrawImageAlignment horizontal = DrawImageAlignment.Center, DrawImageAlignment vertical = DrawImageAlignment.Center)
    {
        var aspectScaleX = AspectScale.X * (float)(ZoomX);
        var aspectScaleY = AspectScale.Y * (float)(ZoomY);

        SKRect display = CalculateDisplayRect(dest,
            aspectScaleX * source.Width, aspectScaleY * source.Height,
            horizontal, vertical);

        //if (this.BlurAmount > 0)
        display.Inflate(new SKSize((float)InflateAmount, (float)InflateAmount));

        display.Offset((float)Math.Round(RenderingScale * HorizontalOffset), (float)Math.Round(RenderingScale * VerticalOffset));

        TextureScale = new(dest.Width / display.Width, dest.Height / display.Height);

        TileWidthPixels = (float)Math.Round(TileWidth * RenderingScale);
        TileHeightPixels = (float)Math.Round(TileHeight * RenderingScale);

        CalculatedForDest = dest;
        NeedRecalculate = false;
    }

    protected float TileWidthPixels { get; set; }
    protected float TileHeightPixels { get; set; }

    protected SKRect CalculatedForDest;
    protected SKSize CalculatedForTile;

    protected override void InvalidateMeasure()
    {
        base.InvalidateMeasure();

        NeedRecalculate = true;
    }

    protected override void DrawSource(DrawingContext ctx, LoadedImageSource source, TransformAspect stretch,
        DrawImageAlignment horizontal = DrawImageAlignment.Center, DrawImageAlignment vertical = DrawImageAlignment.Center,
        SKPaint paint = null)
    {
        if (!DrawTiles)
        {
            //base.DrawSource(ctx, source, dest, scale, stretch, horizontal, vertical, paint);
            return;
        }

        try
        {
            var dest = ctx.Destination;
            var scale = ctx.Scale;

            //existing base code
            if (AspectScale == SKPoint.Empty)
            {
                throw new ApplicationException("AspectScale is not set");
            }

            if (!NeedRecalculate)
            {
                //todo compare tile size and this drawingRect
                if (CalculatedForDest != dest)
                {
                    NeedRecalculate = true;
                }
            }

            if (NeedRecalculate)
            {
                CalculateTileSize(source, dest, scale, horizontal, vertical);
            }

            var useOffsetX = -TileOffsetX % TileWidth;
            var useOffsetY = -TileOffsetY % TileHeight;

            var offsetX = useOffsetX > 0 ? (float)Math.Round(useOffsetX * RenderingScale) : TileWidthPixels + (float)Math.Round(useOffsetX * RenderingScale);
            var offsetY = useOffsetY > 0 ? (float)Math.Round(useOffsetY * RenderingScale) : TileHeightPixels + (float)Math.Round(useOffsetY * RenderingScale);

            var tilesX = (int)Math.Ceiling((dest.Width + offsetX) / TileWidthPixels);
            var tilesY = (int)Math.Ceiling((dest.Height + Math.Abs(offsetY)) / TileHeightPixels);

            var startY = (float)-offsetY;
            var startX = (float)-offsetX;

            for (int x = 0; x < tilesX; x++)
            {
                for (int y = 0; y < tilesY; y++)
                {
                    // Define the destination rectangle for this tile
                    var left = startX + x * TileWidthPixels;
                    var top = startY + y * TileHeightPixels;

                    SKRect tileDest = new SKRect(
                        left,
                        top,
                        left + TileWidthPixels,
                        top + TileHeightPixels);

                    Tile.Render(ctx.WithDestination(tileDest));
                }
            }


        }
        catch (Exception e)
        {
            Trace.WriteLine(e);
        }


    }




    public override void OnDisposing()
    {
        base.OnDisposing();

        Tile = null;

    }
}
