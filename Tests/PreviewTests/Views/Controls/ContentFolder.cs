using AppoMobi.Maui.Gestures;
using AppoMobi.Specials;
using DrawnUi.Infrastructure;
using System.Numerics;

namespace PreviewTests.Views.Controls;

/// <summary>
/// Consume and process the cache of the content
/// </summary>
public class ContentFolder : ContentLayout, ISkiaGestureListener
{
    public ContentFolder()
    {
        CreateShader();
    }

    public static readonly BindableProperty VerticalMarginProperty = BindableProperty.Create(
        nameof(VerticalMargin),
        typeof(double),
        typeof(ContentFolder),
        0.0);

    public double VerticalMargin
    {
        get { return (double)GetValue(VerticalMarginProperty); }
        set { SetValue(VerticalMarginProperty, value); }
    }

    public static readonly BindableProperty SecondarySourceProperty = BindableProperty.Create(
        nameof(SecondarySource),
        typeof(string),
        typeof(ContentFolder),
        defaultValue: null,
        propertyChanged: ApplySourceProperty);

    public string SecondarySource
    {
        get { return (string)GetValue(SecondarySourceProperty); }
        set { SetValue(SecondarySourceProperty, value); }
    }

    private static void ApplySourceProperty(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (oldvalue != newvalue && bindable is ContentFolder control)
        {
            control.ApplySecondarySource((string)newvalue);
        }
    }

    public void SetSecondary(SKImage image)
    {
        var dispose = _imageSecondary;
        _imageSecondary = image;
        if (dispose != _imageSecondary)
            dispose?.Dispose();

        UpdateTextures();
    }

    void ApplySecondarySource(string source)
    {
        Task.Run(async () =>
        {
            var background = await LoadSource(source);
            SetSecondary(background);
        });
    }

    private SemaphoreSlim _semaphoreLoadFile = new(1, 1);

    protected override void OnLayoutChanged()
    {
        base.OnLayoutChanged();

        ApplySecondarySource(this.SecondarySource);
    }

