using SkiaSharp.Views.Android;

namespace DrawnUi.Draw;

public partial class SkiaImageManager
{
    /*
     if (handler is IPlatformViewHandler platformViewHandler && platformViewHandler.ViewController is not null)
       {
        var provider = handler.GetRequiredService<IImageSourceServiceProvider>();
        platformViewHandler.ViewController.View?.UpdateBackground(page, provider);
       }
     */

    public static bool UseGlide = true;

    static string PrefixNative = "native://";

    public static async Task<SKBitmap> LoadImageOnPlatformAsync(ImageSource source, CancellationToken cancel)
    {
        if (source == null)
            return null;

        try
        {
            if (source is UriImageSource uriSource)
            {
                var url = uriSource.Uri.AbsoluteUri;
                if (url.Contains(PrefixNative))
                {
                    var native = url.Replace(PrefixNative, string.Empty).Trim('/');
                    source = new FileImageSource() { File = native };
                    var handlerFile = source.GetHandler();
                    var nativeFile = await handlerFile.LoadImageAsync(source, Platform.CurrentActivity, cancel);
                    if (nativeFile != null)
                    {
                        SkiaImageManager.TraceLog($"[LoadImageOnPlatformAsync] loaded {source} ToSKBitmap");
                        return nativeFile.ToSKBitmap();
                    }
                }
                else
                {
                    if (UseGlide)
                    {
                        var glide = await source.LoadOriginalViaGlide(Platform.CurrentActivity, cancel);
                        if (glide != null)
                        {
                            SkiaImageManager.TraceLog($"[LoadImageOnPlatformAsync-GLIDE] loaded {source} ToSKBitmap");
                            return glide.ToSKBitmap();
                        }

                        SkiaImageManager.TraceLog($"[LoadImageOnPlatformAsync-GLIDE] loaded NULL for {source}");
                        return null;
                    }
                    else
                    {
                        return await LoadImageFromInternetAsync(uriSource, cancel);
                    }
                }
            }

            if (source is FileImageSource fileSource)
            {
                return await LoadFromFile(fileSource.File, cancel);
            }

            var handler = source.GetHandler();
            var androidBitmap = await handler.LoadImageAsync(source, Platform.CurrentActivity, cancel);

            if (androidBitmap != null)
            {
                SkiaImageManager.TraceLog($"[LoadImageOnPlatformAsync] loaded {source} ToSKBitmap");
                return androidBitmap.ToSKBitmap();
            }
        }
        catch (TaskCanceledException)
        {
            SkiaImageManager.TraceLog($"[LoadImageOnPlatformAsync] TaskCanceledException for {source}");
        }
        catch (System.Exception e)
        {
            SkiaImageManager.TraceLog($"[LoadImageOnPlatformAsync] {e}");
        }

        SkiaImageManager.TraceLog($"[LoadImageOnPlatformAsync] loaded NULL for {source}");
        return null;
    }
}
