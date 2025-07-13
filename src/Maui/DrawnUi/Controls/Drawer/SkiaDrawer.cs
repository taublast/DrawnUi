using DrawnUi.Infrastructure.Helpers;
using System.Numerics;
using System.Windows.Input;

namespace DrawnUi.Controls
{
    [ContentProperty("Content")]
    public class SkiaDrawer : SnappingLayout, IVisibilityAware
    {
        public override void ApplyBindingContext()
        {
            if (Content != null && Content.BindingContext == null)
                Content?.SetInheritedBindingContext(this.BindingContext);

            base.ApplyBindingContext();
        }

        /// <summary>
        /// Command to close the drawer programmatically. Sets IsOpen to false.
        /// </summary>
        public ICommand CommandClose
        {
            get
            {
                return new Command(async (context) =>
                {
                    if (TouchEffect.CheckLockAndSet())
                        return;

                    IsOpen = false;
                });
            }
        }

        /// <summary>
        /// Command to open the drawer programmatically. Sets IsOpen to true.
        /// </summary>
        public ICommand CommandOpen
        {
            get
            {
                return new Command(async (context) =>
                {
                    if (TouchEffect.CheckLockAndSet())
                        return;

                    IsOpen = true;
                });
            }
        }

        /// <summary>
        /// Command to toggle the drawer state programmatically. Inverts the current IsOpen value.
        /// </summary>
        public ICommand CommandToggle
        {
            get
            {
                return new Command(async (context) =>
                {
                    if (TouchEffect.CheckLockAndSet())
                        return;

                    IsOpen = !IsOpen;
                });
            }
        }

        public override void OnAppeared()
        {
            base.OnAppeared();

            if (Content is IVisibilityAware aware)
            {
                aware.OnAppeared();
            }
        }

        public override void OnDisappearing()
        {
            base.OnDisappearing();

            if (Content is IVisibilityAware aware)
            {
                aware.OnDisappearing();
            }
        }

        public override void OnDisappeared()
        {
            base.OnDisappeared();

            if (Content is IVisibilityAware aware)
            {
                aware.OnDisappeared();
            }
        }

        public override void OnAppearing()
        {
            base.OnAppearing();

            if (Content is IVisibilityAware aware)
            {
                aware.OnAppearing();
            }
        }

        public override void OnTransitionChanged()
        {
            base.OnTransitionChanged();

            if (!InTransition)
            {
                SetIsOpen();
                SendStateTransitionComplete();
            }
        }

        public override void OnWillDisposeWithChildren()
        {
            base.OnWillDisposeWithChildren();

            StateTransitionComplete = null;
            IsOpenChanged = null;
            Stopped = null;

            Content?.Dispose();
        }

        #region EVENTS

        public virtual void SendStateTransitionComplete()
        {
            StateTransitionComplete?.Invoke(this, IsOpen);
        }

        public event EventHandler<bool> StateTransitionComplete;

        public event EventHandler<bool> IsOpenChanged;
        public event EventHandler<Vector2> Stopped;

        #endregion

        #region PROPERTIES

        public static readonly BindableProperty AmplitudeSizeProperty = BindableProperty.Create(
            nameof(AmplitudeSize),
            typeof(double),
            typeof(SkiaDrawer),
            -1.0, propertyChanged: NeedApplyOptions);

        /// <summary>
        /// If set to other than -1 will be used instead of HeaderSize for amplitude calculation, amplitude = drawer size - header.
        /// </summary>
        public double AmplitudeSize
        {
            get { return (double)GetValue(AmplitudeSizeProperty); }
            set { SetValue(AmplitudeSizeProperty, value); }
        }

        public static readonly BindableProperty HeaderSizeProperty = BindableProperty.Create(
            nameof(HeaderSize),
            typeof(double),
            typeof(SkiaDrawer),
            0.0, propertyChanged: NeedApplyOptions);

        /// <summary>
        /// Size of the area that will remain on screen when drawer is closed
        /// </summary>
        public double HeaderSize
        {
            get { return (double)GetValue(HeaderSizeProperty); }
            set { SetValue(HeaderSizeProperty, value); }
        }

