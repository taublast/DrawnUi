using System.Collections;

using IContainer = Microsoft.Maui.IContainer;

namespace DrawnUi.Views
{

    public partial class DrawnView : ContentView, IDrawnBase, IAnimatorsManager, IVisualTreeElement, IContainer
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

        public virtual void Clear()
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
        private void Init()
        {
            if (!_initialized)
            {
                _initialized = true;

                HorizontalOptions = LayoutOptions.Start;
                VerticalOptions = LayoutOptions.Start;
                Padding = new Thickness(0);

                SizeChanged += ViewSizeChanged;

                //bug this creates garbage on aandroid on every frame
                // DeviceDisplay.Current.MainDisplayInfoChanged += OnMainDisplayInfoChanged;
                InitFramework(true);


                SurfaceCacheManager = new(this);
            }
        }

        protected void FixDensity()
        {
            if (_renderingScale <= 0.0)
            {
                var scale = (float)GetDensity();
                if (scale <= 0.0)
                {
                    scale = (float)(CanvasView.CanvasSize.Width / this.Width);
                }

                RenderingScale = scale;
            }
        }


        public virtual void ReportHotreloadChildRemoved(SkiaControl control)
        {
            if (control == null)
                return;

            var index = Views.FindIndex(control);
            VisualDiagnostics.OnChildRemoved(this, control, index);
        }

        protected override void OnHandlerChanging(HandlerChangingEventArgs args)
        {
            if (args.NewHandler == null || args.OldHandler != null)
            {
                DestroySkiaView();

#if ONPLATFORM
                DisposePlatform();
#endif
            }

            base.OnHandlerChanging(args);
        }

        protected override void OnHandlerChanged()
        {
            base.OnHandlerChanged();

#if ANDROID
            OnHandlerChangedInternal();
#endif

            if (Handler != null)
            {
                CreateSkiaView();

#if ONPLATFORM
                SetupRenderingLoop();
#endif
            }

            HandlerWasSet?.Invoke(this, Handler != null);
            //InvalidateChildren(); //need clear gfx cache
        }

        private VisualElement _visibilityParent;
        private VisualTreeHandler VisualTree;  
        public virtual void OnHotReload()
        {

        }


        IReadOnlyList<IVisualTreeElement> IVisualTreeElement.GetVisualChildren()
        {
            return Views.Cast<IVisualTreeElement>()
                .ToList();
        }

        public void DestroyThis()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    this.Handler?.DisconnectHandler();
                }
                catch (Exception e)
                {
                    Super.Log(e);
                }
            });
        }

        protected virtual void InitFramework(bool subscribe)
        {
            if (subscribe)
            {
                Super.HotReload -= SuperOnHotReload;
                Super.HotReload += SuperOnHotReload;
            }
            else
            {
                Super.HotReload -= SuperOnHotReload;
            }
        }

        private void SuperOnHotReload(Type[] obj)
        {
            OnHotReload();
        }
    }
}
