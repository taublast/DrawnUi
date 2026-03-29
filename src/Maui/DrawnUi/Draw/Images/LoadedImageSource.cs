namespace DrawnUi.Draw;

public class LoadedImageSource : IDisposable
{
    public LoadedImageSource Clone()
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException("Cannot clone a disposed LoadedImageSource");
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
                imageClone = SafeCloneToRaster(Image);
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

    SKImage SafeCloneToRaster(SKImage source)
    {
        if (source == null) return null;

        var info = new SKImageInfo(source.Width, source.Height, source.ColorType, source.AlphaType, source.ColorSpace);

        using var bitmap = new SKBitmap(info);

        // This reads from GPU → CPU if necessary
        if (!source.ReadPixels(info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0))
        {
            // Fallback or error handling
            throw new InvalidOperationException("Failed to read pixels from image");
        }

        // Optional: mark immutable if you don't plan to modify
        bitmap.SetImmutable();

        return SKImage.FromBitmap(bitmap);  // Now this is safe
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
