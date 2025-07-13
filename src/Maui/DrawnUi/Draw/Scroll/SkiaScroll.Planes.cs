#define TMP

using System.Collections.Immutable;
using System.Numerics;

/*
    When Scrolling Down:

    1. User scrolls down → currentScroll changes
    2. Real-time positioning: rectForward.Offset(0, currentScroll + PlaneForward.OffsetY)
    3. Trigger check: rectForward.MidY <= (Viewport.Height / 2)
    4. Swap triggered: SwapDown() rotates planes and repositions
    5. New positioning: PlaneForward.OffsetY = PlaneCurrent.OffsetY + _planeHeight
    6. Background preparation: New forward plane gets rendered with content for the "next" scroll area

    The positioning is continuous and automatic - planes are always positioned relative to the current scroll
    position plus their individual offsets, ensuring seamless infinite scrolling!

 */

namespace DrawnUi.Draw
{

    public partial class SkiaScroll
    {
        //todo complete and move

        public const string PlaneRed = "Red";
        public const string PlaneGreen = "Greeen";
        public const string PlaneBlue = "Blue";

        private bool _childrenNeedRedraw = false;

        public override bool NeedMeasure
        {
            get
            {
                return base.NeedMeasure;
            }
            set
            {
                base.NeedMeasure = value;
            }
        }

        public override void InvalidateInternal()
        {
            base.InvalidateInternal();

            InvalidatePlanes();
        }

        protected virtual void InvalidatePlanes()
        {
            PlaneCurrent?.Invalidate();
            PlaneBackward?.Invalidate();
            PlaneForward?.Invalidate();
        }


        public override void UpdateByChild(SkiaControl control)
        {
            if (UseVirtual)
            {
                // Just set a flag - we'll determine which planes need invalidation during painting
                if (HasDirtyChildren())
                {
                    NeedMeasure = true;
                    _childrenNeedRedraw = true;
                    Content.DirtyChildrenTracker.Clear();
                    //Update();
                }
                //Update(); // Trigger redraw
                return;
            }

            base.UpdateByChild(control);
        }

        /// <summary>
        /// Check dirty children and invalidate specific planes that contain them
        /// </summary>
        protected virtual void CheckAndInvalidateDirtyPlanes()
        {
            if (Content == null)
                return;
                
            var dirtyChildren = Content.DirtyChildrenTracker;
            if (dirtyChildren == null || dirtyChildren.IsEmpty)
                return;

            var planesToInvalidate = new HashSet<Plane>();
            
            // For each dirty child, find which plane contains it
            foreach (var dirtyChild in dirtyChildren.GetList())
            {
                var plane = FindPlaneContainingChild(dirtyChild);
                if (plane != null)
                {
                    planesToInvalidate.Add(plane);
                }
            }
            
            // Invalidate only the planes that contain dirty children
            foreach (var plane in planesToInvalidate)
            {
                plane.Invalidate();
                Debug.WriteLine($"[PLANES] Invalidated plane {plane.Id} due to dirty children");
            }
            
            // Clear the dirty children tracker
            dirtyChildren.Clear();
        }

        protected virtual bool HasDirtyChildren()
        {
            if (Content == null)
                return false;

            var dirtyChildren = Content.DirtyChildrenTracker;
            if (dirtyChildren == null || dirtyChildren.IsEmpty)
                return false;

            var planesToInvalidate = new HashSet<Plane>();

            // For each dirty child, find which plane contains it
            foreach (var dirtyChild in dirtyChildren.GetList())
            {
                var plane = FindPlaneContainingChild(dirtyChild);
                if (plane != null)
                {
                    return true;
                    planesToInvalidate.Add(plane);
                }
            }

            //// Invalidate only the planes that contain dirty children
            //foreach (var plane in planesToInvalidate)
            //{
            //    plane.Invalidate();
            //    Debug.WriteLine($"[PLANES] Invalidated plane {plane.Id} due to dirty children");
            //}

            //// Clear the dirty children tracker
            //dirtyChildren.Clear();

            return false;
        }

