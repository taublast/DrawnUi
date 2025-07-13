using SKBitmap = SkiaSharp.SKBitmap;

namespace DrawnUi.Draw;

/// <summary>
/// Base shader effect class that has 2 input textures. 
/// </summary>
public class ShaderDoubleTexturesEffect : SkiaShaderEffect
{
    protected bool ParentReady()
    {
        return !(Parent == null || Parent.DrawingRect.Width <= 0 || Parent.DrawingRect.Height <= 0);
    }

    protected override bool ShouldDisposePreviousTexture(SKImage image)
    {
        if (image == null) return false;

        // base check, don't dispose parent's cached image
        if (image == Parent?.CachedImage) return false;

        // Don't dispose images from other controls
        if (image == _controlFrom?.RenderObject?.Image) return false;
        if (image == AssignedControlTo?.RenderObject?.Image) return false;

        // It's a snapshot we created, safe to dispose
        return true;
    }

    protected override void OnDisposing()
    {
        base.OnDisposing();

        SecondaryTexture?.Dispose();
        LoadedSecondaryBitmap?.Dispose();

        LoadedPrimaryBitmap?.Dispose();
        ResizedPrimaryImage?.Dispose();

        DetachFrom();
        DetachTo();
    }

    protected SKShaderTileMode TilingSecondaryTexture = SKShaderTileMode.Clamp;

    protected override SKRuntimeEffectChildren CreateTexturesUniforms(SkiaDrawingContext ctx, SKRect destination, SKShader primaryTexture)
    {
        var secondaryTexture = GetSecondaryTexture();

        if (primaryTexture != null && secondaryTexture != null)
        {
            return new SKRuntimeEffectChildren(CompiledShader)
            {
                { "iImage1", primaryTexture }, //main
                { "iImage2", secondaryTexture } //secondary
            };
        }
        else
        {
            return new SKRuntimeEffectChildren(CompiledShader)
            {
            };
        }
    }

    //normally primary texture comes from the parent control
    //but here we add props to have it from file or
    //from another control
    #region PrimaryTexture


    protected override SKImage GetPrimaryTextureImage(SkiaDrawingContext ctx, SKRect destination)
    {
        if (ControlFrom != null)
        {
            return _controlFrom.RenderObject?.Image;
        }

        if (PrimarySource != null)
        {
            if (!_primarySourceBitmapResized && ParentReady() && LoadedPrimaryBitmap != null)
            {
                ResizePrimaryLoadedBitmap();
            }
            return ResizedPrimaryImage;
        }

        return base.GetPrimaryTextureImage(ctx, destination);
    }

    #region FromControl

    SkiaControl _controlFrom;

    void ApplyControlFrom(SkiaControl control)
    {
        if (_controlFrom == control)
            return;

        DetachFrom();
        _controlFrom = control;
    }

    void DetachFrom()
    {
        if (_controlFrom != null)
        {
            _controlFrom = null;
        }
    }

    private static void ApplyControlFromProperty(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (oldvalue != newvalue && bindable is ShaderDoubleTexturesEffect control)
        {
            control.ApplyControlFrom(newvalue as SkiaControl);
        }
    }

    public static readonly BindableProperty ControlFromProperty = BindableProperty.Create(
        nameof(ControlFrom),
        typeof(SkiaControl), typeof(ShaderDoubleTexturesEffect),
        null,
        propertyChanged: ApplyControlFromProperty);

    public SkiaControl ControlFrom
    {
        get { return (SkiaControl)GetValue(ControlFromProperty); }
        set { SetValue(ControlFromProperty, value); }
    }



    #endregion

    #region FromFile

    public static readonly BindableProperty PrimarySourceProperty = BindableProperty.Create(
        nameof(PrimarySource),
        typeof(string),
        typeof(ShaderDoubleTexturesEffect),
        defaultValue: null,
        propertyChanged: ApplyPrimarySourceProperty);

    public string PrimarySource
    {
        get { return (string)GetValue(PrimarySourceProperty); }
        set { SetValue(PrimarySourceProperty, value); }
    }

    private static void ApplyPrimarySourceProperty(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (oldvalue != newvalue && bindable is ShaderDoubleTexturesEffect control)
        {
            control.ApplyPrimarySource((string)newvalue);
        }
    }

