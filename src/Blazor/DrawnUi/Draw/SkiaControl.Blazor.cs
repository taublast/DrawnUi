/*
All the MAUI-related base SkiaControl implementation.
Normally other partial code definitions should be framework independent.
*/

using System.Collections;
using System.Drawing;
using System.Runtime.CompilerServices;


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

    public partial class SkiaControl : BindableObject
    {
 

        public static Color TransparentColor = Color.Transparent;
        public static Color WhiteColor = Color.White;
        public static Color BlackColor = Color.Black;
        public static Color RedColor = Color.Red;

        public static Color GetRandomColor()
        {
            byte r = (byte)Random.Next(256);
            byte g = (byte)Random.Next(256);
            byte b = (byte)Random.Next(256);

            return Color.FromRgb(r, g, b);
        }

        public virtual PrebuiltControlStyle UsingControlStyle
        {
            get
            {
                if (ControlStyle == PrebuiltControlStyle.Platform)
                {
#if IOS || MACCATALYST
                    return PrebuiltControlStyle.Cupertino;
#elif ANDROID
                    return PrebuiltControlStyle.Material;
#elif WINDOWS
                    return PrebuiltControlStyle.Windows;
#endif
                }

                return ControlStyle;
            }
        }

        public static readonly BindableProperty ClearColorProperty = BindableProperty.Create(nameof(ClearColor),
            typeof(Color), typeof(SkiaControl),
            TransparentColor,
            propertyChanged: NeedDraw);

        private SkiaShadow platformShadow;
        private SKPath platformClip;

        public Color ClearColor
        {
            get { return (Color)GetValue(ClearColorProperty); }
            set { SetValue(ClearColorProperty, value); }
        }

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

        /// <summary>
        /// Can override this for custom controls to apply padding differently from the default way
        /// </summary>
        /// <param name="padding"></param>
        /// <returns></returns>
        public virtual Thickness OnPaddingSet(Thickness padding)
        {
            return padding;
        }

        //public static readonly BindableProperty PaddingProperty = BindableProperty.Create(nameof(Padding),
        //    typeof(Thickness),
        //    typeof(SkiaControl), Thickness.Zero,
        //    propertyChanged: NeedInvalidateMeasure);

        //public Thickness Padding
        //{
        //    get { return (Thickness)GetValue(PaddingProperty); }
        //    set { SetValue(PaddingProperty, value); }
        //}

        public virtual void AddSubView(SkiaControl control)
        {
            if (control == null)
                return;

            control.SetParent(this);

            OnChildAdded(control);

            if (Debugger.IsAttached)
                Superview?.PostponeExecutionAfterDraw(() => { ReportHotreloadChildAdded(control); });

            //if (control is IHotReloadableView ihr)
            //{
            //    ihr.ReloadHandler = this;
            //    MauiHotReloadHelper.AddActiveView(ihr);
            //}
        }

        public virtual void RemoveSubView(SkiaControl control)
        {
            if (control == null)
                return;

            if (Debugger.IsAttached)
                Superview?.PostponeExecutionAfterDraw(() => { ReportHotreloadChildRemoved(control); });

            try
            {
                control.SetParent(null);
                OnChildRemoved(control);
            }
            catch (Exception e)
            {
                Super.Log(e);
            }
        }

        /// <summary>
        /// DrawingRect size changed
        /// </summary>
        protected virtual void OnLayoutChanged()
        {
            var ready = this.Height > 0 && this.Width > 0;
            if (ready)
            {
                if (!CompareSize(DrawingRect.Size, _lastSize, 1))
                {
                    _lastSize = DrawingRect.Size;

                    //todo /do we need this here? just for MAUI and this must use main thread and slow everything down.
                    //Frame = new Rect(DrawingRect.Left, DrawingRect.Top, DrawingRect.Width, DrawingRect.Height);
                }
            }

            LayoutReady = ready;
        }

        /// <summary>
        /// DrawingRect location changed. This will not be called if OnLayoutChanged was invoked for this frame!
        /// </summary>
        protected virtual void OnLayoutPositionChanged()
        {

        }

        /// <summary>
        /// Creates Shader for gradient and sets it to passed SKPaint along with BlendMode
        /// </summary>
        /// <param name="paint"></param>
        /// <param name="gradient"></param>
        /// <param name="destination"></param>
        public bool SetupGradient(SKPaint paint, SkiaGradient gradient, SKRect destination)
        {
            if (paint != null)
            {
                if (gradient != null)
                {
                    if (paint.Color.Alpha == 0)
                    {
                        paint.Color = SKColor.FromHsl(0, 0, 0);
                    }

                    paint.Color = SKColors.White;
                    paint.BlendMode = gradient.BlendMode;

                    var kill = paint.Shader;
                    paint.Shader = CreateGradient(destination, gradient);
                    kill?.Dispose();

                    return true;
                }
                else
                {
                    var kill = paint.Shader;
                    paint.Shader = null;
                    kill?.Dispose();
                }
            }

            return false;
        }

        /// <summary>
        /// Caching overload: rebuilds the shader only when the gradient object, its version,
        /// or the destination rect has changed since the last call. The caller owns the cached
        /// shader and must dispose it in their OnDisposing(). Passing gradient=null disposes
        /// and clears the cache.
        /// </summary>
        public bool SetupGradient(SKPaint paint, SkiaGradient gradient, SKRect destination,
            ref SKShader cachedShader, ref int cachedVersion, ref SKRect cachedRect,
            ref SkiaGradient cachedGradient)
        {
            if (paint == null) return false;

            if (gradient != null)
            {
                if (cachedShader == null
                    || !ReferenceEquals(cachedGradient, gradient)
                    || cachedVersion != gradient.Version
                    || cachedRect != destination)
                {
                    cachedShader?.Dispose();
                    cachedShader = CreateGradient(destination, gradient);
                    cachedGradient = gradient;
                    cachedVersion = gradient.Version;
                    cachedRect = destination;
                }

                paint.Color = SKColors.White;
                paint.BlendMode = gradient.BlendMode;
                paint.Shader = cachedShader;
                return true;
            }
            else
            {
                if (cachedShader != null)
                {
                    cachedShader.Dispose();
                    cachedShader = null;
                    cachedGradient = null;
                    cachedVersion = -1;
                }
                paint.Shader = null;
                return false;
            }
        }

        public static SKImageFilter CreateShadow(SkiaShadow shadow, float scale)
        {
            var colorShadow = shadow.Color;
            if (colorShadow.Alpha == 1.0)
            {
                colorShadow = shadow.Color.WithAlpha((float)shadow.Opacity);
            }

            if (shadow.ShadowOnly)
            {
                return SKImageFilter.CreateDropShadowOnly(
                    (float)(shadow.X * scale), (float)(shadow.Y * scale),
                    (float)(shadow.Blur * scale), (float)(shadow.Blur * scale),
                    colorShadow.ToSKColor());
            }
            else
            {
                return SKImageFilter.CreateDropShadow(
                    (float)(shadow.X * scale), (float)(shadow.Y * scale),
                    (float)(shadow.Blur * scale), (float)(shadow.Blur * scale),
                    colorShadow.ToSKColor());
            }
        }

        protected void UpdatePlatformShadow()
        {
            if (this.Shadow != null && Shadow.Brush != null)
            {
                PlatformShadow = this.Shadow.FromPlatform();
            }
            else
            {
                PlatformShadow = null;
            }
            InvalidateShadowPaint();
        }

        protected SkiaShadow PlatformShadow
        {
            get => platformShadow;
            set
            {
                if (platformShadow != value)
                {
                    platformShadow = value;
                    OnPropertyChanged();
                }
            }
        }

        private void GetPlatformClip(SKPath path, SKRect destination, float renderingScale)
        {
            if (this.Clip != null)
            {
                this.Clip.FromPlatform(path, destination, renderingScale);
            }
        }

        protected bool HasPlatformClip()
        {
            return Clip != null;
        }

        public static float GetDensity()
        {
            return (float)Super.Screen.Density;
        }

        /*
        #region HotReload

        IView IReplaceableView.ReplacedView =>
            MauiHotReloadHelper.GetReplacedView(this) ?? this;

        public void TransferState(IView newView)
        {
            //TODO: could hotreload the ViewModel
            if (newView is BindableObject v)
                v.BindingContext = BindingContext;
        }

        public virtual void Reload()
        {
            Invalidate();
        }

        public IReloadHandler ReloadHandler { get; set; }

        #endregion
        */
    }
}