        public static readonly BindableProperty DirectionProperty = BindableProperty.Create(
            nameof(Direction),
            typeof(DrawerDirection),
            typeof(SkiaDrawer),
            DrawerDirection.FromBottom, propertyChanged: NeedApplyOptions);

        public DrawerDirection Direction
        {
            get { return (DrawerDirection)GetValue(DirectionProperty); }
            set { SetValue(DirectionProperty, value); }
        }

        public static readonly BindableProperty IsOpenProperty = BindableProperty.Create(
            nameof(IsOpen),
            typeof(bool),
            typeof(SkiaDrawer),
            false, BindingMode.TwoWay,
            propertyChanged: (b, o, n) =>
            {
                if (b is SkiaDrawer control)
                {
                    control.IsOpenChanged?.Invoke(control, (bool)n);
                    control.ApplyOptions();
                    //Trace.WriteLine($"Drawer {(bool)n}");
                }
            });

        public bool IsOpen
        {
            get { return (bool)GetValue(IsOpenProperty); }
            set { SetValue(IsOpenProperty, value); }
        }

        public static readonly BindableProperty ContentProperty = BindableProperty.Create(
            nameof(Content),
            typeof(SkiaControl), typeof(ContentLayout),
            null,
            propertyChanged: OnReplaceContent);

        private static void OnReplaceContent(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaDrawer control)
            {
                control.SetContent(newvalue as SkiaControl);
            }
        }

        public SkiaControl Content
        {
            get { return (SkiaControl)GetValue(ContentProperty); }
            set { SetValue(ContentProperty, value); }
        }

        #endregion

        protected virtual void SetContent(SkiaControl view)
        {
            var oldContent = Views.FirstOrDefault(x => x == Content);
            if (view != oldContent)
            {
                if (oldContent != null)
                {
                    RemoveSubView(oldContent);
                }

                if (view != null)
                {
                    AddSubView(view);
                }
            }
        }

        public void Close()
        {
            IsOpen = false;
        }

        public void Open()
        {
            IsOpen = true;
        }

        void Init()
        {
            CurrentSnap = new(-1, -1);

            if (VectorAnimatorSpring == null)
            {
                VectorAnimatorSpring = new(this)
                {
                    OnStart = () => { },
                    OnStop = () => { Stopped?.Invoke(this, CurrentPosition); },
                    OnVectorUpdated = (value) => { ApplyPosition(value); }
                };
                AnimatorRange = new(this)
                {
                    OnVectorUpdated = (value) => { ApplyPosition(value); },
                    OnStop = () => { Stopped?.Invoke(this, CurrentPosition); }
                };
            }

            ApplyOptions();
        }

        protected override void OnLayoutChanged()
        {
            base.OnLayoutChanged();

            if (Parent != null)
            {
                Viewport = Parent.DrawingRect;

                if (!CompareRects(Viewport, _lastViewport, 1))
                {
                    _lastViewport = Viewport;
                    Init();
                }
            }
        }

        /// <summary>
        /// In points
        /// </summary>
        /// <returns></returns>
        protected virtual Vector2 GetOffsetToHide()
        {
            var mySize = this.MeasuredSize;
            var headerSize = HeaderSize;
            double amplitudeSize = AmplitudeSize;
            //round
            var height = mySize.Units.Height;
            var width = mySize.Units.Width;

            switch (this.Direction)
            {
                case DrawerDirection.FromLeft:
                    if (AmplitudeSize >= 0)
                        headerSize = amplitudeSize >= 0 ? width - amplitudeSize : width;
                    return new Vector2((float)(-(width - headerSize)), 0);

                case DrawerDirection.FromRight:
                    if (AmplitudeSize >= 0)
                        headerSize = amplitudeSize >= 0 ? width - amplitudeSize : width;
                    return new Vector2((float)(width - headerSize), 0);

                case DrawerDirection.FromBottom:
                    if (AmplitudeSize >= 0)
                        headerSize = amplitudeSize >= 0 ? height - amplitudeSize : height;
                    return new Vector2(0, (float)(height - headerSize));

                case DrawerDirection.FromTop:
                    if (AmplitudeSize >= 0)
                        headerSize = amplitudeSize >= 0 ? height - amplitudeSize : height;
                    return new Vector2(0, (float)(-(height - headerSize)));

                default:
                    return Vector2.Zero;
            }
        }

