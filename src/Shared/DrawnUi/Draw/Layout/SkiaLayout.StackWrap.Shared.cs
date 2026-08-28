namespace DrawnUi.Draw;

public partial class SkiaLayout
{
    public virtual LayoutStructure GetStackStructure()
    {
        var ret = StackStructure ?? StackStructureMeasured;
        if (ret == null)
        {
            return new LayoutStructure();
        }
        return ret;
    }

    public virtual LayoutStructure GetStackStructureForMeasuring()
    {
        var ret = StackStructureMeasured ?? StackStructure;
        if (ret == null)
        {
            return new LayoutStructure();
        }
        return ret;
    }

    /// <summary>
    /// Used for stack-like layouts such as Column, Row, and Wrap.
    /// </summary>
    protected LayoutStructure StackStructure { get; set; }

    /// <summary>
    /// Set during measure and swapped into StackStructure when measure result is applied.
    /// </summary>
    protected LayoutStructure StackStructureMeasured { get; set; }

    //public LayoutStructure LatestStackStructure => StackStructure ?? StackStructureMeasured;


    //public LayoutStructure LatestMeasuredStackStructure => StackStructureMeasured ?? StackStructure;


    protected virtual void ApplyStackMeasureResult()
    {
        var incoming = StackStructureMeasured;
        if (incoming == null)
            return;

        // Don't publish a structure whose ON-SCREEN slots aren't measured yet. BuildStackStructure sets
        // StackStructureMeasured to a structure whose cells are created but NOT YET measured
        // (WasMeasured=false, size 0), then the caller measures them in place; if another thread's Draw
        // swaps that in mid-measure, DrawStack skips the unmeasured VISIBLE cells -> a blank frame (the
        // empty-cell blink). Guard on VISIBLE cells only, keyed by ControlIndex from the last draw: a normal
        // MeasureVisible publish deliberately leaves OFF-SCREEN cells unmeasured/estimated
        // (SkiaLayout.ListView.cs) and MUST still publish, or holding the stale structure during background
        // measurement makes scrolling jerky.
        //
        // CRUCIAL: holding the current structure is only valid when it actually renders those visible slots
        // correctly — i.e. it ALREADY has them measured (an in-place refresh / delivery tick). At a JUMP
        // (ScrollToOldest) the visible set moved to a cold region the current structure does NOT cover, so
        // holding it lands at a stale/estimated offset (the wrong-landing bug). There, publish the incoming
        // structure now: its geometry is correct and the still-cold cells show a brief skeleton at the RIGHT
        // place. So hold only when the current structure has EVERY visible slot measured.
        //
        // And NEVER hold while an ordered jump is homing: ScrollToOldest/ScrollToIndex derives its landing
        // offset from the published structure's extent, so it MUST see the freshest geometry — a held stale
        // structure lands the jump at the wrong offset. The blink-guard is only for steady-state refreshes.
        bool jumpHoming = Parent is SkiaScroll js && js.HasPendingScrollOrder;

        if (!jumpHoming && _lastVisibleControlIndexes.Count > 0 && StackStructure != null)
        {
            _measuredCurrentScratch.Clear();
            foreach (var c in StackStructure.GetChildren())
                if (c != null && c.WasMeasured)
                    _measuredCurrentScratch.Add(c.ControlIndex);

            bool currentCoversVisible = true;
            foreach (var vi in _lastVisibleControlIndexes)
                if (!_measuredCurrentScratch.Contains(vi))
                {
                    currentCoversVisible = false;
                    break;
                }

            if (currentCoversVisible)
            {
                foreach (var cell in incoming.GetChildren())
                    if (cell != null && !cell.WasMeasured
                        && _lastVisibleControlIndexes.Contains(cell.ControlIndex))
                        return; // hold: current is a valid fallback for these slots, don't blink
            }
        }

        var previous = StackStructure;
        StackStructure = incoming;
        StackStructureMeasured = null;
        if (previous != StackStructure)
            previous?.Clear();
    }

