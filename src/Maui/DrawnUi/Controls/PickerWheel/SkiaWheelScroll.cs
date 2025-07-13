using System.Dynamic;

namespace DrawnUi.Controls
{
    /// <summary>
    /// A specialized scroll view that displays items in a 3D wheel-like arrangement
    /// </summary>
    public class SkiaWheelScroll : VirtualScroll
    {
        public override bool AvoidRemeasuring(MeasureRequest request)
        {
            return false;
        }

        public string DebugWheel => ItemsWrapper?.DebugString;

        protected readonly Sk3dView Helper3dChildren = new();

        protected WheelCellInfo[] CellsPool { get; private set; }

        protected List<string> DataSource;

        protected SkiaLayout? ItemsWrapper;

        #region EVENT HANDLERS

        /// <summary>
        /// Event raised when the selected index changes
        /// </summary>
        public event EventHandler<int> SelectedIndexChanged;

        /// <summary>
        /// Raises the SelectedIndexChanged event
        /// </summary>
        protected virtual void OnSelectedIndexChanged(int newIndex)
        {
            SelectedIndexChanged?.Invoke(this, newIndex);
            SetCurrentIndex(newIndex);
        }

        #endregion

        #region PROPERTIES


        public DrawImageAlignment HorizontalChildAlignement => DrawImageAlignment.Center;

        public bool Use3d => true;
        public bool FadeOpacity => true;
        public bool HapticEnabled => false;

        public static readonly BindableProperty LoopProperty = BindableProperty.Create(
            nameof(Loop),
            typeof(bool),
            typeof(SkiaWheelScroll),
            false, propertyChanged: NeedInvalidateMeasure);

        public bool Loop
        {
            get { return (bool)GetValue(LoopProperty); }
            set { SetValue(LoopProperty, value); }
        }

        public static readonly BindableProperty LinesColorProperty = BindableProperty.Create(
            nameof(LinesColor),
            typeof(Color),
            typeof(SkiaWheelScroll),
            Colors.White, propertyChanged: NeedDraw);

        public Color LinesColor
        {
            get { return (Color)GetValue(LinesColorProperty); }
            set { SetValue(LinesColorProperty, value); }
        }


        public static readonly BindableProperty OpacityFadeStrengthProperty = BindableProperty.Create(
            nameof(OpacityFadeStrength),
            typeof(float),
            typeof(SkiaWheelScroll),
            0.2f, propertyChanged: NeedDraw);

        /// <summary>
        /// How much the alpha fading is pronounced. 0-1. Default id 0.2f.
        /// </summary>
        public float OpacityFadeStrength
        {
            get { return (float)GetValue(OpacityFadeStrengthProperty); }
            set { SetValue(OpacityFadeStrengthProperty, value); }
        }


        private float wheelScrollingOffset;
        public float WheelScrollingOffset
        {
            get { return wheelScrollingOffset; }
            set
            {
                if (wheelScrollingOffset != value)
                {
                    wheelScrollingOffset = value;
                    OnPropertyChanged();
                }
            }
        }

        private float _itemHeight;
        protected float ItemHeight
        {
            get
            {
                return _itemHeight;
            }
            set
            {
                _itemHeight = value;
                SetContentSize();
                ApplyContentSize();
            }
        }


        public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(
            nameof(SelectedIndex),
            typeof(int),
            typeof(SkiaWheelScroll),
            -1,
            propertyChanged: (bindable, oldValue, newValue) =>
            {
                if (bindable is SkiaWheelScroll wheel && newValue is int newIndex)
                {
                    // Only apply if the change came from outside (not from wheel scrolling)
                    if (!wheel._updatingSelectedIndexFromScroll)
                    {
                        var valid = wheel.ApplySelectedIndex(newIndex);
                        if (valid)
                        {
                            wheel.OnSelectedIndexChanged(newIndex);
                        }
                    }
                }
            },
            validateValue: (_, value) => value is int);

        // Prevent circular updates
        private bool _updatingSelectedIndexFromScroll = false;
        private float _zeroOffset;