        double Project(double initialVelocity, double decelerationRate)
        {
            if (decelerationRate >= 1)
            {
                Debug.Assert(false);
                return initialVelocity;
            }

            return initialVelocity * decelerationRate / (1 - decelerationRate);
        }

        protected override Vector2 GetAutoVelocity(Vector2 displacement)
        {
            var velocity = 1500; //todo as property

            switch (this.Direction)
            {
                case DrawerDirection.FromLeft:
                case DrawerDirection.FromRight:
                    return new Vector2(-velocity * Math.Sign(displacement.X), 0);

                case DrawerDirection.FromTop:
                case DrawerDirection.FromBottom:
                    return new Vector2(0, -velocity * Math.Sign(displacement.Y));
            }

            return base.GetAutoVelocity(displacement);
        }

        public override SKRect GetContentOffsetBounds()
        {
            switch (this.Direction)
            {
                case DrawerDirection.FromLeft:
                    return new SKRect(
                        SnapPoints[1].X,
                        SnapPoints[0].Y,
                        SnapPoints[0].X,
                        SnapPoints[1].Y
                    );

                case DrawerDirection.FromRight:
                    return new SKRect(
                        SnapPoints[0].X,
                        SnapPoints[0].Y,
                        SnapPoints[1].X,
                        SnapPoints[1].Y
                    );


                case DrawerDirection.FromTop:
                    return new SKRect(
                        SnapPoints[0].X,
                        SnapPoints[1].Y,
                        SnapPoints[1].X,
                        SnapPoints[0].Y
                    );

                case DrawerDirection.FromBottom:
                default:
                    return new SKRect(
                        SnapPoints[0].X,
                        SnapPoints[0].Y,
                        SnapPoints[1].X,
                        SnapPoints[1].Y
                    );
            }
        }

        public override void ApplyPosition(Vector2 position)
        {
            TranslationX = position.X;
            TranslationY = position.Y;

            base.ApplyPosition(position);
        }

        public override void ApplyOptions()
        {
            if (Parent == null)
                return;

            Viewport = Parent.DrawingRect;

            var hideContent = GetOffsetToHide();

            if (HeaderSize > 0)
            {
                hideContent = new(hideContent.X + 1, hideContent.Y + 1);
            }

            if (ItemsSource != null)
            {
                SnapPoints = new List<Vector2>(ItemsSource.Count) { new(0, 0), hideContent };
            }
            else
            {
                SnapPoints = new List<Vector2>() { new(0, 0), hideContent };
            }

            ContentOffsetBounds = GetContentOffsetBounds();

            var snap = Vector2.Zero;

            snap = IsOpen ? SnapPoints[0] : SnapPoints[1];

            ScrollToOffset(snap, Vector2.Zero, Animated && WasDrawn);
        }

        protected override void Paint(DrawingContext ctx)
        {
            if (IsOpen && !LayoutReady)
                return;

            base.Paint(ctx);
        }


        //public override bool CheckTransitionEnded()
        //{
        //    if (this.IsOpen)
        //    {
        //        return AreVectorsEqual(CurrentPosition, SnapPoints[0], 1);
        //    }

        //    return AreVectorsEqual(CurrentPosition, SnapPoints[1], 1);
        //}

        public bool CheckNeedToSnap()
        {
            var ok = AreVectorsEqual(CurrentPosition, SnapPoints[0], 1) ||
                     AreVectorsEqual(CurrentPosition, SnapPoints[1], 1);
            return !ok;
        }


        public void SetIsOpen()
        {
            var isOpen = true;
            if (SnapPoints.Any())
            {
                if (SnapPoints[1] == CurrentSnap)
                    isOpen = false;
            }

            //Debug.WriteLine($"[SkiaDrawer] SetIsOpen: open {isOpen} moving {InTransition}");
            IsOpen = isOpen;
        }

        public override void UpdateReportedPosition()
        {
            if (!InTransition)
            {
                SetIsOpen();
            }
            //Debug.WriteLine($"[SkiaDrawer] UpdateReportedPosition: open {IsOpen} moving {InTransition}");
        }

