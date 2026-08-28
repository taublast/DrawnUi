using System.Runtime.CompilerServices;

namespace DrawnUi.Draw
{
    public partial class SkiaLayout
    {
        #region StackLayout

        /// <summary>
        /// Cell.Area contains the area for layout
        /// </summary>
        /// <param name="cell"></param>
        /// <param name="child"></param>
        /// <param name="scale"></param>
        public record SecondPassArrange(ControlInStack Cell, SkiaControl Child, float Scale);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected float GetSpacingForIndex(int forIndex, float scale)
        {
            var spacing = 0.0f;
            if (forIndex > 0)
            {
                spacing = (float)Math.Round(Spacing * scale);
            }

            return spacing;
        }

        /// <summary>
        /// TRUE on a thread that is currently recording a band-plane bake (SkiaCachedStack async record):
        /// the paint pass must be a PURE READ — no draining of pending structure changes, no render-tree
        /// swap, no dirty-tracker clears, no background-measure kicks, no preparation posts, no Update().
        /// The old app-level async plane let its bake run all of those from the worker thread — consuming
        /// staged batches into a frozen snapshot (lost updates) and clobbering the gesture tree: the
        /// historical structure-corruption class. Thread-static so only the baking thread sees it.
        /// </summary>
        [ThreadStatic] internal static bool IsPlaneBakePass;

        /// <summary>
        /// When set (thread-static), DrawStackVisibleChildren records the min/max canvas Y actually painted
        /// (cells and skeletons) into PaintedBoundsTop/Bottom — the band-plane record uses it to clamp its
        /// coverage claim to what was REALLY painted (prepared-views realizes cells far beyond the painted
        /// window, so "recordable" alone over-claims).
        /// </summary>
        [ThreadStatic] internal static bool CollectPaintedBounds;

        [ThreadStatic] internal static float PaintedBoundsTop;
        [ThreadStatic] internal static float PaintedBoundsBottom;

        /// <summary>
        /// A bake pass (IsPlaneBakePass) deposits the render tree it built here instead of publishing it
        /// via SetRenderingTree (a worker must never swap the live gesture tree mid-frame). The plane
        /// owner captures the list, pairs it with the baked plane, and installs it on the RENDER thread
        /// when the plane is consumed — so gesture hit rects always share the plane's coordinate frame
        /// (the invariant RenderTree.Offset blit-patching relies on). Thread-static: only the baking
        /// thread writes it, and the owner clears it in the same bake scope.
        /// </summary>
        [ThreadStatic] internal static List<SkiaControlWithRect> CollectedBakeTree;

        /// <summary>
        /// Set (under <see cref="CollectPaintedBounds"/>) when the pass painted at least one SKELETON
        /// placeholder instead of real content. A band-plane record reads it to know whether the plane
        /// it just recorded contains skeletons at all — prep-completion re-records are pointless (and
        /// pure CPU waste on weak devices) for a plane that is already fully real content.
        /// </summary>
        [ThreadStatic] internal static bool PaintedSkeleton;

        /// <summary>
        /// Set (under <see cref="CollectPaintedBounds"/>, bake pass only) when the pass STALE-SERVED a cell —
        /// drew its existing (old-size) pixels because it self-invalidated mid-paint. The kick-time gate proved
        /// every band cell ready, so a stale-serve during the worker bake is always the TOCTOU transient
        /// (ready at kick, went NeedMeasure during Paint). Baking that old-size snapshot freezes the pre-grow
        /// height into the plane -> live re-record adopts the new size -> the 3->2->3 line jump. The plane owner
        /// reads this to REJECT the publish and fall back to the (always-correct) live draw, same as a skeleton.
        /// </summary>
        [ThreadStatic] internal static bool PaintedStaleServe;

        internal static void ResetPaintedBounds()
        {
            PaintedBoundsTop = float.MaxValue;
            PaintedBoundsBottom = float.MinValue;
            PaintedSkeleton = false;
            PaintedStaleServe = false;
        }

        internal static void TrackPaintedBounds(float top, float bottom)
        {
            if (top < PaintedBoundsTop) PaintedBoundsTop = top;
            if (bottom > PaintedBoundsBottom) PaintedBoundsBottom = bottom;
        }

        public int GetSizeKey(SKSize size)
        {
            int hKey = 0;
            //if (RecyclingTemplate != RecyclingTemplate.Disabled)
            {
                if (Type == LayoutType.Column)
                {
                    hKey = (int)Math.Round(size.Height);
                }
                else if (Type == LayoutType.Row)
                {
                    hKey = (int)Math.Round(size.Width);
                }
            }
            return hKey;
        }

        //todo code 5656
        /*
List<SkiaControl> dirtyChildren = DirtyChildrenTracker.GetList();

if (Superview != null)
{
    //enable measuring one changed item in foreground only,
    //for background thread need full measurement
    smartMeasuring =
        WasMeasured
        && dirtyChildren.Count > 0
        && Superview.DrawingThreadId == Thread.CurrentThread.ManagedThreadId
                     && UsingCacheType != SkiaCacheType.ImageDoubleBuffered;
}

var dirty = dirtyChildren.FirstOrDefault();
if (smartMeasuring && dirty != null)
{
    //measure only changed child
    var viewIndex = -1;
    if (IsTemplated)
    {
        viewIndex = dirty.ContextIndex;
        if (viewIndex >= 0)
        {
            ScaledSize newContentSize = null;
            SKSize sizeChange = new();

            IReadOnlyList<SkiaControl> views = null;
            if (!IsTemplated)
            {
                views = GetUnorderedSubviews();
            }

            var index = -1;
            foreach (var cell in LatestStackStructure.GetChildren())
            {
                index++;

                if (newContentSize != null)
                {
                    //Offset the subsequent children
                    cell.Area = new SKRect(
                        cell.Area.Left + sizeChange.Width,
                        cell.Area.Top + sizeChange.Height,
                        cell.Area.Right + sizeChange.Width,
                        cell.Area.Bottom + sizeChange.Height);

                    //todo layout cell ?
                    if (views != null)
                    {
                        LayoutCell(cell.Measured, cell, views[index], scale);
                    }
                }
                else
                if (cell.ControlIndex == viewIndex)
                {
                    //Measure only DirtyChild
                    measured = MeasureAndArrangeCell(cell.Area, cell, dirty, scale);

                    //todo offset other children accroding new size of this cell
                    //and adjust new content size to be returned

                    sizeChange = new SKSize(measured.Pixels.Width - cell.Measured.Pixels.Width,
                        measured.Pixels.Height - cell.Measured.Pixels.Height);

                    newContentSize = ScaledSize.FromPixels(MeasuredSize.Pixels.Width + sizeChange.Width, MeasuredSize.Pixels.Height + sizeChange.Height, scale);
                }
            }

            if (newContentSize != null)
            {
                return newContentSize;
            }
        }
    }
    else
    if (false) //todo for non templated too!
    {
        viewIndex = nonTemplated.FindIndex(dirty);
        if (viewIndex >= 0)
        {
            ScaledSize newContentSize = null;
            SKSize sizeChange = new();

            IReadOnlyList<SkiaControl> views = null;
            if (!IsTemplated)
            {
                views = GetUnorderedSubviews();
            }

            var index = -1;
            foreach (var cell in LatestStackStructure.GetChildren())
            {
                index++;

                if (newContentSize != null)
                {
                    //Offset the subsequent children
                    cell.Area = new SKRect(
                        cell.Area.Left + sizeChange.Width,
                        cell.Area.Top + sizeChange.Height,
                        cell.Area.Right + sizeChange.Width,
                        cell.Area.Bottom + sizeChange.Height);

                    //todo layout cell ?
                    if (views != null)
                    {
                        LayoutCell(cell.Measured, cell, views[index], scale);
                    }
                }
                else
                if (cell.ControlIndex == viewIndex)
                {
                    if (dirty.CanDraw)
                    {
                        //Measure only DirtyChild
                        measured = MeasureAndArrangeCell(cell.Area, cell, dirty, scale);

                        //todo offset other children accroding new size of this cell
                        //and adjust new content size to be returned

                        sizeChange = new SKSize(measured.Pixels.Width - cell.Measured.Pixels.Width,
                            measured.Pixels.Height - cell.Measured.Pixels.Height);

                        newContentSize = ScaledSize.FromPixels(MeasuredSize.Pixels.Width + sizeChange.Width, MeasuredSize.Pixels.Height + sizeChange.Height, scale);
                    }
                    else
                    {
                        if (cell.Measured != ScaledSize.Default)
                        {
                            //add new space
                            sizeChange = new SKSize(measured.Pixels.Width + cell.Measured.Pixels.Width,
                                measured.Pixels.Height + cell.Measured.Pixels.Height);

                            newContentSize = ScaledSize.FromPixels(MeasuredSize.Pixels.Width - sizeChange.Width, MeasuredSize.Pixels.Height - sizeChange.Height, scale);
                        }
                        cell.Measured = ScaledSize.Default;
                    }
                }



            }

            if (newContentSize != null)
            {
                return newContentSize;
            }
        }
    }
}
else
else
{


}
*/