        /// <summary>
        /// Determines which plane contains the given child control by checking render trees
        /// </summary>
        protected virtual Plane FindPlaneContainingChild(SkiaControl control)
        {
            // Check each plane's render tree for the invalidated child
            var planes = new[] { PlaneCurrent, PlaneForward, PlaneBackward };

            foreach (var plane in planes)
            {
                if (plane?.RenderTree != null)
                {
                    // Check if this plane's render tree contains the invalidated control
                    for (int i = 0; i < plane.RenderTree.Count; i++)
                    {
                        if (plane.RenderTree[i].Control == control)
                        {
                            return plane;
                        }
                    }
                }
            }

            return null; // Child not found in any plane's render tree
        }


        //todo use when context size is bigger than 2 viewports?
        public virtual bool UseVirtual
        {
            get
            {
                return Content != null
                       && Orientation != ScrollOrientation.Both && Content is SkiaLayout layout &&
                       layout.Virtualisation == VirtualisationType.Managed;
            }
        }


        protected Plane PlaneCurrent { get; set; }
        protected Plane PlaneForward { get; set; }
        protected Plane PlaneBackward { get; set; }
        protected int _planeWidth;
        protected int _planeHeight;
        protected int _planePrepareThreshold;
        private float swappedDownAt;
        private float swappedUpAt;

        public override ScaledRect GetOnScreenVisibleArea(DrawingContext context, Vector2 inflateByPixels = default)
        {
            if (UseVirtual)
            {
                //todo
                if (context.GetArgument(ContextArguments.Viewport.ToString()) is SKRect insideViewport)
                {
                    //we can return the plane rect
                    //Debug.WriteLine($"UsePlanes area: {insideViewport}");
                    return ScaledRect.FromPixels(insideViewport, _zoomedScale);
                }

                return ScaledRect.FromPixels(context.Destination, _zoomedScale);
            }

            if (Virtualisation != VirtualisationType.Disabled) //true by default
            {
                //passing visible area to be rendered
                //when scrolling we will pass changed area to be rendered
                //most suitable for large content
                var inflated = ContentViewport.Pixels;
                inflated.Inflate(inflateByPixels.X, inflateByPixels.Y);
                return ScaledRect.FromPixels(inflated, RenderingScale);
            }
            else
            {
                //passing the whole area to be rendered.
                //when scrolling we will just translate it
                //most suitable for small content
                return ContentRectWithOffset;

                //absoluteViewPort = new SKRect(Viewport.Pixels.Left, Viewport.Pixels.Top,
                //    Viewport.Pixels.Left + ContentSize.Pixels.Width, Viewport.Pixels.Top + ContentSize.Pixels.Height);
            }
        }


        public virtual void InitializePlanes()
        {
            var viewportWidth = Viewport.Pixels.Width;
            var viewportHeight = Viewport.Pixels.Height;

            // Ensure the planes cover twice the viewport area
            _planeWidth = (int)(viewportWidth); //for vertical, todo all orientations
            _planeHeight = (int)(viewportHeight * 2);
            _planePrepareThreshold = (int)(_planeHeight / 2);

            float offsetX = 0, offsetY = 0;

            if (Orientation == ScrollOrientation.Vertical)
            {
                offsetY = _planeHeight;
            }
            else if (Orientation == ScrollOrientation.Horizontal)
            {
                offsetX = _planeWidth;
            }

            PlaneCurrent = new Plane
            {
                Id = PlaneRed,
                Surface = SKSurface.Create(new SKImageInfo(_planeWidth, _planeHeight)),
                BackgroundColor = SKColors.Red,
                Destination = new(0, 0, _planeWidth, _planeHeight)
            };

            PlaneForward = new Plane
            {
                Id = PlaneGreen,
                OffsetX = offsetX,
                OffsetY = offsetY,
                Surface = SKSurface.Create(new SKImageInfo(_planeWidth, _planeHeight)),
                Destination = new(0, 0, _planeWidth, _planeHeight),
                BackgroundColor = SKColors.Green,
            };

            PlaneBackward = new Plane
            {
                Id = PlaneBlue,
                OffsetX = -offsetX,
                OffsetY = -offsetY,
                Surface = SKSurface.Create(new SKImageInfo(_planeWidth, _planeHeight)),
                Destination = new(0, 0, _planeWidth, _planeHeight),
                BackgroundColor = SKColors.Blue,
            };

        }
  