    /// <summary>
    /// Loading from local files only
    /// </summary>
    /// <param name="fileName"></param>
    /// <returns>
    public async Task<SKImage> LoadSource(string fileName)
    {
        if (string.IsNullOrEmpty(fileName) || DrawingRect.Width <= 0 || DrawingRect.Height <= 0)
            return null;

        await _semaphoreLoadFile.WaitAsync();

        SKBitmap originalBitmap;

        try
        {
            if (fileName.SafeContainsInLower("file://"))
            {
                var fullFilename = fileName.Replace("file://", "", StringComparison.InvariantCultureIgnoreCase);
                using var stream = new FileStream(fullFilename, System.IO.FileMode.Open);
                originalBitmap = SKBitmap.Decode(stream);
            }
            else
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
                originalBitmap = SKBitmap.Decode(stream);
            }

            if (originalBitmap != null)
            {
                var info = new SKImageInfo((int)DrawingRect.Width, (int)DrawingRect.Height);
                var resizedBitmap = new SKBitmap(info);
                using (var canvas = new SKCanvas(resizedBitmap))
                {
                    // This will stretch the original image to fill the new size
                    var rect = new SKRect(0, 0, (int)DrawingRect.Width, (int)DrawingRect.Height);
                    canvas.DrawBitmap(originalBitmap, rect);
                    canvas.Flush();
                }

                return SKImage.FromBitmap(resizedBitmap);
            }

            return null;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[ContentFolder] LoadSource failed to load animation {fileName}");
            Console.WriteLine(e);
            return null;
        }
        finally
        {
            _semaphoreLoadFile.Release();
        }
    }

    void DetachContent()
    {
        if (Content != null)
        {
            Content.CreatedCache -= OnCacheCreated;
            Content.DelegateDrawCache -= DrawContentImage;
        }
    }

    void AttachContent()
    {
        if (this.Content != null)
        {
            Content.CreatedCache += OnCacheCreated;
            Content.DelegateDrawCache = DrawContentImage;
        }
    }

    public override void OnDisposing()
    {
        DetachContent();

        _textureFront?.Dispose();
        _textureBack?.Dispose();

        _imageSecondary?.Dispose();

        base.OnDisposing();
    }

    protected override void SetContent(SkiaControl view)
    {
        DetachContent();

        base.SetContent(view);

        AttachContent();
    }

    /// <summary>
    /// Compiled script
    /// </summary>
    SKRuntimeEffect _compiledShader;

    void CreateShader()
    {
        string shaderCode = SkSl.LoadFromResources($"{MauiProgram.ShadersFolder}/curl.sksl");
        _compiledShader = SkSl.Compile(shaderCode);
    }

    void BuildTextures(SKImage front, SKImage back)
    {
        try
        {
            var disposeTexture1 = _textureFront;
            var disposeTexture2 = _textureBack;

            if (_compiledShader != null && front != null)
            {
                _textureFront = front.ToShader();
                if (back != null)
                {
                    _textureBack = _imageSecondary.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat);
                }
            }

            if (_textureFront != null)
            {
                if (_textureBack == null)
                {
                    _passTextures = new SKRuntimeEffectChildren(_compiledShader)
                    {
                        { "iImage1", _textureFront }
                    };
                }
                else
                {
                    _passTextures = new SKRuntimeEffectChildren(_compiledShader)
                    {
                        { "iImage1", _textureFront },
                        { "iImage2", _textureBack }
                    };
                }
            }

            disposeTexture1?.Dispose();
            disposeTexture2?.Dispose();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private void OnCacheCreated(object sender, CachedObject cache)
    {
        UpdateTextures();
    }

    void UpdateTextures()
    {
        var content = Content;
        if (content != null)
        {
            var cache = content.RenderObject;
            if (_compiledShader != null && cache is { Image: not null })
            {
                BuildTextures(cache.Image, _imageSecondary);
            }
        }
    }

    private void DrawContentImage(DrawingContext ctx, CachedObject cache)
    {
        if (Content != null && _compiledShader != null && _passTextures != null)
        {
            var viewport = Content.DrawingRect;

            float margin = (float)Math.Round(VerticalMargin * RenderingScale);
            float timeValue = _offset.Y;//0.0f;
            SKSize iResolution = new(viewport.Width, viewport.Height);
            SKSize iImageResolution = new(cache.Image.Width, cache.Image.Height);

            var uniforms = new SKRuntimeEffectUniforms(_compiledShader);

            uniforms["iOffset"] = new[] { viewport.Left, viewport.Top };
            uniforms["iMargins"] = new[] { 0, margin, 0, margin };

            uniforms["iResolution"] = new[] { iResolution.Width, iResolution.Height };
            uniforms["iImageResolution"] = new[] { iImageResolution.Width, iImageResolution.Height };
            uniforms["iTime"] = timeValue;
            uniforms["iMouse"] = new[] { _offset.X, _offset.Y, 0f, 0f };

            using var paintWithShader = new SKPaint();

            paintWithShader.Shader = _compiledShader.ToShader(uniforms, _passTextures);

            ctx.Context.Canvas.DrawRect(ctx.Destination, paintWithShader);
        }
    }



    /// <summary>
    /// Shader textyre created from cache
    /// </summary>
    SKShader _textureFront;


    SKShader _textureBack;



    /// <summary>
    /// _texture wrapper for later use
    /// </summary>
    private SKRuntimeEffectChildren _passTextures;

    private SKImage _imageSecondary;
    protected Vector2 _offset;
    protected Vector2 _origin;

    public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
    {

        ISkiaGestureListener consumed = null;

        var K = 2;

        switch (args.Type)
        {
        case TouchActionResult.Down when args.Event.NumberOfTouches == 1:
        _origin = _offset;
        consumed = this;
        break;

        case TouchActionResult.Panning when args.Event.NumberOfTouches == 1:
        _offset = new(
            _origin.X - args.Event.Distance.Total.X*K,
            _origin.Y - args.Event.Distance.Total.Y*K
        );
        consumed = this;
        Repaint();
        break;

        //case TouchActionResult.Up:
        //_mouse = Vector2.Zero; //go back, can animate
        //consumed = this;
        //Repaint();
        //break;
        }

        return consumed;
    }


}
