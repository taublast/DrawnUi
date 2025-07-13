/*
All the MAUI-related base SkiaControl implementation.
Normally other partial code definitions should be framework independent.
*/

using System.Collections;
using Microsoft.Maui.Controls;
using Microsoft.Maui.HotReload;
using IContainer = Microsoft.Maui.IContainer;

namespace DrawnUi.Draw
{
    [ContentProperty(nameof(Children))]
    public partial class SkiaControl : VisualElement,
        //IHotReloadableView, IReloadHandler, // to support New HotReload
        IVisualTreeElement, // to support VS HotReload
        IContainer // to support VS HotReload full page reload mode
    {
        #region IContainer

        public IEnumerator<IView> GetEnumerator()
        {
            return Children.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Children.GetEnumerator();
        }

        public void Add(IView item)
        {
            if (item is SkiaControl skia)
            {
                Children.Add(skia);
            }
        }

        public void Clear()
        {
            Children.Clear();
        }

        public bool Contains(IView item)
        {
            return Children.Contains(item);
        }

        public void CopyTo(IView[] array, int arrayIndex)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));
            if (arrayIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(arrayIndex), "Array index must be non-negative.");
            if (arrayIndex + Children.Count > array.Length)
                throw new ArgumentException(
                    "The array is too small to accommodate the collection starting at the specified index.",
                    nameof(array));

            for (int i = 0; i < Children.Count; i++)
            {
                array[arrayIndex + i] = Children[i];
            }
        }

        public bool Remove(IView item)
        {
            var found = false;
            if (item is SkiaControl skia)
            {
                found = Children.Contains(skia);
                if (found)
                {
                    Children.Remove(skia);
                }
            }

            return found;
        }

        public int Count
        {
            get => Children.Count();
        }

        public bool IsReadOnly => false;

        public int IndexOf(IView item)
        {
            var found = -1;
            if (item is SkiaControl skia)
            {
                return Children.IndexOf(skia);
            }

            return found;
        }

        public void Insert(int index, IView item)
        {
            if (item is SkiaControl skia)
            {
                Children.Insert(index, skia);
            }
        }

        public void RemoveAt(int index)
        {
            Children.RemoveAt(index);
        }

        public IView this[int index]
        {
            get { return Children[index]; }
            set
            {
                if (value is SkiaControl skia)
                {
                    Children[index] = skia;
                }
                else
                {
                    throw new ArgumentException("Item must be of type SkiaControl", nameof(value));
                }
            }
        }

        #endregion

        #region IVisualTreeElement

        public virtual IReadOnlyList<IVisualTreeElement> GetVisualChildren() //working fine
        {
            return Views.ToList().Cast<IVisualTreeElement>().ToList();
        }

        public virtual IVisualTreeElement GetVisualParent() //working fine
        {
            return Parent as IVisualTreeElement;
        }

        public virtual void ReportHotreloadChildAdded(SkiaControl child)
        {
            if (child == null)
                return;

            //this.OnChildAdded(child);

            var children = GetVisualChildren();
            var index = children.FindIndex(child);

            if (index >= 0)
                VisualDiagnostics.OnChildAdded(this, child, index);
        }

        public virtual void ReportHotreloadChildRemoved(SkiaControl control)
        {
            if (control == null)
                return;


            var children = GetVisualChildren();
            var index = children.FindIndex(control);

            if (index >= 0)
                VisualDiagnostics.OnChildRemoved(this, control, index);
            //            this.OnChildRemoved(control, index);
        }

        #endregion

        public static Color TransparentColor = Colors.Transparent;
        public static Color WhiteColor = Colors.White;
        public static Color BlackColor = Colors.Black;
        public static Color RedColor = Colors.Red;

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
            Colors.Transparent,
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
                Super.Log(e);
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
            else if (propertyName == nameof(Clip))
            {
                Update();
            }
            else if (propertyName.IsEither(
                         nameof(Padding),
                         nameof(HorizontalOptions), nameof(VerticalOptions),
                         nameof(HeightRequest), nameof(WidthRequest),
                         nameof(MaximumWidthRequest), nameof(MinimumWidthRequest),
                         nameof(MaximumHeightRequest), nameof(MinimumHeightRequest)
                     ))
            {
                InvalidateMeasure();
            }
            else if (propertyName.IsEither(nameof(IsVisible)))
            {
                OnVisibilityChanged(IsVisible);

                Repaint();
            }

            #endregion
        }

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