        private int visibleAreaCaller = 0;
        private bool _availablePlaneC;
        private bool _availablePlaneB;
  
        private readonly Dictionary<string, PlaneBuildState> _planeBuildStates
            = new Dictionary<string, PlaneBuildState>
            {
                { PlaneRed, new PlaneBuildState() },
                { PlaneGreen, new PlaneBuildState() },
                { PlaneBlue, new PlaneBuildState() }
            };

        private class PlaneBuildState
        {
            public bool IsBuilding;
            public CancellationTokenSource Cts;
        }

        private static readonly SemaphoreSlim _globalPlanePreparationLock = new(1, 1);

        protected void TriggerPreparePlane(DrawingContext context, string planeId)
        {
            if (!_planeBuildStates.TryGetValue(planeId, out var state))
            {
                Debug.WriteLine($"Unknown planeId: {planeId}");
                return;
            }

            // If this plane is already building, cancel the previous job
            if (state.IsBuilding && state.Cts != null)
            {
                state.Cts.Cancel(); // signal old task to stop
                //Debug.WriteLine($"Canceling previous rendering: {planeId}");
            }

            // Create a fresh CTS and mark building
            state.Cts?.Dispose();
            state.Cts = new CancellationTokenSource();
            state.IsBuilding = true;
            var token = state.Cts.Token;

            var clone = context; //always clone struct from arguments for another thread!
            Task.Run(async () =>
            {
                try
                {
                    if (token.IsCancellationRequested)
                    {
                        //Debug.WriteLine($"Plane rendering canceled: {planeId}");
                        return; // canceled before starting
                    }

                    await _planeLocks[planeId].WaitAsync(token);

                    // Now do the actual PreparePlane
                    var plane = GetPlaneById(planeId);
                    //Debug.WriteLine($"Run prepare plane {plane?.Id}");

                    await _globalPlanePreparationLock.WaitAsync(token);

                    PreparePlane(clone.WithArgument(new("BThread", true)), plane);

                    if (plane.IsReady)
                    {
                        Repaint();
                    }
                }
                catch (OperationCanceledException)
                {
                    //Debug.WriteLine($"Plane rendering canceled: {planeId}");
                    // Normal if we got canceled
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error building plane {planeId}: {ex}");
                }
                finally
                {
                    _globalPlanePreparationLock.Release();
                    state.IsBuilding = false;
                    _planeLocks[planeId].Release();
                }
            }, token).ConfigureAwait(false);
        }





        protected virtual Plane GetPlaneById(string planeId)
        {
            return planeId switch
            {
                PlaneGreen => PlaneForward,
                PlaneBlue => PlaneBackward,
                PlaneRed => PlaneCurrent,
                _ => throw new ArgumentException("Invalid plane ID", nameof(planeId))
            };
        }

        private readonly Dictionary<string, SemaphoreSlim> _planeLocks
            = new Dictionary<string, SemaphoreSlim>
            {
                { PlaneRed, new SemaphoreSlim(1, 1) },
                { PlaneGreen, new SemaphoreSlim(1, 1) },
                { PlaneBlue, new SemaphoreSlim(1, 1) }
            };


        /// <summary>
        /// Viewport scrolled
        /// </summary>
        protected virtual void OnScrolledForPlanes()
        {
            _availablePlaneB = true;

            if (Content is SkiaLayout layout && layout.IsTemplated
                                             && layout.MeasureItemsStrategy == MeasuringStrategy.MeasureVisible
                                             && layout.LastMeasuredIndex < layout.ItemsSource.Count)
            {
                var measuredEnd = layout.GetMeasuredContentEnd();

                double currentOffset = Orientation == ScrollOrientation.Vertical
                    ? -ViewportOffsetY
                    : -ViewportOffsetX;

                if (measuredEnd - currentOffset < 0)
                {
                    TriggerIncrementalMeasurement(layout);
                }

            }
        }