        /// <summary>
        /// This uses whole viewport size, do not use this for snapping
        /// </summary>
        /// <param name="overscrollPoint"></param>
        /// <param name="contentRect"></param>
        /// <param name="viewportSize"></param>
        /// <returns></returns>
        public static SKPoint GetClosestSidePoint(SKPoint overscrollPoint, SKRect contentRect, SKSize viewportSize)
        {
            SKPoint closestPoint = new SKPoint();

            // The overscrollPoint represents the negative of the content offset, so we need to reverse it for calculation
            SKPoint contentOffset = new SKPoint(-overscrollPoint.X, -overscrollPoint.Y);

            var width = contentRect.Width - viewportSize.Width;
            if (width < 0)
                width = 0;

            if (contentOffset.X < contentRect.Left)
                closestPoint.X = contentRect.Left;
            else if (contentOffset.X > width)
                closestPoint.X = width;
            else
                closestPoint.X = contentOffset.X;

            var height = contentRect.Height - viewportSize.Height;
            if (height < 0)
                height = 0;

            if (contentOffset.Y < contentRect.Top)
                closestPoint.Y = contentRect.Top;
            else if (contentOffset.Y > height)
                closestPoint.Y = height;
            else
                closestPoint.Y = contentOffset.Y;

            // Reverse the offset back to the overscroll representation for the result
            closestPoint.X = -closestPoint.X;
            closestPoint.Y = -closestPoint.Y;

            //if (closestPoint.Y > 0 && closestPoint.Y < viewportSize.Height)
            //{
            //	closestPoint.Y = -viewportSize.Height;
            //}

            //if (closestPoint.X > 0 && closestPoint.X < viewportSize.Width)
            //{
            //	closestPoint.X = -viewportSize.Width;
            //}

            return closestPoint;
        }

        protected bool IsUserFocused { get; set; }
        protected bool IsUserPanning { get; set; }
        protected Vector2 _panningOffset;
        private SKRect _lastViewport;
        protected VelocityAccumulator VelocityAccumulator { get; } = new();
        private bool _inContact;
        protected bool ChildWasTapped;

        private bool GesturesPassThrough = false;

