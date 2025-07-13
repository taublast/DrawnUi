//Adapted code from the Xamarin.Forms Grid implementation

using System.Runtime.CompilerServices;
using Microsoft.Maui.Primitives;

namespace DrawnUi.Draw;

public partial class SkiaLayout
{
    public class SkiaGridStructure
    {
        public double HeightConstraint => _gridHeightConstraint;
        public double WidthConstraint => _gridWidthConstraint;

        readonly ISkiaGridLayout _parentGrid;

        /// <summary>
        /// Pixels
        /// </summary>
        readonly double _gridWidthConstraint;

        /// <summary>
        /// Pixels
        /// </summary>
        readonly double _gridHeightConstraint;

        readonly double _explicitGridHeight;
        readonly double _explicitGridWidth;
        readonly double _gridMaxHeight;
        readonly double _gridMinHeight;
        readonly double _gridMaxWidth;
        readonly double _gridMinWidth;

        public DefinitionInfo[] Rows => _rows;

        public DefinitionInfo[] Columns => _columns;

        readonly ISkiaControl[] _childrenToLayOut;
        Cell[] _cells { get; }

        public readonly Thickness Padding;
        public readonly double RowSpacing;
        public readonly double ColumnSpacing;
        public readonly IReadOnlyList<IGridRowDefinition> RowDefinitions;
        public readonly IReadOnlyList<IGridColumnDefinition> ColumnDefinitions;

        readonly Dictionary<SpanKey, GridSpan> _spans = new();

        private DefinitionInfo[] _rows;
        private DefinitionInfo[] _columns;

        public SkiaGridStructure(ISkiaGridLayout parentGrid, double widthConstraint, double heightConstraint)
        {
            _parentGrid = parentGrid;

            //ported from xamarin Grid

            _explicitGridHeight = _parentGrid.Height; //todo this is incorrect but we don't care for the structure
            _explicitGridWidth = _parentGrid.Width;

            _gridWidthConstraint = widthConstraint;//Dimension.IsExplicitSet(_explicitGridWidth) ? _explicitGridWidth : widthConstraint;
            _gridHeightConstraint = heightConstraint;//Dimension.IsExplicitSet(_explicitGridHeight) ? _explicitGridHeight : heightConstraint;

            _gridMaxHeight = heightConstraint;//_grid.Destination.Height;
            _gridMinHeight = -1;
            _gridMaxWidth = widthConstraint;//_grid.Destination.Width;
            _gridMinWidth = -1;

            // Cache these GridLayout properties so we don't have to keep looking them up via _grid
            // (Property access via _grid may have performance implications for some SDKs.)
            Padding = parentGrid.Padding;
            ColumnSpacing = parentGrid.ColumnSpacing;
            RowSpacing = parentGrid.RowSpacing;
            RowDefinitions = parentGrid.RowDefinitions;
            ColumnDefinitions = parentGrid.ColumnDefinitions;


            var layout = parentGrid as SkiaControl;
            var children = layout.GetOrderedSubviews();

            var gridChildCount = children.Count;

            _rows = InitializeRows();
            _columns = InitializeColumns();

            _childrenToLayOut = new ISkiaControl[gridChildCount];
            int currentChild = 0;
            for (int n = 0; n < gridChildCount; n++)
            {
                if (children[n].CanDraw)//Visibility != Visibility.Collapsed)
                {
                    _childrenToLayOut[currentChild] = children[n];
                    currentChild += 1;
                }
            }

            if (currentChild < gridChildCount)
            {
                Array.Resize(ref _childrenToLayOut, currentChild);
            }

            // We'll ignore any collapsed child views during layout
            _cells = new Cell[_childrenToLayOut.Length];

            InitializeCells();

            MeasureCells();
        }