        /// <summary>
        /// Gets or sets the index of the currently selected item
        /// </summary>
        public int SelectedIndex
        {
            get { return (int)GetValue(SelectedIndexProperty); }
            set { SetValue(SelectedIndexProperty, value); }
        }

        public static readonly BindableProperty VisibleItemCountProperty = BindableProperty.Create(
            nameof(VisibleItemCount),
            typeof(int),
            typeof(SkiaWheelScroll),
            7, // Default value
            propertyChanged: (bindable, oldValue, newValue) =>
            {
                if (bindable is SkiaWheelScroll wheel)
                {
                    wheel.Invalidate();
                }
            },
            validateValue: (_, value) => (int)value >= 3);

        public int VisibleItemCount
        {
            get { return (int)GetValue(VisibleItemCountProperty); }
            set
            {
                // Ensure odd number
                SetValue(VisibleItemCountProperty, value % 2 == 0 ? value + 1 : value);
            }
        }


        private static void NeedInvalidate(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaWheelScroll control)
            {
                control.Invalidate();
            }
        }

        #endregion

        #region OVERRIDES AND LOGIC

        protected override void CreateDefaultContent()
        {
            base.CreateDefaultContent();

            ItemsWrapper = Views.FirstOrDefault(x=>x is SkiaWheelStack) as SkiaWheelStack;
        }

        protected override ScaledSize MeasureContent(float width, float height, float scale)
        {
            ItemsWrapper?.Measure(width, float.PositiveInfinity, scale);

            return base.MeasureContent(width, height, scale);
        }

        protected override void SetContentSize()
        {
            if (Orientation == ScrollOrientation.Vertical)
            {
                ContentSize = ScaledSize.FromPixels(new(MeasuredSize.Pixels.Width, ItemHeight * 15000), MeasuredSize.Scale);
            }
            else if (Orientation == ScrollOrientation.Horizontal)
            {
                base.SetContentSize();
            }
        }

        protected override void OnLayoutChanged()
        {
            base.OnLayoutChanged();

            ApplyVisibleItemCount();

            //todo can inittialize templates, reserve etc..

            InitializeWheelPosition();
        }

        public override void DrawVirtual(DrawingContext context)
        {
            if (ItemsWrapper!=null && CellsPool != null && DrawingRect.Height > 1)
            {
                var logicalIndexes = new int[CellsCount + 2];
                int startingIndex = this.GetIndexAtScrollOffset(this.WheelScrollingOffset, false, false) - this.VisibleCellsCountHalf;

                for (int i = startingIndex - 1; i < startingIndex + CellsCount + 1; i++)
                {
                    int logical = this.GetIndexAtCellPosition(i);

                    // Store the logical index or -1 if it's outside the valid range
                    logicalIndexes[i - (startingIndex - 1)] = (IsIndexValid(logical) ? logical : -1);
                }

                if (ItemsWrapper.RecyclingTemplate == RecyclingTemplate.Enabled)
                {
                    //todo if `logicalIndexes` doesn't contain the index we can recycle the view
                    // if (!logicalIndexes.Contains<int>( index ))
                }

                // Loop through all visible items to draw them
                int itemIndex = startingIndex;
                int cellsToDraw = VisibleCellsCountHalf;
                var offset = WheelScrollingOffset - GetScrollOffsetForIndex(startingIndex + VisibleCellsCountHalf);

                while (itemIndex < startingIndex + CellsCount)
                {
                    // Get the logical index for this position
                    int currentIndex = logicalIndexes[itemIndex - (startingIndex - 1)];

                    if (currentIndex != -1)
                    {
                        WheelCellInfo cell = CellsPool[VisibleCellsCountHalf - cellsToDraw];

                        if (!cell.WasMeasured || cell.Index != currentIndex || !CompareFloats(cell.Offset, WheelScrollingOffset))
                        {
                            float cellOffset = this.WheelVerticalCenter - (float)cellsToDraw * (ItemHeight + this.UseSpacing) - offset;

                            // Compute full 3D position for this item
                            cell.Index = currentIndex;
                            this.PrepareCell(context, cell, cellOffset);
                        }

                        // Draw the item with all transformations applied
                        this.DrawItem(context, cell);
                    }

                    // Move to next item
                    itemIndex++;
                    cellsToDraw--;
                }


            }

            PaintSelectionIndicator(context);

        }




