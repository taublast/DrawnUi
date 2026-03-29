using SkiaSharp;

namespace DrawnUi.Camera;

/// <summary>
/// A <see cref="SkiaLayout"/> that rotates and repositions its content so child controls stay
/// visually upright when the device is rotated, matching the orientation of the camera feed.
///
/// Place this over your <see cref="SkiaCamera"/> view and call
/// <see cref="Layout(SKRect, DeviceOrientation, int, int)"/> whenever the camera preview rect
/// or device orientation changes.
/// </summary>
public class CameraOverlayLayout : SkiaLayer
{
    protected CameraOverlayContent Content;

    public virtual void SetOrientation(DeviceOrientation orientation, SKRect frameRect, float overlayScale)
    {
        Content.Orientation = orientation;

        if (orientation == DeviceOrientation.LandscapeLeft || orientation == DeviceOrientation.LandscapeRight)
        {
            var rectLimits = new SKRect(
                frameRect.Top,
                frameRect.Left,
                frameRect.Top + frameRect.Height,
                frameRect.Left + frameRect.Width
            );

            if (orientation == DeviceOrientation.LandscapeLeft)
            {
                SetTransforms(rectLimits, 90, frameRect.Width / overlayScale - rectLimits.Left / overlayScale, rectLimits.Left / overlayScale);
            }
            else // LandscapeRight
            {
                SetTransforms(rectLimits, -90, -rectLimits.Left / overlayScale, frameRect.Height / overlayScale - rectLimits.Left / overlayScale);
            }
        }
        else
        {
            SetTransforms(frameRect, 0, 0, 0);
        }
    }

    public void SetTransforms(SKRect limits, float rotation, float x, float y)
    {
        Content.AnchorX = 0;
        Content.AnchorY = 0;
        Content.TranslationX = x;
        Content.TranslationY = y;
        Content.Rotation = rotation;
        Content.Limits = limits;
    }



}