        /// <summary>
        /// Measuring column/row
        /// </summary>
        /// <param name="rectForChildrenPixels"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        public virtual ScaledSize MeasureStackLegacy(SKRect rectForChildrenPixels, float scale)
        {
            var childrenCount = ChildrenFactory.GetChildrenCount(); // Cache count
            if (childrenCount > 0)
            {
                bool isRtl = Super.IsRtl; //todo!!!

                ScaledSize measured;
                SKRect rectForChild = rectForChildrenPixels; //.Clone();

                SkiaControl[] nonTemplated = null;
                if (!IsTemplated)
                {
                    //preload with condition..
                    nonTemplated = GetDrawableChildren(); // Optimized: no LINQ overhead
                }

                bool smartMeasuring = false;

                /*
                List<SkiaControl> dirtyChildren = DirtyChildrenTracker.GetList();

                if (Superview != null)
                {
                    //enable measuring one changed item in foreground only,
                    //for background thread need full measurement
                    smartMeasuring =
                        WasMeasured
                        && dirtyChildren.Count > 0
                        && Superview.DrawingThreadId == Thread.CurrentThread.ManagedThreadId
                                     && UsingCacheType != SkiaCacheType.ImageDoubleBuffered;
                }

                var dirty = dirtyChildren.FirstOrDefault();
                if (smartMeasuring && dirty != null)
                {
                    //measure only changed child
                    var viewIndex = -1;
                    if (IsTemplated)
                    {
                        viewIndex = dirty.ContextIndex;
                        if (viewIndex >= 0)
                        {
                            ScaledSize newContentSize = null;
                            SKSize sizeChange = new();

                            IReadOnlyList<SkiaControl> views = null;
                            if (!IsTemplated)
                            {
                                views = GetUnorderedSubviews();
                            }

                            var index = -1;
                            foreach (var cell in LatestStackStructure.GetChildren())
                            {
                                index++;

                                if (newContentSize != null)
                                {
                                    //Offset the subsequent children
                                    cell.Area = new SKRect(
                                        cell.Area.Left + sizeChange.Width,
                                        cell.Area.Top + sizeChange.Height,
                                        cell.Area.Right + sizeChange.Width,
                                        cell.Area.Bottom + sizeChange.Height);

                                    //todo layout cell ?
                                    if (views != null)
                                    {
                                        LayoutCell(cell.Measured, cell, views[index], scale);
                                    }
                                }
                                else
                                if (cell.ControlIndex == viewIndex)
                                {
                                    //Measure only DirtyChild
                                    measured = MeasureAndArrangeCell(cell.Area, cell, dirty, scale);

                                    //todo offset other children accroding new size of this cell
                                    //and adjust new content size to be returned

                                    sizeChange = new SKSize(measured.Pixels.Width - cell.Measured.Pixels.Width,
                                        measured.Pixels.Height - cell.Measured.Pixels.Height);

                                    newContentSize = ScaledSize.FromPixels(MeasuredSize.Pixels.Width + sizeChange.Width, MeasuredSize.Pixels.Height + sizeChange.Height, scale);
                                }
                            }

                            if (newContentSize != null)
                            {
                                return newContentSize;
                            }
                        }
                    }
                    else
                    if (false) //todo for non templated too!
                    {
                        viewIndex = nonTemplated.FindIndex(dirty);
                        if (viewIndex >= 0)
                        {
                            ScaledSize newContentSize = null;
                            SKSize sizeChange = new();

                            IReadOnlyList<SkiaControl> views = null;
                            if (!IsTemplated)
                            {
                                views = GetUnorderedSubviews();
                            }

                            var index = -1;
                            foreach (var cell in LatestStackStructure.GetChildren())
                            {
                                index++;

                                if (newContentSize != null)
                                {
                                    //Offset the subsequent children
                                    cell.Area = new SKRect(
                                        cell.Area.Left + sizeChange.Width,
                                        cell.Area.Top + sizeChange.Height,
                                        cell.Area.Right + sizeChange.Width,
                                        cell.Area.Bottom + sizeChange.Height);

                                    //todo layout cell ?
                                    if (views != null)
                                    {
                                        LayoutCell(cell.Measured, cell, views[index], scale);
                                    }
                                }
                                else
                                if (cell.ControlIndex == viewIndex)
                                {
                                    if (dirty.CanDraw)
                                    {
                                        //Measure only DirtyChild
                                        measured = MeasureAndArrangeCell(cell.Area, cell, dirty, scale);

                                        //todo offset other children accroding new size of this cell
                                        //and adjust new content size to be returned

                                        sizeChange = new SKSize(measured.Pixels.Width - cell.Measured.Pixels.Width,
                                            measured.Pixels.Height - cell.Measured.Pixels.Height);

                                        newContentSize = ScaledSize.FromPixels(MeasuredSize.Pixels.Width + sizeChange.Width, MeasuredSize.Pixels.Height + sizeChange.Height, scale);
                                    }
                                    else
                                    {
                                        if (cell.Measured != ScaledSize.Default)
                                        {
                                            //add new space
                                            sizeChange = new SKSize(measured.Pixels.Width + cell.Measured.Pixels.Width,
                                                measured.Pixels.Height + cell.Measured.Pixels.Height);

                                            newContentSize = ScaledSize.FromPixels(MeasuredSize.Pixels.Width - sizeChange.Width, MeasuredSize.Pixels.Height - sizeChange.Height, scale);
                                        }
                                        cell.Measured = ScaledSize.Default;
                                    }
                                }



                            }

                            if (newContentSize != null)
                            {
                                return newContentSize;
                            }
                        }
                    }
                }
                else
                else
                {


                }
                */

                SkiaControl template = null;
                ControlInStack firstCell = null;
                measured = ScaledSize.Default;

                var stackHeight = 0.0f;
                var stackWidth = 0.0f;

                var layoutStructure = BuildStackStructure(scale);

                bool useOneTemplate = IsTemplated && //ItemSizingStrategy == ItemSizingStrategy.MeasureFirstItem &&
                                      RecyclingTemplate != RecyclingTemplate.Disabled;

                if (useOneTemplate)
                {
                    template = ChildrenFactory.GetTemplateInstance();
                }

                var maybeSecondPass = true;
                List<SecondPassArrange> listSecondPass = new();
                bool stopMeasuring = false;

                //var visibleArea = GetOnScreenVisibleArea((float)this.VirtualisationInflated * scale);

                //measure
                //left to right, top to bottom
                var cellsToRelease = new List<SkiaControl>();
                var index = -1;
                try
                {
                    for (var row = 0; row < layoutStructure.MaxRows; row++)
                    {
                        if (stopMeasuring)
                        {
                            break;
                        }

                        var maxHeight = 0.0f;
                        var maxWidth = 0.0f;

                        var columnsCount = layoutStructure.GetColumnCountForRow(row);

                        var needMeasureAll = true;
                        if (useOneTemplate)
                        {
                            needMeasureAll = RecyclingTemplate == RecyclingTemplate.Disabled ||
                                             MeasureItemsStrategy == MeasuringStrategy.MeasureAll ||
                                             (MeasureItemsStrategy == MeasuringStrategy.MeasureFirst
                                              && columnsCount != Split)
                                             || !(MeasureItemsStrategy == MeasuringStrategy.MeasureFirst
                                                  && firstCell != null);
                        }

                        if (!DynamicColumns && columnsCount < Split)
                        {
                            columnsCount = Split;
                        }

                        // Calculate the width for each column
                        float widthPerColumn;
                        if (Type == LayoutType.Column)
                        {
                            widthPerColumn = (float)Math.Round(columnsCount > 1
                                ? (rectForChildrenPixels.Width - (columnsCount - 1) * Spacing * scale) / columnsCount
                                : rectForChildrenPixels.Width);
                        }
                        else
                        {
                            widthPerColumn = rectForChildrenPixels.Width;
                        }

                        int column;

                        for (column = 0; column < columnsCount; column++)
                        {
                            try
                            {
                                if (layoutStructure.GetColumnCountForRow(row) < column + 1)
                                    continue; //case when we last row with less items to fill all columns

                                index++;

                                var cell = layoutStructure.Get(column, row);

                                SkiaControl child = null;
                                if (IsTemplated)
                                {
                                    child = ChildrenFactory.GetViewForIndex(cell.ControlIndex, template, 0,
                                        RecyclingTemplate != RecyclingTemplate.Disabled);
                                    //Trace.WriteLine($"[CELL] MEASURE {index} {child.Uid}");
                                    if (template == null)
                                    {
                                        cellsToRelease.Add(child);
                                    }
                                }
                                else
                                {
                                    child = nonTemplated[cell.ControlIndex];
                                }

                                if (child == null)
                                {
                                    Super.Log($"[MeasureStack] FAILED to get child at index {cell.ControlIndex}");
                                    return ScaledSize.Default;
                                }

                                if (!child.CanDraw)
                                {
                                    cell.Measured = ScaledSize.Default;
                                }

                                if (column == 0)
                                    rectForChild.Top += GetSpacingForIndex(row, scale);

                                rectForChild.Left += GetSpacingForIndex(column, scale);
                                var rectFitChild = new SKRect(rectForChild.Left, rectForChild.Top,
                                    rectForChild.Left + widthPerColumn, rectForChild.Bottom);

                                if (IsTemplated)
                                {
                                    bool needMeasure =
                                        needMeasureAll ||
                                        (MeasureItemsStrategy == MeasuringStrategy.MeasureFirst &&
                                         columnsCount != Split)
                                        || !(MeasureItemsStrategy == MeasuringStrategy.MeasureFirst &&
                                             firstCell != null);

                                    if (needMeasure)
                                    {
                                        measured = MeasureAndArrangeCell(rectFitChild, cell, child,
                                            rectForChildrenPixels, scale);
                                        firstCell = cell;
                                    }
                                    else
                                    {
                                        //apply first measured size to cell
                                        var offsetX = rectFitChild.Left - firstCell.Area.Left;
                                        var offsetY = rectFitChild.Top - firstCell.Area.Top;
                                        var arranged = firstCell.Destination;
                                        arranged.Offset(new(offsetX, offsetY));

                                        cell.Area = rectFitChild;
                                        cell.Measured = measured.Clone();
                                        cell.Destination = arranged;
                                        cell.WasMeasured = true;
                                    }
                                }
                                //todo !!!
                                //else
                                //if (Type == LayoutType.Column && child.VerticalOptions == LayoutOptions.Fill)
                                //{
                                //    listSecondPass.Add(new(cell, child, scale));
                                //}
                                //else
                                //if (Type == LayoutType.Row && child.HorizontalOptions == LayoutOptions.Fill)
                                //{
                                //    listSecondPass.Add(new(cell, child, scale));
                                //}
                                else
                                {
                                    measured = MeasureAndArrangeCell(rectFitChild, cell, child, rectForChildrenPixels,
                                        scale);

                                    if (maybeSecondPass) //has infinity in destination
                                    {
                                        if (Type == LayoutType.Column && child.HorizontalOptions != LayoutOptions.Start)
                                        {
                                            listSecondPass.Add(new(cell, child, scale));
                                        }
                                        else if (Type == LayoutType.Row && child.VerticalOptions != LayoutOptions.Start)
                                        {
                                            listSecondPass.Add(new(cell, child, scale));
                                        }
                                    }
                                }

                                if (!measured.IsEmpty)
                                {
                                    maxWidth += measured.Pixels.Width + GetSpacingForIndex(column, scale);

                                    if (measured.Pixels.Height > maxHeight)
                                        maxHeight = measured.Pixels.Height;

                                    //offset -->
                                    rectForChild.Left += (float)(measured.Pixels.Width);
                                }

                                cell.WasMeasured = true;

                                //if (IsTemplated && MeasureItemsStrategy == MeasuringStrategy.MeasureVisible)
                                //{
                                //    if (!visibleArea.Pixels.IntersectsWithInclusive(cell.Destination))
                                //    {
                                //        stopMeasuring = true;
                                //        break;
                                //    }
                                //}

                                //if (!useOneTemplate && IsTemplated)
                                //{
                                //    ChildrenFactory.ReleaseView(child);
                                //}
                            }
                            catch (Exception e)
                            {
                                Super.Log(e);
                                break;
                            }
                        } //end of iterate columns

                        if (maxWidth > stackWidth)
                            stackWidth = maxWidth;

                        stackHeight += maxHeight + GetSpacingForIndex(row, scale);
                        rectForChild.Top += (float)(maxHeight);

                        rectForChild.Left = 0; //reset to start
                    } //end of iterate rows


                    if (HorizontalOptions.Alignment == LayoutAlignment.Fill || SizeRequest.Width >= 0)
                    {
                        stackWidth = float.IsFinite(rectForChildrenPixels.Width) ? rectForChildrenPixels.Width : stackWidth; // Fill inside an unbounded axis = content size, never infinity
                    }

                    if (VerticalOptions.Alignment == LayoutAlignment.Fill || SizeRequest.Height >= 0)
                    {
                        stackHeight = float.IsFinite(rectForChildrenPixels.Height) ? rectForChildrenPixels.Height : stackHeight;
                    }

                    //second layout pass in some cases
                    var autoRight = rectForChildrenPixels.Right;
                    if (!this.NeedFillX)
                    {
                        autoRight = rectForChildrenPixels.Left + stackWidth;
                    }

                    var autoBottom = rectForChildrenPixels.Bottom;
                    if (!this.NeedFillY)
                    {
                        autoBottom = rectForChildrenPixels.Top + stackHeight;
                    }

                    var autoRect = new SKRect(
                        rectForChildrenPixels.Left, rectForChildrenPixels.Top,
                        autoRight,
                        autoBottom);

                    foreach (var secondPass in listSecondPass)
                    {
                        if (float.IsInfinity(secondPass.Cell.Area.Bottom))
                        {
                            secondPass.Cell.Area = new(secondPass.Cell.Area.Left, secondPass.Cell.Area.Top,
                                secondPass.Cell.Area.Right, secondPass.Cell.Area.Top + stackHeight);
                        }
                        else if (float.IsInfinity(secondPass.Cell.Area.Top))
                        {
                            secondPass.Cell.Area = new(secondPass.Cell.Area.Left,
                                secondPass.Cell.Area.Bottom - stackHeight,
                                secondPass.Cell.Area.Right, secondPass.Cell.Area.Bottom);
                        }

                        if (secondPass.Cell.Area.Height > stackHeight)
                        {
                            secondPass.Cell.Area = new(secondPass.Cell.Area.Left, secondPass.Cell.Area.Top,
                                secondPass.Cell.Area.Right, secondPass.Cell.Area.Top + stackHeight);
                        }

                        if (float.IsInfinity(secondPass.Cell.Area.Right))
                        {
                            secondPass.Cell.Area = new(secondPass.Cell.Area.Left, secondPass.Cell.Area.Top,
                                secondPass.Cell.Area.Left + stackWidth, secondPass.Cell.Area.Bottom);
                        }
                        else if (float.IsInfinity(secondPass.Cell.Area.Left))
                        {
                            secondPass.Cell.Area = new(secondPass.Cell.Area.Right - stackWidth,
                                secondPass.Cell.Area.Top,
                                secondPass.Cell.Area.Right, secondPass.Cell.Area.Bottom);
                        }

                        if (secondPass.Cell.Area.Width > stackWidth)
                        {
                            secondPass.Cell.Area = new(secondPass.Cell.Area.Left, secondPass.Cell.Area.Top,
                                secondPass.Cell.Area.Left + stackWidth, secondPass.Cell.Area.Bottom);
                        }

                        LayoutCell(secondPass.Child.MeasuredSize, secondPass.Cell, secondPass.Child,
                            autoRect,
                            secondPass.Scale);
                    }
                }
                finally
                {
                    if (useOneTemplate)
                    {
                        ChildrenFactory.ReleaseTemplateInstance(template);
                    }
                    else if (IsTemplated)
                        foreach (var cell in cellsToRelease)
                        {
                            ChildrenFactory.ReleaseViewInUse(cell);
                        }
                }


                if (HorizontalOptions.Alignment == LayoutAlignment.Fill && WidthRequest < 0)
                {
                    stackWidth = float.IsFinite(rectForChildrenPixels.Width) ? rectForChildrenPixels.Width : stackWidth; // Fill inside an unbounded axis = content size, never infinity
                }

                if (VerticalOptions.Alignment == LayoutAlignment.Fill && HeightRequest < 0)
                {
                    stackHeight = float.IsFinite(rectForChildrenPixels.Height) ? rectForChildrenPixels.Height : stackHeight;
                }

                return ScaledSize.FromPixels(stackWidth, stackHeight, scale);
            }

            return ScaledSize.FromPixels(rectForChildrenPixels.Width, rectForChildrenPixels.Height, scale);
        }


        /// <summary>
        /// Optimized method to get drawable children without LINQ overhead
        /// </summary>
        private SkiaControl[] GetDrawableChildren()
        {
            var views = GetUnorderedSubviews();
            var result = new SkiaControl[views.Count]; // Pre-size to avoid reallocations
            var count = 0;

            for (int i = 0; i < views.Count; i++)
            {
                if (views[i].CanDraw)
                    result[count++] = views[i];
            }

            // Resize only if needed (rare case)
            if (count != result.Length)
                Array.Resize(ref result, count);

            return result;
        }

        /// <summary>
        /// Measuring column/row with 3-pass approach to handle Fill options correctly
        /// </summary>
        public virtual ScaledSize MeasureStackNonTemplated(SKRect rectForChildrenPixels, float scale)
        {
            var childrenCount = ChildrenFactory.GetChildrenCount(); // Cache count
            if (childrenCount <= 0)
                return ScaledSize.FromPixels(rectForChildrenPixels.Width, rectForChildrenPixels.Height, scale);

            // SMART INCREMENTAL MEASURING: Try smart approach first for maximum FPS  
            if (TrySmartIncrementalMeasure(rectForChildrenPixels, scale, out var smartResult))
            {
                return smartResult;
            }

            var nonTemplated = GetDrawableChildren(); // Optimized: no LINQ overhead
            var layoutStructure = BuildStackStructure(scale);

            // Use FastMeasurement property to conditionally skip multi-pass FILL calculations
            if (FastMeasurement)
            {
                return MeasureStackNonTemplatedFast(rectForChildrenPixels, scale, layoutStructure, nonTemplated);
            }

            ////todo fix gestures for ImageComposite without this and then remove
            //if (UsingCacheType == SkiaCacheType.ImageComposite)
            //{
            //    return MeasureStackLegacy(rectForChildrenPixels, scale);
            //}

            return MeasureStack(rectForChildrenPixels, scale, layoutStructure, false, null, nonTemplated);
        }