        protected void OrderToPreparePlaneForwardInBackground(DrawingContext context)
        {
            if (_planeBuildStates[PlaneGreen].IsBuilding
                //|| !_availablePlaneB
                || PlaneForward == null
                || PlaneForward.IsReady
                || ViewportOffsetY == 0)
            {
                return;
            }

            //Debug.WriteLine($"Preparing PLANE {PlaneGreen}..");

            // Capture current viewport state to avoid race conditions
            var capturedOffset = InternalViewportOffset.Pixels;
            var capturedContext = context.WithArgument(new(nameof(ContextArguments.Offset), capturedOffset));

            TriggerPreparePlane(capturedContext, PlaneGreen);

            //_availablePlaneB = false;
        }

        protected void OrderToPreparePlaneBackwardInBackground(DrawingContext context)
        {
            if (_planeBuildStates[PlaneBlue].IsBuilding
                //|| !_availablePlaneB                          
                || PlaneBackward == null
                || PlaneBackward.IsReady
                || ViewportOffsetY >= 0)
            {
                return;
            }

            //Debug.WriteLine($"Preparing PLANE {PlaneBlue}..");

            // Capture current viewport state to avoid race conditions
            var capturedOffset = InternalViewportOffset.Pixels;
            var capturedContext = context.WithArgument(new(nameof(ContextArguments.Offset), capturedOffset));

            TriggerPreparePlane(capturedContext, PlaneBlue);
        }

        /// <summary>
        /// Determines if we should swap down based on visual position and content boundaries
        /// </summary>
        protected virtual bool ShouldSwapDown(SKRect rectForward)
        {
            // Original visual trigger: forward plane center reaches viewport center
            bool visualTrigger = rectForward.MidY <= (Viewport.Pixels.Height / 2) + DrawingRect.Top;

            // Content boundary trigger: at end of content and forward plane is becoming visible
            bool contentBoundaryTrigger = false;

            if (Content is SkiaLayout layout && layout.IsTemplated &&
                layout.MeasureItemsStrategy == MeasuringStrategy.MeasureVisible)
            {
                // Check if we've measured all content and forward plane is entering viewport
                bool atContentEnd = layout.LastMeasuredIndex >= layout.ItemsSource.Count - 1;
                bool forwardPlaneEntering = rectForward.Top < Viewport.Pixels.Height * 0.8f; // Trigger slightly earlier

                contentBoundaryTrigger = atContentEnd && forwardPlaneEntering;
            }

            return visualTrigger || contentBoundaryTrigger;
        }

        /// <summary>
        /// Determines if we should swap up based on visual position and content boundaries  
        /// </summary>
        protected virtual bool ShouldSwapUp(SKRect rectBackward)
        {
            // Original visual trigger: backward plane center crosses viewport center
            bool visualTrigger = rectBackward.MidY > Viewport.Pixels.Height / 2 + DrawingRect.Top;

            // Content boundary trigger: at start of content and backward plane is becoming visible
            bool contentBoundaryTrigger = false;

            if (Content is SkiaLayout layout && layout.IsTemplated &&
                layout.MeasureItemsStrategy == MeasuringStrategy.MeasureVisible)
            {
                // Check if we're at content start and backward plane is entering viewport
                bool atContentStart = layout.FirstMeasuredIndex <= 0;
                bool backwardPlaneEntering =
                    rectBackward.Bottom > Viewport.Pixels.Height * 0.2f; // Trigger slightly earlier

                contentBoundaryTrigger = atContentStart && backwardPlaneEntering;
            }

            return visualTrigger || contentBoundaryTrigger;
        }