        public void ApplyVisibleItemCount()
        {
            var visibleItemCount = this.CellsCount;

            CellsPool = new WheelCellInfo[visibleItemCount];
            for (int i = 0; i < visibleItemCount; i++)
            {
                CellsPool[i] = new WheelCellInfo();
            }

            ItemHeight = DrawingRect.Height / (VisibleItemCount - 2);
        }


        /// <summary>
        /// Get a (maybe recycled) view from the stack
        /// </summary>
        /// <param name="context"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        private SkiaControl GetView(DrawingContext context, WheelCellInfo item)
        {
            // Get stack structure from content
            var structure = ItemsWrapper.LatestStackStructure;
            if (structure == null || structure.GetCount() <= 0)
                return null;

            ControlInStack cell = structure.FindChildAtIndex(item.Index);
            SkiaControl child = null;

            if (ItemsWrapper.IsTemplated)
            {
                if (ItemsWrapper.ChildrenFactory.TemplatesAvailable)
                {

                    child = ItemsWrapper.ChildrenFactory.GetViewForIndex(item.Index, null,
                        ItemsWrapper.GetSizeKey(cell.Measured.Pixels));
                }
            }
            else
            {
                child = cell.View;
            }

            if (child != null)
            {
                //todo can set props to child if needed, `isSelected`, colors etc..

                if (child.NeedMeasure)
                {
                    child.Measure((float)cell.Area.Width, (float)cell.Area.Height, context.Scale);
                }
            }

            return child;
        }

#if IOS
        static AudioToolbox.SystemSound soundPicker = new (1157);
#endif