        /// <summary>
        /// Fast single-pass measurement for non-templated layouts when FastMeasurement=true
        /// Optimized version that inlines critical calculations and skips FILL handling
        /// </summary>
        private ScaledSize MeasureStackNonTemplatedFast(SKRect rectForChildrenPixels, float scale,
            LayoutStructure layoutStructure, SkiaControl[] nonTemplated)
        {
            var stackHeight = 0.0f;
            var stackWidth = 0.0f;

            var rectForChild = rectForChildrenPixels;

            // Cache layout type check and pre-calculate spacing
            var isColumn = Type == LayoutType.Column;
            var spacingScaled = (float)Math.Round(Spacing * scale); // Pre-calculate once

            for (var row = 0; row < layoutStructure.MaxRows; row++)
            {
                var maxHeight = 0.0f;
                var maxWidth = 0.0f;

                // Inline GetEffectiveColumnsCount
                var columnsCount = layoutStructure.GetColumnCountForRow(row);
                if (!DynamicColumns && columnsCount < Split)
                    columnsCount = Split;

                // Inline CalculateWidthPerColumn with pre-calculated spacing
                var widthPerColumn = isColumn
                    ? (columnsCount > 1
                        ? (float)Math.Round((rectForChildrenPixels.Width - (columnsCount - 1) * spacingScaled) /
                                            columnsCount)
                        : rectForChildrenPixels.Width)
                    : rectForChildrenPixels.Width;

                for (int column = 0; column < columnsCount; column++)
                {
                    if (layoutStructure.GetColumnCountForRow(row) < column + 1)
                        continue;

                    var cell = layoutStructure.Get(column, row);
                    var child = cell.ControlIndex < nonTemplated.Length ? nonTemplated[cell.ControlIndex] : null;

                    if (child?.CanDraw != true)
                    {
                        if (child != null) cell.Measured = ScaledSize.Default;
                        continue;
                    }

                    // Inline ApplySpacing with pre-calculated values
                    if (column == 0 && row > 0)
                        rectForChild.Top += spacingScaled;
                    if (column > 0)
                        rectForChild.Left += spacingScaled;

                    var rectFitChild = new SKRect(rectForChild.Left, rectForChild.Top,
                        rectForChild.Left + widthPerColumn, rectForChild.Bottom);

                    var measured = MeasureAndArrangeCell(rectFitChild, cell, child, rectForChildrenPixels, scale);

                    // Track child as dirty for ImageComposite cache support
                    if (IsCacheComposite)
                    {
                        TrackChildAsDirty(child);
                    }

                    if (!measured.IsEmpty)
                    {
                        // Inline UpdateRowDimensions with pre-calculated spacing
                        maxWidth += measured.Pixels.Width + (column > 0 ? spacingScaled : 0);
                        if (measured.Pixels.Height > maxHeight)
                            maxHeight = measured.Pixels.Height;

                        // Column slot advances by the SLOT width (Split > 1), not by what the cell measured
                        rectForChild.Left += isColumn ? widthPerColumn : measured.Pixels.Width;
                    }

                    cell.WasMeasured = true;
                }

                // Inline UpdateStackSize with pre-calculated spacing
                if (maxWidth > stackWidth)
                    stackWidth = maxWidth;
                stackHeight += maxHeight + (row > 0 ? spacingScaled : 0);

                rectForChild.Top += maxHeight;
                rectForChild.Left = 0;
            }

            // Inline ApplyFillConstraints
            if (HorizontalOptions.Alignment == LayoutAlignment.Fill || SizeRequest.Width >= 0)
                stackWidth = float.IsFinite(rectForChildrenPixels.Width) ? rectForChildrenPixels.Width : stackWidth; // Fill inside an unbounded axis = content size, never infinity
            if (VerticalOptions.Alignment == LayoutAlignment.Fill || SizeRequest.Height >= 0)
                stackHeight = float.IsFinite(rectForChildrenPixels.Height) ? rectForChildrenPixels.Height : stackHeight;

            return ScaledSize.FromPixels(stackWidth, stackHeight, scale);
        }


        /// <summary>
        /// Measuring column/row for templated for fastest way possible
        /// </summary>
        public virtual ScaledSize MeasureStackTemplated(SKRect rectForChildrenPixels, float scale)
        {
            //return MeasureStackLegacy(rectForChildrenPixels, scale);

            var childrenCount = ChildrenFactory.GetChildrenCount(); // Cache count
            if (childrenCount <= 0)
                return ScaledSize.FromPixels(rectForChildrenPixels.Width, rectForChildrenPixels.Height, scale);

            if (TrySmartIncrementalMeasure(rectForChildrenPixels, scale, out var smartResult))
            {
                return smartResult;
            }

            var layoutStructure = BuildStackStructure(scale);
            var useOneTemplate = IsTemplated && RecyclingTemplate != RecyclingTemplate.Disabled;
            var template = useOneTemplate ? ChildrenFactory.GetTemplateInstance() : null;

            try
            {
                // Use FastMeasurement property to conditionally skip multi-pass FILL calculations
                if (true) //FastMeasurement)
                {
                    return MeasureStackTemplatedFast(rectForChildrenPixels, scale, layoutStructure, template);
                }
                else
                {
                    return MeasureStack(rectForChildrenPixels, scale, layoutStructure, true, template, null);
                }
            }
            finally
            {
                if (useOneTemplate && template != null)
                {
                    ChildrenFactory.ReleaseTemplateInstance(template);
                }
            }
        }

        /// <summary>
        /// Fast single-pass measurement for templated layouts when FastMeasurement=true
        /// Optimized version that inlines critical calculations and skips FILL handling
        /// </summary>
        private ScaledSize MeasureStackTemplatedFast(SKRect rectForChildrenPixels, float scale,
            LayoutStructure layoutStructure, SkiaControl template)
        {
            var stackHeight = 0.0f;
            var stackWidth = 0.0f;
            var firstCell = (ControlInStack)null;
            var useOneTemplate = template != null;

            // Only allocate collections if we'll actually use them
            List<SkiaControl> cellsToRelease = null;
            if (!useOneTemplate)
            {
                cellsToRelease = new List<SkiaControl>();
            }

            var rectForChild = rectForChildrenPixels;

            // Cache layout type check and pre-calculate spacing
            var isColumn = Type == LayoutType.Column;
            var spacingScaled = (float)Math.Round(Spacing * scale); // Pre-calculate once

            try
            {
                for (var row = 0; row < layoutStructure.MaxRows; row++)
                {
                    var maxHeight = 0.0f;
                    var maxWidth = 0.0f;

                    // Inline GetEffectiveColumnsCount
                    var columnsCount = layoutStructure.GetColumnCountForRow(row);
                    if (!DynamicColumns && columnsCount < Split)
                        columnsCount = Split;

                    // Inline ShouldMeasureAll
                    var needMeasureAll = !useOneTemplate ||
                                         RecyclingTemplate == RecyclingTemplate.Disabled ||
                                         MeasureItemsStrategy == MeasuringStrategy.MeasureAll ||
                                         (MeasureItemsStrategy == MeasuringStrategy.MeasureFirst &&
                                          columnsCount != Split) ||
                                         !(MeasureItemsStrategy == MeasuringStrategy.MeasureFirst && firstCell != null);

                    // Inline CalculateWidthPerColumn with pre-calculated spacing
                    var widthPerColumn = isColumn
                        ? (columnsCount > 1
                            ? (float)Math.Round((rectForChildrenPixels.Width - (columnsCount - 1) * spacingScaled) /
                                                columnsCount)
                            : rectForChildrenPixels.Width)
                        : rectForChildrenPixels.Width;

                    for (int column = 0; column < columnsCount; column++)
                    {
                        if (layoutStructure.GetColumnCountForRow(row) < column + 1)
                            continue;

                        var cell = layoutStructure.Get(column, row);

                        // Inline GetChildForMeasurement
                        var child = ChildrenFactory.GetViewForIndex(cell.ControlIndex, template, 0,
                            RecyclingTemplate != RecyclingTemplate.Disabled);
                        if (!useOneTemplate && child != null)
                            cellsToRelease?.Add(child);

                        if (child?.CanDraw != true)
                        {
                            if (child != null)
                            {
                                cell.Measured = ScaledSize.Default;
                                cell.WasMeasured = true;
                                LastMeasuredIndexLocal = cell.ControlIndex;
                            }

                            continue;
                        }

                        // Inline ApplySpacing with pre-calculated values
                        if (column == 0 && row > 0)
                            rectForChild.Top += spacingScaled;
                        if (column > 0)
                            rectForChild.Left += spacingScaled;

                        var rectFitChild = new SKRect(rectForChild.Left, rectForChild.Top,
                            rectForChild.Left + widthPerColumn, rectForChild.Bottom);

                        // Inline MeasureChildCell
                        ScaledSize measured;
                        if (needMeasureAll)
                        {
                            measured = MeasureAndArrangeCell(rectFitChild, cell, child, rectForChildrenPixels, scale);
                            firstCell = cell;
                        }
                        else
                        {
                            var offsetX = rectFitChild.Left - firstCell.Area.Left;
                            var offsetY = rectFitChild.Top - firstCell.Area.Top;
                            var arranged = firstCell.Destination;
                            arranged.Offset(new(offsetX, offsetY));

                            cell.Area = rectFitChild;
                            cell.Measured = firstCell.Measured.Clone();
                            cell.Destination = arranged;
                            cell.WasMeasured = true;
                            measured = firstCell.Measured;
                        }

                        // Track child as dirty for ImageComposite cache support
                        if (IsCacheComposite)
                        {
                            TrackChildAsDirty(child);
                        }

                        if (!measured.IsEmpty)
                        {
                            // Inline UpdateRowDimensions with pre-calculated spacing
                            maxWidth += measured.Pixels.Width + (column > 0 ? spacingScaled : 0);
                            if (measured.Pixels.Height > maxHeight)
                                maxHeight = measured.Pixels.Height;

                            // Column slot advances by the SLOT width (Split > 1), not by what the cell measured
                            rectForChild.Left += isColumn ? widthPerColumn : measured.Pixels.Width;
                        }

                        cell.WasMeasured = true;
                        LastMeasuredIndexLocal = cell.ControlIndex;
                    }

                    // Inline UpdateStackSize with pre-calculated spacing
                    if (maxWidth > stackWidth)
                        stackWidth = maxWidth;
                    stackHeight += maxHeight + (row > 0 ? spacingScaled : 0);

                    rectForChild.Top += maxHeight;
                    rectForChild.Left = 0;
                }

                // Inline ApplyFillConstraints
                if (HorizontalOptions.Alignment == LayoutAlignment.Fill || SizeRequest.Width >= 0)
                    stackWidth = float.IsFinite(rectForChildrenPixels.Width) ? rectForChildrenPixels.Width : stackWidth; // Fill inside an unbounded axis = content size, never infinity
                if (VerticalOptions.Alignment == LayoutAlignment.Fill || SizeRequest.Height >= 0)
                    stackHeight = float.IsFinite(rectForChildrenPixels.Height) ? rectForChildrenPixels.Height : stackHeight;

                return ScaledSize.FromPixels(stackWidth, stackHeight, scale);
            }
            finally
            {
                // Release cells if needed
                if (cellsToRelease?.Count > 0)
                {
                    foreach (var cell in cellsToRelease)
                    {
                        ChildrenFactory.ReleaseViewInUse(cell);
                    }
                }
            }
        }


