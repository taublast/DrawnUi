using System.Security.Cryptography.X509Certificates;

namespace DrawnUi.Controls;

public partial class SkiaShell
{
    public class ModalWrapper : ContentLayout
    {
        private readonly bool _animated;
        private readonly SkiaShell _shell;

        public ModalWrapper(bool useGestures,
            bool animated,
            bool willFreeze,
            Color backgroundColor,
            SkiaShell shell)
        {
            _willFreeze = willFreeze;
            _animated = animated;
            _shell = shell;
            if (backgroundColor == null)
                backgroundColor = PopupBackgroundColor;
            _color = backgroundColor;
            _useGestures = useGestures;
        }



        public virtual void WrapContent(SkiaControl content)
        {
            var drawer = new SkiaDrawer()
            {
                BlockGesturesBelow = true,
                RespondsToGestures = _useGestures,
                Animated = _animated,
                // slide in/out over PopupsAnimationSpeed ms (SnappingLayout: 0.7 * 300 / v seconds);
                // the drawer default of 1500 pt/s gives 0.14 s, too fast to read as an animation
                AutoVelocity = 300.0 * 0.7 / Math.Max(0.1, SkiaShell.PopupsAnimationSpeed / 1000.0),
                Tag = $"ModalDrawer_{content.GetType().Name}",
                Bounces = false,
                Direction = DrawerDirection.FromBottom,
                HeaderSize = 0,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Content = content
            };

            this.Content = drawer;
        }

        public SkiaDrawer Drawer
        {
            get
            {
                return Content as SkiaDrawer; ;
            }
        }

        public override void OnWillDisposeWithChildren()
        {
            base.OnWillDisposeWithChildren();

            if (_snapshot!=null)
                DisposeObject(_snapshot);

            Backdrop?.Dispose();
        }

        public SkiaBackdrop Backdrop { get; protected set; }

        protected override void SetContent(SkiaControl view)
        {
            base.SetContent(view);


            if (Backdrop == null)
            {
                Backdrop = new SkiaBackdrop()
                {
                    Tag = "backdrop",
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    ZIndex = -1,
                    InputTransparent = true,
                };
            }
            if (Views.All(x => x != Backdrop))
            {
                AddSubView(Backdrop);
            }

            if (Backdrop != null)
            {
                if (_willFreeze)
                {
                    Backdrop.Blur = 0;
                }
                else
                {
                    Backdrop.BackgroundColor = _color;
                    Backdrop.Blur = SkiaShell.PopupsBackgroundBlur;
                }
                //Backdrop.Brightness = 1 - _shell.FrozenBackgroundDim;
            }

            if (Content != null && _animated)
            {
                SetupAppearingAnimation(Content);
            }
        }

        protected override int DrawViews(DrawingContext context)
        {
            if (context.Context.Superview == null || context.Destination.Width <= 0 || context.Destination.Height <= 0)
            {
                return 0;
            }

            var drawViews = GetOrderedSubviews();
            return RenderViewsList(context, drawViews);
        }

        public virtual void SetupAppearingAnimation(SkiaControl content)
        {
            //content.Scale = 0.5;
            //content.Opacity = 0.1;
        }

        //public async Task CloseAsync()
        //{
        //    if (Content != null && _animated && IsVisibleInViewTree())
        //    {
        //        await Task.WhenAll(
        //            Content.FadeToAsync(0, PopupsAnimationSpeed),
        //            Content.ScaleToAsync(0, 0, PopupsAnimationSpeed));
        //    }

        //    await _shell.Popups.Close(this, _animated);
        //}

        //protected override void OnLayoutReady()
        //{
        //    base.OnLayoutReady();

        //    if (Content != null && _animated)
        //    {
        //        Task.WhenAll(
        //            Content.FadeToAsync(1, PopupsAnimationSpeed),
        //            Content.ScaleToAsync(1, 1, PopupsAnimationSpeed));
        //    }


        //}

        bool frozen;
        private readonly Color _color;
        private readonly bool _willFreeze;
        private readonly bool _useGestures;
        private SKImage _snapshot;

        public bool IsFrozen { get; set; }

        protected override void Draw(DrawingContext context)
        {
            base.Draw(context);

            if (_willFreeze && !frozen && LayoutReady)
            {
                if (_snapshot == null)
                    _snapshot = Backdrop.GetImage();

                if (_snapshot != null)
                {
                    IsFrozen = true;
                    frozen = true;
                    _shell.FreezeRootLayout(this, _snapshot, _animated, _color, (float)SkiaShell.PopupsBackgroundBlur).ConfigureAwait(true);
                    Backdrop.IsVisible = false;
                }
            }
        }


    }


}
