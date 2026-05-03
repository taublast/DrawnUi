/*
All the MAUI-related base SkiaControl implementation.
Normally other partial code definitions should be framework independent.
*/

using System.Collections;
using System.Runtime.CompilerServices;
using Color = DrawnUi.Color;


namespace DrawnUi.Draw
{
    public class VisualDiagnostics
    {
        public static void OnChildRemoved(SkiaControl skiaControl, SkiaControl control, int index)
        {
 
        }

        public static void OnChildAdded(SkiaControl skiaControl, SkiaControl child, int index)
        {
 
        }
    }

    public partial class SkiaControl : View
    {
        private static void ReportHotreloadChildAdded(SkiaControl control)
        {
        }

        private static void ReportHotreloadChildRemoved(SkiaControl control)
        {
        }


        public static Color TransparentColor = Colors.Transparent;
        public static Color WhiteColor = Colors.White;
        public static Color BlackColor = Colors.Black;
        public static Color RedColor = Colors.Red;

        protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            try
            {
                base.OnPropertyChanged(propertyName);
            }
            catch (Exception e)
            {
                //we are avoiding MAUI crashes due concurrent access to properties from different threads
                Super.Log($"[{propertyName}] {e}");
            }

            //if (!isApplyingStyle && !string.IsNullOrEmpty(propertyName))
            //{
            //    ExplicitPropertiesSet[propertyName] = true;
            //}

            #region intercept properties coming from VisualElement..

            //some VisualElement props will not call this method so we would override them as new

            if (propertyName == nameof(ZIndex))
            {
                Parent?.InvalidateViewsList();
                Repaint();
            }
            else if (propertyName.IsEither(
                         nameof(Opacity),
                         nameof(TranslationX), nameof(TranslationY),
                         nameof(Rotation),
                         nameof(AnchorX), nameof(AnchorY),
                         nameof(RotationX), nameof(RotationY),
                         nameof(ScaleX), nameof(ScaleY)
                     ))
            {
                Repaint(); 
            }
            else if (propertyName.IsEither(nameof(BackgroundColor),
                         nameof(IsClippedToBounds)
                     ))
            {
                Update();
            }
            else if (propertyName == nameof(Shadow))
            {
                UpdatePlatformShadow();
            }
            else if (propertyName == "Shadows")
            {
                var stop = 1;
            }
            else if (propertyName == nameof(Clip))
            {
                Update();
            }
            else if (propertyName == nameof(Padding))
            {
                UsePadding = OnPaddingSet(this.Padding);
                InvalidateMeasure();
            }
            else if (propertyName.IsEither(
                         nameof(HorizontalOptions), nameof(VerticalOptions)))
            {
                InvalidateMeasure();
            }
            else if (propertyName.IsEither(
                         nameof(Margin),
                         nameof(HeightRequest), nameof(WidthRequest),
                         nameof(MaximumWidthRequest), nameof(MinimumWidthRequest),
                         nameof(MaximumHeightRequest), nameof(MinimumHeightRequest)
                     ))
            {
                InvalidateMeasure();
                if (UsingCacheType != SkiaCacheType.ImageDoubleBuffered)
                {
                    UpdateSizeRequest();
                }
            }
            else if (propertyName.IsEither(nameof(IsVisible)))
            {
                OnVisibilityChanged(IsVisible);

                Repaint();
            }

            #endregion
        }
         
         
    }
}
