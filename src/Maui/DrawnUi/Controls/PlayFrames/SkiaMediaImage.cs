
namespace DrawnUi.Controls;

public class SkiaMediaImage : SkiaImage
{
    public SkiaMediaImage()
    {
        UseCache = SkiaCacheType.ImageDoubleBuffered;
    }

    //public override bool CanUseCacheDoubleBuffering => false;

    protected override void OnLayoutChanged()
    {
        base.OnLayoutChanged();

        GifRenderer?.PlayIfNeeded();
    }

    public override void OnVisibilityChanged(bool newvalue)
    {
        base.OnVisibilityChanged(newvalue);

        GifRenderer?.OnVisibilityChanged(newvalue);
    }

    public override void SetImageSource(ImageSource source)
    {
        string uri = null;
        if (!source.IsEmpty)
        {
            if (source is UriImageSource sourceUri)
            {
                uri = sourceUri.Uri.ToString();
            }
            else if (source is FileImageSource sourceFile)
            {
                uri = sourceFile.File;
            }
            else if (source is ImageSourceResourceStream stream)
            {
                uri = stream.Url;
            }

            if (uri.ToLower().SafeContainsInLower(".webp") || uri.ToLower().SafeContainsInLower(".gif"))
            {
                LoadImageFrames(uri);
                return;
            }

            base.SetImageSource(source);
        }
        else
        {
            ClearBitmap();
        }

    }

    public virtual async void LoadImageFrames(string uri)
    {
        if (GifRenderer == null)
        {
            GifRenderer = new(this)
            {
                Repeat = -1,
                AutoPlay = true
            };
        }

        var animation = await GifRenderer.LoadSource(uri);

        if (animation != null && animation.Frame != null)
        {
            GifRenderer.SetAnimation(animation, true);
            Invalidate();
        }

    }

    public override void OnDisposing()
    {
        base.OnDisposing();

        GifRenderer?.Dispose();
    }

    protected SkiaGif GifRenderer { get; set; }

}