        /// <summary>
        /// Core measurement logic shared between templated and non-templated scenarios
        /// </summary>
        protected virtual ScaledSize MeasureStack(SKRect rectForChildrenPixels, float scale,
            LayoutStructure layoutStructure,
            bool isTemplated, SkiaControl template, SkiaControl[] nonTemplated)
        {
            var stackHeight = 0.0f;
            var stackWidth = 0.0f;
            var firstCell = (ControlInStack)null;
            var useOneTemplate = template != null;

            // Only clear collections if we'll actually use them
            var needSecondPass = false;
            var needCellsToRelease = isTemplated && !useOneTemplate;

            if (needCellsToRelease)
                _tempCellsToRelease.Clear();

            // Handle Fill children pre-calculation for non-templated
            var hasFillHandling = !isTemplated;
            var fixedSpaceUsed = 0.0f;
            var spacingUsed = 0.0f;
            var spacePerFillChild = 0.0f;

            if (hasFillHandling)
            {
                CalculateFillSpace(rectForChildrenPixels, scale, layoutStructure, nonTemplated,
                    ref fixedSpaceUsed, ref spacingUsed, ref spacePerFillChild);
            }

            var rectForChild = rectForChildrenPixels;
            var index = -1;

            // Cache layout type check
            var isColumn = Type == LayoutType.Column;

            // Track minimum dimensions from Fill children in perpendicular direction
            var minStackWidthFromFill = 0.0f;
            var minStackHeightFromFill = 0.0f;
            var sawFillX = false;
            var sawFillY = false;

            // Subpixel accumulation
            double stackY = rectForChildrenPixels.Top;

            try
            {
                for (var row = 0; row < layoutStructure.MaxRows; row++)
                {
                    var maxRowHeight = 0.0f;
                    var maxWidth = 0.0f;

                    // Inline GetEffectiveColumnsCount
                    var columnsCount = layoutStructure.GetColumnCountForRow(row);
                    if (!DynamicColumns && columnsCount < Split)
                        columnsCount = Split;

                    // Inline ShouldMeasureAll
                    var needMeasureAll = !isTemplated || !useOneTemplate ||
                                         RecyclingTemplate == RecyclingTemplate.Disabled ||
                                         MeasureItemsStrategy == MeasuringStrategy.MeasureAll ||
                                         (MeasureItemsStrategy == MeasuringStrategy.MeasureFirst &&
                                          columnsCount != Split) ||
                                         !(MeasureItemsStrategy == MeasuringStrategy.MeasureFirst && firstCell != null);

                    // Inline CalculateWidthPerColumn
                    var widthPerColumn = isColumn
                        ? (columnsCount > 1
                            ? (float)Math.Round((rectForChildrenPixels.Width - (columnsCount - 1) * Spacing * scale) /
                                                columnsCount)
                            : rectForChildrenPixels.Width)
                        : rectForChildrenPixels.Width;

                    double stackX = rectForChildrenPixels.Left;

                    for (int column = 0; column < columnsCount; column++)
                    {
                        if (layoutStructure.GetColumnCountForRow(row) < column + 1)
                            continue;

                        index++;
                        var cell = layoutStructure.Get(column, row);

                        // Inline GetChildForMeasurement
                        SkiaControl child;
                        if (isTemplated)
                        {
                            child = ChildrenFactory.GetViewForIndex(cell.ControlIndex, template, 0,
                                RecyclingTemplate != RecyclingTemplate.Disabled);
                            if (template == null && child != null)
                            {
                                _tempCellsToRelease.Add(child);
                            }
                        }
                        else
                        {
                            child = cell.ControlIndex < nonTemplated.Length ? nonTemplated[cell.ControlIndex] : null;
                        }

                        if (child?.CanDraw != true)
                        {
                            if (child != null) cell.Measured = ScaledSize.Default;
                            continue;
                        }

                        // Inline ApplySpacing
                        if (column == 0)
                        {
                            stackY += GetSpacingForIndex(row, scale);
                        }

                        rectForChild.Top = (float)Math.Round(stackY);

                        stackX += GetSpacingForIndex(column, scale);
                        rectForChild.Left = (float)Math.Round(stackX);

                        var rectFitChild = CreateChildMeasureRect(rectForChild, widthPerColumn, cell,
                            hasFillHandling, spacePerFillChild, nonTemplated, rectForChildrenPixels);

                        var measured = MeasureChildCell(rectFitChild, cell, child, rectForChildrenPixels, scale,
                            isTemplated, needMeasureAll, ref firstCell);

                        // Track child as dirty for ImageComposite cache support
                        if (IsCacheComposite)
                        {
                            TrackChildAsDirty(child);
                        }

                        if (!measured.IsEmpty)
                        {
                            // Track minimum dimensions from Fill children in perpendicular direction
                            if (!isTemplated)
                            {
                                if (isColumn && child.NeedFillX)
                                    sawFillX = true;
                                else if (!isColumn && child.NeedFillY)
                                    sawFillY = true;

                                // Column layout: check Fill-X children for MinimumWidthRequest
                                if (isColumn && child.NeedFillX && child.MinimumWidthRequest >= 0)
                                {
                                    var minWidth = (float)Math.Round(
                                        (child.MinimumWidthRequest + child.Margins.HorizontalThickness) * scale);
                                    if (minWidth > minStackWidthFromFill)
                                        minStackWidthFromFill = minWidth;
                                }
                                // Row layout: check Fill-Y children for MinimumHeightRequest
                                else if (!isColumn && child.NeedFillY && child.MinimumHeightRequest >= 0)
                                {
                                    var minHeight = (float)Math.Round(
                                        (child.MinimumHeightRequest + child.Margins.VerticalThickness) * scale);
                                    if (minHeight > minStackHeightFromFill)
                                        minStackHeightFromFill = minHeight;
                                }
                            }

                            // Inline UpdateRowDimensions
                            // Width calculation:
                            // - For Row (stacking horizontally): fill-X children already handled by second pass (keep original logic)
                            // - For Column (perpendicular): fill-X children should NOT contribute to width
                            if (isTemplated || !child.NeedFillX || !isColumn)
                            {
                                var widthToUse = measured.Pixels.Width;
                                // For Fill-X children in Row layout (stacking direction), use allocated space if larger
                                if (hasFillHandling && child.NeedFillX && !isColumn && float.IsFinite(spacePerFillChild) && spacePerFillChild > widthToUse)
                                {
                                    widthToUse = spacePerFillChild;
                                }

                                maxWidth += widthToUse + GetSpacingForIndex(column, scale);
                            }

                            // Height calculation:
                            // - For Column (stacking vertically): fill-Y children already handled by second pass (keep original logic)
                            // - For Row (perpendicular): fill-Y children should NOT contribute to height
                            if (isTemplated || !child.NeedFillY || isColumn)
                            {
                                var heightToUse = measured.Pixels.Height;
                                // For Fill-Y children in Column layout (stacking direction), use allocated space if larger
                                if (hasFillHandling && child.NeedFillY && isColumn && float.IsFinite(spacePerFillChild) && spacePerFillChild > heightToUse)
                                {
                                    heightToUse = spacePerFillChild;
                                }

                                if (heightToUse > maxRowHeight)
                                    maxRowHeight = heightToUse;
                            }

                            // Subpixel snapping correction
                            if (!isColumn) // Horizontal stacking
                            {
                                stackX += measured.Pixels.Width;
                                float snappedRight = (float)Math.Round(stackX);
                                float snappedWidth = snappedRight - rectForChild.Left;

                                // Force the cell destination to match snapped width
                                var dest = cell.Destination;
                                dest.Right = dest.Left + snappedWidth;
                                cell.Destination = dest;

                                widthPerColumn -= measured.Pixels.Width;
                            }
                            else
                            {
                                // Next column slot (Split > 1): the slot is widthPerColumn wide regardless of
                                // what the cell measured. Was never advanced, so every column-2+ cell landed at
                                // Left = spacing, overlapping column 1.
                                stackX += widthPerColumn;

                                // Vertical stacking (single column)
                                if (columnsCount == 1)
                                {
                                    double tempY = stackY + measured.Pixels.Height;
                                    float snappedBottom = (float)Math.Round(tempY);
                                    float snappedHeight = snappedBottom - rectForChild.Top;

                                    var dest = cell.Destination;
                                    dest.Bottom = dest.Top + snappedHeight;
                                    cell.Destination = dest;

                                    if (snappedHeight > maxRowHeight)
                                    {
                                        maxRowHeight = snappedHeight;
                                    }
                                }
                            }
                        }

                        // Inline CheckSecondPassNeeded
                        if (!isTemplated &&
                            ((isColumn && child.HorizontalOptions != LayoutOptions.Start) ||
                             (!isColumn && child.VerticalOptions != LayoutOptions.Start)))
                        {
                            if (!needSecondPass)
                            {
                                needSecondPass = true;
                                _tempSecondPassList.Clear();
                            }

                            _tempSecondPassList.Add(new(cell, child, scale));
                        }

                        cell.WasMeasured = true;
                    }

                    // Inline UpdateStackSize
                    if (maxWidth > stackWidth)
                        stackWidth = maxWidth;
                    stackHeight += maxRowHeight + GetSpacingForIndex(row, scale);

                    stackY += maxRowHeight;
                }

                // Apply minimum dimensions from Fill children in perpendicular direction
                if (minStackWidthFromFill > stackWidth)
                    stackWidth = minStackWidthFromFill;
                if (minStackHeightFromFill > stackHeight)
                    stackHeight = minStackHeightFromFill;

                // An auto-sized stack whose only cross-axis children are Fill has no content size of its own:
                // adopt the constraint (what a Fill child already does to an auto Row on its main axis)
                // instead of collapsing to 0 and re-measuring every child at width 0.
                if (stackWidth <= 0 && sawFillX && float.IsFinite(rectForChildrenPixels.Width))
                    stackWidth = rectForChildrenPixels.Width;
                if (stackHeight <= 0 && sawFillY && float.IsFinite(rectForChildrenPixels.Height))
                    stackHeight = rectForChildrenPixels.Height;

                // apply fill constraints
                if (float.IsFinite(rectForChildrenPixels.Width) &&
                    HorizontalOptions.Alignment == LayoutAlignment.Fill || SizeRequest.Width >= 0)
                    stackWidth = float.IsFinite(rectForChildrenPixels.Width) ? rectForChildrenPixels.Width : stackWidth; // Fill inside an unbounded axis = content size, never infinity

                if (float.IsFinite(rectForChildrenPixels.Height) && VerticalOptions.Alignment == LayoutAlignment.Fill ||
                    SizeRequest.Height >= 0)
                    stackHeight = float.IsFinite(rectForChildrenPixels.Height) ? rectForChildrenPixels.Height : stackHeight;

                if (needSecondPass)
                {
                    ProcessSecondPass(rectForChildrenPixels, stackWidth, stackHeight);
                }

                return ScaledSize.FromPixels(stackWidth, stackHeight, scale);
            }
            finally
            {
                if (needCellsToRelease && _tempCellsToRelease.Count > 0)
                {
                    foreach (var cell in _tempCellsToRelease)
                    {
                        ChildrenFactory.ReleaseViewInUse(cell);
                    }
                }
            }
        }

        /// <summary>
        /// Calculate space requirements for Fill children (PASS 1)
        /// </summary>
        private void CalculateFillSpace(SKRect rectForChildrenPixels, float scale, LayoutStructure layoutStructure,
            SkiaControl[] nonTemplated, ref float fixedSpaceUsed, ref float spacingUsed, ref float spacePerFillChild)
        {
            var fillChildrenCount = 0;

            for (var row = 0; row < layoutStructure.MaxRows; row++)
            {
                if (row > 0) spacingUsed += GetSpacingForIndex(row, scale);

                var columnsCount = GetEffectiveColumnsCount(layoutStructure, row);

                for (int column = 0; column < columnsCount; column++)
                {
                    if (Type == LayoutType.Row)
                    {
                        spacingUsed += GetSpacingForIndex(column, scale);
                    }

                    if (layoutStructure.GetColumnCountForRow(row) < column + 1)
                        continue;

                    var cell = layoutStructure.Get(column, row);
                    var child = nonTemplated[cell.ControlIndex];

                    if (child?.CanDraw != true) continue;

                    var isFillChild = IsChildFill(child);

                    if (isFillChild)
                    {
                        fillChildrenCount++;
                    }
                    else
                    {
                        fixedSpaceUsed += CalculateChildFixedSpace(child, rectForChildrenPixels, scale);
                    }
                }
            }

            var totalAvailableSpace = Type == LayoutType.Column
                ? rectForChildrenPixels.Height
                : rectForChildrenPixels.Width;
            var remainingSpace = Math.Max(0, totalAvailableSpace - fixedSpaceUsed - spacingUsed);
            // Infinite on an unbounded axis (scroll content) — consumers treat that as "no Fill distribution":
            // Fill children measure their content instead of an infinite slot that blew the stack up to ∞.
            spacePerFillChild = fillChildrenCount > 0 ? remainingSpace / fillChildrenCount : 0;
        }

        /// <summary>
        /// Get child for measurement based on strategy
        /// </summary>
        private SkiaControl GetChildForMeasurement(int controlIndex, bool isTemplated, SkiaControl template,
            SkiaControl[] nonTemplated)
        {
            if (isTemplated)
            {
                var child = ChildrenFactory.GetViewForIndex(controlIndex, template, 0,
                    RecyclingTemplate != RecyclingTemplate.Disabled);
                if (template == null)
                {
                    _tempCellsToRelease.Add(child);
                }

                return child;
            }

            return controlIndex < nonTemplated.Length ? nonTemplated[controlIndex] : null;
        }

        /// <summary>
        /// Create measurement rectangle for child
        /// </summary>
        private SKRect CreateChildMeasureRect(SKRect rectForChild, float widthPerColumn, ControlInStack cell,
            bool hasFillHandling, float spacePerFillChild, SkiaControl[] nonTemplated, SKRect rectForChildrenPixels)
        {
            var rectFitChild = new SKRect(rectForChild.Left, rectForChild.Top,
                rectForChild.Left + widthPerColumn, rectForChild.Bottom);

            // Row main-axis slot must not extend past the row's right edge.
            // Otherwise an End/Center-aligned child arranges itself to this out-of-bounds
            // Right and overflows the row (ignoring Right padding). Pure upper-bound clamp:
            // no-op for any in-bounds child, only pulls back overflowed slots.
            if (Type == LayoutType.Row && rectFitChild.Right > rectForChildrenPixels.Right)
            {
                rectFitChild.Right = rectForChildrenPixels.Right;
            }

            if (hasFillHandling)
            {
                var child = nonTemplated[cell.ControlIndex];
                var isFillChild = IsChildFill(child);

                // Unbounded main axis (content of a scroll): a Fill child is auto-sized, no infinite slot
                if (isFillChild && float.IsFinite(spacePerFillChild))
                {
                    if (Type == LayoutType.Column)
                    {
                        rectFitChild = new SKRect(rectFitChild.Left, rectFitChild.Top,
                            rectFitChild.Right, rectFitChild.Top + spacePerFillChild);
                    }
                    else
                    {
                        rectFitChild = new SKRect(rectFitChild.Left, rectFitChild.Top,
                            rectFitChild.Left + spacePerFillChild, rectFitChild.Bottom);
                    }
                }
            }

            return rectFitChild;
        }

        /// <summary>
        /// Measure individual child cell
        /// </summary>
        private ScaledSize MeasureChildCell(SKRect rectFitChild, ControlInStack cell, SkiaControl child,
            SKRect rectForChildrenPixels, float scale, bool isTemplated, bool needMeasureAll,
            ref ControlInStack firstCell)
        {
            if (isTemplated)
            {
                if (needMeasureAll)
                {
                    var measured = MeasureAndArrangeCell(rectFitChild, cell, child, rectForChildrenPixels, scale);
                    firstCell = cell;
                    return measured;
                }
                else
                {
                    var offsetX = rectFitChild.Left - firstCell.Area.Left;
                    var offsetY = rectFitChild.Top - firstCell.Area.Top;
                    var arranged = firstCell.Destination;
                    arranged.Offset(new(offsetX, offsetY));

                    cell.Area = rectFitChild;
                    cell.Measured = firstCell.Measured.Clone();
                    cell.Destination = arranged;
                    cell.WasMeasured = true;

                    return firstCell.Measured;
                }
            }

            return MeasureAndArrangeCell(rectFitChild, cell, child, rectForChildrenPixels, scale);
        }

        /// <summary>
        /// Helper methods to reduce code duplication
        /// </summary>
        private int GetEffectiveColumnsCount(LayoutStructure layoutStructure, int row)
        {
            var columnsCount = layoutStructure.GetColumnCountForRow(row);
            return !DynamicColumns && columnsCount < Split ? Split : columnsCount;
        }

        private float CalculateWidthPerColumn(SKRect rectForChildrenPixels, int columnsCount, float scale)
        {
            if (Type == LayoutType.Column)
            {
                return (float)Math.Round(columnsCount > 1
                    ? (rectForChildrenPixels.Width - (columnsCount - 1) * Spacing * scale) / columnsCount
                    : rectForChildrenPixels.Width);
            }

            return rectForChildrenPixels.Width;
        }

        private bool ShouldMeasureAll(bool isTemplated, bool useOneTemplate, int columnsCount, ControlInStack firstCell)
        {
            if (!isTemplated || !useOneTemplate) return true;

            return RecyclingTemplate == RecyclingTemplate.Disabled ||
                   MeasureItemsStrategy == MeasuringStrategy.MeasureAll ||
                   (MeasureItemsStrategy == MeasuringStrategy.MeasureFirst && columnsCount != Split) ||
                   !(MeasureItemsStrategy == MeasuringStrategy.MeasureFirst && firstCell != null);
        }

