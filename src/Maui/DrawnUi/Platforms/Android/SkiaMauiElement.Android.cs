using Android.Graphics;
using Android.Views;
using Microsoft.Maui.Platform;
using View = Android.Views.View;

namespace DrawnUi.Draw;

public partial class SkiaMauiElement
{
    protected void TakeNativeSnapshot(SKSurface skiaSurface)
    {
        if (Element.Handler?.PlatformView is View nativeView)
        {
            Debug.WriteLine($"Taking snapshot..");
            if (nativeView.Width > 0 && nativeView.Height > 0)
            {
                using var snapshot = CreateBitmapFromView(nativeView);
                CachedBitmap.Canvas.DrawBitmap(snapshot, 0, 0);
                // Make sure to call Flush() to ensure that the drawing commands are executed
                CachedBitmap.Canvas.Flush();
                Debug.WriteLine($"Took snapshot !!!");
                SnapshotReady = true;
            }
        }
    }

    public SKBitmap CreateBitmapFromView(Android.Views.View view)
    {
        Bitmap bitmap = Bitmap.CreateBitmap(view.Width, (int)(view.Height), Bitmap.Config.Argb8888);
        var canvas = new Android.Graphics.Canvas(bitmap);

        //canvas.DrawColor(Android.Graphics.Color.Red); //debug snapshot with color

        view.Draw(canvas);

        // Convert the Android Bitmap to a SkiaSharp SKBitmap
        using (var stream = new MemoryStream())
        {
            bitmap.Compress(Bitmap.CompressFormat.Png, 100, stream);
            stream.Position = 0;
            return SKBitmap.Decode(stream);
        }
    }

    /// <summary>
    /// This is mainly ued by show/hide to display Snapshot instead the native view
    /// </summary>
    /// <param name="state"></param>
    public virtual void SetNativeVisibility(bool state)
    {
        IsNativeVisible = state;

        LayoutNativeView(Element);
    }

    public void NativeInvalidate()
    {
        NativeInvalidated = true;
        if (Element != null)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LayoutNativeView(Element);
            });
        }
    }

    private bool NativeInvalidated;
    public SKPoint ArrangedAt { get; set; }

    protected virtual void LayoutNativeView(VisualElement element)
    {
        if (element.Handler?.PlatformView is View nativeView)
        {
            nativeView.Visibility = VisualTransformNative.IsVisible && IsNativeVisible
                ? ViewStates.Visible
                : ViewStates.Invisible;

            if (nativeView.Visibility == ViewStates.Visible)
            {
                //nativeView.TranslationX = VisualTransformNative.Translation.X / RenderingScale;
                //nativeView.TranslationY = VisualTransformNative.Translation.Y / RenderingScale;
                nativeView.Rotation = VisualTransformNative.Rotation;
                nativeView.ScaleX = VisualTransformNative.Scale.X;
                nativeView.ScaleY = VisualTransformNative.Scale.Y;
                nativeView.Alpha = VisualTransformNative.Opacity;

                var locationX = (int)(VisualTransformNative.Translation.X / RenderingScale +
                                      VisualTransformNative.Rect.Left + this.Padding.Left * RenderingScale);
                var locationY = (int)(VisualTransformNative.Translation.Y / RenderingScale +
                                      VisualTransformNative.Rect.Top + this.Padding.Top * RenderingScale);

                nativeView.Layout(
                    locationX,
                    locationY,
                    (int)(locationX + VisualTransformNative.Rect.Width - this.Padding.Right * RenderingScale),
                    (int)(locationY + VisualTransformNative.Rect.Height - this.Padding.Bottom * RenderingScale));

                ArrangedAt = VisualTransformNative.Rect.Location;
            }

            if (!WasRendered)
                WasRendered = nativeView.Width > 0;

            Debug.WriteLine(
                $"[LayoutNativeView] {nativeView.Visibility} at {VisualTransformNative.Rect.Left}, {VisualTransformNative.Rect.Top}, vis {nativeView.Visibility}, opa {VisualTransformNative.Opacity} size {nativeView.Width}x{nativeView.Height}");
        }
    }

    private object locknative = new();

    protected void RemoveMauiElement(Element element)
    {
        lock (locknative)
        {
            if (element == null || element.Handler == null || IsDisposed)
                return;

            var layout = Superview.Handler?.PlatformView as ViewGroup;
            if (layout != null)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (element.Handler?.PlatformView is View nativeView)
                    {
                        layout.RemoveView(nativeView);
                        if (element is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                        //todo destroy child handler?
                    }
                });
            }
        }
    }

    protected virtual void SetupMauiElement(VisualElement element)
    {
        if (element == null)
            return;

        IViewHandler handler = Superview.Handler;

        if (handler != null)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (element.Handler == null)
                {
                    try
                    {
                        //create handler
                        var childHandler = element.ToHandler(handler.MauiContext);
                        //LayoutNativeView(Element);
                    }
                    catch (Exception e)
                    {
                        Super.Log(e);
                        //Java.Lang.NullPointerException: 'Attempt to read from field 'int android.view.ViewGroup$LayoutParams.width' on a null object reference'
                        return;
                    }
                }

                //add native view to canvas
                var view = element.Handler?.PlatformView as Android.Views.View;
                var layout = Superview.Handler?.PlatformView as ViewGroup;
                if (layout != null)
                    layout.AddView(view);

                if (VisualTransformNative.IsVisible)
                    LayoutNativeView(Element);
            });
        }
    }
}

public class MakeInputTransparent : Java.Lang.Object, View.IOnTouchListener
{
    public bool OnTouch(View v, MotionEvent e)
    {
        return false;
    }
}