    void ApplyPrimarySource(string source)
    {
        Task.Run(async () =>
        {
            await LoadPrimarySource(source);
            if (ParentReady() && LoadedPrimaryBitmap != null)
            {
                ResizePrimaryLoadedBitmap();
            }
        });
    }

    public void ResizePrimaryLoadedBitmap()
    {
        SKImage image = null;

        var kill = ResizedPrimaryImage;

        if (LoadedPrimaryBitmap != null)
        {
            _primarySourceBitmapResized = true;

            var outRect = Parent.DrawingRect;
            var info = new SKImageInfo((int)outRect.Width, (int)outRect.Height);
            var resizedBitmap = new SKBitmap(info);
            using (var canvas = new SKCanvas(resizedBitmap))
            {
                var rect = new SKRect(0, 0, (int)outRect.Width, (int)outRect.Height);
                //resize source to apply higher quality and have it antialised
                using var bmp = LoadedPrimaryBitmap.Resize(new SKSizeI((int)rect.Width, (int)rect.Height), SKFilterQuality.High);

                canvas.DrawBitmap(bmp, rect);
                canvas.Flush();
            }

            ResizedPrimaryImage = SKImage.FromBitmap(resizedBitmap);

            if (kill != null)
            {
                Tasks.StartDelayed(TimeSpan.FromSeconds(2.5), () =>
                {
                    kill.Dispose();
                });
            }
        }

    }

    /// <summary>
    /// Loaded from file
    /// </summary>
    protected SKImage ResizedPrimaryImage { get; set; }
    protected SKBitmap LoadedPrimaryBitmap { get; set; }
    private readonly SemaphoreSlim _semaphoreLoadPrimaryFile = new(1, 1);
    private bool _primarySourceBitmapResized;