        /// <summary>
        /// This is called when scrolling changes when in UseVirtual mode, override this to draw custom content
        /// </summary>
        /// <param name="context"></param>
        public virtual void DrawVirtual(DrawingContext context)
        {
            // Check if children need redraw and invalidate specific planes
            if (_childrenNeedRedraw)
            {
                Content.DirtyChildrenTracker.Clear();
                _childrenNeedRedraw = false;

                InvalidatePlanes();
            }
            
            if (PlaneCurrent == null)
            {
                InitializePlanes();
                if (PlaneCurrent == null || PlaneForward == null || PlaneBackward == null)
                {
                    Super.Log("Failed to create planes");
                    return;
                }
            }

            var displayRectA = new SKRect(
                ContentRectWithOffset.Pixels.Left,
                ContentRectWithOffset.Pixels.Top,
                ContentRectWithOffset.Pixels.Left + _planeWidth,
                ContentRectWithOffset.Pixels.Top + _planeHeight
            );

            if (!PlaneCurrent.IsReady)
            {
                //Debug.WriteLine($"Preparing PLANE {PlaneCurrent.Id}..");
                PreparePlane(context.WithDestination(displayRectA), PlaneCurrent);
            }

            // Draw the planes
            var currentScroll = InternalViewportOffset.Pixels.Y;

            var rectBase = new SKRect(0, 0, _planeWidth, _planeHeight);
            rectBase.Offset(DrawingRect.Left, DrawingRect.Top);

            var rectCurrent = rectBase;
            var rectForward = rectBase;
            var rectBackward = rectBase;

            // Apply vertical offsets
            rectCurrent.Offset(0, currentScroll + PlaneCurrent.OffsetY);
            rectForward.Offset(0, currentScroll + PlaneForward.OffsetY);
            rectBackward.Offset(0, currentScroll + PlaneBackward.OffsetY);

            //  if we've moved enough can re-allow a swap
            if (swappedDownAt != 0 && Math.Abs(currentScroll - swappedDownAt) > _planeHeight / 2f)
            {
                swappedDownAt = 0;
            }

            if (swappedUpAt != 0 && Math.Abs(currentScroll - swappedUpAt) > _planeHeight / 2f)
            {
                swappedUpAt = 0;
            }

            // Draw Backward
            if (PlaneBackward.IsReady)
            {
                if (ContentViewport.Pixels.IntersectsWithInclusive(rectBackward))
                {
                    PlaneBackward.CachedObject.Draw(context.Context.Canvas, rectBackward.Left, rectBackward.Top, null);
                    PlaneBackward.LastDrawnAt = rectBackward;
                }
            }
            else
            {
                OrderToPreparePlaneBackwardInBackground(context);
                PlaneBackward.CachedObject?.Draw(context.Context.Canvas, rectBackward.Left, rectBackward.Top, null);
            }

            // Draw Current
            if (ContentViewport.Pixels.IntersectsWith(rectCurrent))
            {
                PlaneCurrent.CachedObject.Draw(context.Context.Canvas, rectCurrent.Left, rectCurrent.Top, null);
                PlaneCurrent.LastDrawnAt = rectCurrent;
            }

            // Draw Forward
            if (PlaneForward.IsReady)
            {
                if (ContentViewport.Pixels.IntersectsWith(rectForward))
                {
                    PlaneForward.CachedObject.Draw(context.Context.Canvas, rectForward.Left, rectForward.Top, null);
                    PlaneForward.LastDrawnAt = rectForward;
                }
            }
            else
            {
                OrderToPreparePlaneForwardInBackground(context);
                PlaneForward.CachedObject?.Draw(context.Context.Canvas, rectForward.Left, rectForward.Top,
                    null); //repeat last image for fast scrolling
            }

            // --------------------------------------------------------------------
            // Multiple-swap logic to handle fast scrolling
            // --------------------------------------------------------------------

            int swaps = 0;
            bool swappedSomething = false;
            while (!swappedSomething)
            {
                // ------------------------------------------------------
                // then swap down as many times as needed
                // ------------------------------------------------------
                var topDown = -1f; // break when same 
                while (topDown != rectForward.Top && ShouldSwapDown(rectForward))
                {
                    topDown = rectForward.Top;
                    //if (swappedDownAt != 0)
                    //    break;

                    SwapDown();
                    swaps++;
                    swappedDownAt = currentScroll;
                    swappedSomething = true;

                    rectForward = rectBase;
                    rectForward.Offset(0, currentScroll + PlaneForward.OffsetY);
                }

                // ------------------------------------------------------
                // swap up as many times as needed
                // ------------------------------------------------------
                var topUp = -1f;
                while (topUp != rectBackward.Top && ShouldSwapUp(rectBackward))
                {
                    topUp = rectBackward.Top;
                    //if (swappedUpAt != 0)
                    //    break;

                    SwapUp();
                    swaps++;
                    swappedUpAt = currentScroll;
                    swappedSomething = true;

                    rectBackward = rectBase;
                    rectBackward.Offset(0, currentScroll - rectBackward.Top);
                }

                if (!swappedSomething)
                    break;
            }


        }



