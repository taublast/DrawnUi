using System.Runtime.CompilerServices;

namespace DrawnUi.Draw
{
    public partial class SkiaLayout
    {
        #region StackLayout

        /// <summary>
        /// Used for StackLayout (Stack, Row) kind of layout
        /// </summary>
        public LayoutStructure StackStructure { get; set; }

        /// <summary>
        /// When measuring we set this, and it will be swapped with StackStructure upon drawing so we don't affect the drawing if measuring in background.
        /// </summary>
        public LayoutStructure StackStructureMeasured { get; set; }

        public LayoutStructure LatestStackStructure
        {
            get
            {
                if (StackStructure != null)
                    return StackStructure;

                return StackStructureMeasured;
            }
        }

        public LayoutStructure LatestMeasuredStackStructure
        {
            get
            {
                if (StackStructureMeasured != null)
                    return StackStructureMeasured;

                return StackStructure;
            }
        }

        protected ScaledSize MeasureAndArrangeCell(SKRect destination,
            ControlInStack cell, SkiaControl child,
            SKRect rectForChildrenPixels, float scale)
        {
            cell.Area = destination;

            ScaledSize measured = child.MeasuredSize;
            //if (child.NeedMeasure) //todo
            {
                measured = MeasureChild(child, cell.Area.Width, cell.Area.Height, scale);
            }

            cell.Measured = measured;
            cell.WasMeasured = true;

            LayoutCell(measured, cell, child, rectForChildrenPixels, scale);

            return measured;
        }

        public virtual void LayoutCell(
            ScaledSize measured,
            ControlInStack cell,
            SkiaControl child,
            SKRect rectForChildrenPixels,
            float scale)
        {
            cell.Layout = rectForChildrenPixels;

            if (!measured.IsEmpty)
            {
                var area = cell.Area;
                if (child != null)
                {
                    if (Type == LayoutType.Column && child.VerticalOptions != LayoutOptions.Start && !child.NeedFillY)
                    {
                        area = new(area.Left,
                            rectForChildrenPixels.Top,
                            area.Right,
                            rectForChildrenPixels.Bottom);
                    }
                    else if (Type == LayoutType.Row && child.HorizontalOptions != LayoutOptions.Start && !child.NeedFillX)
                    {
                        area = new(rectForChildrenPixels.Left,
                            area.Top,
                            rectForChildrenPixels.Right,
                            area.Bottom);
                    }
                }

                child.Arrange(area, measured.Units.Width, measured.Units.Height, scale);

                var maybeArranged = child.Destination;

                var arranged =
                    new SKRect(cell.Area.Left, cell.Area.Top,
                        cell.Area.Left + cell.Measured.Pixels.Width,
                        cell.Area.Top + cell.Measured.Pixels.Height);

                if (float.IsNormal(maybeArranged.Height))
                {
                    arranged.Top = maybeArranged.Top;
                    arranged.Bottom = maybeArranged.Bottom;
                }

                if (float.IsNormal(maybeArranged.Width))
                {
                    arranged.Left = maybeArranged.Left;
                    arranged.Right = maybeArranged.Right;
                }

                cell.Destination = arranged;
            }
        }

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
                        stackWidth = rectForChildrenPixels.Width;
                    }