    /// <summary>
    /// Loading from local files only
    /// </summary>
    /// <param name="fileName"></param>
    /// <returns></returns>
    public async Task LoadPrimarySource(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return;

        await _semaphoreLoadPrimaryFile.WaitAsync();

        try
        {
            var kill = LoadedPrimaryBitmap;


            if (fileName.SafeContainsInLower("file://"))
            {
                var fullFilename = fileName.Replace("file://", "", StringComparison.InvariantCultureIgnoreCase);
                using var stream = new FileStream(fullFilename, System.IO.FileMode.Open);
                LoadedPrimaryBitmap = SKBitmap.Decode(stream);
            }
            else
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
                LoadedPrimaryBitmap = SKBitmap.Decode(stream);
            }

            _primarySourceBitmapResized = false;
        }
        catch (Exception e)
        {
            Console.WriteLine($"LoadSource failed to load {fileName}");
            Console.WriteLine(e);
            return;
        }
        finally
        {
            _semaphoreLoadPrimaryFile.Release();
        }
    }

    #endregion


    #endregion

    #region SecondaryTexture

    protected virtual SKShader GetSecondaryTexture()
    {
        if (!_secondarySourceSet && ParentReady())
        {
            if (ControlTo != null)
            {
                ImportCacheTo();
            }
            else
            {
                ResizeSecondaryLoadedBitmapAndCompileTexture();
            }
        }

        return SecondaryTexture;
    }

    /// <summary>
    /// Flag set my CompileSecondaryTexture
    /// </summary>
    private bool _secondarySourceSet;

    /// <summary>
    /// Will be normally set by CompileSecondaryTexture
    /// </summary>
    protected SKShader SecondaryTexture;

    public void CompileSecondaryTexture(SKImage image)
    {
        var dispose = SecondaryTexture;

        if (image != null)
        {
            SecondaryTexture = image.ToShader(TilingSecondaryTexture, TilingSecondaryTexture);
        }

        if (dispose != SecondaryTexture)
            dispose?.Dispose();

        _secondarySourceSet = true;

        Update();
    }

    #region FromFile

    protected SKBitmap LoadedSecondaryBitmap;
    private readonly SemaphoreSlim _semaphoreLoadSecondaryFile = new(1, 1);


    public static readonly BindableProperty SecondarySourceProperty = BindableProperty.Create(
        nameof(SecondarySource),
        typeof(string),
        typeof(ShaderDoubleTexturesEffect),
        defaultValue: null,
        propertyChanged: ApplySecondarySourceProperty);

    public string SecondarySource
    {
        get { return (string)GetValue(SecondarySourceProperty); }
        set { SetValue(SecondarySourceProperty, value); }
    }

    private static void ApplySecondarySourceProperty(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (oldvalue != newvalue && bindable is ShaderDoubleTexturesEffect control)
        {
            control.ApplySecondarySource((string)newvalue);
        }
    }

    void ApplySecondarySource(string source)
    {
        Task.Run(async () =>
        {
            await LoadSecondarySource(source);
            if (ParentReady() && LoadedSecondaryBitmap != null)
            {
                ResizeSecondaryLoadedBitmapAndCompileTexture();
            }

        });
    }

    public void ResizeSecondaryLoadedBitmapAndCompileTexture()
    {
        SKImage image = null;

        if (LoadedSecondaryBitmap != null)
        {
            var outRect = Parent.DrawingRect;
            var info = new SKImageInfo((int)outRect.Width, (int)outRect.Height);
            var resizedBitmap = new SKBitmap(info);
            using (var canvas = new SKCanvas(resizedBitmap))
            {
                var rect = new SKRect(0, 0, (int)outRect.Width, (int)outRect.Height);
                using var bmp = LoadedSecondaryBitmap.Resize(new SKSizeI((int)rect.Width, (int)rect.Height), SKFilterQuality.High);

                canvas.DrawBitmap(bmp, rect);
                canvas.Flush();
            }

            image = SKImage.FromBitmap(resizedBitmap);
        }

        CompileSecondaryTexture(image);
    }


    /// <summary>
    /// Loading from local files only
    /// </summary>
    /// <param name="fileName"></param>
    /// <returns></returns>
    public async Task LoadSecondarySource(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return;

        await _semaphoreLoadSecondaryFile.WaitAsync();

        try
        {
            if (fileName.SafeContainsInLower("file://"))
            {
                var fullFilename = fileName.Replace("file://", "", StringComparison.InvariantCultureIgnoreCase);
                using var stream = new FileStream(fullFilename, System.IO.FileMode.Open);
                LoadedSecondaryBitmap = SKBitmap.Decode(stream);
            }
            else
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
                LoadedSecondaryBitmap = SKBitmap.Decode(stream);
            }

            _secondarySourceSet = false;
            return;
        }
        catch (Exception e)
        {
            Console.WriteLine($"LoadSource failed to load animation {fileName}");
            Console.WriteLine(e);
            return;
        }
        finally
        {
            _semaphoreLoadSecondaryFile.Release();
        }
    }

    #endregion

    #region FromControl

    protected SkiaControl AssignedControlTo;

    protected virtual void ImportCacheTo()
    {
        if (AssignedControlTo?.RenderObject?.Image == null || !ParentReady())
            return;

        //Debug.WriteLine($"ImportCacheTo {_controlTo.BindingContext}");

        CompileSecondaryTexture(AssignedControlTo.RenderObject.Image);
    }

    private void OnCacheCreatedTo(object sender, CachedObject e)
    {
        ImportCacheTo();
    }

    protected virtual void ApplyControlTo(SkiaControl control)
    {
        if (AssignedControlTo == control)
            return;

        DetachTo();
        AssignedControlTo = control;
        if (AssignedControlTo != null)
        {
            AssignedControlTo.CreatedCache += OnCacheCreatedTo;
            ImportCacheTo();
        }
    }

    void DetachTo()
    {
        if (AssignedControlTo != null)
        {
            AssignedControlTo.CreatedCache -= OnCacheCreatedTo;
            AssignedControlTo = null;
        }
    }

    private static void ApplyControlToProperty(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (oldvalue != newvalue && bindable is ShaderDoubleTexturesEffect control)
        {
            control.ApplyControlTo(newvalue as SkiaControl);
        }
    }

    public static readonly BindableProperty ControlToProperty = BindableProperty.Create(
        nameof(ControlTo),
        typeof(SkiaControl), typeof(ShaderDoubleTexturesEffect),
        null,
        propertyChanged: ApplyControlToProperty);

    public SkiaControl ControlTo
    {
        get { return (SkiaControl)GetValue(ControlToProperty); }
        set { SetValue(ControlToProperty, value); }
    }



    #endregion

    #endregion

}