        // -----------------------------------------------------------
        // SWAP LOGIC
        // -----------------------------------------------------------
        private void SwapDown()
        {
            Debug.WriteLine(
                $"Swap DOWN: {PlaneForward.Id} becomes Current, {PlaneCurrent.Id} becomes Backward, {PlaneBackward.Id} becomes Forward");
            // forward ↑ current
            // current ↑ backward
            // backward ↓ forward + invalidate
            var temp = PlaneBackward;
            PlaneBackward = PlaneCurrent;
            PlaneCurrent = PlaneForward;
            PlaneForward = temp;
            PlaneForward.OffsetY = PlaneCurrent.OffsetY + _planeHeight;
            PlaneBackward.OffsetY = PlaneCurrent.OffsetY - _planeHeight;
            PlaneForward.Invalidate();
        }

        private void SwapUp()
        {
            Debug.WriteLine(
                $"Swap UP: {PlaneBackward.Id} becomes Current, {PlaneCurrent.Id} becomes Forward, {PlaneForward.Id} becomes Backward");
            var temp = PlaneForward;
            PlaneForward = PlaneCurrent;
            PlaneCurrent = PlaneBackward;
            PlaneBackward = temp;
            PlaneBackward.OffsetY = PlaneCurrent.OffsetY - _planeHeight;
            PlaneForward.OffsetY = PlaneCurrent.OffsetY + _planeHeight;
            PlaneBackward.Invalidate();
        }

        /// <summary>
        /// Calculate the specific viewport area this plane should render
        /// </summary>
        protected virtual SKRect CalculateViewportForPlane(Plane plane, SKPoint offsetToUse)
        {
            // Create a viewport that represents the area this plane should render
            var planeViewport = new SKRect(0, 0, _planeWidth, _planeHeight);
            
            // Apply the same offsets as the plane rendering
            planeViewport.Offset(offsetToUse.X, offsetToUse.Y);
            planeViewport.Offset(DrawingRect.Left, DrawingRect.Top);
            planeViewport.Offset(plane.OffsetX, plane.OffsetY);
            
            //Debug.WriteLine($"[{plane.Id}] Calculated plane-specific viewport: {planeViewport}");
            
            return planeViewport;
        }