        DefinitionInfo[] InitializeRows()
        {
            int count = RowDefinitions.Count;

            if (count == 0)
            {
                // Since no rows are specified, we'll create an implied row 0 
                return Implied(true);
            }

            var rows = new DefinitionInfo[count];

            for (int n = 0; n < count; n++)
            {
                var definition = RowDefinitions[n];
                rows[n] = new DefinitionInfo(definition.Height);
            }

            return rows;
        }

        DefinitionInfo[] InitializeColumns()
        {
            int count = ColumnDefinitions.Count;

            if (count == 0)
            {
                // Since no columns are specified, we'll create an implied column 0 
                return Implied(false);
            }

            var definitions = new DefinitionInfo[count];

            for (int n = 0; n < count; n++)
            {
                var definition = ColumnDefinitions[n];
                definitions[n] = new DefinitionInfo(definition.Width);
            }

            return definitions;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CreateDefinitionIfMissing(ref DefinitionInfo[] array, int requiredSize, bool isRow)
        {
            if (requiredSize >= array.Length)
            {
                var currentLength = array.Length;
                var newSize = requiredSize + 1; // As arrays are zero-indexed
                Array.Resize(ref array, newSize);
                for (int i = currentLength; i < newSize; i++)
                {
                    if (isRow)
                    {
                        array[i] = new DefinitionInfo(this._parentGrid.DefaultRowDefinition.Height);
                    }
                    else
                    {
                        array[i] = new DefinitionInfo(this._parentGrid.DefaultColumnDefinition.Width);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        DefinitionInfo[] Implied(bool isRow)
        {
            if (isRow)
            {
                return new DefinitionInfo[]
                {
                    new DefinitionInfo(this._parentGrid.DefaultRowDefinition.Height)
                };
            }
            else
            {
                return new DefinitionInfo[]
                {
                    new DefinitionInfo(this._parentGrid.DefaultColumnDefinition.Width)
                };
            }
        }

        /// <summary>
        /// We are also going to auto-create column/row definitions here
        /// </summary>
        void InitializeCells()
        {
            // Create a structure to hold the cached values
            var cachedValues = new Dictionary<ISkiaControl, (int Row, int Column, int RowSpan, int ColumnSpan)>();

            // Pass 1: Determine maximum row and column and cache values
            int maxRow = 0, maxColumn = 0;
            foreach (var child in _childrenToLayOut)
            {
                if (child.CanDraw)
                {
                    int row = _parentGrid.GetRow(child as BindableObject);
                    int column = _parentGrid.GetColumn(child as BindableObject);
                    int rowSpan = _parentGrid.GetRowSpan(child as BindableObject);
                    int columnSpan = _parentGrid.GetColumnSpan(child as BindableObject);

                    maxRow = Math.Max(maxRow, row + rowSpan - 1);
                    maxColumn = Math.Max(maxColumn, column + columnSpan - 1);

                    cachedValues[child] = (row, column, rowSpan, columnSpan);
                }
            }

            // Resize Rows and Columns arrays
            CreateDefinitionIfMissing(ref _rows, maxRow, true);
            CreateDefinitionIfMissing(ref _columns, maxColumn, false);

            // Pass 2: Initialize cells using cached values
            for (int n = 0; n < _childrenToLayOut.Length; n++)
            {
                var view = _childrenToLayOut[n];

                var (row, column, rowSpan, columnSpan) = cachedValues[view];

                var columnGridLengthType = GridLengthType.None;

                for (int columnIndex = column; columnIndex < column + columnSpan; columnIndex++)
                {
                    columnGridLengthType |= ToGridLengthType(Columns[columnIndex].GridLength.GridUnitType);
                }

                var rowGridLengthType = GridLengthType.None;

                for (int rowIndex = row; rowIndex < row + rowSpan; rowIndex++)
                {
                    rowGridLengthType |= ToGridLengthType(Rows[rowIndex].GridLength.GridUnitType);
                }

                _cells[n] = new Cell(n, row, column, rowSpan, columnSpan, columnGridLengthType, rowGridLengthType);
            }


        }

        static GridLengthType ToGridLengthType(GridUnitType gridUnitType)
        {
            return gridUnitType switch
            {
                GridUnitType.Absolute => GridLengthType.Absolute,
                GridUnitType.Star => GridLengthType.Star,
                GridUnitType.Auto => GridLengthType.Auto,
                _ => GridLengthType.None,
            };
        }

        public Rect GetCellBoundsFor(ISkiaControl view, double xOffset, double yOffset)
        {
            var firstColumn = _parentGrid.GetColumn(view as BindableObject).Clamp(0, Columns.Length - 1);
            var columnSpan = _parentGrid.GetColumnSpan(view as BindableObject).Clamp(1, Columns.Length - firstColumn);
            var lastColumn = firstColumn + columnSpan;

            var firstRow = _parentGrid.GetRow(view as BindableObject).Clamp(0, Rows.Length - 1);
            var rowSpan = _parentGrid.GetRowSpan(view as BindableObject).Clamp(1, Rows.Length - firstRow);
            var lastRow = firstRow + rowSpan;

            double top = TopEdgeOfRow(firstRow);
            double left = LeftEdgeOfColumn(firstColumn);

            double width = 0;
            double height = 0;

            for (int n = firstColumn; n < lastColumn; n++)
            {
                width += Columns[n].Size;
            }

            for (int n = firstRow; n < lastRow; n++)
            {
                height += Rows[n].Size;
            }

            // Account for any space between spanned rows/columns
            width += (columnSpan - 1) * ColumnSpacing;
            height += (rowSpan - 1) * RowSpacing;

            return new Rect(
                left + xOffset,
                top + yOffset,
                width,
                height);
        }

        public double GridHeight()
        {
            return SumDefinitions(Rows, RowSpacing) + Padding.VerticalThickness;
        }

        public double GridWidth()
        {
            return SumDefinitions(Columns, ColumnSpacing) + Padding.HorizontalThickness;
        }

        public double MeasuredGridHeight()
        {
            var height = Dimension.IsExplicitSet(_explicitGridHeight) ? _explicitGridHeight : GridHeight();

            if (_gridMaxHeight >= 0 && height > _gridMaxHeight)
            {
                height = _gridMaxHeight;
            }

            if (_gridMinHeight >= 0 && height < _gridMinHeight)
            {
                height = _gridMinHeight;
            }

            return height;
        }

        public double MeasuredGridWidth()
        {
            var width = Dimension.IsExplicitSet(_explicitGridWidth) ? _explicitGridWidth : GridWidth();

            if (_gridMaxWidth >= 0 && width > _gridMaxWidth)
            {
                width = _gridMaxWidth;
            }

            if (_gridMinWidth >= 0 && width < _gridMinWidth)
            {
                width = _gridMinWidth;
            }

            return width;
        }

        static double SumDefinitions(DefinitionInfo[] definitions, double spacing)
        {
            double sum = 0;

            for (int n = 0; n < definitions.Length; n++)
            {
                sum += definitions[n].Size;

                if (n > 0)
                {
                    sum += spacing;
                }
            }

            return sum;
        }

        void MeasureCells()
        {
            // Do the initial pass for all the auto/star stuff
            MeasureCellsWithUnknowns();

            ResolveStarColumns(_gridWidthConstraint);
            ResolveStarRows(_gridHeightConstraint);

            // Measure the content for cells where we know the dimensions
            MeasureKnownCells();

            ResolveSpans();

            // Compress the star values to their minimums for measurement 
            CompressStarMeasurements();
        }

        void MeasureChild(Cell cell)
        {
            if (cell.IsAbsolute)
            {
                // This cell is entirely within rows/columns with absolute sizes; we don't need to measure
                // it to figure out those sizes
                return;
            }

            //var availableWidth = cell.IsColumnSpanAuto ? _gridWidthConstraint : AvailableWidth(cell);
            //var availableHeight = cell.IsRowSpanAuto ? _gridHeightConstraint : AvailableHeight(cell);

            var availableWidth = AvailableWidth(cell);
            var availableHeight = AvailableHeight(cell);


            //if (availableWidth > 0 && availableHeight > 0)
            {
                var control = _childrenToLayOut[cell.ViewIndex];
                var scale = (float)control.RenderingScale;

                var width = availableWidth * scale;
                if (width < 0)
                    width = -1;

                var height = availableHeight * scale;
                if (height < 0)
                    height = -1;

                var child = _childrenToLayOut[cell.ViewIndex];

                Size measure;

                if (!child.IsVisible)
                {
                    measure = new Size(0, 0);
                }
                else
                {
                    var pixels = child.Measure((float)width, (float)height, scale);

                    measure = new Size(pixels.Units.Width, pixels.Units.Height);
                }

                if (cell.IsColumnSpanAuto)
                {
                    if (cell.ColumnSpan == 1)
                    {
                        Columns[cell.Column].Update(measure.Width);
                    }
                    else
                    {
                        var span = new GridSpan(cell.Column, cell.ColumnSpan, true, measure.Width);
                        TrackSpan(span);
                    }
                }

                if (cell.IsRowSpanAuto)
                {
                    if (cell.RowSpan == 1)
                    {
                        Rows[cell.Row].Update(measure.Height);
                    }
                    else
                    {
                        var span = new GridSpan(cell.Row, cell.RowSpan, false, measure.Height);
                        TrackSpan(span);
                    }
                }
            }

        }

        void MeasureCellsWithUnknowns()
        {
            for (int n = 0; n < _cells.Length; n++)
            {
                var cell = _cells[n];
                MeasureChild(cell);
            }
        }

        void TrackSpan(GridSpan span)
        {
            if (_spans.TryGetValue(span.Key, out GridSpan otherSpan))
            {
                // This span may replace an equivalent but smaller span
                if (span.Requested > otherSpan.Requested)
                {
                    _spans[span.Key] = span;
                }
            }
            else
            {
                _spans[span.Key] = span;
            }
        }

        void ResolveSpans()
        {
            foreach (var span in _spans.Values)
            {
                if (span.IsColumn)
                {
                    ResolveSpan(Columns, span.Start, span.Length, ColumnSpacing, span.Requested);
                }
                else
                {
                    ResolveSpan(Rows, span.Start, span.Length, RowSpacing, span.Requested);
                }
            }
        }

        static void ResolveSpan(DefinitionInfo[] definitions, int start, int length, double spacing, double requestedSize)
        {
            double currentSize = 0;
            var end = start + length;

            // Determine how large the spanned area currently is
            for (int n = start; n < end; n++)
            {
                currentSize += definitions[n].Size;

                if (n > start)
                {
                    currentSize += spacing;
                }
            }

            if (requestedSize <= currentSize)
            {
                // If our request fits in the current size, we're good
                return;
            }

            // Figure out how much more space we need in this span
            double required = requestedSize - currentSize;

            // And how many parts of the span to distribute that space over
            int autoCount = 0;
            for (int n = start; n < end; n++)
            {
                if (definitions[n].IsAuto)
                {
                    autoCount += 1;
                }
                else if (definitions[n].IsStar)
                {
                    // Ah, part of this span is a Star; that means it doesn't count
                    // for sizing the Auto parts of the span at all. We can just cut out now.
                    return;
                }
            }

            double distribution = required / autoCount;

            // And distribute that over the rows/columns in the span
            for (int n = start; n < end; n++)
            {
                if (definitions[n].IsAuto)
                {
                    definitions[n].Size += distribution;
                }
            }
        }

        public double LeftEdgeOfColumn(int column)
        {
            double left = Padding.Left;

            for (int n = 0; n < column; n++)
            {
                left += Columns[n].Size;
                left += ColumnSpacing;
            }

            return left;
        }

        public double TopEdgeOfRow(int row)
        {
            double top = Padding.Top;

            for (int n = 0; n < row; n++)
            {
                top += Rows[n].Size;
                top += RowSpacing;
            }

            return top;
        }

        void ResolveStars(DefinitionInfo[] defs, double availableSpace, Func<Cell, bool> cellCheck, Func<SKSize, double> dimension)
        {
            // Count up the total weight of star columns (e.g., "*, 3*, *" == 5)

            var starCount = 0.0;

            foreach (var definition in defs)
            {
                if (definition.IsStar)
                {
                    starCount += (float)definition.GridLength.Value;
                }
            }

            if (starCount == 0)
            {
                return;
            }

            double starSize = 0;

            if (double.IsInfinity(availableSpace))
            {
                // If the available space we're measuring is infinite, then the 'star' doesn't really mean anything
                // (each one would be infinite). So instead we'll use the size of the actual view in the star row/column.
                // This means that an empty star row/column goes to zero if the available space is infinite. 

                foreach (var cell in _cells)
                {
                    if (cellCheck(cell)) // Check whether this cell should count toward the type of star value we're measuring
                    {
                        // Update the star width if the view in this cell is bigger
                        starSize = Math.Max(starSize, dimension(_childrenToLayOut[cell.ViewIndex].MeasuredSize.Units));
                    }
                }
            }
            else
            {
                // If we have a finite space, we can divvy it up among the full star weight
                starSize = availableSpace / starCount;
            }

            foreach (var definition in defs)
            {
                if (definition.IsStar)
                {
                    // Give the star row/column the appropriate portion of the space based on its weight
                    definition.Size = starSize * definition.GridLength.Value;
                }
            }
        }

        void ResolveStarColumns(double widthConstraint)
        {
            var availableSpace = widthConstraint - GridWidth();
            static bool cellCheck(Cell cell) => cell.IsColumnSpanStar;
            static double getDimension(SKSize size) => size.Width;

            ResolveStars(Columns, availableSpace, cellCheck, getDimension);
        }

        void ResolveStarRows(double heightConstraint)
        {
            var availableSpace = heightConstraint - GridHeight();
            static bool cellCheck(Cell cell) => cell.IsRowSpanStar;
            static double getDimension(SKSize size) => size.Height;

            ResolveStars(Rows, availableSpace, cellCheck, getDimension);
        }

        void MeasureKnownCells()
        {
            foreach (var cell in _cells)
            {
                if (!cell.NeedsKnownMeasurePass)
                {
                    continue;
                }

                double width = 0;
                double height = 0;

                for (int n = cell.Row; n < cell.Row + cell.RowSpan; n++)
                {
                    height += Rows[n].Size;
                }

                for (int n = cell.Column; n < cell.Column + cell.ColumnSpan; n++)
                {
                    width += Columns[n].Size;
                }

                if (width == 0 || height == 0)
                {
                    continue;
                }

                var control = _childrenToLayOut[cell.ViewIndex];
                //var scale = (float)control.RenderingScale;

                var rectCell = GetCellBoundsFor(control, 0, 0);
                var measure = rectCell;

                var scale = (float)control.RenderingScale;
                control.Measure((float)(rectCell.Width * scale), (float)Math.Round(rectCell.Height * scale),
                    scale);

                if (cell.IsColumnSpanStar && cell.ColumnSpan > 1)
                {
                    var span = new GridSpan(cell.Column, cell.ColumnSpan, true, measure.Width);
                    TrackSpan(span);
                }

                if (cell.IsRowSpanStar && cell.RowSpan > 1)
                {
                    var span = new GridSpan(cell.Row, cell.RowSpan, false, measure.Height);
                    TrackSpan(span);
                }
            }
        }

        double AvailableWidth(Cell cell)
        {
            // Because our cell may overlap columns that are already measured (and counted in GridWidth()),
            // we'll need to add the size of those columns back into our available space
            double cellColumnsWidth = 0;

            // So we'll have to tally up the known widths of those rows. While we do that, we'll
            // keep track of whether all the columns spanned by this cell are absolute widths
            bool absolute = true;

            for (int c = cell.Column; c < cell.Column + cell.ColumnSpan; c++)
            {
                cellColumnsWidth += Columns[c].Size;

                if (!Columns[c].IsAbsolute)
                {
                    absolute = false;
                }
            }

            cellColumnsWidth += (cell.ColumnSpan - 1) * ColumnSpacing;

            if (absolute)
            {
                // If all the spanned columns were absolute, then we know the exact available width for 
                // the view that's in this cell
                return cellColumnsWidth;
            }

            // Since some of the columns weren't already specified, we'll need to work out what's left
            // of the Grid's width for this cell

            var alreadyUsed = GridWidth();
            var available = _gridWidthConstraint - alreadyUsed;

            return available + cellColumnsWidth;
        }

        double AvailableHeight(Cell cell)
        {
            // Because our cell may overlap rows that are already measured (and counted in GridHeight()),
            // we'll need to add the size of those rows back into our available space
            double cellRowsHeight = 0;

            // So we'll have to tally up the known heights of those rows. While we do that, we'll
            // keep track of whether all the rows spanned by this cell are absolute heights
            bool absolute = true;

            for (int c = cell.Row; c < cell.Row + cell.RowSpan; c++)
            {
                cellRowsHeight += Rows[c].Size;

                if (!Rows[c].IsAbsolute)
                {
                    absolute = false;
                }
            }

            cellRowsHeight += (cell.RowSpan - 1) * RowSpacing;

            if (absolute)
            {
                // If all the spanned rows were absolute, then we know the exact available height for 
                // the view that's in this cell
                return cellRowsHeight;
            }

            // Since some of the rows weren't already specified, we'll need to work out what's left
            // of the Grid's height for this cell

            var alreadyUsed = GridHeight();
            var available = _gridHeightConstraint - alreadyUsed;

            return available + cellRowsHeight;
        }

        public void DecompressStarsInternal(Size targetSize)
        {
            if (double.IsFinite(targetSize.Height))// || _grid.VerticalLayoutAlignment == LayoutAlignment.Fill
            {
                // Reset the size on all star rows
                ZeroOutStarSizes(Rows);

                // And compute them for the actual arrangement height
                ResolveStarRows(targetSize.Height);
            }
            else
            {
                MakeInfiniteStarSizes(Rows);
            }

            if (double.IsFinite(targetSize.Width)) //_grid.HorizontalLayoutAlignment == LayoutAlignment.Fill
            {
                // Reset the size on all star columns
                ZeroOutStarSizes(Columns);

                // And compute them for the actual arrangement width
                ResolveStarColumns(targetSize.Width);
            }
            else
            {
                MakeInfiniteStarSizes(Columns);
            }
        }

        public void DecompressStars(SKSize targetSize)
        {
            if (Dimension.IsExplicitSet(_explicitGridHeight))// || _grid.VerticalLayoutAlignment == LayoutAlignment.Fill
            {
                // Reset the size on all star rows
                ZeroOutStarSizes(Rows);

                // And compute them for the actual arrangement height
                ResolveStarRows(targetSize.Height);
            }

            if (Dimension.IsExplicitSet(_explicitGridWidth)) //_grid.HorizontalLayoutAlignment == LayoutAlignment.Fill
            {
                // Reset the size on all star columns
                ZeroOutStarSizes(Columns);

                // And compute them for the actual arrangement width
                ResolveStarColumns(targetSize.Width);
            }
        }

        void CompressStarMeasurements()
        {
            CompressStarRows();
            CompressStarColumns();
        }

        void CompressStarRows()
        {
            var copy = ScratchCopy(Rows);

            // Iterate over the cells and inflate the star row sizes in the copy
            // to the minimum required in order to contain the cells

            for (int n = 0; n < _cells.Length; n++)
            {
                var cell = _cells[n];

                if (!cell.IsRowSpanStar)
                {
                    // This cell doesn't span any star rows, nothing to do
                    continue;
                }

                var start = cell.Row;
                var end = start + cell.RowSpan;

                var desiredHeight = Math.Min(_gridHeightConstraint, _childrenToLayOut[cell.ViewIndex].MeasuredSize.Units.Height);

                ExpandStarsInSpan(desiredHeight, Rows, copy, start, end);
            }

            UpdateStarSizes(Rows, copy);
        }

        void CompressStarColumns()
        {
            var copy = ScratchCopy(Columns);

            // Iterate over the cells and inflate the star column sizes in the copy
            // to the minimum required in order to contain the cells

            for (int n = 0; n < _cells.Length; n++)
            {
                var cell = _cells[n];

                if (!cell.IsColumnSpanStar)
                {
                    // This cell doesn't span any star columns, nothing to do
                    continue;
                }

                var start = cell.Column;
                var end = start + cell.ColumnSpan;

                var cellRequiredWidth = Math.Min(_gridWidthConstraint, _childrenToLayOut[cell.ViewIndex].MeasuredSize.Units.Width);

                ExpandStarsInSpan(cellRequiredWidth, Columns, copy, start, end);
            }

            UpdateStarSizes(Columns, copy);
        }

        static DefinitionInfo[] ScratchCopy(DefinitionInfo[] original)
        {
            var copy = new DefinitionInfo[original.Length];

            for (int n = 0; n < original.Length; n++)
            {
                copy[n] = new DefinitionInfo(original[n].GridLength)
                {
                    Size = original[n].Size
                };
            }

            // zero out the star sizes in the copy
            ZeroOutStarSizes(copy);

            return copy;
        }

        static void ExpandStarsInSpan(double spaceNeeded, DefinitionInfo[] original, DefinitionInfo[] updated, int start, int end)
        {
            // Remove the parts of spaceNeeded which are already covered by explicit and auto columns in the span
            for (int n = start; n < end; n++)
            {
                if (original[n].IsAbsolute || original[n].IsAuto)
                {
                    spaceNeeded -= original[n].Size;
                }
            }

            // Determine how much space the star sizes in the span are already requesting
            // (because of other overlapping cells)

            double spaceAvailable = 0;
            int starCount = 0;
            for (int n = start; n < end; n++)
            {
                if (updated[n].IsStar)
                {
                    starCount += 1;
                    spaceAvailable += updated[n].Size;
                }
            }

            // If previous inflations from other cells haven't given us enough room,
            // distribute the amount of space we still need evenly across the stars in the span
            if (spaceAvailable < spaceNeeded)
            {
                var toAdd = (spaceNeeded - spaceAvailable) / starCount;
                for (int n = start; n < end; n++)
                {
                    if (updated[n].IsStar)
                    {
                        updated[n].Size += toAdd;
                    }
                }
            }
        }

        static void ZeroOutStarSizes(DefinitionInfo[] definitions)
        {
            for (int n = 0; n < definitions.Length; n++)
            {
                var definition = definitions[n];
                if (definition.IsStar)
                {
                    definition.Size = 0;
                }
            }
        }

        static void MakeInfiniteStarSizes(DefinitionInfo[] definitions)
        {
            for (int n = 0; n < definitions.Length; n++)
            {
                var definition = definitions[n];
                if (definition.IsStar)
                {
                    definition.Size = double.PositiveInfinity;
                }
            }
        }

        static void UpdateStarSizes(DefinitionInfo[] original, DefinitionInfo[] updated)
        {
            for (int n = 0; n < updated.Length; n++)
            {
                if (!updated[n].IsStar)
                {
                    continue;
                }

                original[n].Size = updated[n].Size;
            }
        }
    }
}
