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
    public partial class SkiaControl : VisualElement,
        //IHotReloadableView, IReloadHandler, // to support New HotReload
        IVisualTreeElement, // to support VS HotReload
        IContainer // to support VS HotReload full page reload mode
    {
        #region IContainer //used by xaml generator

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
            }

            #endregion
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