        private bool IsChildFill(SkiaControl child)
        {
            return (Type == LayoutType.Column &&
                    child.VerticalOptions.Alignment == LayoutAlignment.Fill &&
                    child.HeightRequest < 0) ||
                   (Type == LayoutType.Row &&
                    child.HorizontalOptions.Alignment == LayoutAlignment.Fill &&
                    child.WidthRequest < 0);
        }

        private float CalculateChildFixedSpace(SkiaControl child, SKRect rectForChildrenPixels, float scale)
        {
            if (Type == LayoutType.Column && child.HeightRequest >= 0)
            {
                return child.GetHeightRequestPixelsWIthMargins(scale);
            }

            if (Type == LayoutType.Row && child.WidthRequest >= 0)
            {
                return child.GetWidthRequestPixelsWIthMargins(scale);
            }

            var tempRect = Type == LayoutType.Column
                ? new SKRect(0, 0, rectForChildrenPixels.Width, float.PositiveInfinity)
                : new SKRect(0, 0, float.PositiveInfinity, rectForChildrenPixels.Height);

            var tempMeasured = MeasureChild(child, tempRect.Width, tempRect.Height, scale);
            return Type == LayoutType.Column ? tempMeasured.Pixels.Height : tempMeasured.Pixels.Width;
        }

        private void ApplySpacing(ref SKRect rectForChild, int row, int column, float scale)
        {
            if (column == 0)
                rectForChild.Top += GetSpacingForIndex(row, scale);

            rectForChild.Left += GetSpacingForIndex(column, scale);
        }

        private void UpdateRowDimensions(ref float maxWidth, ref float maxHeight, ScaledSize measured,
            SkiaControl child, int column, float scale, bool isTemplated)
        {
            if (isTemplated || !child.NeedFillX || Type != LayoutType.Column)
            {
                maxWidth += measured.Pixels.Width + GetSpacingForIndex(column, scale);
            }

            if (isTemplated || !child.NeedFillY || Type != LayoutType.Row)
            {
                if (measured.Pixels.Height > maxHeight)
                    maxHeight = measured.Pixels.Height;
            }
        }

        private void UpdateStackSize(ref float stackWidth, ref float stackHeight, float maxWidth, float maxHeight,
            int row, float scale)
        {
            if (maxWidth > stackWidth)
                stackWidth = maxWidth;

            stackHeight += maxHeight + GetSpacingForIndex(row, scale);
        }

        private void CheckSecondPassNeeded(ControlInStack cell, SkiaControl child, float scale, bool isTemplated)
        {
            if (!isTemplated)
            {
                if ((Type == LayoutType.Column && child.HorizontalOptions != LayoutOptions.Start) ||
                    (Type == LayoutType.Row && child.VerticalOptions != LayoutOptions.Start))
                {
                    _tempSecondPassList.Add(new(cell, child, scale));
                }
            }
        }

        private void ApplyFillConstraints(ref float stackWidth, ref float stackHeight, SKRect rectForChildrenPixels)
        {
            if (HorizontalOptions.Alignment == LayoutAlignment.Fill || SizeRequest.Width >= 0)
            {
                stackWidth = float.IsFinite(rectForChildrenPixels.Width) ? rectForChildrenPixels.Width : stackWidth; // Fill inside an unbounded axis = content size, never infinity
            }

            if (VerticalOptions.Alignment == LayoutAlignment.Fill || SizeRequest.Height >= 0)
            {
                stackHeight = float.IsFinite(rectForChildrenPixels.Height) ? rectForChildrenPixels.Height : stackHeight;
            }
        }

        private void ProcessSecondPass(SKRect rectForChildrenPixels, float stackWidth, float stackHeight)
        {
            var autoRight = !NeedFillX
                ? rectForChildrenPixels.Left + stackWidth
                : rectForChildrenPixels.Right;

            var autoBottom = !NeedFillY
                ? rectForChildrenPixels.Top + stackHeight
                : rectForChildrenPixels.Bottom;

            var autoRect = new SKRect(rectForChildrenPixels.Left, rectForChildrenPixels.Top, autoRight, autoBottom);

            foreach (var secondPass in _tempSecondPassList)
            {
                AdjustSecondPassCell(secondPass.Cell, stackWidth, stackHeight);

                // Only re-measure fill children in the perpendicular direction with the final stack size
                bool needRemeasure = false;
                if (Type == LayoutType.Column && secondPass.Child.NeedFillX)
                {
                    // Column layout: remeasure fill-X children (perpendicular) with final width
                    needRemeasure = true;
                }
                else if (Type == LayoutType.Row && secondPass.Child.NeedFillY)
                {
                    // Row layout: remeasure fill-Y children (perpendicular) with final height
                    needRemeasure = true;
                }

                ScaledSize measured;
                if (needRemeasure)
                {
                    // Only the PERPENDICULAR constraint changed (final stack width/height). The main-axis
                    // constraint must stay what the first pass used: the layout rect edge, possibly infinite
                    // (content of a scroll). AdjustSecondPassCell clamps an infinite Area to the final stack
                    // extent for ARRANGE purposes; using that as a MEASURE constraint hands a Start/Center
                    // child the whole stack height (sum of all children) — any max/ratio-based sizing inside
                    // (LockRatio, Fill descendants) then consumes it and the child balloons to the stack size.
                    // A main-axis Fill child keeps its finite slot (space distribution) — unless the main axis is
                    // UNBOUNDED: then it is auto-sized (no distribution exists), so it must not be handed the
                    // clamped Area (= whole stack height) either.
                    var mainAxisUnbounded = Type == LayoutType.Column
                        ? float.IsInfinity(rectForChildrenPixels.Bottom)
                        : float.IsInfinity(rectForChildrenPixels.Right);

                    var remeasureWidth = secondPass.Cell.Area.Width;
                    var remeasureHeight = secondPass.Cell.Area.Height;
                    if (Type == LayoutType.Column && (!secondPass.Child.NeedFillY || mainAxisUnbounded))
                    {
                        remeasureHeight = rectForChildrenPixels.Bottom - secondPass.Cell.Area.Top;
                    }
                    else if (Type == LayoutType.Row && (!secondPass.Child.NeedFillX || mainAxisUnbounded))
                    {
                        remeasureWidth = rectForChildrenPixels.Right - secondPass.Cell.Area.Left;
                    }

                    measured = MeasureChild(secondPass.Child, remeasureWidth, remeasureHeight, secondPass.Scale);
                    secondPass.Cell.Measured = measured;

                    if (mainAxisUnbounded)
                    {
                        // auto-sized Fill child: its slot is its measured size, not the stack extent
                        var a = secondPass.Cell.Area;
                        if (Type == LayoutType.Column && secondPass.Child.NeedFillY)
                            secondPass.Cell.Area = new(a.Left, a.Top, a.Right, a.Top + measured.Pixels.Height);
                        else if (Type == LayoutType.Row && secondPass.Child.NeedFillX)
                            secondPass.Cell.Area = new(a.Left, a.Top, a.Left + measured.Pixels.Width, a.Bottom);
                    }
                }
                else
                {
                    measured = secondPass.Child.MeasuredSize;
                }

                LayoutCell(measured, secondPass.Cell, secondPass.Child, autoRect,
                    secondPass.Scale);
            }
        }

        private void AdjustSecondPassCell(ControlInStack cell, float stackWidth, float stackHeight)
        {
            if (float.IsInfinity(cell.Area.Bottom))
            {
                cell.Area = new(cell.Area.Left, cell.Area.Top, cell.Area.Right, cell.Area.Top + stackHeight);
            }
            else if (float.IsInfinity(cell.Area.Top))
            {
                cell.Area = new(cell.Area.Left, cell.Area.Bottom - stackHeight, cell.Area.Right, cell.Area.Bottom);
            }

            if (cell.Area.Height > stackHeight)
            {
                cell.Area = new(cell.Area.Left, cell.Area.Top, cell.Area.Right, cell.Area.Top + stackHeight);
            }

            if (float.IsInfinity(cell.Area.Right))
            {
                cell.Area = new(cell.Area.Left, cell.Area.Top, cell.Area.Left + stackWidth, cell.Area.Bottom);
            }
            else if (float.IsInfinity(cell.Area.Left))
            {
                cell.Area = new(cell.Area.Right - stackWidth, cell.Area.Top, cell.Area.Right, cell.Area.Bottom);
            }

            if (cell.Area.Width > stackWidth)
            {
                cell.Area = new(cell.Area.Left, cell.Area.Top, cell.Area.Left + stackWidth, cell.Area.Bottom);
            }
        }

        // Reusable collections to avoid allocations - these should be class fields
        private readonly List<SecondPassArrange> _tempSecondPassList = new();
        private readonly List<SkiaControl> _tempCellsToRelease = new();

        /// <summary>
        /// SMART INCREMENTAL MEASURING - Only re-measure dirty cells
        /// </summary>
        private bool TrySmartIncrementalMeasure(SKRect rectForChildrenPixels, float scale, out ScaledSize result)
        {
            result = ScaledSize.Default;

            // Cache type safety
            if (UsingCacheType == SkiaCacheType.ImageDoubleBuffered
                || IsCacheComposite)
            {
                return false;
            }

            // Performance guard: Only attempt smart measuring if conditions are optimal
            if (!IsTemplated ||
                //MeasureItemsStrategy != MeasuringStrategy.MeasureAll ||
                DirtyChildrenTracker.IsEmpty || !WasMeasured ||
                Type == LayoutType.Wrap) // Wrap layouts are too complex for this optimization
            {
                return false;
            }

            // Thread safety: Only enable in foreground thread for templated layouts
            if (Superview?.DrawingThreadId != Thread.CurrentThread.ManagedThreadId)
            {
                return false;
            }


            var layoutStructure = GetStackStructureForMeasuring();
            if (layoutStructure?.GetChildren() == null || layoutStructure.GetCount() == 0)
            {
                return false; // No previous layout to work with
            }

            var dirtyChildren = DirtyChildrenTracker.GetList();
            if (dirtyChildren.Count == 0)
            {
                return false; // No dirty children - shouldn't happen but safety first
            }

            return ProcessIncrementalChanges(rectForChildrenPixels, scale, layoutStructure, dirtyChildren, out result);
        }

        /// <summary>
        /// Core smart measuring logic - re-measure only dirty cells and offset others
        /// </summary>
        private bool ProcessIncrementalChanges(SKRect rectForChildrenPixels, float scale,
            LayoutStructure layoutStructure, List<SkiaControl> dirtyChildren, out ScaledSize result)
        {
            result = ScaledSize.Default;
            bool hasChanges = false;
            var totalDeltaWidth = 0f;
            var totalDeltaHeight = 0f;
            var useOneTemplate = RecyclingTemplate != RecyclingTemplate.Disabled;
            var template = useOneTemplate ? ChildrenFactory.GetTemplateInstance() : null;

            try
            {
                // Process each dirty child
                foreach (var dirtyChild in dirtyChildren)
                {
                    // Find the dirty child in current layout structure
                    var currentCell = FindCellByContextIndex(layoutStructure, dirtyChild.ContextIndex);
                    if (currentCell == null) continue; // Child not found in layout
                    var oldSize = currentCell.Measured;

                    // Get the actual child view for re-measuring
                    SkiaControl childView = null;
                    if (IsTemplated)
                    {
                        childView = ChildrenFactory.GetViewForIndex(currentCell.ControlIndex, template, 0,
                            RecyclingTemplate != RecyclingTemplate.Disabled);
                    }
                    else
                    {
                        // For non-templated, dirtyChild should be the actual view
                        childView = dirtyChild;
                    }

                    // Re-measure only this cell with same constraints
                    ScaledSize newSize = ScaledSize.CreateEmpty(scale);

                    if (childView.CanDraw)
                    {
                        newSize = MeasureChild(childView, currentCell.Area.Width, currentCell.Area.Height, scale);
                    }

                    childView.NeedMeasure = false;

                    // Calculate size delta
                    var deltaWidth = newSize.Pixels.Width - oldSize.Pixels.Width;
                    var deltaHeight = newSize.Pixels.Height - oldSize.Pixels.Height;

                    // Skip if change is negligible (avoid floating-point noise)
                    if (Math.Abs(deltaWidth) < 0.5f && Math.Abs(deltaHeight) < 0.5f)
                        continue;

                    hasChanges = true;

                    // Update this cell with new measurements
                    currentCell.Measured = newSize;
                    currentCell.WasMeasured = true;

                    // Re-layout this cell to update Destination
                    LayoutCell(newSize, currentCell, childView, rectForChildrenPixels, scale);

                    // Accumulate deltas for content size adjustment
                    if (Type == LayoutType.Column)
                    {
                        totalDeltaHeight += deltaHeight;
                        // For row layout in column mode, only add width if it makes the row wider
                        if (deltaWidth > 0)
                        {
                            totalDeltaWidth = Math.Max(totalDeltaWidth, deltaWidth);
                        }
                    }
                    else if (Type == LayoutType.Row)
                    {
                        totalDeltaWidth += deltaWidth;
                        // For column in row layout, only add height if it makes the column taller
                        if (deltaHeight > 0)
                        {
                            totalDeltaHeight = Math.Max(totalDeltaHeight, deltaHeight);
                        }
                    }

                    // Offset subsequent cells in the layout  
                    OffsetSubsequentCells(layoutStructure, currentCell, deltaWidth, deltaHeight);
                }

                if (hasChanges)
                {
                    // Calculate new content size by adjusting current size
                    var newContentWidth = MeasuredSize.Pixels.Width + totalDeltaWidth;
                    var newContentHeight = MeasuredSize.Pixels.Height + totalDeltaHeight;

                    // Apply layout constraints (Fill options)
                    if (HorizontalOptions.Alignment == LayoutAlignment.Fill || SizeRequest.Width >= 0)
                        newContentWidth = rectForChildrenPixels.Width;
                    if (VerticalOptions.Alignment == LayoutAlignment.Fill || SizeRequest.Height >= 0)
                        newContentHeight = rectForChildrenPixels.Height;

                    result = ScaledSize.FromPixels(newContentWidth, newContentHeight, scale);

                    // Clear dirty tracking since we've processed all changes
                    ClearDirtyChildren();

                    return true; // Smart measuring succeeded!
                }
            }
            finally
            {
                if (useOneTemplate && template != null)
                {
                    ChildrenFactory.ReleaseTemplateInstance(template);
                }
            }

            return false; // No significant changes, fall back to full measure
        }

