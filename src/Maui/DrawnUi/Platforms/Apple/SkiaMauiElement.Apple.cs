using CoreGraphics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using UIKit;


namespace DrawnUi.Draw;

public partial class SkiaMauiElement
{

    public bool ShowSnapshot => false;

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
        if (element.Handler?.PlatformView is UIView nativeView)
        {
            var visibility = //IsVisibleInViewTree() &&
                                    VisualTransformNative.IsVisible && IsNativeVisible ? Visibility.Visible : Visibility.Hidden;

            Debug.WriteLine($"[SkiaMauiElement] LayoutNativeView maybe at {VisualTransformNative.Rect.Location} visibility {visibility} size {VisualTransformNative.Rect.Size}");

            if (nativeView.IsFirstResponder && visibility == Visibility.Hidden)
            {
                nativeView.ResignFirstResponder();
            }

            nativeView.UpdateVisibility(visibility);

            bool needLayout = false;

            if (visibility == Visibility.Visible)
            {
                nativeView.ClipsToBounds = true;

                // Start with identity transform
                var transform = CGAffineTransform.MakeIdentity();

                transform = CGAffineTransform.Rotate(
                    transform,
                    VisualTransformNative.Rotation);

                transform = CGAffineTransform.Scale(
                    transform,
                    VisualTransformNative.Scale.X,
                    VisualTransformNative.Scale.Y);

                // Apply the combined transform
                nativeView.Transform = transform;

                var locationX = VisualTransformNative.Translation.X / RenderingScale  + VisualTransformNative.Rect.Left + this.Padding.Left;
                var locationY = VisualTransformNative.Translation.Y / RenderingScale + VisualTransformNative.Rect.Top + this.Padding.Top;

                // Set other properties
                nativeView.Alpha = VisualTransformNative.Opacity;
                nativeView.Frame = new CGRect(
                    locationX,
                    locationY,
                    VisualTransformNative.Rect.Width - (this.Padding.Left + this.Padding.Right),
                   VisualTransformNative.Rect.Height - (this.Padding.Top + this.Padding.Bottom)
                );

                //Debug.WriteLine($"[SkiaMauiElement] LayoutNativeView ARRANGED at {locationX},{locationY}, size {nativeView.Frame.Size}, translation: {VisualTransformNative.Translation}");
            }

            //Debug.WriteLine($"Layout Maui : {VisualTransformNative.Opacity} {VisualTransformNative.Translation} {VisualTransformNative.IsVisible}");
        }
    }

    public virtual void SetNativeVisibility(bool state)
    {
        IsNativeVisible = state;
        LayoutNativeView(Element);
    }

    protected void RemoveMauiElement(Element element)
    {
        if (element == null)
            return;

        var layout = Superview.Handler?.PlatformView as UIView;
        if (layout != null)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (element.Handler?.PlatformView is UIView nativeView)
                {
                    nativeView.RemoveFromSuperview();
                    if (element is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    //todo destroy child handler?
                }
                else
                if (NativeView != null)
                {
                    NativeView.RemoveFromSuperview();
                    if (NativeView is IDisposable disposable)
                    {
                        NativeView.Dispose();
                    }
                    NativeView = null;
                    //todo destroy child handler?
                }
            });
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
                    //create handler
                    var childHandler = element.ToHandler(handler.MauiContext);
                }
                //add native view to canvas
                var view = element.Handler?.PlatformView as UIView;
                var layout = Superview.Handler?.PlatformView as UIView;
                if (layout != null)
                {
                    this.NativeView = view;
                    layout.AddSubview(view);
                }

                LayoutNativeView(Element);
            });

        }

    }

    public UIView NativeView { get; set; }
}