    protected virtual ScaledSize MeasureAndArrangeCell(SKRect destination,
        ControlInStack cell, SkiaControl child,
        SKRect rectForChildrenPixels, float scale)
    {
        cell.Area = destination;

        // Always route through the child's Measure path so constraint changes
        // use its built-in measurement cache instead of blindly reusing a stale size.
        var measured = MeasureChild(child, cell.Area.Width, cell.Area.Height, scale);

        cell.Measured = measured;
        cell.WasMeasured = true;

        // Feed the item-keyed memo: data item (templated cell's BindingContext) -> size at this width.
        // Survives window swaps/recycling so a later revisit is seeded instead of remeasured.
        if (IsTemplated)
            StoreMemoSize(child?.BindingContext, cell.Area.Width, measured);

        LayoutCell(measured, cell, child, rectForChildrenPixels, scale);

        //SkiaLayout.TraceIdx(cell.ControlIndex, "FG-MEASURE",
        //    $"ctx={child?.ContextIndex} measuredH={measured.Pixels.Height:0} areaTop={cell.Area.Top:0} destTop={cell.Destination.Top:0} destH={cell.Destination.Height:0}");

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
            var desiredWidth = child?.WidthRequest >= 0
                ? (float)Math.Ceiling(child.WidthRequest * scale)
                : measured.Pixels.Width;
            var desiredHeight = child?.HeightRequest >= 0
                ? (float)Math.Ceiling(child.HeightRequest * scale)
                : measured.Pixels.Height;

            if (child != null)
            {
                // Horizontal alignment must resolve against the cell's COLUMN SLOT (cell.Area), not the
                // full multi-column container: for Split>1 a Fill child stretched to rectForChildrenPixels
                // becomes full-container width and the MeasureFirst prototype copy propagates that (with a
                // one-stride X shift) to every cell -> grid collapses to a single column. For Split==1 the
                // slot equals the container, so this is identical to the previous behaviour.
                var hSlot = cell.Area;
                if (Type == LayoutType.Column
                    && hSlot.Width > desiredWidth)
                {
                    if (child.HorizontalOptions.Alignment == LayoutAlignment.Fill && child.NeedFillX)
                    {
                        area = new(hSlot.Left,
                            area.Top,
                            hSlot.Right,
                            area.Bottom);
                    }
                    else if (child.HorizontalOptions.Alignment == LayoutAlignment.Center)
                    {
                        var left = hSlot.Left +
                                   (float)Math.Ceiling((hSlot.Width - desiredWidth) / 2f);
                        area = new(left,
                            area.Top,
                            left + desiredWidth,
                            area.Bottom);
                    }
                    else if (child.HorizontalOptions.Alignment == LayoutAlignment.End)
                    {
                        var left = hSlot.Right - desiredWidth;
                        area = new(left,
                            area.Top,
                            hSlot.Right,
                            area.Bottom);
                    }
                    else
                    {
                        area = new(hSlot.Left,
                            area.Top,
                            hSlot.Left + desiredWidth,
                            area.Bottom);
                    }
                }
                else if (Type == LayoutType.Row
                         && rectForChildrenPixels.Height > desiredHeight)
                {
                    if (child.VerticalOptions.Alignment == LayoutAlignment.Fill && child.NeedFillY)
                    {
                        area = new(area.Left,
                            rectForChildrenPixels.Top,
                            area.Right,
                            rectForChildrenPixels.Bottom);
                    }
                    else if (child.VerticalOptions.Alignment == LayoutAlignment.Center)
                    {
                        var top = rectForChildrenPixels.Top +
                                  (float)Math.Ceiling((rectForChildrenPixels.Height - desiredHeight) / 2f);
                        area = new(area.Left,
                            top,
                            area.Right,
                            top + desiredHeight);
                    }
                    else if (child.VerticalOptions.Alignment == LayoutAlignment.End)
                    {
                        var top = rectForChildrenPixels.Bottom - desiredHeight;
                        area = new(area.Left,
                            top,
                            area.Right,
                            rectForChildrenPixels.Bottom);
                    }
                    else
                    {
                        area = new(area.Left,
                            rectForChildrenPixels.Top,
                            area.Right,
                            rectForChildrenPixels.Top + desiredHeight);
                    }
                }
                else if (Type == LayoutType.Wrap && Split <= 0 && !child.NeedFillX && area.Width > desiredWidth)
                {
                    // A flow-wrap cell IS its measured width (Split > 0 keeps its fixed slot, alignment inside
                    // it is wanted). The measure rect spans to the row's right edge, so a Center/End child
                    // used to float inside the leftover strip while the flow continued from its measured
                    // width (Center cell drawn far right, next cell placed over its slot).
                    area = new(area.Left, area.Top, area.Left + desiredWidth, area.Bottom);
                }

                // MAIN axis: a Center child sits in its own slot (measured size), never in whatever remains of
                // the stack rect below/right of it (that centered it in the leftover area — or, in a finite
                // stack, outside the stack entirely). A Fill child arranged into an UNBOUNDED slot (content of
                // a scroll) is auto-sized too, else Arrange stretches it to float.MaxValue.
                // End is deliberately left as is (pushes the child to the stack's end) — semantic call for the
                // owner, not changed here.
                if (Type == LayoutType.Column)
                {
                    if (child.VerticalOptions.Alignment == LayoutAlignment.Center
                        || (child.NeedFillY && !float.IsFinite(area.Height)))
                    {
                        area = new(area.Left, area.Top, area.Right, area.Top + desiredHeight);
                    }
                }
                else if (Type == LayoutType.Row)
                {
                    if (child.HorizontalOptions.Alignment == LayoutAlignment.Center
                        || (child.NeedFillX && !float.IsFinite(area.Width)))
                    {
                        area = new(area.Left, area.Top, area.Left + desiredWidth, area.Bottom);
                    }
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

    protected virtual ScaledSize MeasureStackBase(SKRect rectForChildrenPixels, float scale)
    {
        var children = GetOrderedSubviews();
        if (children.Count == 0)
        {
            StackStructureMeasured = new LayoutStructure();
            return ScaledSize.FromPixels(0, 0, scale);
        }

        var spacingPixels = (float)(Spacing * scale);
        var width = 0f;
        var height = 0f;
        var widthCut = false;
        var heightCut = false;
        var index = 0;
        var structure = new LayoutStructure();
        var currentTop = rectForChildrenPixels.Top;
        var currentLeft = rectForChildrenPixels.Left;

        foreach (var child in children)
        {
            if (child == null)
                continue;

            child.OnBeforeMeasure();
            if (!child.CanDraw)
                continue;

            var measured = MeasureChild(child, rectForChildrenPixels.Width, rectForChildrenPixels.Height, scale);
            widthCut |= measured.WidthCut;
            heightCut |= measured.HeightCut;

            var cell = new ControlInStack
            {
                ControlIndex = index,
                View = child,
                Measured = measured,
                WasMeasured = true,
                Column = Type == LayoutType.Row ? index : 0,
                Row = Type == LayoutType.Column ? index : 0,
            };

            if (Type == LayoutType.Column)
            {
                var childWidth = measured.Pixels.Width > 0
                    ? Math.Min(measured.Pixels.Width, rectForChildrenPixels.Width)
                    : rectForChildrenPixels.Width;
                cell.Area = new SKRect(rectForChildrenPixels.Left, currentTop, rectForChildrenPixels.Left + childWidth,
                    currentTop + measured.Pixels.Height);
            }
            else
            {
                var childHeight = measured.Pixels.Height > 0
                    ? Math.Min(measured.Pixels.Height, rectForChildrenPixels.Height)
                    : rectForChildrenPixels.Height;
                cell.Area = new SKRect(currentLeft, rectForChildrenPixels.Top, currentLeft + measured.Pixels.Width,
                    rectForChildrenPixels.Top + childHeight);
            }

            var layoutArea = Type == LayoutType.Column
                ? new SKRect(rectForChildrenPixels.Left, cell.Area.Top, rectForChildrenPixels.Right, cell.Area.Bottom)
                : new SKRect(cell.Area.Left, rectForChildrenPixels.Top, cell.Area.Right, rectForChildrenPixels.Bottom);

            LayoutCell(measured, cell, child, layoutArea, scale);
            if (!float.IsNormal(cell.Destination.Width))
                cell.Destination = cell.Area;

            structure.Add(cell, cell.Column, cell.Row);

            if (Type == LayoutType.Column)
            {
                width = Math.Max(width, measured.Pixels.Width);
                height += measured.Pixels.Height;
                if (index > 0)
                    height += spacingPixels;
                currentTop = cell.Destination.Bottom + spacingPixels;
            }
            else
            {
                width += measured.Pixels.Width;
                if (index > 0)
                    width += spacingPixels;
                height = Math.Max(height, measured.Pixels.Height);
                currentLeft = cell.Destination.Right + spacingPixels;
            }

            index++;
        }

        StackStructureMeasured = structure;
        return ScaledSize.FromPixels(width, height, widthCut, heightCut, scale);
    }

    protected virtual ScaledSize MeasureWrapBase(SKRect rectForChildrenPixels, float scale)
    {
        var children = GetOrderedSubviews();
        var structure = new LayoutStructure();
        StackStructureMeasured = structure;

        if (children.Count == 0)
            return ScaledSize.FromPixels(0, 0, scale);

        var spacingPixels = (float)(Spacing * scale);
        var left = rectForChildrenPixels.Left;
        var top = rectForChildrenPixels.Top;
        var right = rectForChildrenPixels.Right;
        var x = left;
        var y = top;
        var row = 0;
        var column = 0;
        var rowHeight = 0f;
        var contentWidth = 0f;
        var contentHeight = 0f;
        var widthCut = false;
        var heightCut = false;

        for (var index = 0; index < children.Count; index++)
        {
            var child = children[index];
            if (child == null)
                continue;

            child.OnBeforeMeasure();
            if (!child.CanDraw)
                continue;

            var remainingWidth = Math.Max(0, right - x);
            var measured = MeasureChild(child,
                remainingWidth > 0 ? remainingWidth : rectForChildrenPixels.Width,
                rectForChildrenPixels.Height,
                scale);

            if (measured.IsEmpty)
                continue;

            widthCut |= measured.WidthCut;
            heightCut |= measured.HeightCut;

            var childWidth = measured.Pixels.Width;
            var childHeight = measured.Pixels.Height;

            if (x > left && x + childWidth > right)
            {
                x = left;
                y += rowHeight + spacingPixels;
                row++;
                column = 0;
                rowHeight = 0;

                measured = MeasureChild(child, rectForChildrenPixels.Width, rectForChildrenPixels.Height, scale);
                childWidth = measured.Pixels.Width;
                childHeight = measured.Pixels.Height;
                widthCut |= measured.WidthCut;
                heightCut |= measured.HeightCut;
            }

            var cell = new ControlInStack
            {
                ControlIndex = index,
                View = child,
                Measured = measured,
                WasMeasured = true,
                Area = new SKRect(x, y, x + childWidth, y + childHeight),
                Column = column,
                Row = row,
            };

            LayoutCell(measured, cell, child, cell.Area, scale);

            if (!float.IsNormal(cell.Destination.Width))
                cell.Destination = cell.Area;

            structure.Add(cell, column, row);

            x = cell.Destination.Right + spacingPixels;
            rowHeight = Math.Max(rowHeight, cell.Destination.Height > 0 ? cell.Destination.Height : childHeight);
            contentWidth = Math.Max(contentWidth, cell.Destination.Right - left);
            contentHeight = Math.Max(contentHeight, cell.Destination.Bottom - top);
            column++;
        }

        return ScaledSize.FromPixels(contentWidth, contentHeight, widthCut, heightCut, scale);
    }
}
