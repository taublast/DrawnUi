namespace DrawnUi.Draw;

public partial class SkiaLayout
{
    /// <summary>
    /// Used for stack-like layouts such as Column, Row, and Wrap.
    /// </summary>
    public LayoutStructure StackStructure { get; set; }

    /// <summary>
    /// Set during measure and swapped into StackStructure when measure result is applied.
    /// </summary>
    public LayoutStructure StackStructureMeasured { get; set; }

    public LayoutStructure LatestStackStructure => StackStructure ?? StackStructureMeasured;

    public LayoutStructure LatestMeasuredStackStructure => StackStructureMeasured ?? StackStructure;

    protected virtual void ApplyStackMeasureResult()
    {
        if (StackStructureMeasured != null)
        {
            var previous = StackStructure;
            StackStructure = StackStructureMeasured;
            StackStructureMeasured = null;
            if (previous != StackStructure)
                previous?.Clear();
        }
    }

    protected virtual ScaledSize MeasureAndArrangeCell(SKRect destination,
        ControlInStack cell, SkiaControl child,
        SKRect rectForChildrenPixels, float scale)
    {
        cell.Area = destination;

        ScaledSize measured = child.MeasuredSize;
        if (IsTemplated || child.NeedMeasure)
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
            var desiredWidth = child?.WidthRequest >= 0
                ? (float)Math.Ceiling(child.WidthRequest * scale)
                : measured.Pixels.Width;
            var desiredHeight = child?.HeightRequest >= 0
                ? (float)Math.Ceiling(child.HeightRequest * scale)
                : measured.Pixels.Height;

            if (child != null)
            {
                if (Type == LayoutType.Column
                    && rectForChildrenPixels.Width > desiredWidth)
                {
                    if (child.HorizontalOptions.Alignment == LayoutAlignment.Fill && child.NeedFillX)
                    {
                        area = new(rectForChildrenPixels.Left,
                            area.Top,
                            rectForChildrenPixels.Right,
                            area.Bottom);
                    }
                    else if (child.HorizontalOptions.Alignment == LayoutAlignment.Center)
                    {
                        var left = rectForChildrenPixels.Left + (float)Math.Ceiling((rectForChildrenPixels.Width - desiredWidth) / 2f);
                        area = new(left,
                            area.Top,
                            left + desiredWidth,
                            area.Bottom);
                    }
                    else if (child.HorizontalOptions.Alignment == LayoutAlignment.End)
                    {
                        var left = rectForChildrenPixels.Right - desiredWidth;
                        area = new(left,
                            area.Top,
                            rectForChildrenPixels.Right,
                            area.Bottom);
                    }
                    else
                    {
                        area = new(rectForChildrenPixels.Left,
                            area.Top,
                            rectForChildrenPixels.Left + desiredWidth,
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
                        var top = rectForChildrenPixels.Top + (float)Math.Ceiling((rectForChildrenPixels.Height - desiredHeight) / 2f);
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
                var childWidth = measured.Pixels.Width > 0 ? Math.Min(measured.Pixels.Width, rectForChildrenPixels.Width) : rectForChildrenPixels.Width;
                cell.Area = new SKRect(rectForChildrenPixels.Left, currentTop, rectForChildrenPixels.Left + childWidth, currentTop + measured.Pixels.Height);
            }
            else
            {
                var childHeight = measured.Pixels.Height > 0 ? Math.Min(measured.Pixels.Height, rectForChildrenPixels.Height) : rectForChildrenPixels.Height;
                cell.Area = new SKRect(currentLeft, rectForChildrenPixels.Top, currentLeft + measured.Pixels.Width, rectForChildrenPixels.Top + childHeight);
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