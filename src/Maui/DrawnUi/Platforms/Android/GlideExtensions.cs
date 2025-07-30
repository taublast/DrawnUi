using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using Bumptech.Glide;
using Bumptech.Glide.Load.Engine;
using Bumptech.Glide.Request.Target;
using Microsoft.Maui.Controls.Compatibility.Platform.Android;
using System.Diagnostics;
using Bumptech.Glide.Load.Model;
using Activity = Android.App.Activity;
using Platform = Microsoft.Maui.ApplicationModel.Platform;

namespace DrawnUi.Views;

public static class GlideExtensions
{
    internal static void CancelGlide(this ImageView imageView)
    {
        if (imageView.Handle == IntPtr.Zero)
        {
            return;
        }

        //NOTE: we may be doing a Cancel after the Activity has just exited
        // To make this work we have to use the Application.Context
        Glide.With(Platform.AppContext).Clear(imageView);
    }

    public static async Task<Bitmap> LoadOriginalViaGlide(this ImageSource source, Context context, CancellationToken token)
    {
        try
        {
            if (source is null)
            {
                Debug.WriteLine("`{0}` is null", nameof(ImageSource));
                return null;
            }

            var isAlive = IsActivityAlive(context, source);

            if (!isAlive)
                return null;

            RequestManager request = Glide.With(context);
            RequestBuilder? builder = null;

            switch (source)
            {
            case FileImageSource fileSource:
            builder = HandleFileImageSource(request, fileSource, context);
            break;

            case UriImageSource uriSource:
            builder = HandleUriImageSource(request, uriSource);
            //Debug.WriteLine($"[IMAGE] load url {uriSource.Uri}");
            break;

            case StreamImageSource streamSource:
            builder = await HandleStreamImageSource(request, streamSource, token, () => !IsActivityAlive(context, source));
            break;
            }

            if (builder is null)
                return null;

            var future = builder
                .SetDiskCacheStrategy(DiskCacheStrategy.All)
                .Override(Target.SizeOriginal, Target.SizeOriginal)
                .Submit();

            //var result = await Task.Run(() => future.Get(), token);

            token.Register(() => future.Cancel(true));

            var result = await Task.Run(() => future.Get(), token);

            if (result is BitmapDrawable drawable)
                return drawable.Bitmap;

        }
        catch (TaskCanceledException)
        {

        }
        catch (Java.Util.Concurrent.CancellationException)
        {
            // Handle Java layer task cancellation
            Debug.WriteLine("Java task was cancelled.");
            return null;
        }
        catch (Exception exc)
        {
            //Since developers can't catch this themselves, I think we should log it and silently fail
            Trace.WriteLine($"Unexpected exception in glidex: {exc}");
        }
        return null;
    }

    static RequestBuilder HandleFileImageSource(RequestManager request, FileImageSource source, Context context)
    {
        RequestBuilder builder;

        var fileName = source.File;
        var drawable = ResourceManager.GetDrawableId(context, fileName);
        if (drawable != 0)
        {
            builder = request.Load(drawable);
        }
        else
        {
            builder = request.Load(fileName);
        }
        return builder;
    }

    static RequestBuilder HandleUriImageSource(RequestManager request, UriImageSource source)
    {
        var url = source.Uri.OriginalString;
        var forGlide = new GlideUrl(url, new LazyHeaders.Builder().AddHeader("User-Agent", Super.UserAgent).Build());
        return request.Load(forGlide);
    }

    static async Task<RequestBuilder> HandleStreamImageSource(RequestManager request, StreamImageSource source, CancellationToken token, Func<bool> cancelled)
    {
        using var memoryStream = new MemoryStream();
        using var stream = await source.Stream(token);
        if (token.IsCancellationRequested || cancelled())
            return null;
        if (stream is null)
            return null;
        await stream.CopyToAsync(memoryStream, token);
        if (token.IsCancellationRequested || cancelled())
            return null;

        return request
            //.AsBitmap() //bug fixed
            .Load(memoryStream.ToArray());
    }

    static bool IsActivityAlive(ImageView imageView, ImageSource source)
    {
        // The imageView.Handle could be IntPtr.Zero? Meaning we somehow have a reference to a disposed ImageView...
        // I think this is within the realm of "possible" after the await call in LoadViaGlide().
        if (imageView.Handle == IntPtr.Zero)
        {
            return false;
        }

        return IsActivityAlive(imageView.Context, source);
    }

    /// <summary>
    /// NOTE: see https://github.com/bumptech/glide/issues/1484#issuecomment-365625087
    /// </summary>
    static bool IsActivityAlive(Context? context, ImageSource source)
    {
        if (context is Activity activity)
        {
            if (activity.IsFinishing)
            {
                return false;
            }
            if (activity.IsDestroyed)
            {
                return false;
            }
        }
        else if (context != null)
        {
            return false;
        }
        else
        {
            return false;
        }
        return true;
    }

    /// <summary>
    /// Cancels the Request and "clears" the ImageView
    /// </summary>
    static void Clear(RequestManager request, ImageView imageView)
    {
        imageView.Visibility = ViewStates.Gone;
        imageView.SetImageBitmap(null);

        //We need to call Clear for Glide to know this image is now unused
        //https://bumptech.github.io/glide/doc/targets.html
        request.Clear(imageView);
    }


}