        protected virtual void PreparePlane(DrawingContext context, Plane plane)
        {
            // Ensure content is properly measured before painting the plane
            //if (Content != null && Content.NeedMeasure)
            //{
            //    // Force re-measurement of the entire content to handle cases where
            //    // cells have !WasMeasured (strategy changes, global layout changes, etc.)
            //    var availableSize = ContentSize.Pixels;
            //    Content.Measure(availableSize.Width, availableSize.Height, RenderingScale);
            //}
            
            var destination = plane.Destination;

            var recordingContext = context.CreateForRecordingImage(plane.Surface, destination.Size);

            var viewport = plane.Destination;

            // Use captured offset from trigger time to avoid race conditions
            var capturedOffset = context.GetArgument(nameof(ContextArguments.Offset)) as SKPoint?;
            var offsetToUse = capturedOffset ?? InternalViewportOffset.Pixels;

            //if (capturedOffset.HasValue)
            //{
            //    Debug.WriteLine($"Using captured offset for {plane.Id}: {capturedOffset.Value}");
            //}
            //else
            //{
            //    Debug.WriteLine($"No captured offset for {plane.Id}, using current: {InternalViewportOffset.Pixels}");
            //}

            viewport.Offset(offsetToUse.X, offsetToUse.Y);
            viewport.Offset(DrawingRect.Left, DrawingRect.Top);
            viewport.Offset(plane.OffsetX, plane.OffsetY);

            // Calculate plane-specific viewport for managed virtualization
            var planeSpecificViewport = CalculateViewportForPlane(plane, offsetToUse);

            var c = recordingContext.Context.Canvas.Save();
            recordingContext.Context.Canvas.Translate(-viewport.Left, -viewport.Top);
            recordingContext.Context.Canvas.Clear(plane.BackgroundColor);

            PaintOnPlane(recordingContext
                .WithDestination(viewport)
                .WithArguments(
                    new(nameof(ContextArguments.Plane), plane.Id),
                    new(nameof(ContextArguments.Viewport), viewport),
                    new(nameof(ContextArguments.PlaneViewport), planeSpecificViewport)), plane);

            recordingContext.Context.Canvas.RestoreToCount(c);

            // Capture rendering tree for gesture processing after content is painted
            if (Content is SkiaLayout layout && layout.RenderTree != null)
            {
                plane.CaptureRenderTree(layout.RenderTree, offsetToUse, plane.OffsetY);
                //Debug.WriteLine($"Captured render tree for {plane.Id}: {plane.RenderTree?.Count ?? 0} controls at offset {offsetToUse}, planeOffsetY: {plane.OffsetY}");
            }

            recordingContext.Context.Canvas.Flush();
            DisposeObject(plane.CachedObject);
            plane.CachedObject = new CachedObject(
                SkiaCacheType.Image,
                plane.Surface,
                new SKRect(0, 0, _planeWidth, _planeHeight),
                destination) { PreserveSourceFromDispose = true };

            plane.IsReady = true;
            //Debug.WriteLine($"Plane rendering READY: {plane.Id}");
        }



        protected virtual void PaintOnPlane(DrawingContext context, Plane plane)
        {
            PaintViews(context);
        }



        /// <summary>
        /// Check if gesture point intersects with plane's visible area
        /// </summary>
        protected virtual bool IsGestureInPlane(Plane plane, PointF location)
        {
            var currentScroll = InternalViewportOffset.Pixels.Y;
            var planeRect = new SKRect(0, 0, _planeWidth, _planeHeight);
            planeRect.Offset(DrawingRect.Left, DrawingRect.Top);
            planeRect.Offset(0, currentScroll + plane.OffsetY);

            return ContentViewport.Pixels.IntersectsWith(planeRect) &&
                   planeRect.ContainsInclusive(location.X, location.Y);
        }