        /// <summary>
        /// Find cell by child's ContextIndex for smart measuring
        /// </summary>
        private ControlInStack FindCellByContextIndex(LayoutStructure layoutStructure, int contextIndex)
        {
            foreach (var cell in layoutStructure.GetChildren())
            {
                if (cell.ControlIndex == contextIndex)
                {
                    return cell;
                }
            }

            return null; // Not found
        }

        /// <summary>
        /// Offset subsequent cells after a size change - optimized for performance
        /// </summary>
        private void OffsetSubsequentCells(LayoutStructure layoutStructure, ControlInStack changedCell,
            float deltaWidth, float deltaHeight)
        {
            // Early exit if no significant change
            if (Math.Abs(deltaWidth) < 0.1f && Math.Abs(deltaHeight) < 0.1f) return;

            // Determine which dimension to offset based on layout type
            var offsetX = Type == LayoutType.Row ? deltaWidth : 0f;
            var offsetY = Type == LayoutType.Column ? deltaHeight : 0f;

            if (Math.Abs(offsetX) < 0.1f && Math.Abs(offsetY) < 0.1f) return;

            // Find cells that come after the changed cell and offset them
            foreach (var cell in layoutStructure.GetChildren())
            {
                // For Column layout: offset cells in same column that are in rows below
                // For Row layout: offset cells in same row that are in columns to the right
                bool shouldOffset = false;

                if (Type == LayoutType.Column)
                {
                    // Same column, row is after the changed cell
                    shouldOffset = cell.Column == changedCell.Column && cell.Row > changedCell.Row;
                }
                else if (Type == LayoutType.Row)
                {
                    // Same row, column is after the changed cell
                    shouldOffset = cell.Row == changedCell.Row && cell.Column > changedCell.Column;
                }

                if (shouldOffset)
                {
                    // Offset both Area (for measuring) and Destination (for rendering)
                    cell.Area = new SKRect(
                        cell.Area.Left + offsetX,
                        cell.Area.Top + offsetY,
                        cell.Area.Right + offsetX,
                        cell.Area.Bottom + offsetY);

                    cell.Destination = new SKRect(
                        cell.Destination.Left + offsetX,
                        cell.Destination.Top + offsetY,
                        cell.Destination.Right + offsetX,
                        cell.Destination.Bottom + offsetY);
                }
            }
        }

        private LayoutStructure BuildStackStructure(float scale)
        {
            //build stack grid
            //fill table
            var column = 0;
            var row = 0;
            var rows = new List<List<ControlInStack>>();
            var columns = new List<ControlInStack>();
            var maxColumns = Split;
            int maxRows = 0;

            //returns true if can continue
            bool ProcessStructure(int i, SkiaControl control)
            {
                var add = new ControlInStack { ControlIndex = i, View = control };
                if (control != null)
                {
                    add.ZIndex = control.ZIndex;
                    add.ControlIndex = i;
                }

                // vertical stack or if maxColumns is exceeded
                if (Type == LayoutType.Column && column >= maxColumns
                    || Type == LayoutType.Row && (maxColumns > 0 && column >= maxColumns)
                    || LineBreaks.Contains(i))
                {
                    if (i > 0)
                    {
                        //insert a vbreak between all children
                        rows.Add(columns);
                        columns = new();
                        column = 0;
                        row++;
                    }
                }

                // If maxRows is reached and exceeded, break the loop
                if (maxRows > 0 && row >= maxRows)
                {
                    return false;
                }

                columns.Add(add);
                column++;

                return true;
            }

            if (!IsTemplated)
            {
                var index = -1;
                foreach (var view in GetUnorderedSubviews())
                {
                    if (!view.CanDraw) //this is a critical point, we do not store invisible stuff in structure!
                        continue;

                    index++;
                    if (!ProcessStructure(index, view))
                        break;
                }
            }
            else
            {
                var childrenCount = ChildrenFactory.GetChildrenCount();
                for (int index = 0; index < childrenCount; index++)
                {
                    if (!ProcessStructure(index, null))
                        break;
                }
            }

            rows.Add(columns);

            var structure = new LayoutStructure(rows);

            StackStructureMeasured = structure;

            return structure;
        }

        // Cache for visible area calculation
        private struct VisibleAreaCache
        {
            public SKRect Destination;
            public ScaledRect VisibleArea;
            public DateTime CalculatedAt;
        }

        private VisibleAreaCache? _visibleAreaCache;
        private readonly TimeSpan _cacheLifetime = TimeSpan.FromMilliseconds(16); // 60fps

        /// <summary>   
        /// REPLACE your existing GetOnScreenVisibleArea call in DrawStack with this:
        /// </summary>
        private ScaledRect GetVisibleAreaCached(DrawingContext ctx)
        {
            var now = DateTime.Now;

            // Check if we can reuse cached calculation
            if (_visibleAreaCache.HasValue)
            {
                var cache = _visibleAreaCache.Value;
                var positionDelta = Math.Abs(ctx.Destination.Left - cache.Destination.Left) +
                                    Math.Abs(ctx.Destination.Top - cache.Destination.Top);
                var age = now - cache.CalculatedAt;

                // Reuse if viewport moved less than 5px and cache is fresh
                if (positionDelta < 5 && age < _cacheLifetime)
                {
                    return cache.VisibleArea;
                }
            }

            // Calculate new visible area (expensive operation)
            var inflate = (float)(this.VirtualisationInflated * ctx.Scale);
            float inflateX = inflate, inflateY = inflate;

            // VirtualisationInflatedRatio: extra inflation as a fraction of the viewport size along the
            // Orientation (height for Column, width for Row). Viewport-relative band ahead of the viewport.
            if (VirtualisationInflatedRatio >= 0)
            {
                var viewport = GetOnScreenVisibleArea(ctx).Pixels; // un-inflated viewport
                if (Type == LayoutType.Row)
                    inflateX += (float)(VirtualisationInflatedRatio * viewport.Width);
                else
                    inflateY += (float)(VirtualisationInflatedRatio * viewport.Height);
            }

            var visibleArea = GetOnScreenVisibleArea(ctx, new(inflateX, inflateY));

            // Cache the result
            _visibleAreaCache = new VisibleAreaCache
            {
                Destination = ctx.Destination, VisibleArea = visibleArea, CalculatedAt = now
            };

            return visibleArea;
        }


        /// <summary>
        /// Can be called by some layouts after they calculated the list of visible children to be drawn, but have not drawn them yet
        /// </summary>
        protected virtual void OnBeforeDrawingVisibleChildren(DrawingContext ctx, LayoutStructure structure,
            List<ControlInStack> visibleElements)
        {
        }

        // DEBUG: color-code each visible cell by the background-measure batch that produced it. A left-edge
        // stripe + a top seam line per cell. Where the color changes = a batch boundary (cells glued in by a
        // background MeasureVisible pass). Overlaps / wrong gaps at a color change point straight at the bug.
        private void DrawMeasureBatchOverlay(DrawingContext ctx, List<ControlInStack> visible)
        {
            if (visible == null) return;
            var canvas = ctx.Context.Canvas;
            using var paint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = false };
            foreach (var cell in visible)
            {
                if (cell == null) continue;
                paint.Color = DebugBatchColor(cell.DebugMeasureBatch);
                float l = cell.Drawn.Left, t = cell.Drawn.Top, r = cell.Drawn.Right, b = cell.Drawn.Bottom;
                canvas.DrawRect(l, t, 6f, b - t, paint); // left-edge stripe = this cell's batch
                canvas.DrawRect(l, t, r - l, 2f, paint); // top seam line = batch boundary visible across width
            }
        }

        private static SKColor DebugBatchColor(int batch)
        {
            if (batch <= 0)
                return new SKColor(0x99, 0x99, 0x99, 0xCC); // gray = initial / foreground measure
            var hue = (batch * 67) % 360; // distinct hue per background batch
            return SKColor.FromHsv(hue, 90, 100).WithAlpha(0xCC);
        }

        // DEBUG: enable to log structural corruption the instant it's in the structure: a cell whose slot
        // height (Destination) disagrees with its measured content height, or that overlaps the previous
        // cell (Top above prev Bottom). Prints only offending cells with the exact numbers.
        public static bool DebugAssertStructure;

        // DEBUG: trace one specific item (by ContextIndex/ControlIndex) and its immediate neighbours through
        // every measure, draw and shift. Set to -1 to disable. The window is target-1 .. target+1.
        public static int DebugTraceIndex = -1;

        // True if idx is the traced item or one of its two neighbours.
        public static bool IsTraced(int idx) =>
            DebugTraceIndex >= 0 && Math.Abs(idx - DebugTraceIndex) <= 1;

        public static void TraceIdx(int idx, string where, string msg)
        {
            if (IsTraced(idx))
                Super.Log($"[TRACE {idx}{(idx == DebugTraceIndex ? "*" : "")}] {where}: {msg}");
        }

        private void DebugAssertStructureIntegrity(LayoutStructure structure, SKRect visiblePixels)
        {
            if (structure == null) return;

            // Head insert/remove stages new cells at unsettled positions until CommitPendingStructureRebase;
            // those are legitimately overlapping for a frame -> skip to avoid drowning the real signal.
            if (HeadInsertInFlight || HeadRemoveInFlight) return;

            var spacingPx = (float)Math.Round(Spacing * RenderingScale);

            // Collect only cells actually painted last frame AND inside the visible viewport, then compare
            // in POSITION order (sorted by paint Top) — structure iteration order is scrambled by shifts.
            var painted = new List<ControlInStack>();
            foreach (var cell in structure.GetChildren())
            {
                if (cell == null || !cell.WasLastDrawn || cell.IsCollapsed || cell.Drawn.Height <= 0)
                    continue;
                var r = new SKRect(cell.Drawn.Left, cell.Drawn.Top, cell.Drawn.Left + cell.Drawn.Width,
                    cell.Drawn.Bottom);
                if (!r.IntersectsWith(visiblePixels))
                    continue;
                painted.Add(cell);
            }

            painted.Sort((a, b) => a.Drawn.Top.CompareTo(b.Drawn.Top));

            for (int i = 1; i < painted.Count; i++)
            {
                var prev = painted[i - 1];
                var cell = painted[i];
                var delta = cell.Drawn.Top - prev.Drawn.Bottom; // expected ~= spacingPx
                if (delta < spacingPx - 2f)
                    Super.Log(
                        $"[DRAWN-BAD overlap] idx{cell.ControlIndex} top={cell.Drawn.Top:0} < prev idx{prev.ControlIndex} bottom={prev.Drawn.Bottom:0} (overlap {prev.Drawn.Bottom - cell.Drawn.Top:0}) prevH={prev.Drawn.Height:0} thisH={cell.Drawn.Height:0} prevBatch{prev.DebugMeasureBatch} batch{cell.DebugMeasureBatch}");
                else if (delta > spacingPx + 2f)
                    Super.Log(
                        $"[DRAWN-BAD gap] idx{cell.ControlIndex} top={cell.Drawn.Top:0} > prev idx{prev.ControlIndex} bottom={prev.Drawn.Bottom:0} (gap {delta - spacingPx:0}) prevH={prev.Drawn.Height:0} thisH={cell.Drawn.Height:0} prevBatch{prev.DebugMeasureBatch} batch{cell.DebugMeasureBatch}");
            }
        }

        /// <summary>
        /// Can be called by some layouts after they draw visible children  
        /// </summary>
        protected virtual void OnAfterDrawingVisibleChildren(DrawingContext ctx, LayoutStructure structure,
            List<ControlInStack> visibleElements)
        {
        }

        private long _countVisible;

        /// <summary>
        /// Draw-time follower shift for a cell that re-measured during DrawStack: the delta is gated to
        /// the stacking axis (Row -> X, Column -> Y), same rule as OffsetSubsequentCells. Applying the
        /// cross-axis component would slide followers sideways in a Column (or vertically in a Row) when
        /// a sibling merely changes width (e.g. a centered label mutating text every frame).
        /// </summary>
        private Vector2 GetFollowersOffsetDelta(SKSize diff)
        {
            return new Vector2(
                Type == LayoutType.Row ? diff.Width : 0f,
                Type == LayoutType.Column ? diff.Height : 0f);
        }

        /// <summary>
        /// Single source of truth for the three draw-time grow-shift sites in DrawStackVisibleChildren
        /// (sync-measure, prepared-reconcile, streaming-bridge). Always sets the per-frame OffsetOthers
        /// accumulator from the size delta; when <paramref name="durable"/> also persists the shift into
        /// the structure via OffsetSubsequentCells. Behaviour-identical to the inlined copies it replaces.
        /// </summary>
        private void ShiftFollowersByGrow(LayoutStructure structure, ControlInStack cell,
            SKSize newSize, SKSize reservedSlot, bool durable)
        {
            var diff = newSize - reservedSlot;
            cell.OffsetOthers = GetFollowersOffsetDelta(diff);
            if (durable)
                OffsetSubsequentCells(structure, cell, diff.Width, diff.Height);
        }

        protected virtual SKRect GetStackChildDrawRect(int index, float x, float y, ControlInStack cell)
        {
            if (IsTemplated)
            {
                // Arranged size, same as non-templated. Used to be the SLOT (cell.Area.Width x cell.Area.Bottom —
                // an absolute coordinate, infinite inside a scroll) at the arranged origin: x,y already carry the
                // Center/End offset from LayoutCell, so the child re-aligned inside the slot at draw (double
                // centering, End cells pushed out of their column), and a Fill-Y cell arranged into an infinite
                // height painted float.MaxValue tall. cell.Destination is refreshed by every draw-time LayoutCell.
                return new SKRect(x, y,
                    x + cell.Destination.Width, y + cell.Destination.Height);
            }
            else
            {
                return new SKRect(x, y, x + cell.Drawn.Width,
                    y + cell.Drawn.Height);
            }
        }

