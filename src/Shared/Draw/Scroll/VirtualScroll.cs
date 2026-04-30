


namespace DrawnUi.Draw
{


    /// <summary>
    /// this control gets a view and draws it on a virtual scrolling plane
    /// </summary>
    public class VirtualScroll : SkiaScroll
    {

        public VirtualScroll()
        {
            Content = new SkiaControl() // simulated
            {
                BackgroundColor = Colors.Red
            };
        }

        protected override bool IsContentActive
        {
            get
            {
                return true;
            }
        }

        public override bool UseVirtual
        {
            get
            {
                return true;
            }
        }

        protected override void SetDetectIndexChildPoint(RelativePositionType option = RelativePositionType.Start)
        {
            return;
        }

        protected virtual void SetContentSize()
        {
            if (Orientation == ScrollOrientation.Vertical)
            {
                ContentSize = ScaledSize.FromPixels(new(MeasuredSize.Pixels.Width, 10000), MeasuredSize.Scale);
            }
            else if (Orientation == ScrollOrientation.Horizontal)
            {
                ContentSize = ScaledSize.FromPixels(new(10000, MeasuredSize.Pixels.Height), MeasuredSize.Scale);
            }
        }

        protected override void OnMeasured()
        {
            SetContentSize();

            base.OnMeasured();
        }

        /// <summary>
        /// Returns a view for a specific index. Actually used for virtual scroll.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="destination"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        protected virtual SkiaLayout GetMeasuredView(int index, SKRect destination, float scale)
        {
            //todo
 
            return null;
        }

 
        protected override bool PositionViewport(SKRect destination, SKPoint offsetPixels, float viewportScale, float scale, bool forceSyncOffsets)
        {
            if (!IsSnapping)
                Snapped = false;

            //todo do i need this here?
            ContentAvailableSpace = GetContentAvailableRect(destination);

            InternalViewportOffset = ScaledPoint.FromPixels(offsetPixels.X, offsetPixels.Y, scale); //removed pixel rounding

            var childRect = ContentAvailableSpace;
            childRect.Offset(InternalViewportOffset.Pixels.X, InternalViewportOffset.Pixels.Y);

            ContentRectWithOffset = ScaledRect.FromPixels(childRect, scale);

            //Debug.WriteLine($"VirtualScroll at {offsetPixels.Y}");

            //okay... hmm
            ContentViewport = ScaledRect.FromPixels(DrawingRect, scale);

            if (forceSyncOffsets)
            {
                _viewportOffsetX = InternalViewportOffset.Units.X;
                _viewportOffsetY = InternalViewportOffset.Units.Y;
            }

            //POST EVENTS

            OverscrollDistance = CalculateOverscrollDistance(InternalViewportOffset.Units.X, InternalViewportOffset.Units.Y);

            return true;
        }



 
    }
}