        public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args,
            GestureEventProcessingInfo apply)
        {
            var consumedDefault = BlockGesturesBelow ? this : null;

            SKRect hitbox;
            if (Super.UseFrozenVisualLayers)
            {
                hitbox = Content.VisualLayer.HitBoxWithTransforms.Pixels;
            }
            else
            {
                var legacy = this.Content.GetPositionOnCanvas();
                hitbox = new SKRect(legacy.X, legacy.Y, legacy.X + DrawingRect.Width, legacy.Y + DrawingRect.Height);
            }

            //check we are inside the content
            if (!_inContact && args.Type != TouchActionResult.Up)
            {
                if (!hitbox.ContainsInclusive(args.Event.Location.X, args.Event.Location.Y))
                {
                    if (AutoClose && IsOpen && !InTransition)
                    {
                        IsOpen = false;
                    }

                    return null;
                }
            }
            else
            {
                var passed = true;
            }

            bool passedToChildren = false;

            ISkiaGestureListener PassToChildren()
            {
                passedToChildren = true;

                return base.ProcessGestures(args, apply);
            }

            if (TouchEffect.LogEnabled)
                Super.Log($"[DRAWER] {this.Tag} Got {args.Type} touches {args.Event.NumberOfTouches}..");

            ISkiaGestureListener consumed = null;

            //pass Released always to children first
            if (args.Type == TouchActionResult.Up
                || args.Type == TouchActionResult.Tapped
                || !IsUserPanning
                || !RespondsToGestures)
            {
                consumed = PassToChildren();
                if (consumed == this)
                {
                    //BlockGesturesBelow fired
                    consumed = null;
                }

                if (consumed != null && args.Type != TouchActionResult.Up)
                {
                    if (TouchEffect.LogEnabled)
                        Super.Log($"[DRAWER] {this.Tag} {args.Type} CONSUMED by {consumed}");

                    if (args.Type == TouchActionResult.Tapped)
                    {
                        ChildWasTapped = true;
                    }

                    return consumed;
                }
            }

            if (!RespondsToGestures)
                return consumed;


            // todo
            // if the gesture is not in the header we first will pass it to children, 
            // and process only if children didn't consume it

            void ResetPan()
            {
                IsUserFocused = true;
                IsUserPanning = false;
                ChildWasTapped = false;

                AnimatorRange?.Stop();
                VectorAnimatorSpring.Stop();
                VelocityAccumulator.Clear();

                _panningOffset = new((float)TranslationX, (float)TranslationY);
            }

            Vector2 velocity;
            float useVelocity = 0;

            if (true)
            {
                _inContact = true;

                switch (args.Type)
                {
                    case TouchActionResult.Tapped:
                    case TouchActionResult.LongPressing:

                        consumed = this;
                        break;

                    case TouchActionResult.Down:
                        if (args.Event.NumberOfTouches == 1) //first finger down
                        {
                            ResetPan();
                        }

                        //consumed = this;

                        break;

                    case TouchActionResult.Panning when args.Event.NumberOfTouches == 1:

                        var direction = DirectionType.None;
                        bool lockBounce = false;

                        // Determine if the gesture is panning towards an existing snap point
                        if (Direction == DrawerDirection.FromLeft)
                        {
                            direction = DirectionType.Horizontal;
                            if (args.Event.Distance.Delta.X > 0)
                                // horizontal, lock if panning to right and we are already at SnapPoints[0]
                                lockBounce = AreVectorsEqual(CurrentPosition, SnapPoints[0], 1);
                        }
                        else if (Direction == DrawerDirection.FromRight)
                        {
                            direction = DirectionType.Horizontal;
                            if (args.Event.Distance.Delta.X < 0)
                                // horizontal, lock if panning to left and we are already at SnapPoints[0]
                                lockBounce = AreVectorsEqual(CurrentPosition, SnapPoints[0], 1);
                        }
                        else if (Direction == DrawerDirection.FromBottom)
                        {
                            direction = DirectionType.Vertical;
                            if (args.Event.Distance.Delta.Y < 0)
                                // vertical, lock if panning to top and we are already at SnapPoints[0]
                                lockBounce = AreVectorsEqual(CurrentPosition, SnapPoints[0], 1);
                        }
                        else if (Direction == DrawerDirection.FromTop)
                        {
                            direction = DirectionType.Vertical;
                            if (args.Event.Distance.Delta.Y > 0)
                                // vertical, lock if panning to bottom and we are already at SnapPoints[1]
                                lockBounce = AreVectorsEqual(CurrentPosition, SnapPoints[1], 1);
                        }

                        if (!IsUserFocused)
                        {
                            ResetPan();
                            _panningOffset = new(_panningOffset.X - args.Event.Distance.Delta.X / RenderingScale,
                                _panningOffset.Y - args.Event.Distance.Delta.Y / RenderingScale);
                        }

                        var x = _panningOffset.X + args.Event.Distance.Delta.X / RenderingScale;
                        var y = _panningOffset.Y + args.Event.Distance.Delta.Y / RenderingScale;


                        if (!IsUserPanning) //for the first panning move only
                        {
                            var mainDirection = GetDirectionType(_panningOffset, new Vector2(x, y), 0.9f);

                            if (direction == DirectionType.None || mainDirection != direction && IgnoreWrongDirection)
                            {
                                break; //ignore this gesture
                            }

                            IsUserPanning = true;
                        }

                        if (IsUserPanning)
                        {
                            if (direction == DirectionType.Horizontal)
                            {
                                useVelocity = (float)(args.Event.Distance.Velocity.X / RenderingScale);
                                velocity = new(useVelocity, 0);
                                y = 0;
                            }
                            else
                            {
                                useVelocity = (float)(args.Event.Distance.Velocity.Y / RenderingScale);
                                velocity = new(0, useVelocity);
                                x = 0;
                            }

                            //record velocity
                            VelocityAccumulator.CaptureVelocity(velocity);

                            //saving non clamped
                            _panningOffset.X = x;
                            _panningOffset.Y = y;

                            // Allow bouncing only if Bounces is true and we are not panning towards an existing snap point
                            bool shouldBounce = Bounces && !lockBounce;

                            var clamped = ClampOffset((float)x, (float)y, shouldBounce);

                            if (!Bounces &&
                                lockBounce) //if we reached side and boucing is off we cannot drag further so maybe pass pan to parent
                            {
                                if (AreEqual(clamped.X, 0, 1) && AreEqual(clamped.Y, 0, 1))
                                {
                                    var verticalMove = DrawerDirection.FromBottom;
                                    if (args.Event.Distance.Delta.Y < 0)
                                        verticalMove = DrawerDirection.FromTop;

                                    var horizontalMove = DrawerDirection.FromLeft;
                                    if (args.Event.Distance.Delta.X < 0)
                                        horizontalMove = DrawerDirection.FromRight;

                                    if ((direction == DirectionType.Vertical && Direction == verticalMove)
                                        || (direction == DirectionType.Horizontal && Direction == horizontalMove))
                                    {
                                        IsUserPanning = false;
                                        return null;
                                    }
                                }
                            }

                            ApplyPosition(clamped);

                            consumed = this;
                            //var clamped = ClampOffset((float)x, (float)y, Bounces);                   
                        }
                        else
                        {
                            return consumedDefault;
                        }

                        break;

                    case TouchActionResult.Up:

                        if (ChildWasTapped || !IsUserPanning)
                            break;

                        direction = DirectionType.None;
                        var Velocity = Vector2.Zero;

                        if (direction == DirectionType.Horizontal)
                        {
                            useVelocity = (float)(args.Event.Distance.Velocity.X / RenderingScale);
                            velocity = new(useVelocity, 0);
                        }
                        else
                        {
                            useVelocity = (float)(args.Event.Distance.Velocity.Y / RenderingScale);
                            velocity = new(0, useVelocity);
                        }

                        VelocityAccumulator.CaptureVelocity(velocity);

                        Velocity = VelocityAccumulator.CalculateFinalVelocity(3000);
                        //Debug.WriteLine($"[SkiaDrawer] Velocity: {Velocity}");

                        bool rightDirection = false;

                        switch (this.Direction)
                        {
                            case DrawerDirection.FromLeft:
                            case DrawerDirection.FromRight:
                                direction = DirectionType.Horizontal;
                                if (GetDirectionType(Velocity, direction, 0.9f) == direction)
                                {
                                    rightDirection = true;
                                }

                                Velocity.Y = 0; //be clean
                                break;
                            case DrawerDirection.FromBottom:
                            case DrawerDirection.FromTop:
                                direction = DirectionType.Vertical;
                                if (GetDirectionType(Velocity, direction, 0.9f) == direction)
                                {
                                    rightDirection = true;
                                }

                                Velocity.X = 0; //be clean
                                break;
                        }

                        //animate, only but if velociy is according drawer direction
                        if (!IgnoreWrongDirection)
                        {
                            rightDirection = true;
                        }

                        if (rightDirection || CheckNeedToSnap())
                        {
                            CurrentSnap = new Vector2((float)TranslationX, (float)TranslationY);
                            ScrollToNearestAnchor(new Vector2((float)TranslationX, (float)TranslationY), Velocity);

                            consumed = this;
                        }

                        IsUserPanning = false;
                        IsUserFocused = false;


                        _inContact = false;

                        break;
                }

                if (consumed != null || IsUserPanning) // || args.Event.NumberOfTouches > 1)
                {
                    if (consumed == null && args.Type != TouchActionResult.Up)
                    {
                        return this;
                    }

                    return consumed ?? consumedDefault;
                }

                if (!passedToChildren)
                {
                    return PassToChildren();
                }
            }

            return consumedDefault;
        }

        public bool AutoClose { get; set; }

        /// <summary>
        /// Called for manual finger panning
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        protected override Vector2 ClampOffsetWithRubberBand(float x, float y)
        {
            var clampedElastic =
                RubberBandUtils.ClampOnTrack(new Vector2(x, y), ContentOffsetBounds, (float)RubberEffect);

            if (Direction == DrawerDirection.FromBottom || Direction == DrawerDirection.FromTop)
            {
                var clampedX = Math.Max(ContentOffsetBounds.Left, Math.Min(ContentOffsetBounds.Right, x));
                return clampedElastic with { X = clampedX };
            }
            else if (Direction == DrawerDirection.FromLeft || Direction == DrawerDirection.FromRight)
            {
                var clampedY = Math.Max(ContentOffsetBounds.Top, Math.Min(ContentOffsetBounds.Bottom, y));
                return clampedElastic with { Y = clampedY };
            }

            return clampedElastic;
        }
    }
}