        /// <summary>
        /// Renders stack/wrap layout.
        /// Returns number of drawn children.
        /// </summary>
        protected virtual int DrawStack(DrawingContext ctx, LayoutStructure structure)
        {
            var drawn = 0;
            List<SkiaControlWithRect> tree = new();
            var needrebuild = templatesInvalidated;
            List<ControlInStack> visibleElements = new();
            bool updateInternal = false;

            var planeId = ctx.GetArgument(nameof(ContextArguments.Plane)) as string;

            if (structure != null)
            {
                if (IsTemplated && !ChildrenFactory.TemplatesAvailable)
                {
                    return 0;
                }

                //var inflate = (float)(this.VirtualisationInflated * ctx.Scale);

                ScaledRect visibilityAreaReal = GetVisibleAreaCached(ctx);
                ScaledRect visibilityArea = visibilityAreaReal;
                bool usesExpandedViewport = false;

                var recyclingAreaPixels = visibilityArea.Pixels;
                var expendRecycle = ((float)RecyclingBuffer * ctx.Scale);
                recyclingAreaPixels.Inflate(expendRecycle, expendRecycle);

                var firstVisibleIndex = -1;
                var lastVisibleIndex = -1;

                //PASS 1 - VISIBILITY
                Vector2 offsetOthers = Vector2.Zero;
                var currentIndex = -1;
                foreach (var cell in structure.GetChildrenAsSpans())
                {
                    currentIndex++;

                    if (cell == null)
                    {
                        continue;
                    }

                    if (cell.WasMeasured && cell.Destination == SKRect.Empty || cell.Measured.Pixels.Width < 1 ||
                        cell.Measured.Pixels.Height < 1)
                    {
                        cell.IsVisible = false;
                    }
                    else
                    {
                        if (!cell.WasMeasured) // && MeasureItemsStrategy != MeasuringStrategy.MeasureVisible)
                        {
                            // DrawStack tried to draw unmeasured cell!
                            continue; // Skip unmeasured
                        }

                        // Calculate screen position  
                        var x = ctx.Destination.Left + cell.Destination.Left;
                        var y = ctx.Destination.Top + cell.Destination.Top;

                        cell.Drawn.Set(x, y, x + cell.Destination.Width, y + cell.Destination.Height);

                        offsetOthers += cell.OffsetOthers;

                        var insideViewport = cell.Drawn.IntersectsWith(visibilityArea.Pixels);

                        if (firstVisibleIndex >= 0 && !insideViewport)
                        {
                            lastVisibleIndex = currentIndex - 1;
                        }

                        if (Virtualisation != VirtualisationType.Disabled)
                        {
                            if (needrebuild
                                && UsingCacheType == SkiaCacheType.None
                                && Virtualisation == VirtualisationType.Smart
                                && !(IsTemplated && RecyclingTemplate == RecyclingTemplate.Enabled))
                            {
                                cell.IsVisible = true;
                            }
                            else
                            {
                                var viewportVisible = insideViewport;
                                cell.IsVisible = viewportVisible;
                            }
                        }
                        else
                        {
                            cell.IsVisible = true;
                        }

                        if (firstVisibleIndex < 0)
                        {
                            firstVisibleIndex = currentIndex;
                        }
                    }

                    cell.OffsetOthers = Vector2.Zero;
                    cell.WasLastDrawn = false;

                    if (!cell.IsVisible || cell.IsCollapsed)
                    {
                        ChildrenFactory.MarkViewAsHidden(cell.ControlIndex);
                    }
                    //else
                    //if (Virtualisation != VirtualisationType.Disabled &&
                    //         cell.Destination != SKRect.Empty &&
                    //         !cell.Measured.Pixels.IsEmpty)
                    //{
                    //    if (!cell.Drawn.IntersectsWith(recyclingAreaPixels))
                    //    {
                    //        ChildrenFactory.MarkViewAsHidden(cell.ControlIndex);
                    //    }
                    //}

                    // Add to visible elements for drawing
                    if (cell.IsVisible && !cell.IsCollapsed)
                    {
                        visibleElements.Add(cell);
                    }
                }

                _countVisible = visibleElements.Count;

                if (OutputDebug)
                {
                    Super.Log(
                        $"[SkiaLayout] visibility area {visibilityArea}, recycling area {recyclingAreaPixels}, visible items: {visibleElements.Count}");
                }

                OnBeforeDrawingVisibleChildren(ctx, structure, visibleElements);

                if (visibleElements.Count > 0)
                {
                    if (IsTemplated)
                    {
                        int minControlIndex = int.MaxValue;
                        int maxControlIndex = -1;
                        foreach (var e in visibleElements)
                        {
                            if (e.ControlIndex < minControlIndex) minControlIndex = e.ControlIndex;
                            if (e.ControlIndex > maxControlIndex) maxControlIndex = e.ControlIndex;
                        }

                        //FirstMeasuredIndexLocal = visibleElements[0].ControlIndex;
                        //LastVisibleIndexLocal = visibleElements[visibleElements.Count - 1].ControlIndex;
                        FirstVisibleIndexLocal = minControlIndex;
                        LastVisibleIndexLocal = maxControlIndex;
                    }
                    else
                    {
                        //visibleElements.Sort((a, b) => a.ZIndex.CompareTo(b.ZIndex));

                        FirstMeasuredIndexLocal = firstVisibleIndex;
                        FirstVisibleIndexLocal = firstVisibleIndex;
                        LastVisibleIndexLocal = lastVisibleIndex;
                    }
                }
                else
                {
                    FirstVisibleIndexLocal = -1;
                    FirstMeasuredIndexLocal = -1;
                    LastVisibleIndexLocal = -1;
                }

                // Start background measurement if needed
                if (!IsPlaneBakePass &&
                    IsTemplated && structure != null &&
                    MeasureItemsStrategy == MeasuringStrategy.MeasureVisible &&
                    EffectiveItemsSource != null &&
                    lastVisibleIndex < EffectiveItemsSource.Count - 1 && // More items to measure
                    !IsBackgroundMeasuring && _pendingStructureChanges.Count == 0 &&
                    !HeadInsertInFlight && // tail measuring would integrate positions made stale by the head commit
                    !HeadRemoveInFlight) // same: staged positions would miss the pending head-trim translation
                {
                    // We have unmeasured items beyond visible area
                    var nextUnmeasuredIndex = lastVisibleIndex + 1;

                    // Check if we already have measurements cached
                    while (nextUnmeasuredIndex < EffectiveItemsSource.Count &&
                           _measuredItems.ContainsKey(nextUnmeasuredIndex))
                    {
                        nextUnmeasuredIndex++;
                    }

                    if (nextUnmeasuredIndex < EffectiveItemsSource.Count)
                    {
                        StartBackgroundMeasurement(ctx.Destination, ctx.Scale, nextUnmeasuredIndex);
                    }
                }

                if (!IsPlaneBakePass)
                {
                    ClearDirtyChildren();
                }

                //PASS 2 DRAW VISIBLE
                drawn = DrawStackVisibleChildren(ctx, structure, visibleElements, usesExpandedViewport,
                    visibilityAreaReal, tree, ref updateInternal);

                // PREPARED VIEWS: post the priority want-list (visible skeletons first, then ahead of the
                // scroll direction) to the preparation worker, once per draw pass.
                if (!IsPlaneBakePass && UsePreparedViewsActive)
                {
                    PostCellPreparationWants();
                }

                if (DebugDrawMeasureBatches)
                    DrawMeasureBatchOverlay(ctx, visibleElements);
            }

            // A plane-bake pass is a PURE READ: it must not publish a render tree (gestures belong to the
            // live frame), track viewport indices, request updates or advance frame counters. The tree it
            // built is DEPOSITED for the plane owner to install render-thread-side at plane consume —
            // hit rects must share the plane's coordinate frame or blit Offset-patching maps taps wrong.
            if (IsPlaneBakePass)
            {
                CollectedBakeTree = tree;
                return drawn;
            }

            if (needrebuild && visibleElements.Count > 0)
            {
                templatesInvalidated = false;
            }

            SetRenderingTree(tree);

            if (Parent is IDefinesViewport viewport &&
                viewport.TrackIndexPosition != RelativePositionType.None)
            {
                viewport.UpdateVisibleIndex();
            }

            OnPropertyChanged(nameof(DebugString));
            if (updateInternal)
            {
                Update();
            }

            WillDrawFromFreshItemssSource++;

            return drawn;
        }