        void SetCurrentIndex(int index)
        {
            if (CurrentIndex != index)
            {
                if (CurrentIndex != -1 && IsLayoutReady)
                {
#if IOS
                    soundPicker.PlaySystemSound();
#endif

                    if (HapticEnabled)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            if (HapticFeedback.Default.IsSupported)
                            {
                                HapticFeedback.Default.Perform(HapticFeedbackType.Click);
                            }
                        });
                    }
                }

                CurrentIndex = index;
            }
        }

        /// <summary>
        /// Fill CellInfo
        /// </summary>
        /// <param name="context"></param>
        /// <param name="info"></param>
        /// <param name="itemCenterY"></param>
        private void PrepareCell(DrawingContext context, WheelCellInfo info, float itemCenterY)
        {
            info.WasMeasured = true;
            info.Offset = this.WheelScrollingOffset;

            SKRect drawingRect = DrawingRect; //global parent output frame
            SKMatrix matrixTransform = SKMatrix.Identity;

            if (Use3d)
            {
                // Calculate relative position in the wheel (from -1 to 1)
                float relativePosition = (WheelVerticalCenter - itemCenterY) / WheelHalfHeight;
                relativePosition = (float)Math.Clamp(relativePosition, -Curvature, Curvature);

                // Apply sine adjustment to create natural wheel effect
                float adjustFor3d = (float)(WheelHalfHeight * Math.Sin(relativePosition));
                itemCenterY = WheelVerticalCenter - adjustFor3d;

                var centerX = drawingRect.MidX;
                var centerY = itemCenterY;

                var childViewAngle = relativePosition.ToDegrees();
                var zDepth = (float)(WheelHalfHeight * (1 - Math.Cos(relativePosition)));

                Helper3dChildren.Reset();
                Helper3dChildren.RotateXDegrees(childViewAngle);
                Helper3dChildren.Translate(0, 0, zDepth);
                var applyMatrix = Helper3dChildren.Matrix;

                matrixTransform = SKMatrix.CreateTranslation(-centerX, -centerY);
                matrixTransform = matrixTransform.PostConcat(applyMatrix);
                matrixTransform = matrixTransform.PostConcat(SKMatrix.CreateTranslation(centerX, centerY));
            }

            info.Transform = matrixTransform;

            float upperSelectionLimit = WheelVerticalCenter - ItemHeight / 2;
            float lowerSelectionLimit = WheelVerticalCenter + ItemHeight / 2;
            info.IsSelected = (itemCenterY >= upperSelectionLimit) && (itemCenterY <= lowerSelectionLimit);

            if (info.IsSelected)
            {
                SetCurrentIndex(info.Index);
            }

            // get (recycled) view
            SkiaControl view = null;
            try
            {
                view = GetView(context, info);
                if (view == null)
                {
                    return;
                }
                info.View = view;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return;
            }

            SKRect finalRect = new SKRect(
                drawingRect.Left,
                (float)Math.Floor(itemCenterY - ItemHeight / 2f),
                drawingRect.Left + view.MeasuredSize.Pixels.Width,
                (float)Math.Ceiling(itemCenterY + ItemHeight / 2f)
            );

            // Align
            int availableWidth = (int)drawingRect.Width;
            if (view.MeasuredSize.Pixels.Width < availableWidth)
            {
                if (HorizontalChildAlignement == DrawImageAlignment.End)
                {
                    finalRect.Offset(availableWidth - view.MeasuredSize.Pixels.Width, 0);
                }
                else if (HorizontalChildAlignement == DrawImageAlignment.Center)
                {
                    finalRect.Offset((availableWidth - view.MeasuredSize.Pixels.Width) / 2, 0);
                }
            }

            if (FadeOpacity && !info.IsSelected)
            {
                info.Opacity = (WheelHalfHeight - Math.Abs(WheelVerticalCenter - itemCenterY)) / (WheelHalfHeight * OpacityFadeStrength);
            }
            else
            {
                info.Opacity = 1;
            }

            // Store the final rectangle and layout the views
            info.Destination = finalRect;
            //if (!CompareRects(view.DrawingRect, finalRect, float.Epsilon))
            {
                //need remeasure next time
                //view.Layout(context, finalRect.Left, finalRect.Top, finalRect.Right, finalRect.Bottom);
                // item.WasMeasured = false;
            }
        }

        private void DrawItem(DrawingContext context, WheelCellInfo item)
        {
            if (item.View == null)
                return;

            var canvas = context.Context.Canvas;
            int saveCount = canvas.Save();

            try
            {
                if (Use3d)
                {
                    canvas.SetMatrix(item.Transform.PostConcat(canvas.TotalMatrix));
                }

                if (item.View is IWheelPickerCell cell)
                {
                    cell.UpdateContext(item);
                }
                else
                if (FadeOpacity)
                {
                    item.View.Opacity = item.Opacity;
                }

                item.View.Render(context.WithDestination(item.Destination));
            }
            finally
            {
                canvas.RestoreToCount(saveCount);
            }
        }

        /// <summary>
        /// Draw selection indicator lines if needed
        /// </summary>
        /// <param name="context"></param>
        protected virtual void PaintSelectionIndicator(DrawingContext context)
        {
            float lineThickness = context.Scale;

            float upperY = WheelVerticalCenter - ItemHeight / 2;
            SKRect upperLine = new SKRect(
                context.Destination.Left,
                upperY - lineThickness / 2,
                context.Destination.Right,
                upperY + lineThickness / 2
            );

            float lowerY = WheelVerticalCenter + ItemHeight / 2;
            SKRect lowerLine = new SKRect(
                context.Destination.Left,
                lowerY - lineThickness / 2,
                context.Destination.Right,
                lowerY + lineThickness / 2
            );

            using (var paint = new SKPaint { Color = LinesColor.ToSKColor(), Style = SKPaintStyle.Fill })
            {
                context.Context.Canvas.DrawRect(upperLine, paint);
                context.Context.Canvas.DrawRect(lowerLine, paint);
            }
        }


        #endregion

        #region SCROLLING

        /// <summary>
        /// Initiates a fling animation that precisely mirrors the Xamarin Android implementation
        /// </summary>
        public override bool StartToFlingFrom(ScrollFlingAnimator animator, float from, float velocity)
        {
            animator.InitializeWithVelocity(from, velocity, 1f - DecelerationRatio);
            var time = (float)animator.Speed;

            var predictedWheelY = -animator.Parameters.ValueAt(time) * RenderingScale;

            // Find nearest item to this predicted position
            int predictedIndex;
            if (!Loop)
            {
                // First, get the unconstrained index
                predictedIndex = GetIndexAtScrollOffset(predictedWheelY, false, true);

                // Now constrain it to valid range (0 to LogicalRows-1)
                predictedIndex = Math.Max(0, Math.Min(predictedIndex, ItemsSourceCount - 1));
            }
            else
            {
                // For circular wheels, just get the nearest index
                predictedIndex = GetIndexAtScrollOffset(predictedWheelY, true, true);
            }

            // Calculate target wheel position (exact item position)
            float targetWheelY = GetScrollOffsetForIndex(predictedIndex);

            float targetScrollY = -targetWheelY / RenderingScale;

            // Initialize with destination
            animator.InitializeWithDestination(from, targetScrollY, time, 1f - DecelerationRatio);

            // Start the animation
            if (PrepareToFlingAfterInitialized(animator))
            {
                animator.RunAsync(null).ConfigureAwait(false);
                return true;
            }

            return false;
        }

        protected override bool PositionViewport(SKRect destination, SKPoint offsetPixels, float viewportScale, float scale,
            bool forceSyncOffsets)
        {

            var changed = base.PositionViewport(destination, offsetPixels, viewportScale, scale, forceSyncOffsets);
            if (changed)
            {
                //Debug.WriteLine($"{offsetPixels} - {InternalViewportOffset.Pixels.Y}");
                WheelScrollingOffset = -InternalViewportOffset.Pixels.Y;
            }
            return changed;
        }

        /// <summary>
        /// Checks if the wheel is already positioned at a valid snap position
        /// </summary>
        private bool IsAlreadyAtSnapPosition()
        {
            // Get the current position index
            int currentIndex = GetIndexAtScrollOffset(WheelScrollingOffset, true, true);

            // Calculate the exact Y position for this index
            float snapPositionY = GetScrollOffsetForIndex(currentIndex);

            // Check if we're already very close to this position
            return Math.Abs(WheelScrollingOffset - snapPositionY) < 1.0f;
        }

        protected override bool CheckNeedToSnap()
        {
            if (IsAlreadyAtSnapPosition())
            {
                return false;
            }
            return true;
        }

        public override void Snap(float maxTimeSecs)
        {
            int nearestIndex = GetIndexAtScrollOffset(WheelScrollingOffset, true, true);

            // Calculate the exact Y position we need to snap to
            float targetY = GetScrollOffsetForIndex(nearestIndex);

            // Calculate distance to move
            float distance = targetY - WheelScrollingOffset;

            // If we're already very close, just snap immediately
            if (Math.Abs(distance) < RenderingScale * 5.0f)
            {
                //Debug.WriteLine($"[WHEEL] Small snapping");
                ViewportOffsetY = -targetY / RenderingScale;
                return;
            }

            float duration = Math.Min(maxTimeSecs,
                Math.Max(0.1f, Math.Abs(distance) / ItemHeight * 0.2f));

            // Start the snap animation
            var ptsTarget = -targetY / RenderingScale;
            ScrollToOffset(new(0, ptsTarget), duration);
        }

        protected override void OnScrollCompleted()
        {
            base.OnScrollCompleted();

            ActualizeSelectedIndex();

            //Debug.WriteLine($"[WHEEL] Stopped at y:{ViewportOffsetY}, index:{SelectedIndex}");
        }

        /// <summary>
        /// Updates the SelectedIndex property based on current scroll position
        /// </summary>
        protected void ActualizeSelectedIndex()
        {
            // Get the physical index from the current wheel position
            int physicalIndex = GetIndexAtScrollOffset(WheelScrollingOffset, true, true);

            // Convert to logical index (data source index)
            int logicalIndex = GetIndexAtCellPosition(physicalIndex);

            // For non-circular wheels, ensure we're within bounds
            if (!Loop && ItemsWrapper?.ItemsSource != null)
            {
                logicalIndex = Math.Max(0, Math.Min(logicalIndex, ItemsWrapper.ItemsSource.Count - 1));
            }

            // Set flag to prevent recursive updates
            _updatingSelectedIndexFromScroll = true;

            try
            {
                // Update the SelectedIndex property
                if (SelectedIndex != logicalIndex)
                {
                    SelectedIndex = logicalIndex;

                    // Raise SelectedIndexChanged event if needed
                    OnSelectedIndexChanged(logicalIndex);
                }
            }
            finally
            {
                _updatingSelectedIndexFromScroll = false;
            }

            //System.Diagnostics.Debug.WriteLine($"[WHEEL] Updated SelectedIndex to {logicalIndex} from physicalIndex={physicalIndex}, WheelY={WheelScrollingOffset}");

            //todo re-enable !!!
            if (Loop && Math.Abs(physicalIndex - LoopedOffset) > LoopedOffsetHalf)
            {
                // Reset to a position near 10000
                var wheelY = GetScrollOffsetForIndex(LoopedOffset + (logicalIndex % ItemsSourceCount));

                ViewportOffsetY = -wheelY / RenderingScale;
            }
        }

        void SetupClampBounds()
        {
            if (!Loop)
            {
                var index = 0;
                if (!IsIndexValid(index))
                    return;
                index = Math.Max(0, Math.Min(index, ItemsSourceCount - 1));
                int useIndex = index;
                float targetY = GetScrollOffsetForIndex(useIndex);
                _zeroOffset = -targetY / RenderingScale;
            }
        }

        /// <summary>
        /// Apply selected index by scrolling to cell
        /// </summary>
        /// <param name="index"></param>
        private bool ApplySelectedIndex(int index)
        {
            if (!IsIndexValid(index))
                return false;

            // Ensure index is within bounds
            if (!Loop)
            {
                index = Math.Max(0, Math.Min(index, ItemsSourceCount - 1));
            }
            else
            {
                // ensure index wraps around
                index = ((index % ItemsSourceCount) + ItemsSourceCount) % ItemsSourceCount;
            }

            // Convert to physical index for circular wheels
            int useIndex = Loop ? LoopedOffset + index : index;

            float targetY = GetScrollOffsetForIndex(useIndex);

            ViewportOffsetY = -targetY / RenderingScale;

            if (index == 0)
            {
                _zeroOffset = -targetY / RenderingScale;
            }

            return true;
        }

        public override Vector2 ClampOffset(float x, float y, SKRect contentOffsetBounds, bool strict = false)
        {
            var ret = base.ClampOffset(x, y - _zeroOffset, contentOffsetBounds, strict);

            //Debug.WriteLine($"Tuned {y - _zeroOffset} => {ret.Y + _zeroOffset}");

            return new(ret.X, ret.Y + _zeroOffset);
        }

        private void InitializeWheelPosition()
        {
            if (ItemsWrapper != null)
            {
                if (ItemsWrapper.ItemsSource != null && ItemsWrapper.ItemsSource.Count > 0)
                {
                    if (SelectedIndex < 0)
                        SelectedIndex = 0;
                }
            }

            SetupClampBounds();

            if (SelectedIndex == -1)
                return;

            int selectedIndex = SelectedIndex;

            ApplySelectedIndex(selectedIndex);

            return;

            // Only needed for circular wheels
            if (!Loop || SelectedIndex == -1)
                return;

            // Use the developer-set SelectedIndex

            // Convert to proper physical index centered at 10000
            int physicalIndex = LoopedOffset + (selectedIndex % ItemsSourceCount);

            // Set the wheel's Y position
            WheelScrollingOffset = GetScrollOffsetForIndex(physicalIndex);

            //System.Diagnostics.Debug.WriteLine($"[WHEEL] Initialized with SelectedIndex={selectedIndex}, physicalIndex={physicalIndex}, WheelY={WheelScrollingOffset}");
        }

        public void ScrollToIndex(int index, bool animated = true, float duration = 0.3f)
        {
            // This just sets the SelectedIndex property which will then trigger ApplySelectedIndex
            if (animated)
            {
                // Calculate the current logical index
                int currentIndex = SelectedIndex;

                // Calculate number of positions to move
                int positions = index - currentIndex;

                // For circular wheels, find the shortest path
                if (Loop && Math.Abs(positions) > ItemsSourceCount / 2)
                {
                    positions = positions > 0
                        ? positions - ItemsSourceCount
                        : positions + ItemsSourceCount;
                }

                // Calculate current and target physical positions
                int currentPhysical = Loop
                    ? LoopedOffset + (currentIndex % ItemsSourceCount)
                    : currentIndex;

                int targetPhysical = currentPhysical + positions;

                // Calculate Y positions
                float fromY = WheelScrollingOffset;
                float toY = GetScrollOffsetForIndex(targetPhysical);

                // Animate
                var needOffsetY = -toY / RenderingScale;
                Snapped = true;
                _animatorFlingX.Stop();
                _animatorFlingY.Stop();
                ScrollTo(ViewportOffsetX, needOffsetY, AutoScrollingSpeedMs);

            }
            else
            {
                // Non-animated just sets the property
                SelectedIndex = index;
            }
        }

        #endregion

        #region HELPERS

        protected int CellsCount => VisibleItemCount + 2;
        protected int VisibleCellsCountHalf => CellsCount / 2;
        protected float WheelHalfHeight => DrawingRect.Height / 2.0f;
        protected float WheelVerticalCenter => DrawingRect.MidY;
        protected float UseSpacing
        {
            get
            {
                if (ItemsWrapper == null)
                {
                    return 0;
                }
                return (float)(ItemsWrapper.Spacing * RenderingScale);
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsIndexValid(int position)
        {
            if (ItemsWrapper == null || ItemsWrapper.ItemsSource == null || ItemsWrapper.ItemsSource.Count == 0 || position < 0)
            {
                return false;
            }

            return position < ItemsWrapper.ItemsSource.Count;
        }


        public int ItemsSourceCount
        {
            get
            {
                if (ItemsWrapper?.ItemsSource == null)
                {
                    return 0;
                }
                return ItemsWrapper.ItemsSource.Count;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float GetScrollOffsetForIndex(int index)
        {
            return (ItemHeight + UseSpacing) * (index + VisibleCellsCountHalf);
        }

        private int GetIndexAtScrollOffset(float y, bool checkBounds = false, bool nearest = false)
        {
            if (ItemHeight < float.Epsilon) //prevents division by zero or calculations with invalid height
            {
                return 0;
            }
            int logicalRows = ItemsSourceCount;

            int num = (nearest ?
                (int)Math.Round((double)(y / (ItemHeight + UseSpacing)))
                : (int)Math.Floor((double)(y / (ItemHeight + this.UseSpacing)))) - VisibleCellsCountHalf;

            if (checkBounds && !Loop)
            {
                if (num >= logicalRows)
                {
                    num = logicalRows - 1;
                }
                if (num < 0)
                {
                    num = 0;
                }
            }
            return num;
        }



        public int GetCellPositionForIndex(int index)
        {
            if (!Loop)
            {
                return index;
            }
            if (ItemsSourceCount == 0)
            {
                return 0;
            }
            return LoopedOffset + index % ItemsSourceCount;
        }

        public int GetIndexAtCellPosition(int position)
        {
            if (!Loop)
            {
                return position;
            }
            int rows = ItemsSourceCount;
            int adjust = position - LoopedOffset;
            int index = Math.Abs(adjust % rows);
            if (adjust < 0)
            {
                index = (rows + rows - index) % rows;
            }
            return index;
        }

        #endregion

        #region INTERNALS

        static double Curvature = Math.PI / 2.0;

        /// <summary>
        /// 10 000
        /// </summary>
        const int LoopedOffset = 10000;

        /// <summary>
        /// 10 000 / 2
        /// </summary>
        const int LoopedOffsetHalf = 5000;

        #endregion
    }
}
