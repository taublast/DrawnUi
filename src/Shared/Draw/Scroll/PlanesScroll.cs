namespace DrawnUi.Draw
{
    /// <summary>
    /// Provides the ability to create/draw views directly while scrolling.
    /// Content will be generated dynamically, instead of the usual way.
    /// This control main logic is inside PaintOnPlane override, also it hacks content to work without a real Content.
    /// You have to override `GetMeasuredView` to provide your views to be drawn upon passed index.
    /// TODO: for horizonal
    /// </summary>
    public class PlanesScroll : VirtualScroll
    {
        protected Dictionary<Plane, List<ViewLayoutInfo>> _planeLayoutData = new();

        /// <summary>
        /// Holds layout information for a rendered month cell.
        /// </summary>
        public struct ViewLayoutInfo
        {
            /// <summary>
            /// Relative month index (0 for current, negative for past, positive for future).
            /// </summary>
            public int Index;


            public SKRect DrawingRect;
        }

        public PlanesScroll()
        {
 
        }

        public override bool UseVirtual
        {
            get
            {
                return true && Orientation != ScrollOrientation.Both;
            }
        }


        protected override void OnMeasured()
        {

            //TODO?

            base.OnMeasured();
        }

        


        Plane? GetNeighborPlaneId(Plane plane, bool isScrollingDown)
        {
            Plane? ret = null;
            if (isScrollingDown)
            {
                ret = _planeLayoutData.Keys
                    .Where(x => x.IsReady && x.OffsetY < plane.OffsetY)
                    .OrderByDescending(x => x.OffsetY)
                    .FirstOrDefault();
            }
            else
            {
                ret = _planeLayoutData.Keys
                    .Where(x => x.IsReady && x.OffsetY > plane.OffsetY)
                    .OrderBy(x => x.OffsetY)
                    .FirstOrDefault();
            }
            return ret;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="previousPlaneLayout"></param>
        /// <param name="planeHeight"></param>
        /// <param name="isScrollingDown"></param>
        /// <returns></returns>
        (int startingMonthIndex, float startingOffset) GetNextPlaneStartingLayout(IList<ViewLayoutInfo> previousPlaneLayout, float planeHeight, bool isScrollingDown)
        {
            if (previousPlaneLayout == null || previousPlaneLayout.Count == 0)
            {
                return (0, 0);
            }

            if (isScrollingDown)
            {
                var lastInfo = previousPlaneLayout.Last();
                float totalContentHeight = lastInfo.DrawingRect.Top + lastInfo.DrawingRect.Height;
                float overflow = totalContentHeight - planeHeight;
                overflow = overflow < 0 ? 0 : overflow;
                if (overflow > 0)
                {
                    return (lastInfo.Index, -lastInfo.DrawingRect.Height + overflow);
                }
                int startingMonthIndex = lastInfo.Index + 1;
                return (startingMonthIndex, overflow);
            }
            else
            {
                var firstInfo = previousPlaneLayout.First();
                float underflow = -firstInfo.DrawingRect.Top;
                underflow = underflow < 0 ? 0 : underflow;
                if (underflow > 0)
                {
                    return (firstInfo.Index, firstInfo.DrawingRect.Height - underflow);
                }
                int startingMonthIndex = firstInfo.Index - 1;
                return (startingMonthIndex, -underflow);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="globalOffset"></param>
        /// <param name="top"></param>
        /// <param name="spacingPixels"></param>
        /// <param name="destination"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        (int, float) ComputeDefaultOffset(
            float globalOffset,
            float top,
            float spacingPixels,
            SKRect destination,
            float scale)
        {
            var tempDefaultMonthView = GetMeasuredView(0, destination, scale);
            float defaultCellHeight = tempDefaultMonthView.MeasuredSize.Pixels.Height;
            float estimatedCellTotalHeight = defaultCellHeight + spacingPixels;
            int firstMonthIndex = (int)Math.Floor(globalOffset / estimatedCellTotalHeight);
            float offsetWithinCell = globalOffset - (firstMonthIndex * estimatedCellTotalHeight);
            float startingOffset = top - offsetWithinCell;

            return (firstMonthIndex, startingOffset);
        }

        protected override void PaintOnPlane(DrawingContext context, Plane plane)
        {
            var destination = context.Destination;
            var scale = context.Scale;

            bool isScrollingDown = (this.ScrollingDirection != LinearDirectionType.Backward);

            float planeTop = destination.Top;
            float planeHeight = _planeHeight;
            float spacingPixels = 0f;          // vertical spacing between months

            float globalOffset = InternalViewportOffset.Pixels.Y;

            int firstMonthIndex;
            float startingOffset;

            //glue to neighbor
            var neighbor = GetNeighborPlaneId(plane, isScrollingDown);
            if (neighbor != null && _planeLayoutData.TryGetValue(neighbor, out var previousLayout)
                && previousLayout.Count > 0)
            {
                (firstMonthIndex, startingOffset)
                    = GetNextPlaneStartingLayout(previousLayout, planeHeight, isScrollingDown);
            }
            else
            {
                // no neighbor data
                (firstMonthIndex, startingOffset)
                    = ComputeDefaultOffset(globalOffset, planeTop, spacingPixels, destination, scale);
            }

            // will draw with direction: +1 for down, -1 for up
            int direction = isScrollingDown ? +1 : -1;
            float currentPos = isScrollingDown
                ? (planeTop + startingOffset)
                : (planeTop + planeHeight + startingOffset);

            int monthIndex = firstMonthIndex;
            var layoutInfoList = new List<ViewLayoutInfo>();

            while (true)
            {
                bool outOfPlane = isScrollingDown
                    ? (currentPos >= (planeTop + planeHeight))
                    : (currentPos <= planeTop);

                if (outOfPlane)
                    break; // we've filled this plane


                var view = GetMeasuredView(monthIndex, destination, scale);
                float cellHeight = view.MeasuredSize.Pixels.Height;

                // C) Calculate rect
                SKRect rect;
                if (isScrollingDown)
                {
                    // top->bottom
                    rect = new SKRect(
                        destination.Left,
                        currentPos,
                        destination.Right,
                        currentPos + cellHeight
                    );

                    // Move downward
                    currentPos += (cellHeight + spacingPixels);
                }
                else
                {
                    // bottom->top
                    float cellTop = currentPos - cellHeight;
                    rect = new SKRect(
                        destination.Left,
                        cellTop,
                        destination.Right,
                        currentPos
                    );

                    // Move upward
                    currentPos -= (cellHeight + spacingPixels);
                }

                //rect.Offset(DrawingRect.Location);

                view.Arrange(rect, view.SizeRequest.Width, view.SizeRequest.Height, scale);
                view.Render(context.WithDestination(rect));

                var info = new ViewLayoutInfo
                {
                    Index = monthIndex,
                    DrawingRect = rect,
                };

                if (isScrollingDown)
                {
                    layoutInfoList.Add(info);
                }
                else
                {
                    layoutInfoList.Insert(0, info); // is drawing bottom->top
                }

                monthIndex += direction;
            }

            // 5) Save the layout data for continuity
            _planeLayoutData[plane] = layoutInfoList;

        }


        protected override void PreparePlane(DrawingContext context, Plane plane)
        {
            var destination = context.Destination;
            plane.Destination = destination;

            var recordingContext = context.CreateForRecordingImage(plane.Surface, destination.Size);
            recordingContext.Context.Canvas.Clear(plane.BackgroundColor);

            var fromZero = destination;
            fromZero.Offset(-DrawingRect.Left, -DrawingRect.Top);

            var viewport = plane.Destination;
            viewport.Offset(-InternalViewportOffset.Pixels.X, -InternalViewportOffset.Pixels.Y);
            viewport.Offset(plane.OffsetX, plane.OffsetY);

            PaintOnPlane(recordingContext.WithDestination(fromZero)
                .WithArguments(
                new(ContextArguments.Plane.ToString(), plane.Id),
                new(ContextArguments.Viewport.ToString(), viewport)), plane); ;

            recordingContext.Context.Canvas.Flush();
            DisposeObject(plane.CachedObject);

            plane.CachedObject = new CachedObject(
                SkiaCacheType.Image,
                plane.Surface,
                new SKRect(0, 0, _planeWidth, _planeHeight),
                destination)
            {
                PreserveSourceFromDispose = true
            };

            plane.IsReady = true;
        }




    }
}