        /// <summary>
        /// Draws visible stack children (PASS 2).
        /// Returns drawn count; when <paramref name="shouldExitEarly"/> is true, caller must return that value immediately.
        /// </summary>
        protected virtual int DrawStackVisibleChildren(
            DrawingContext ctx,
            LayoutStructure structure,
            List<ControlInStack> visibleElements,
            bool usesExpandedViewport,
            ScaledRect visibilityAreaReal,
            List<SkiaControlWithRect> tree,
            ref bool updateInternal)
        {
            var drawn = 0;
            bool hadAdjustments = false;
            bool wasVisible = false;
            var index = -1;
            var cellsToRelease = new List<SkiaControl>();
            int countRendered = 0;
            Vector2 offsetOthers = Vector2.Zero;

            var preparedMode = UsePreparedViewsActive;
            var bakePass = IsPlaneBakePass; // pure-read pass: no live-state mutation (see the flag's doc)
            if (preparedMode && !bakePass)
            {
                _prepVisibleUnprepared.Clear();
            }

            // Snapshot which ControlIndexes are visible this frame so ApplyStackMeasureResult can refuse to
            // publish a structure that leaves any of THESE slots unmeasured (on-screen blank). Off-screen
            // unmeasured cells stay allowed (normal MeasureVisible), so background measurement never stalls
            // the scroll. Real draw only — a bake pass is a pure read and must not redefine "visible".
            if (!bakePass)
            {
                _lastVisibleControlIndexes.Clear();
                foreach (var vc in CollectionsMarshal.AsSpan(visibleElements))
                    if (vc != null && !vc.IsCollapsed)
                        _lastVisibleControlIndexes.Add(vc.ControlIndex);
            }

            try
            {
                if (!bakePass && WillDrawFromFreshItemssSource == 0 && IsTemplated
                    //&& RecyclingTemplate != RecyclingTemplate.Disabled
                   )
                {
                    // First paint of a fresh ItemsSource just happened — the visible measured cells are now on
                    // screen. ONLY NOW warm the rest of the pool, up to the prefill target (e.g.
                    // ItemTemplatePoolSize for RecyclingTemplate.Disabled), on a BACKGROUND thread so later
                    // scrolls into cold cells don't create them on the render thread. Deferred + off-thread so
                    // it never competes with the visible-first paint.
                    var warmTarget = Math.Max(visibleElements.Count + ReserveTemplates, GetTemplatesPoolPrefill());
                    if (warmTarget > 0)
                    {
                        Tasks.StartDelayed(TimeSpan.FromMilliseconds(50),
                            () =>
                            {
                                ChildrenFactory.FillPoolInBackgroundAsync(warmTarget);
                                OnPropertyChanged(nameof(DebugString));
                            });
                    }
                }


                foreach (var cell in CollectionsMarshal.AsSpan(visibleElements))
                {
                    // Update measured items access time for visible items
                    if (_measuredItems.TryGetValue(cell.ControlIndex, out var info))
                    {
                        info.LastAccessed = DateTime.UtcNow;
                        info.IsInViewport = true;
                    }

                    if (cell.IsCollapsed)
                        continue;

                    if (!cell.WasMeasured)
                    {
                        continue; // Skip unmeasured
                    }

                    index++;

                    SkiaControl child = null;
                    if (IsTemplated)
                    {
                        child = ChildrenFactory.GetViewForIndex(cell.ControlIndex, null,
                            GetSizeKey(cell.Measured.Pixels));
                        if (child == null)
                        {
                            // A view can be unavailable transiently (adapter contexts mid-swap during a
                            // window trim/rebase, pool not yet realized after a grow/rekey). Returning here
                            // aborted the WHOLE remaining paint — one null view on the first cell produced a
                            // fully EMPTY frame (caught by the scroll+trim band repro). Skip just this cell:
                            // its slot heals next frame, every other cell still paints.
                            continue;
                        }

                        cellsToRelease.Add(child);
                    }
                    else
                    {
                        child = cell.View;
                    }

                    if (child is SkiaControl control)
                    {
                        SKRect destinationRect;
                        var x = offsetOthers.X + cell.Drawn.Left;
                        var y = offsetOthers.Y + cell.Drawn.Top;

                        //SkiaLayout.TraceIdx(cell.ControlIndex, "DRAW-in",
                        //    $"ctx={child.ContextIndex} slotMeasuredH={cell.Measured.Pixels.Height:0} drawnTop={cell.Drawn.Top:0} drawnH={cell.Drawn.Height:0} destTop={cell.Destination.Top:0} destH={cell.Destination.Height:0} viewMeasuredH={child.MeasuredSize.Pixels.Height:0} need={child.NeedMeasure} sizeKeyReq={GetSizeKey(cell.Measured.Pixels)} sizeKeyView={GetSizeKey(child.MeasuredSize.Pixels)} x={x:0} y={y:0} offOthersY={offsetOthers.Y:0}");

                        // Drawing a plane/tile window: every cell is a recycled view just rebound to this
                        // index's content, and GetSizeKey is forced to 0 here (single pool bucket), so the
                        // size-key comparison below can't detect that the rebound content differs in height.
                        // Force a measure so the painted cell matches its real height (and the reserved slot
                        // from BuildPlaneWindowStructure) — otherwise a tall row painted by a previously-short
                        // recycled cell paints short and leaves a gap (and vice-versa overlaps).
                        //bool forcePlaneMeasure = PlaneOverrideStructure != null;

                        // PREPARED VIEWS: the render thread NEVER measures a cell (a single Debug cell
                        // measure exceeds a frame). An unprepared cell draws its placeholder skeleton at
                        // the structure's reserved slot and gets top priority in the preparation want-list;
                        // CellPreparationService measures it off-thread and Repaints when ready.
                        // IsPreparingOffthread also shields against reading half-measured state while the
                        // worker is measuring the live instance.
                        //
                        // STALE-SERVE exception: a cell that ALREADY OWNS PIXELS (front or previous cache)
                        // and merely self-invalidated (delivery tick, image loaded, streaming text) must
                        // NOT flash a skeleton — draw its existing cache at the reserved slot via the
                        // normal child pipeline (measure-free: DrawChild only schedules invalidation) while
                        // the worker re-measures; the reconcile branch adopts the new size afterwards.
                        // CONTEXT GUARD: the pixels must belong to THIS index's data item. A recycled view
                        // rebound to a new context (PixelsForeign) or caught mid-eviction-rebind still
                        // carries the PREVIOUS context's RenderObject — serving it would paint the WRONG
                        // message at this slot. Those cells go to the gap-rescue measure below instead.
                        var stalePixels = preparedMode
                                          && (child.NeedMeasure || child.IsPreparingOffthread)
                                          && (child.RenderObject != null || child.RenderObjectPrevious != null)
                                          && !child.PixelsForeign
                                          && !cell.Measured.Pixels.IsEmpty
                                          && ReferenceEquals(child.BindingContext, GetItemForMemo(cell.ControlIndex));

                        // Skeleton is the LAST resort, not the default for unprepared cells: only when
                        // the render thread genuinely cannot produce content this frame — the prep worker
                        // holds the live instance (concurrent measure = corruption), this is the pure-read
                        // bake pass (a bake must never bind/measure; its gates reject unprepared cells
                        // anyway), or the atomic measure claim was lost to the worker mid-frame.
                        // An idle unprepared cell falls through to the GAP-RESCUE inline measure below and
                        // draws REAL content — same cost the plain direct-draw path pays for every appearing
                        // cell, paid here only when the worker lost the race (fast fling, jump landing).
                        var needsRescue = preparedMode && !stalePixels && !bakePass
                                          && child.NeedMeasure && !child.IsPreparingOffthread;
                        var rescueClaimed = needsRescue &&
                                            Interlocked.CompareExchange(ref child.MeasureClaim, 1, 0) == 0;

                        if (preparedMode && !stalePixels &&
                            (child.IsPreparingOffthread || (bakePass && child.NeedMeasure) ||
                             (needsRescue && !rescueClaimed)))
                        {
                            var slot = cell.Measured.Pixels;
                            if (child.IsVisible && slot.Width >= 1 && slot.Height >= 1)
                            {
                                var placeholderRect = new SKRect(x, y, x + slot.Width, y + slot.Height);
                                child.DrawPlaceholder(ctx.WithDestination(placeholderRect));

                                if (CollectPaintedBounds)
                                {
                                    TrackPaintedBounds(placeholderRect.Top, placeholderRect.Bottom);
                                    PaintedSkeleton = true;
                                }

                                // Enter the render tree at the reserved slot: the skeleton occupies real layout
                                // space, so gestures land on the (already bound) cell and integrity watchers see
                                // a contiguous sequence instead of a fake hole. CreateHitRect() would return a
                                // stale DrawingRect here (never arranged) — use the slot rect for both.
                                tree.Add(new SkiaControlWithRect(child,
                                    placeholderRect,
                                    placeholderRect,
                                    index,
                                    child.ContextIndex,
                                    child.BindingContext));
                            }

                            if (!bakePass)
                            {
                                _prepVisibleUnprepared.Add(cell.ControlIndex);
                            }

                            cell.WasLastDrawn = false;
                            continue;
                        }

                        if (child.NeedMeasure)
                        {
                            if (stalePixels)
                            {
                                // STALE-SERVE (prepared mode): arrange the cell to its reserved slot and let
                                // the normal draw below blit its existing cache — at most one update stale,
                                // never a skeleton flash, never a render-thread measure. Prioritize the
                                // off-thread re-measure; the prepared-reconcile branch adopts the fresh size
                                // on a following frame.
                                if (child.IsVisible)
                                {
                                    LayoutCell(cell.Measured, cell, child, cell.Area, ctx.Scale);
                                }

                                if (bakePass)
                                {
                                    // TOCTOU: this cell was ready at the kick gate and self-invalidated during
                                    // the worker paint -> we just froze its OLD-size pixels. Flag so the plane
                                    // owner rejects the publish (live draw is always current). See PaintedStaleServe.
                                    if (CollectPaintedBounds)
                                        PaintedStaleServe = true;
                                }
                                else
                                {
                                    _prepVisibleUnprepared.Add(cell.ControlIndex);
                                }
                            }
                            else if (!IsTemplated
                                || MeasureItemsStrategy == MeasuringStrategy.MeasureVisible
                                || (MeasureItemsStrategy == MeasuringStrategy.MeasureFirst && !child.WasMeasured)
                                || GetSizeKey(child.MeasuredSize.Pixels) != GetSizeKey(cell.Measured.Pixels)
                                || InvalidatedChildrenInternal.Contains(child)
                               )
                            {
                                // DETECTOR(B): the slot the structure RESERVED for this index, captured before
                                // cell.Measured is overwritten. The OffsetOthers delta below is computed from
                                // oldSize (the recycled view's PREVIOUS item size), not from this reserved slot
                                // -> followers in the same batch can be shifted by the wrong magnitude.
                                var reservedSlot = cell.Measured.Pixels;

                                if (IsTemplated && !bakePass)
                                {
                                    // prepared mode reaches here only via GAP-RESCUE (worker lost the race):
                                    // tracked separately — the prepared pipeline itself stays measure-free.
                                    if (preparedMode)
                                        CountGapRescueMeasures++;
                                    else
                                        CountRenderThreadCellMeasures++;
                                }

                                ScaledSize measured;
                                try
                                {
                                    measured = child.Measure((float)cell.Area.Width, (float)cell.Area.Height,
                                        ctx.Scale);
                                }
                                finally
                                {
                                    if (rescueClaimed)
                                        Interlocked.Exchange(ref child.MeasureClaim, 0);
                                }

                                cell.Measured = measured;
                                cell.WasMeasured = true;

                                if (child.IsVisible)
                                {
                                    LayoutCell(measured, cell, child, cell.Area, ctx.Scale);
                                }

                                if (reservedSlot != SKSize.Empty &&
                                    !CompareSize(reservedSlot, child.MeasuredSize.Pixels, 1f))
                                {
                                    // Durably shift the FOLLOWING cells' structure positions by this grow delta so
                                    // the new layout persists (PASS 1 derives cell.Drawn from cell.Destination every
                                    // frame). Without it the draw-time grow only patched the current frame via the
                                    // OffsetOthers accumulator below, and the next cell overlapped until a staged
                                    // restack landed a frame later — the visible "wrong Top" flicker while a bubble
                                    // grows in realtime (streaming AI answer). Same call the smart-measure path uses.
                                    ShiftFollowersByGrow(structure, cell, child.MeasuredSize.Pixels, reservedSlot, durable: true);
                                }
                            }
                        }

                        // PREPARED VIEWS RECONCILE (arithmetic only, no measure): a cell re-measured OFF-thread
                        // (self-invalidated growing bubble, image loaded) arrives here with a fresh MeasuredSize
                        // that differs from the structure's reserved slot. The view is per-context (Disabled
                        // recycling) so ITS size is the truth — adopt it and durably shift the followers, exactly
                        // what the sync-measure branch above does after measuring, minus the measure.
                        else if (preparedMode && !cell.Measured.IsEmpty
                                 && child.MeasuredSize.Pixels.Width >= 1 && child.MeasuredSize.Pixels.Height >= 1
                                 && !CompareSize(cell.Measured.Pixels, child.MeasuredSize.Pixels, 1f))
                        {
                            var reservedSlot = cell.Measured.Pixels;
                            var adopted = child.MeasuredSize;

                            cell.Measured = adopted;
                            cell.WasMeasured = true;

                            if (child.IsVisible)
                            {
                                LayoutCell(adopted, cell, child, cell.Area, ctx.Scale);
                            }

                            ShiftFollowersByGrow(structure, cell, adopted.Pixels, reservedSlot, durable: true);
                        }

                        // RECYCLED STALE SIZE (light, no remeasure): a pooled view can still carry its PREVIOUS
                        // index's height when its cache is valid (NeedMeasure == false) — it would paint at the stale
                        // height and overrun the next cell (the transient overlap during fast recycling / a send
                        // rebind). Instead of a costly draw-time REMEASURE (which janks the scroll), just ARRANGE it
                        // to the KNOWN reserved slot (cell.Measured) so it draws at the correct height NOW; the
                        // ImageDoubleBuffered content repaints to the new size in the background a frame later. Arrange
                        // only = cheap. Coarse size-key gate so only real (cross-bucket) mismatches trigger — normal
                        // same-height recycling never enters here, keeping the scroll smooth and allocation-free.
                        else if (IsTemplated && !cell.Measured.IsEmpty
                                 && GetSizeKey(control.MeasuredSize.Pixels) != GetSizeKey(cell.Measured.Pixels))
                        {
                            LayoutCell(cell.Measured, cell, control, cell.Area, ctx.Scale);
                        }

                        if (child.IsVisible)
                        {
                            bool willDraw = true;

                            if (child.MeasuredSize.Pixels.Width >= 1 && child.MeasuredSize.Pixels.Height >= 1)
                            {
                                destinationRect = GetStackChildDrawRect(index, x, y, cell);

                                //SkiaLayout.TraceIdx(cell.ControlIndex, "DRAW-paint",
                                //    $"ctx={child.ContextIndex} paintTop={destinationRect.Top:0} paintBottom={destinationRect.Bottom:0} paintH={destinationRect.Height:0} viewMeasuredH={child.MeasuredSize.Pixels.Height:0} cellDrawnTop={cell.Drawn.Top:0} cellDrawnH={cell.Drawn.Height:0}");

                                // A bake pass must paint EVERY visible cell (a composition pass paints only
                                // dirty ones — a plane recorded from it would have holes).
                                if (IsRenderingWithComposition && !bakePass)
                                {
                                    if (child.PostAnimators.Count > 0)
                                    {
                                        updateInternal = true;
                                    }

                                    if (DirtyChildrenInternal.Contains(child) || child.PostAnimators.Count > 0)
                                    {
                                        if (usesExpandedViewport)
                                        {
                                            if (!cell.Drawn.IntersectsWith(visibilityAreaReal.Pixels))
                                            {
                                                willDraw = false;
                                            }
                                        }

                                        if (willDraw)
                                        {
                                            DrawChild(ctx.WithDestination(destinationRect), child);
                                            countRendered++;
                                        }
                                    }
                                    else
                                    {
                                        // Use ArrangeCache to update cache's LastDestination for gesture coordinate translation
                                        child.ArrangeCache(destinationRect, child.SizeRequest.Width,
                                            child.SizeRequest.Height,
                                            ctx.Scale);

                                        willDraw = true; //simulate to be entered in rendering tree, for gestures etc
                                    }
                                }
                                else
                                {
                                    if (usesExpandedViewport)
                                    {
                                        if (!cell.Drawn.IntersectsWith(visibilityAreaReal.Pixels))
                                        {
                                            willDraw = false;
                                        }
                                    }

                                    if (willDraw)
                                    {
                                        DrawChild(ctx.WithDestination(destinationRect), child);
                                        countRendered++;
                                    }
                                }

                                if (willDraw && CollectPaintedBounds)
                                    TrackPaintedBounds(destinationRect.Top, destinationRect.Bottom);

                                cell.WasLastDrawn = willDraw;

                                if (willDraw)
                                {
                                    drawn++;

                                    tree.Add(new SkiaControlWithRect(control,
                                        destinationRect,
                                        control.CreateHitRect(),
                                        index,
                                        control.ContextIndex, // freeze index
                                        control.BindingContext)); // freeze binding context
                                }
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"INVISIBLE {child.ContextIndex}");
                        }

                        // STREAMING-GROWTH BRIDGE: a stale-served self-invalidated cell measures ITSELF inside
                        // DrawChild above (standard NeedMeasure self-measure) — the prep worker can never own
                        // that re-measure (the self-measure races it for NeedMeasure and wins), and the
                        // reconcile branch needs a live frame arriving with NeedMeasure ALREADY false, which
                        // streaming never produces (every live frame IS an invalidation frame). Consume the
                        // fresh size here by staging the EXISTING SingleItemUpdate change (same one
                        // Remeasure/MeasureSingleItem stage): drained at the next DrawStack start it resizes
                        // the slot and shifts followers BEFORE pass 1. No measure — the size already exists.
                        if (stalePixels && !bakePass
                            && child.MeasuredSize.Pixels.Height >= 1
                            && !CompareSize(cell.Measured.Pixels, child.MeasuredSize.Pixels, 1f))
                        {
                            StageSelfMeasuredCellUpdate(cell.ControlIndex, child.MeasuredSize,
                                cell.Measured.Pixels);

                            // Shift the followers THIS frame too via the accumulator (same pattern as the
                            // sync-measure branch), otherwise the grown cell paints over the next cell for the
                            // one frame until the staged change lands — the visible flicker. NON-durable: the
                            // StageSelfMeasuredCellUpdate above persists the structure shift next frame, so
                            // OffsetSubsequentCells here would double-shift. PASS 1 clears OffsetOthers next
                            // frame and the applied structure change takes over, so this never double-shifts.
                            ShiftFollowersByGrow(structure, cell, child.MeasuredSize.Pixels, cell.Measured.Pixels, durable: false);
                        }
                    }

                    // Shift the FOLLOWING cells THIS frame by this cell's draw-time remeasure delta. cell.Drawn
                    // was computed in PASS 1 from the pre-grow structure, and PASS 1 does NOT apply OffsetOthers
                    // to positions (it accumulates a dead local and resets cell.OffsetOthers). So without this, a
                    // cell that remeasures TALLER while drawing (e.g. a streaming AI bubble growing word by word)
                    // overlaps the next cell for one frame — until the structure restack lands next frame: the
                    // visible "wrong Top" flicker. OffsetOthers is set only on the remeasure frame and cleared by
                    // PASS 1 next frame, and the restack fixes positions from then on, so this never double-shifts.
                    offsetOthers += cell.OffsetOthers;
                }

                OnAfterDrawingVisibleChildren(ctx, structure, visibleElements);
            }
            finally
            {
                if (IsTemplated)
                    foreach (var cell in cellsToRelease)
                    {
                        ChildrenFactory.ReleaseViewInUse(cell);
                    }
            }

            return drawn;
        }
    }

    #endregion
}
