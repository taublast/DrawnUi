namespace DrawnUi.Draw;

public class LoadedImageSource : IDisposable
{
    public LoadedImageSource Clone()
    {
        if (IsDisposed)
        {
            return null;//throw new ObjectDisposedException("Cannot clone a disposed LoadedImageSource");
        }

        if (Bitmap != null)
        {
            // Clone the SKBitmap
            var bitmapClone = new SKBitmap(Bitmap.Width, Bitmap.Height, Bitmap.ColorType, Bitmap.AlphaType);
            Bitmap.CopyTo(bitmapClone);
            return new LoadedImageSource(bitmapClone)
            {
                ProtectBitmapFromDispose = this.ProtectBitmapFromDispose,
                ProtectFromDispose = this.ProtectFromDispose
            };
        }
        else if (Image != null)
        {
            // Clone the SKImage
            SKImage imageClone;

            if (Image.IsTextureBacked)  
            {
                // Force a CPU raster copy from GPU
                imageClone = DrawImageOnCpuSurface(Image);
            }
            else
            {
                imageClone = SKImage.FromBitmap(SKBitmap.FromImage(Image));
            }

            return new LoadedImageSource(imageClone)
            {
                ProtectFromDispose = this.ProtectFromDispose
            };
        }
        else
        {
            // If there's no image or bitmap, return a new empty instance
            return new LoadedImageSource()
            {
                ProtectFromDispose = this.ProtectFromDispose
            };
        }
    }

    /// <summary>
    /// Forces a GPU-backed (texture-backed) SKImage to be rasterized into CPU memory
    /// by drawing it onto a new raster (CPU) SKSurface. 
    /// Returns the resulting SKSurface (caller is responsible for disposing it).
    /// </summary>
    public static SKImage DrawImageOnCpuSurface(SKImage image)
    {
        if (image == null)
            throw new ArgumentNullException(nameof(image));

        if (!image.IsTextureBacked)
        {
            // Already raster — just create a surface and draw it (or you could optimize to return a snapshot surface, but for consistency we draw)
            var info = new SKImageInfo(image.Width, image.Height, image.ColorType, image.AlphaType, image.ColorSpace);
            using var surface = SKSurface.Create(info);
            surface.Canvas.DrawImage(image, 0, 0);
            surface.Canvas.Flush(); // ensure drawing is complete
            return surface.Snapshot();
        }

        // Texture-backed case → force CPU copy via raster surface
        var rasterInfo = new SKImageInfo(
            image.Width,
            image.Height,
            SKColorType.Rgba8888,           // safe default, or use image.ColorType if known to be compatible
            image.AlphaType,
            image.ColorSpace);

        using var cpuSurface = SKSurface.Create(rasterInfo);

        using (var canvas = cpuSurface.Canvas) // or directly cpuSurface.Canvas
        {
            canvas.Clear(SKColors.Transparent); // optional, but safe
            canvas.DrawImage(image, 0, 0);      // this triggers GPU → CPU transfer
            canvas.Flush();                     // important for GPU flush before ToImage()
        }

        return cpuSurface.Snapshot();
    }

    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// As this can be disposed automatically by the consuming control like SkiaImage etc we can manually prohibit this for cases this instance is used elsewhere. 
    /// </summary>
    public bool ProtectFromDispose { get; set; }

    /// <summary>
    /// Should be set to true for loaded with SkiaImageManager.ReuseBitmaps
    /// </summary>
    public bool ProtectBitmapFromDispose { get; set; }

    public void Dispose()
    {
        if (!IsDisposed && !ProtectFromDispose)
        {
            IsDisposed = true;

            if (!ProtectBitmapFromDispose)
            {
                Bitmap?.Dispose();
            }
            Bitmap = null;

            Image?.Dispose();
            Image = null;
        }
    }

    public LoadedImageSource(SKBitmap bitmap)
    {
        Bitmap = bitmap;
    }

    public LoadedImageSource(SKImage image)
    {
        Image = image;
    }

    public LoadedImageSource(byte[] bytes)
    {
        Bitmap = SKBitmap.Decode(bytes);
    }

    public LoadedImageSource()
    {

    }

    private int _height = 0;
    private int _width = 0;
    private SKBitmap _bitmap;
    private SKImage _image;

    public int Width
    {
        get
        {
            return _width;
        }
    }
    public int Height
    {
        get
        {
            return _height;
        }
    }

    public bool IsDisposed { get; protected set; }

    public SKBitmap Bitmap
    {
        get => _bitmap;
        set
        {
            if (!IsDisposed)
            {
                _bitmap = value;
                if (_bitmap == null)
                {
                    if (_image == null)
                    {
                        _height = 0;
                        _width = 0;
                    }
                    else
                    {
                        _height = _image.Height;
                        _width = _image.Width;
                    }
                }
                else
                {
                    _height = _bitmap.Height;
                    _width = _bitmap.Width;
                }
            }
        }
    }

    public SKImage Image
    {
        get => _image;
        set
        {
            if (!IsDisposed)
            {
                _image = value;
                if (_image == null)
                {
                    if (_bitmap == null)
                    {
                        _height = 0;
                        _width = 0;
                    }
                }
                else
                {
                    if (_bitmap == null)
                    {
                        _height = _image.Height;
                        _width = _image.Width;
                    }
                }
            }
        }
    }

    public SKBitmap GetBitmap()
    {
        if (Bitmap != null)
        {
            return Bitmap;
        }
        if (Image != null)
        {
            return SKBitmap.FromImage(Image);
        }
        return null;
    }
}