        /// <summary>
        /// Process gestures for a specific plane using its rendering tree
        /// </summary>
        protected virtual ISkiaGestureListener ProcessGesturesForPlane(
            Plane plane,
            SkiaGesturesParameters args,
            GestureEventProcessingInfo apply)
        {
            var thisOffset = TranslateInputCoords(apply.ChildOffset);
            var currentScroll = InternalViewportOffset.Pixels.Y;

            // Calculate the plane's current rendered position
            var planeOffsetY = currentScroll + plane.OffsetY;

            // Keep gesture coordinates as-is, but adjust child HitRects to current plane position
            var gesturePoint = new SKPoint(
                args.Event.Location.X + thisOffset.X,
                args.Event.Location.Y + thisOffset.Y);


            // Process gestures using plane's render tree in reverse Z-order
            var renderTree = plane.RenderTree;
            //Debug.WriteLine($"[PLANE {plane.Id}] Processing {renderTree.Count} children");

            bool hadDebug = false;
            for (int i = renderTree.Count - 1; i >= 0; i--)
            {
                var child = renderTree[i];

                if (child.Control == null || child.Control.IsDisposed || child.Control.IsDisposing ||
                    child.Control.InputTransparent || !child.Control.CanDraw)
                    continue;

                //Debug.WriteLine($"[PLANE {plane.Id}] Child {i}: {child.Control.Tag} Rect: {child.Rect} HitRect: {child.HitRect}");

                // Adjust child's HitRect to current plane position
                // Account for: scroll offset change + plane offset change since capture
                var scrollMovement = currentScroll - plane.RenderTreeCaptureOffset.Y;
                var planeMovement = plane.OffsetY - plane.RenderTreeCapturePlaneOffsetY;
                var totalMovement = scrollMovement + planeMovement;
                var adjustedHitRect = child.HitRect;
                adjustedHitRect.Offset(0, totalMovement);

                //if (args.Type == TouchActionResult.Tapped && !hadDebug)
                //{
                //    hadDebug = true;
                //    Debug.WriteLine($"[PLANE {plane.Id}] Raw gesture: {args.Event.Location}, thisOffset: {thisOffset}");
                //    Debug.WriteLine($"[PLANE {plane.Id}] currentScroll: {currentScroll}, plane.OffsetY: {plane.OffsetY}, planeOffsetY: {planeOffsetY}");
                //    Debug.WriteLine($"[PLANE {plane.Id}] captureOffset: {plane.RenderTreeCaptureOffset}, capturePlaneOffsetY: {plane.RenderTreeCapturePlaneOffsetY}");
                //    Debug.WriteLine($"[PLANE {plane.Id}] scrollMovement: {scrollMovement}, planeMovement: {planeMovement}, totalMovement: {totalMovement}");
                //    Debug.WriteLine($"[PLANE {plane.Id}] Gesture point: {gesturePoint}");
                //    Debug.WriteLine($"[PLANE {plane.Id}] Child {i} original HitRect: {child.HitRect}");
                //    Debug.WriteLine($"[PLANE {plane.Id}] Child {i} adjusted HitRect: {adjustedHitRect}");
                //}

                // Use the adjusted HitRect for hit testing
                if (adjustedHitRect.ContainsInclusive(gesturePoint.X, gesturePoint.Y))
                {
                    //Debug.WriteLine($"[PLANE {plane.Id}] HIT! Loop index {i}, ContextIndex {child.Control.ContextIndex}");

                    // FREEZE FIX: Restore the correct BindingContext from when the plane was captured
                    var originalBindingContext = child.Control.BindingContext;
                    if (child.FreezeBindingContext != null)
                    {
                        child.Control.BindingContext = child.FreezeBindingContext;
                    }

                    try
                    {
                        // Handle child tapped events
                        if (args.Type == TouchActionResult.Tapped)
                        {
                            Content.OnChildTapped(child.Control, args, apply);
                        }

                        // Get gesture listener for this child
                        ISkiaGestureListener listener = child.Control.GesturesEffect;
                        if (listener == null && child.Control is ISkiaGestureListener listen)
                        {
                            listener = listen;
                        }

                        if (listener != null)
                        {
                            var childOffset = TranslateInputCoords(apply.ChildOffsetDirect, false);

                            // Forward gesture to child with proper coordinate transformation
                            var consumed = listener.OnSkiaGestureEvent(args,
                                new GestureEventProcessingInfo(
                                    apply.MappedLocation,
                                    thisOffset,
                                    childOffset,
                                    apply.AlreadyConsumed));

                            if (consumed != null)
                            {
                                return consumed;
                            }

                            // Check attached gesture listeners
                            if (AddGestures.AttachedListeners.TryGetValue(child.Control, out var effect))
                            {
                                var attachedConsumed = effect.OnSkiaGestureEvent(args,
                                    new GestureEventProcessingInfo(
                                        apply.MappedLocation,
                                        thisOffset,
                                        childOffset,
                                        apply.AlreadyConsumed));

                                if (attachedConsumed != null)
                                {
                                    return effect;
                                }
                            }
                        }
                        
                        // Return after first hit to prevent multiple hits
                        return null;
                    }
                    finally
                    {
                        // FREEZE FIX: Always restore the original BindingContext
                        if (child.FreezeBindingContext != null)
                        {
                            child.Control.BindingContext = originalBindingContext;
                        }
                    }
                }
            }

            return null;
        }
    }
}
