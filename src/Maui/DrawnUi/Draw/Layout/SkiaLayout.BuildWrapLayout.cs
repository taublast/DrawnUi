using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DrawnUi.Draw;

public partial class SkiaLayout
{
    /// <summary>
    /// Implementation for LayoutType.Wrap 
    /// </summary>
    public class BuildWrapLayout : StackLayoutStructure
    {
        public BuildWrapLayout(SkiaLayout layout) : base(layout)
        {
        }

        public override ScaledSize Build(SKRect rectForChildrenPixels, float scale)
        {
            bool isRtl = Super.IsRtl; //todo!!!

            //content size
            float stackHeight = 0.0f;
            float stackWidth = 0.0f;

            // row size
            float maxHeight = 0.0f;
            float currentLineWidth = 0.0f;
            float currentLineRealWidth = 0.0f;

            // for autosize
            float maxWidth = 0.0f;

            var structure = new LayoutStructure();

            //for current row, to layout upon finalizing
            var cellsToLayoutLater = new List<ControlInStack>();

            var maxAvailableSpace = rectForChildrenPixels.Width;
            float sizePerChunk = maxAvailableSpace;

            SKRect rectForChild = rectForChildrenPixels;
            bool useFixedSplitSize = _layout.Split > 0;
            bool alignSplit = _layout.SplitAlign && useFixedSplitSize;

            if (_layout.Split > 1)
            {
                sizePerChunk = (float)Math.Round(
                    (maxAvailableSpace - (_layout.Split - 1) * _layout.Spacing * scale) / _layout.Split);
            }

            var column = 0;
            var row = 0;
            bool rowFinalized = true;
            bool colFinalized = true;

            SKRect rectFitChild = rectForChild; //will be adjusted by StartColumn
            if (isRtl)
            {
                rectForChild.Left = rectForChildrenPixels.Right - sizePerChunk;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void StartRow()
            {
                rowFinalized = false;

                maxHeight = 0.0f;
                currentLineWidth = 0f;
                currentLineRealWidth = 0f;

                rectForChild.Left = isRtl ? rectForChildrenPixels.Right : 0;  //reset to start

                var add = GetSpacingForIndex(row, scale);

                stackHeight += add;
                rectForChild.Top += add;

                column = 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void FinalizeRow(float addHeight)
            {
                rowFinalized = true;

                if (currentLineRealWidth > maxWidth)
                {
                    maxWidth = currentLineRealWidth;
                }

                if (currentLineWidth > stackWidth)
                    stackWidth = currentLineWidth;

                //layout cells in this row for the final height:
                LayoutCellsInternal(); //todo move to  second pass !!!

                stackHeight += addHeight;
                rectForChild.Top += addHeight;

                row++;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void LayoutCellsInternal()
            {
                //layout cells in this row for the final height:
                if (!_layout.IsTemplated)
                {
                    var layoutMaxHeight = float.IsFinite(rectFitChild.Height) ? rectFitChild.Height : maxHeight;
                    var layoutHeight = _layout.NeedFillY ? layoutMaxHeight : maxHeight;

                    foreach (var controlInStack in CollectionsMarshal.AsSpan(cellsToLayoutLater))
                    {
                        if (_layout.Type == LayoutType.Column || _layout.Type == LayoutType.Row)
                        {
                            controlInStack.Area = new(
                                controlInStack.Area.Left,
                                controlInStack.Area.Top,
                                controlInStack.Area.Left + controlInStack.View.DrawingRect.Width,
                                controlInStack.Area.Top + controlInStack.View.DrawingRect.Height);
                        }
                        else
                        {
                            controlInStack.Area = new(
                                controlInStack.Area.Left,
                                controlInStack.Area.Top,
                                controlInStack.Area.Right,
                                controlInStack.Area.Top + layoutHeight);
                        }

                        _layout.LayoutCell(controlInStack.Measured, controlInStack, controlInStack.View, controlInStack.Area, scale);
                    }
                }
                cellsToLayoutLater.Clear();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void StartColumn()
            {
                colFinalized = false;

                var add = GetSpacingForIndex(column, scale);

                if (isRtl)
                {
                    rectForChild.Left -= add;
                }
                else
                {
                    rectForChild.Left += add;
                }

                currentLineWidth += add;
                currentLineRealWidth += add;

                if (useFixedSplitSize)
                {
                    //always take the full column width
                    rectFitChild = new SKRect(
                        rectForChild.Left,
                        rectForChild.Top,
                        rectForChild.Left + sizePerChunk,
                        rectForChild.Bottom);
                }
                else
                {
                    //take the remaining width
                    rectFitChild = new SKRect(
                        rectForChild.Left,
                        rectForChild.Top,
                        rectForChild.Right,
                        rectForChild.Bottom);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void BreakRow()
            {
                var removeSpacing = GetSpacingForIndex(column, scale);
                currentLineWidth -= removeSpacing;
                currentLineRealWidth -= removeSpacing;
                FinalizeRow(maxHeight);
                StartRow();
                StartColumn();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void FinalizeColumn(float width, float height)
            {
                colFinalized = true;

                if (height > maxHeight)
                    maxHeight = height;

                if (alignSplit) //todo check space distribution for full/auto
                {
                    if (_layout.NeedAutoWidth)
                    {
                        currentLineRealWidth += width;
                        width = sizePerChunk;
                    }
                    else
                    {
                        width = sizePerChunk;
                        currentLineRealWidth += width;
                    }
                }
                else
                {
                    currentLineRealWidth += width;
                }

                currentLineWidth += width;

                if (isRtl)
                {
                    rectForChild.Left -= width;
                }
                else
                {
                    rectForChild.Left += width;
                }

                column++;
            }

            int index = -1;
            ControlInStack measuredCell = null; //if strategy is to measure first cell only

            foreach (var child in EnumerateViewsForMeasurement())
            {
                index++;

                child.OnBeforeMeasure();
                if (!child.CanDraw)
                {
                    continue;
                }

                if (rowFinalized)
                {
                    StartRow();
                }
                if (colFinalized)
                {
                    StartColumn();
                }

                var view = _layout.IsTemplated ? null : child;
                var cell = CreateWrapper(index, view);

                var remainingSize = rectForChild.Width;
                if (useFixedSplitSize)
                {
                    remainingSize = rectFitChild.Width;
                }

                if (remainingSize <= 0)
                {
                    //start new line
                    BreakRow();
                    remainingSize = rectForChild.Width;
                    if (useFixedSplitSize)
                    {
                        remainingSize = rectFitChild.Width;
                    }
                }

                ScaledSize measured;

                //will use dynamically changing rectFitChild
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                ScaledSize MeasureCellInternal()
                {
                    if (_layout.IsTemplated)
                    {
                        bool needMeasure =
                            _layout.MeasureItemsStrategy != MeasuringStrategy.MeasureFirst ||
                            _layout.RecyclingTemplate == RecyclingTemplate.Disabled ||
                            measuredCell == null;

                        if (needMeasure)
                        {
                            measuredCell = cell;
                            return _layout.MeasureAndArrangeCell(rectFitChild,
                                cell, child, rectFitChild, scale);
                        }
                        else
                        {
                            //apply first measured size to cell
                            var offsetX = rectFitChild.Left - measuredCell.Area.Left;
                            var offsetY = rectFitChild.Top - measuredCell.Area.Top;
                            var arranged = measuredCell.Destination;
                            arranged.Offset(new(offsetX, offsetY));

                            cell.Area = rectFitChild;
                            cell.Measured = measuredCell.Measured.Clone();
                            cell.Destination = arranged;
                            cell.WasMeasured = true;

                            return cell.Measured;
                        }
                    }

                    //non-templated
                    //return MeasureAndArrangeCell(rectFitChild, cell, child, scale);
                    return MeasureCell(rectFitChild, cell, child, scale);
                }

                //we know we will not fit in advance
                if (child.WidthRequestWithMargins * scale > rectFitChild.Width)
                {
                    BreakRow();
                    remainingSize = rectForChild.Width;
                    if (useFixedSplitSize)
                    {
                        remainingSize = rectFitChild.Width;
                    }
                }

                measured = MeasureCellInternal();

                if (!measured.IsEmpty)
                {
                    //add logic for col row

                    //check we are within bounds
                    var fitsH = cell.Measured.Pixels.Width <= remainingSize && !cell.Measured.WidthCut;

                    if (!fitsH && !useFixedSplitSize)
                    {
                        BreakRow();
                        measured = MeasureCellInternal();
                    }
                    else
                    {
                        var fitsV = cell.Measured.Pixels.Height <= maxHeight;
                        //todo keke we need bigger height
                    }

                    structure.Add(cell, column, row);
                    FinalizeColumn(cell.Measured.Pixels.Width, cell.Measured.Pixels.Height);

                    cellsToLayoutLater.Add(cell);

                    //check we need a new line?
                    if (!rowFinalized
                        && _layout.Split > 0 && column >= _layout.Split)
                    {
                        FinalizeRow(maxHeight);
                    }

                    //SplitMax = 2  Split Auto, Even, EvenAdaptive

                }
            }

            if (!rowFinalized)
            {
                FinalizeRow(maxHeight);
            }

            _layout.StackStructureMeasured = structure;

            if (_layout.HorizontalOptions.Alignment == LayoutAlignment.Fill && _layout.WidthRequest < 0)
            {
                stackWidth = rectForChildrenPixels.Width;
            }
            if (_layout.VerticalOptions.Alignment == LayoutAlignment.Fill && _layout.HeightRequest < 0)
            {
                stackHeight = rectForChildrenPixels.Height;
            }

            return ScaledSize.FromPixels(stackWidth, stackHeight, scale);
        }

    }
}