                    if (VerticalOptions.Alignment == LayoutAlignment.Fill || SizeRequest.Height >= 0)
                    {
                        stackHeight = rectForChildrenPixels.Height;
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
                            ChildrenFactory.ReleaseViewInUse(cell.ContextIndex, cell);
                        }
                }


                if (HorizontalOptions.Alignment == LayoutAlignment.Fill && WidthRequest < 0)
                {
                    stackWidth = rectForChildrenPixels.Width;
                }

                if (VerticalOptions.Alignment == LayoutAlignment.Fill && HeightRequest < 0)
                {
                    stackHeight = rectForChildrenPixels.Height;
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

            return MeasureStackCore(rectForChildrenPixels, scale, layoutStructure, false, null, nonTemplated);
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
                    if (UsingCacheType == SkiaCacheType.ImageComposite)
                    {
                        TrackChildAsDirty(child);
                    }

                    if (!measured.IsEmpty)
                    {
                        // Inline UpdateRowDimensions with pre-calculated spacing
                        maxWidth += measured.Pixels.Width + (column > 0 ? spacingScaled : 0);
                        if (measured.Pixels.Height > maxHeight)
                            maxHeight = measured.Pixels.Height;

                        rectForChild.Left += measured.Pixels.Width;
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
                stackWidth = rectForChildrenPixels.Width;
            if (VerticalOptions.Alignment == LayoutAlignment.Fill || SizeRequest.Height >= 0)
                stackHeight = rectForChildrenPixels.Height;

            return ScaledSize.FromPixels(stackWidth, stackHeight, scale);
        }

        public void TrackChildAsDirty(SkiaControl child)
        {
            DirtyChildrenTracker.Add(child);
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
                    return MeasureStackCore(rectForChildrenPixels, scale, layoutStructure, true, template, null);
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
                        if (UsingCacheType == SkiaCacheType.ImageComposite)
                        {
                            TrackChildAsDirty(child);
                        }

                        if (!measured.IsEmpty)
                        {
                            // Inline UpdateRowDimensions with pre-calculated spacing
                            maxWidth += measured.Pixels.Width + (column > 0 ? spacingScaled : 0);
                            if (measured.Pixels.Height > maxHeight)
                                maxHeight = measured.Pixels.Height;

                            rectForChild.Left += measured.Pixels.Width;
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
                    stackWidth = rectForChildrenPixels.Width;
                if (VerticalOptions.Alignment == LayoutAlignment.Fill || SizeRequest.Height >= 0)
                    stackHeight = rectForChildrenPixels.Height;

                return ScaledSize.FromPixels(stackWidth, stackHeight, scale);
            }
            finally
            {
                // Release cells if needed
                if (cellsToRelease?.Count > 0)
                {
                    foreach (var cell in cellsToRelease)
                    {
                        ChildrenFactory.ReleaseViewInUse(cell.ContextIndex, cell);
                    }
                }
            }
        }

 
        /// <summary>
        /// Core measurement logic shared between templated and non-templated scenarios
        /// </summary>
        private ScaledSize MeasureStackCore(SKRect rectForChildrenPixels, float scale, LayoutStructure layoutStructure,
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
                            rectForChild.Top += GetSpacingForIndex(row, scale);
                        rectForChild.Left += GetSpacingForIndex(column, scale);

                        var rectFitChild = CreateChildMeasureRect(rectForChild, widthPerColumn, cell,
                            hasFillHandling, spacePerFillChild, nonTemplated);

                        var measured = MeasureChildCell(rectFitChild, cell, child, rectForChildrenPixels, scale,
                            isTemplated, needMeasureAll, ref firstCell);

                        // Track child as dirty for ImageComposite cache support
                        if (UsingCacheType == SkiaCacheType.ImageComposite)
                        {
                            TrackChildAsDirty(child);
                        }

                        if (!measured.IsEmpty)
                        {
                            // Inline UpdateRowDimensions
                            if (isTemplated || !child.NeedFillX || !isColumn)
                            {
                                maxWidth += measured.Pixels.Width + GetSpacingForIndex(column, scale);
                            }

                            if (isTemplated || !child.NeedFillY || isColumn)
                            {
                                if (measured.Pixels.Height > maxHeight)
                                    maxHeight = measured.Pixels.Height;
                            }

                            rectForChild.Left += measured.Pixels.Width;
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
                    stackHeight += maxHeight + GetSpacingForIndex(row, scale);

                    rectForChild.Top += maxHeight;
                    rectForChild.Left = 0;
                }

                // Inline ApplyFillConstraints
                if (HorizontalOptions.Alignment == LayoutAlignment.Fill || SizeRequest.Width >= 0)
                    stackWidth = rectForChildrenPixels.Width;
                if (VerticalOptions.Alignment == LayoutAlignment.Fill || SizeRequest.Height >= 0)
                    stackHeight = rectForChildrenPixels.Height;

                // Only process second pass if needed
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
                        ChildrenFactory.ReleaseViewInUse(cell.ContextIndex, cell);
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
            bool hasFillHandling, float spacePerFillChild, SkiaControl[] nonTemplated)
        {
            var rectFitChild = new SKRect(rectForChild.Left, rectForChild.Top,
                rectForChild.Left + widthPerColumn, rectForChild.Bottom);

            if (hasFillHandling)
            {
                var child = nonTemplated[cell.ControlIndex];
                var isFillChild = IsChildFill(child);

                if (isFillChild)
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
                return (float)((child.HeightRequest + child.Margins.VerticalThickness) * scale);
            }
            else if (Type == LayoutType.Row && child.WidthRequest >= 0)
            {
                return (float)((child.WidthRequest + child.Margins.HorizontalThickness) * scale);
            }
            else
            {
                var tempRect = Type == LayoutType.Column
                    ? new SKRect(0, 0, rectForChildrenPixels.Width, float.PositiveInfinity)
                    : new SKRect(0, 0, float.PositiveInfinity, rectForChildrenPixels.Height);

                var tempMeasured = MeasureChild(child, tempRect.Width, tempRect.Height, scale);
                return Type == LayoutType.Column ? tempMeasured.Pixels.Height : tempMeasured.Pixels.Width;
            }
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
                stackWidth = rectForChildrenPixels.Width;
            }

            if (VerticalOptions.Alignment == LayoutAlignment.Fill || SizeRequest.Height >= 0)
            {
                stackHeight = rectForChildrenPixels.Height;
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
                LayoutCell(secondPass.Child.MeasuredSize, secondPass.Cell, secondPass.Child, autoRect,
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
                || UsingCacheType == SkiaCacheType.ImageComposite)
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


            var layoutStructure = LatestMeasuredStackStructure;
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
                    DirtyChildrenTracker.Clear();

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
            var visibleArea = GetOnScreenVisibleArea(ctx, new(inflate, inflate));

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
                //var inflate = (float)(this.VirtualisationInflated * ctx.Scale);

                // For managed virtualization, bypass cache to ensure each plane gets its own visibility area
                ScaledRect visibilityArea;
                if (Virtualisation == VirtualisationType.Managed)
                {
                    var inflate = (float)(this.VirtualisationInflated * ctx.Scale);
                    visibilityArea = GetOnScreenVisibleArea(ctx, new(inflate, inflate));
                }
                else
                {
                    visibilityArea = GetVisibleAreaCached(ctx);
                }

                // for plane virtualization
                if (!string.IsNullOrEmpty(planeId))
                    //{
                    //    Debug.WriteLine($"[{planeId}] DrawStack visibility area: {visibilityArea.Pixels}");
                    //}

                    // EXPAND DRAWING VIEWPORT during initial drawing to pre-create cells and avoid lagspike at scrolling start
                    if (Virtualisation != VirtualisationType.Managed && IsTemplated &&
                        _isInitialDrawingFromFreshSource && _initialDrawFrameCount < 2)
                    {
                        if (Type == LayoutType.Column)
                        {
                            var expand = visibilityArea.Pixels.Height / 4f;
                            var expanded = visibilityArea.Pixels;
                            expanded.Inflate(0, expand);
                            visibilityArea = ScaledRect.FromPixels(expanded, visibilityArea.Scale);
                        }
                        else if (Type == LayoutType.Row)
                        {
                        }
                    }

                var recyclingAreaPixels = visibilityArea.Pixels;
                var expendRecycle = ((float)RecyclingBuffer * ctx.Scale);
                recyclingAreaPixels.Inflate(expendRecycle, expendRecycle);

                //PASS 1 - VISIBILITY
                Vector2 offsetOthers = Vector2.Zero;
                var currentIndex = -1;
                foreach (var cell in structure.GetChildrenAsSpans())
                {
                    if (!cell.WasMeasured)
                    {
                        Super.Log(
                            "DrawStack tried to draw unmeasured cell!"); //would be unexpected due to flaw in custom control
                        continue; //structure changed, must be measured by Measure method
                    }

                    currentIndex++;

                    if (cell.Destination == SKRect.Empty || cell.Measured.Pixels.Width < 1 ||
                        cell.Measured.Pixels.Height < 1)
                    {
                        cell.IsVisible = false;
                    }
                    else
                    {
                        // Calculate screen position (unchanged)
                        var x = ctx.Destination.Left + cell.Destination.Left;
                        var y = ctx.Destination.Top + cell.Destination.Top;

                        cell.Drawn.Set(x, y, x + cell.Destination.Width, y + cell.Destination.Height);

                        offsetOthers += cell.OffsetOthers;

                        if (Virtualisation != VirtualisationType.Disabled)
                        {
                            if (needrebuild && UsingCacheType == SkiaCacheType.None &&
                                Virtualisation == VirtualisationType.Smart
                                && !(IsTemplated && RecyclingTemplate == RecyclingTemplate.Enabled))
                            {
                                cell.IsVisible = true;
                            }
                            else
                            {
                                // SOLUTION PART 1: Use normal area for visibility
                                cell.IsVisible = cell.Drawn.IntersectsWith(visibilityArea.Pixels);

                                // for plane virtualization
                                //if (!string.IsNullOrEmpty(planeId) && cell.ControlIndex < 3)
                                //{
                                //    Debug.WriteLine($"[{planeId}] Cell {cell.ControlIndex}: drawn={cell.Drawn}, visible={cell.IsVisible}");
                                //}
                            }
                        }
                        else
                        {
                            cell.IsVisible = true;
                        }
                    }

                    cell.OffsetOthers = Vector2.Zero;
                    cell.WasLastDrawn = false;

                    if (!cell.IsVisible) //fix case of collapsing groups
                    {
                        ChildrenFactory.MarkViewAsHidden(cell.ControlIndex);
                    }
                    else
                    if (Virtualisation != VirtualisationType.Disabled &&
                             cell.Destination != SKRect.Empty &&
                             !cell.Measured.Pixels.IsEmpty)
                    {
                        if (!cell.Drawn.IntersectsWith(recyclingAreaPixels))
                        {
                            ChildrenFactory.MarkViewAsHidden(cell.ControlIndex);
                        }
                    }

                    // Add to visible elements for drawing
                    if (cell.IsVisible)
                    {
                        visibleElements.Add(cell);
                    }
                }

                if (OutputDebug)
                {
                    Super.Log(
                        $"[SkiaLayout] visibility area {visibilityArea}, recycling area {recyclingAreaPixels}, visible items: {visibleElements.Count}");
                }

                OnBeforeDrawingVisibleChildren(ctx, structure, visibleElements);

                if (visibleElements.Count > 1)
                {
                    visibleElements.Sort((a, b) => a.ZIndex.CompareTo(b.ZIndex));
                }

                //PASS 2 DRAW VISIBLE
                bool hadAdjustments = false;
                bool wasVisible = false;
                var index = -1;
                var cellsToRelease = new List<SkiaControl>();
                int countRendered = 0;
                offsetOthers = Vector2.Zero;

                try
                {
                    foreach (var cell in CollectionsMarshal.AsSpan(visibleElements))
                    {
                        if (!cell.WasMeasured)
                            continue;

                        index++;

                        SkiaControl child = null;
                        if (IsTemplated)
                        {
                            child = ChildrenFactory.GetViewForIndex(cell.ControlIndex, null,
                                GetSizeKey(cell.Measured.Pixels));
                            if (child == null)
                            {
                                return countRendered;
                            }

                            //if (child.NeedMeasure)
                            //{
                            //    Trace.WriteLine($"NeedMeasure {child.Uid}");
                            //}
                            //Trace.WriteLine($"[CELL] DRAW {index} {child.Uid}");

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

                            if (child.NeedMeasure)
                            {
                                if (!IsTemplated ||
                                    !child.WasMeasured || InvalidatedChildrenInternal.Contains(child) ||
                                    GetSizeKey(child.MeasuredSize.Pixels) != GetSizeKey(cell.Measured.Pixels))
                                {
                                    var oldSize = child.MeasuredSize.Pixels;
                                    var measured = child.Measure((float)cell.Area.Width, (float)cell.Area.Height,
                                        ctx.Scale);

                                    cell.Measured = measured;
                                    cell.WasMeasured = true;

                                    if (child.IsVisible)
                                    {
                                        LayoutCell(measured, cell, child, cell.Area, ctx.Scale);
                                    }
           

                                    if (oldSize != SKSize.Empty && !CompareSize(oldSize, MeasuredSize.Pixels, 1f))
                                    {
                                        //Trace.WriteLine($"[CELL] remeasured {child.Uid}");
                                        var diff = child.MeasuredSize.Pixels - oldSize;
                                        cell.OffsetOthers = new Vector2(diff.Width, diff.Height);
                                    }
                                }
                            }

                            if (child.IsVisible)
                            {
                                if (child.MeasuredSize.Pixels.Width >= 1 && child.MeasuredSize.Pixels.Height >= 1)
                                {
                                    if (IsTemplated)
                                    {
                                        destinationRect = new SKRect(x, y,
                                            x + cell.Area.Width, y + cell.Area.Bottom);
                                    }
                                    else
                                    {
                                        destinationRect = new SKRect(x, y, x + cell.Drawn.Width,
                                            y + cell.Drawn.Height);
                                    }

                                    if (IsRenderingWithComposition)
                                    {
                                        if (child.PostAnimators.Count > 0)
                                        {
                                            updateInternal = true;
                                        }

                                        if (DirtyChildrenInternal.Contains(child) || child.PostAnimators.Count > 0)
                                        {
                                            DrawChild(ctx.WithDestination(destinationRect), child);
                                            countRendered++;
                                        }
                                        else
                                        {
                                            child.Arrange(destinationRect, child.SizeRequest.Width,
                                                child.SizeRequest.Height,
                                                ctx.Scale);
                                        }
                                    }
                                    else
                                    {
                                        DrawChild(ctx.WithDestination(destinationRect), child);
                                        countRendered++;
                                    }

                                    cell.WasLastDrawn = true;

                                    drawn++;

                                    tree.Add(new SkiaControlWithRect(control,
                                        destinationRect,
                                        control.DrawingRect, 
                                        index,
                                        -1, // Default freeze index
                                        control.BindingContext)); // Capture current binding context
                                }
                            }
                            else
                            {
                                Debug.WriteLine($"INVISIBLE {child.ContextIndex}");
                            }
                        }
                    }
                }
                finally
                {
                    if (IsTemplated)
                        foreach (var cell in cellsToRelease)
                        {
                            ChildrenFactory.ReleaseViewInUse(cell.ContextIndex, cell);
                        }
                }
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

            // Turn off initial drawing mode after a few frames to prevent continuous expanded viewport
            if (_isInitialDrawingFromFreshSource)
            {
                _initialDrawFrameCount++;
                if (_initialDrawFrameCount >= 2)
                {
                    _isInitialDrawingFromFreshSource = false;
                    //Super.Log($"[SkiaLayout] Initial drawing complete, created cells for {drawn} items, returning to normal viewport");
                }
            }

            // for plane virtualization
            //if (!string.IsNullOrEmpty(planeId))
            //{
            //    Debug.WriteLine($"[{planeId}] DrawStack result: {drawn} children drawn, {structure.GetCount()} total cells");
            //}

            return drawn;
        }
    }

    #endregion
}
