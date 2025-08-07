using System.Diagnostics;
using System.Reflection;
using Foundation;
using SkiaSharp.Views.iOS;
using UIKit;

#if USENUKE
using Xamarin.Nuke;
#endif

namespace DrawnUi.Draw;

public partial class SkiaImageManager
{
    public static bool UseNuke = true;

    static string PrefixNative = "native://";

    /// <summary>
    /// Platform-specific image loading implementation for iOS with Nuke caching
    /// </summary>
    /// <param name="source">Image source to load</param>
    /// <param name="cancel">Cancellation token</param>
    /// <returns>SKBitmap or null</returns>
    public static async Task<SKBitmap> LoadImageOnPlatformAsync(ImageSource source, CancellationToken cancel)
    {
        if (source == null)
            return null;

        try
        {
            if (source is FileImageSource fileSource)
            {
                return await LoadFromFile(fileSource.File, cancel);
            }
            else if (source is UriImageSource uriSource)
            {
                var url = uriSource.Uri.AbsoluteUri;

                if (url.Contains(PrefixNative))
                {
                    var iosImage = await LoadNativeImage(source, cancel, 1.0f);
                    if (iosImage != null)
                    {
                        TraceLog($"[LoadImageOnPlatformAsync] loaded {source} ToSKBitmap");
                        return iosImage.ToSKBitmap();
                    }
                }

                #if USENUKE
                else
                if (UseNuke)
                {
                    var nukeImage = await source.LoadOriginalViaNuke(cancel);
                    if (nukeImage != null)
                    {
                        TraceLog($"[LoadImageOnPlatformAsync-NUKE] loaded {source} ToSKBitmap");
                        return nukeImage.ToSKBitmap();
                    }

                    TraceLog($"[LoadImageOnPlatformAsync-NUKE] loaded NULL for {source}");
                    return null;
                }
                #endif

                return await LoadImageFromInternetAsync(uriSource, cancel);
            }
        }
        catch (TaskCanceledException)
        {
            TraceLog($"[LoadImageOnPlatformAsync] TaskCanceledException for {source}");
        }
        catch (Exception e)
        {
            Super.Log($"[LoadImageOnPlatformAsync] Exception for {source}: {e}");
            //TraceLog($"[LoadImageOnPlatformAsync] {e}");
        }

        TraceLog($"[LoadImageOnPlatformAsync] loaded NULL for {source}");
        return null;
    }

    /// <summary>
    /// Loads native iOS image using platform handlers
    /// </summary>
    /// <param name="source">Image source</param>
    /// <param name="token">Cancellation token</param>
    /// <param name="scale">Image scale factor</param>
    /// <returns>UIImage or null</returns>
    public static async Task<UIImage> LoadNativeImage(ImageSource source, CancellationToken token, float scale)
    {
        if (source == null)
            return null;

        try
        {
            var handler = source.GetHandler();
            return await handler.LoadImageAsync(source, token);
        }
        catch (Exception e)
        {
            TraceLog($"[LoadNativeImage] {e}");
        }

        return null;
    }
}

#if USENUKE

public static class NukeExtensions
{

    /// <summary>
    /// Manually encodes URL components that contain Unicode characters
    /// </summary>
    /// <param name="url">URL string to encode</param>
    /// <returns>Properly encoded URL string</returns>
    private static string EncodeUrlManually(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return uri.AbsoluteUri;
        }

        var parts = url.Split('?');
        var baseUrl = parts[0];
        var queryString = parts.Length > 1 ? parts[1] : string.Empty;

        var uriBuilder = new UriBuilder();
        var baseParts = baseUrl.Split('/');

        if (baseParts.Length > 2)
        {
            uriBuilder.Scheme = baseParts[0].TrimEnd(':');
            uriBuilder.Host = baseParts[2];

            if (baseParts.Length > 3)
            {
                var pathParts = new string[baseParts.Length - 3];
                Array.Copy(baseParts, 3, pathParts, 0, pathParts.Length);

                for (int i = 0; i < pathParts.Length; i++)
                {
                    pathParts[i] = Uri.EscapeDataString(pathParts[i]);
                }

                uriBuilder.Path = "/" + string.Join("/", pathParts);
            }
        }

        if (!string.IsNullOrEmpty(queryString))
        {
            uriBuilder.Query = queryString;
        }

        return uriBuilder.Uri.AbsoluteUri;
    }

    /// <summary>
    /// Creates NSUrl safely handling Unicode characters in URLs by properly encoding them
    /// </summary>
    /// <param name="urlString">The URL string that may contain Unicode characters</param>
    /// <returns>NSUrl instance or null if URL is invalid</returns>
    public static NSUrl CreateNSUrlSafely(string urlString)
    {
        if (string.IsNullOrEmpty(urlString))
            return null;

        try
        {
            var uri = new Uri(urlString);
            var encodedUrl = uri.AbsoluteUri;
            return new NSUrl(encodedUrl);
        }
        catch
        {
            try
            {
                var encodedUrl = EncodeUrlManually(urlString);
                return new NSUrl(encodedUrl);
            }
            catch
            {
                return null;
            }
        }
    }



    /// <summary>
    /// Loads an image from the specified ImageSource using Nuke, caching the original and returning it as a UIImage.
    /// </summary>
    /// <param name="source">The ImageSource to load the image from.</param>
    /// <param name="token">A CancellationToken to cancel the operation.</param>
    /// <returns>A Task containing the loaded UIImage.</returns>
    /// <exception cref="NotSupportedException">Thrown if the ImageSource type is not supported.</exception>
    public static async Task<UIImage> LoadOriginalViaNuke(this ImageSource source, CancellationToken token)
    {
        NSUrl url;

        // Handle different ImageSource types
        if (source is UriImageSource uriSource)
        {
            url = CreateNSUrlSafely(uriSource.Uri.AbsoluteUri);
        }
        else if (source is FileImageSource fileSource)
        {
            // Convert file path to NSUrl for local images
            url = NSUrl.FromFilename(fileSource.File);
        }
        else
        {
            throw new NotSupportedException("Only UriImageSource and FileImageSource are supported.");
        }

        // Load the image using Nuke, ensuring original size is preserved
        return await LoadCachedImageAsync(url, token);
    }


    public static Task<UIImage?> LoadCachedImageAsync(NSUrl url, CancellationToken token, Action<string>? onFail = null)
    {
        var tcs = new TaskCompletionSource<UIImage?>();

        // Start the image loading process
        ImagePipeline.Shared.LoadImageWithUrl(
            url,
            (image, errorMessage) =>
            {
                if (token.IsCancellationRequested)
                {
                    // If cancellation was requested, mark the task as canceled
                    tcs.TrySetCanceled();
                }
                else if (image == null)
                {
                    // If image loading failed, invoke onFail and return null
                    onFail?.Invoke(errorMessage);
                    tcs.SetResult(null);
                }
                else
                {
                    // Successfully loaded the image
                    tcs.SetResult(image);
                }
            });

        // Register cancellation to update the task state if canceled
        token.Register(() =>
        {
            tcs.TrySetCanceled();
        });

        return tcs.Task;
    }

    public static void ClearCache()
    {
        DataLoader.Shared.RemoveAllCachedResponses();
        ImageCache.Shared.RemoveAll();
    }

 

}

#endif


