//reusing some code from #dotnetmaui Layout

using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace DrawnUi.Draw;

public partial class SkiaLayout
{

    public bool IsBackgroundMeasuring => _isBackgroundMeasuring;
    public int BackgroundMeasurementProgress => _backgroundMeasurementProgress;
    public int TotalMeasuredItems => _measuredItems.Count;

    public int FirstMeasuredIndex { get; protected set; }
    public int LastMeasuredIndex { get; protected set; }
    public int FirstVisibleIndex { get; protected set; }
    public int LastVisibleIndex { get; protected set; }

    /// <summary>
    /// Percentage of items that have been measured (0.0 to 1.0)
    /// </summary>
    protected float MeasuredItemsPercentage => ItemsSource?.Count > 0 ? (float)(LastMeasuredIndex + 1) / ItemsSource.Count : 0f;

    // Background measurement support
    private CancellationTokenSource _backgroundMeasurementCts;
    private Task _backgroundMeasurementTask;
    private readonly object _measurementLock = new object();
    private volatile bool _isBackgroundMeasuring = false;

    // Sliding window configuration
    private const int SLIDING_WINDOW_SIZE = 300; // Keep 300 measured items max
    private const int MEASUREMENT_BATCH_SIZE = 20; // Measure 20 items per batch
    private const int AHEAD_BUFFER = 100; // Measure 100 items ahead of visible area
    private const int BEHIND_BUFFER = 50;  // Keep 50 items behind visible area

    // Track measurement state
    private readonly ConcurrentDictionary<int, MeasuredItemInfo> _measuredItems = new();
    private volatile int _backgroundMeasurementProgress = 0;

    // Background measurement staging for rendering pipeline integration
    private readonly object _pendingLock = new();
    private readonly List<List<MeasuredItemInfo>> _pendingMeasurements = new();

    /// <summary>
    /// Information about a measured item for sliding window management
    /// </summary>
    private class MeasuredItemInfo
    {
        public ControlInStack Cell { get; set; }
        public DateTime LastAccessed { get; set; } = DateTime.UtcNow;
        public bool IsInViewport { get; set; }
    }

    /// <summary>
    /// Cancels any ongoing background measurement
    /// </summary>
    public void CancelBackgroundMeasurement()
    {
        if (!_isBackgroundMeasuring)
            return;

        lock (_measurementLock)
        {
            _backgroundMeasurementCts?.Cancel();
            _backgroundMeasurementCts?.Dispose();
            _backgroundMeasurementCts = null;
            _isBackgroundMeasuring = false;
        }

        Debug.WriteLine("[CancelBackgroundMeasurement] Background measurement cancelled");
    }

    /// <summary>
    /// Starts background measurement of items beyond the visible area
    /// </summary>
    private void StartBackgroundMeasurement(SKRect constraints, float scale, int startFromIndex)
    {
        if (!IsTemplated || ItemsSource == null || ItemsSource.Count <= startFromIndex)
            return;

        // Cancel any existing background measurement
        CancelBackgroundMeasurement();

        lock (_measurementLock)
        {
            _backgroundMeasurementCts = new CancellationTokenSource();
            _isBackgroundMeasuring = true;
        }

        var cancellationToken = _backgroundMeasurementCts.Token;

        _backgroundMeasurementTask = Task.Run(async () =>
        {
            try
            {
                await BackgroundMeasureItems(constraints, scale, startFromIndex, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[StartBackgroundMeasurement] Background measurement was cancelled");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StartBackgroundMeasurement] Background measurement error: {ex.Message}");
            }
            finally
            {
                lock (_measurementLock)
                {
                    _isBackgroundMeasuring = false;
                }
            }
        });
    }

    private int _listAdditionalMeasurements;

    /// <summary>
    /// Enhanced MeasureList with background measurement support
    /// </summary>
    public virtual ScaledSize MeasureList(SKRect rectForChildrenPixels, float scale)
    {
        // Cancel any ongoing background measurement when starting fresh measurement
        CancelBackgroundMeasurement();

        if (IsTemplated && ItemsSource.Count > 0)
        {
            int measuredCount = 0;
            var itemsCount = ItemsSource.Count;
            ScaledSize measured = ScaledSize.Default;
            SKRect rectForChild = rectForChildrenPixels;

            SkiaControl[] nonTemplated = null;
            bool smartMeasuring = false;

            var stackHeight = 0.0f;
            var stackWidth = 0.0f;

            SkiaControl template = null;
            bool useOneTemplate = IsTemplated && RecyclingTemplate != RecyclingTemplate.Disabled;

            if (useOneTemplate)
            {
                template = ChildrenFactory.GetTemplateInstance();
            }

            var maybeSecondPass = true;
            List<SecondPassArrange> listSecondPass = new();
            bool stopMeasuring = false;

            var inflate = (float)this.VirtualisationInflated * scale;
            var visibleArea = base.GetOnScreenVisibleArea(new(null, rectForChildrenPixels, scale), new(inflate, inflate));

            if (visibleArea.Pixels.Height < 1 || visibleArea.Pixels.Width < 1)
            {
                return ScaledSize.CreateEmpty(scale);
            }

            var rowsCount = itemsCount;
            var columnsCount = 1;
            if (Type == LayoutType.Row)
            {
                rowsCount = 1;
                columnsCount = itemsCount;
            }

            var rows = new List<List<ControlInStack>>();
            var columns = new List<ControlInStack>(columnsCount);

            int index = -1;
            var cellsToRelease = new List<SkiaControl>();

            // For MeasureVisible strategy, limit initial measurement to visible area + buffer
            var effectiveRowsCount = rowsCount;
            if (MeasureItemsStrategy == MeasuringStrategy.MeasureVisible)
            {
                var estimatedItemHeight = 60f;
                var visibleAreaHeight = visibleArea.Pixels.Height;
                var estimatedVisibleItems = Math.Max(1, (int)Math.Ceiling(visibleAreaHeight / estimatedItemHeight));

                var bufferMultiplier = 3f;
                var initialMeasureCount = Math.Min(itemsCount, (int)(estimatedVisibleItems * bufferMultiplier));

                initialMeasureCount = Math.Max(20, Math.Min(200, initialMeasureCount));

                if (Type == LayoutType.Column)
                {
                    effectiveRowsCount = Math.Min(rowsCount, initialMeasureCount);
                }
                else if (Type == LayoutType.Row)
                {
                    effectiveRowsCount = Math.Min(rowsCount, initialMeasureCount);
                }

                Debug.WriteLine($"[MeasureList] INITIAL MEASURE: {effectiveRowsCount} items out of {itemsCount} total (visible area: {visibleAreaHeight:F1}px, estimated per item: {estimatedItemHeight}px)");
            }

            try
            {
                // Initial measurement loop (same as before)
                for (var row = 0; row < effectiveRowsCount; row++)
                {
                    if (stopMeasuring || index + 2 > itemsCount)
                        break;

                    var rowMaxHeight = 0.0f;
                    var maxWidth = 0.0f;

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
                            if (index + 2 > itemsCount)
                            {
                                stopMeasuring = true;
                                break;
                            }

                            index++;
                            var cell = new ControlInStack() { Column = column, Row = row, ControlIndex = index };

                            SkiaControl child = ChildrenFactory.GetViewForIndex(cell.ControlIndex, template, 0, true);

                            if (template == null)
                            {
                                cellsToRelease.Add(child);
                            }

                            if (child == null)
                            {
                                Super.Log($"[MeasureStack] FAILED to get child at index {cell.ControlIndex}");
                                return ScaledSize.Default;
                            }

                            if (column == 0)
                                rectForChild.Top += GetSpacingForIndex(row, scale);
                            rectForChild.Left += GetSpacingForIndex(column, scale);

                            if (!child.CanDraw)
                            {
                                cell.Measured = ScaledSize.Default;
                            }
                            else
                            {
                                var rectFitChild = new SKRect(rectForChild.Left, rectForChild.Top,
                                    rectForChild.Left + widthPerColumn, rectForChild.Bottom);
                                measured = MeasureAndArrangeCell(rectFitChild, cell, child, rectForChildrenPixels, scale);

                                if (!visibleArea.Pixels.IntersectsWithInclusive(cell.Destination))
                                {
                                    stopMeasuring = true;
                                    break;
                                }

                                cell.Measured = measured;
                                cell.WasMeasured = true;

                                // Store in sliding window cache
                                _measuredItems[cell.ControlIndex] = new MeasuredItemInfo
                                {
                                    Cell = cell,
                                    LastAccessed = DateTime.UtcNow,
                                    IsInViewport = true
                                };

                                measuredCount++;

                                if (!measured.IsEmpty)
                                {
                                    maxWidth += measured.Pixels.Width + GetSpacingForIndex(column, scale);

                                    if (measured.Pixels.Height > rowMaxHeight)
                                        rowMaxHeight = measured.Pixels.Height;

                                    rectForChild.Left += (float)(measured.Pixels.Width);
                                }
                            }

                            columns.Add(cell);
                        }
                        catch (Exception e)
                        {
                            Super.Log(e);
                            break;
                        }
                    }

                    rows.Add(columns);
                    columns = new();

                    if (maxWidth > stackWidth)
                        stackWidth = maxWidth;

                    stackHeight += rowMaxHeight + GetSpacingForIndex(row, scale);
                    rectForChild.Top += (float)(rowMaxHeight);
                    rectForChild.Left = 0;
                }
            }
            finally
            {
                foreach (var cell in cellsToRelease)
                {
                    ChildrenFactory.ReleaseViewInUse(cell.ContextIndex, cell);
                }
            }

            // Rest of the method stays the same until the return...
            // [Previous layout logic continues here]

            if (HorizontalOptions.Alignment == LayoutAlignment.Fill || SizeRequest.Width >= 0)
            {
                stackWidth = rectForChildrenPixels.Width;
            }

            if (VerticalOptions.Alignment == LayoutAlignment.Fill || SizeRequest.Height >= 0)
            {
                stackHeight = rectForChildrenPixels.Height;
            }

            // Second layout pass logic stays the same...
            var autoRight = rectForChildrenPixels.Right;
            if (this.HorizontalOptions != LayoutOptions.Fill)
            {
                autoRight = rectForChildrenPixels.Left + stackWidth;
            }

            var autoBottom = rectForChildrenPixels.Bottom;
            if (this.VerticalOptions != LayoutOptions.Fill)
            {
                autoBottom = rectForChildrenPixels.Top + stackHeight;
            }

            var autoRect = new SKRect(rectForChildrenPixels.Left, rectForChildrenPixels.Top, autoRight, autoBottom);

            foreach (var secondPass in listSecondPass)
            {
                // Second pass logic stays the same...
                if (float.IsInfinity(secondPass.Cell.Area.Bottom))
                {
                    secondPass.Cell.Area = new(secondPass.Cell.Area.Left, secondPass.Cell.Area.Top,
                        secondPass.Cell.Area.Right, secondPass.Cell.Area.Top + stackHeight);
                }
                else if (float.IsInfinity(secondPass.Cell.Area.Top))
                {
                    secondPass.Cell.Area = new(secondPass.Cell.Area.Left, secondPass.Cell.Area.Bottom - stackHeight,
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
                    secondPass.Cell.Area = new(secondPass.Cell.Area.Right - stackWidth, secondPass.Cell.Area.Top,
                        secondPass.Cell.Area.Right, secondPass.Cell.Area.Bottom);
                }

                if (secondPass.Cell.Area.Width > stackWidth)
                {
                    secondPass.Cell.Area = new(secondPass.Cell.Area.Left, secondPass.Cell.Area.Top,
                        secondPass.Cell.Area.Left + stackWidth, secondPass.Cell.Area.Bottom);
                }

                LayoutCell(secondPass.Child.MeasuredSize, secondPass.Cell, secondPass.Child, autoRect, secondPass.Scale);
            }

            if (HorizontalOptions.Alignment == LayoutAlignment.Fill && WidthRequest < 0)
            {
                stackWidth = rectForChildrenPixels.Width;
            }

            if (VerticalOptions.Alignment == LayoutAlignment.Fill && HeightRequest < 0)
            {
                stackHeight = rectForChildrenPixels.Height;
            }

            var structure = new LayoutStructure(rows);
            StackStructureMeasured = structure;

            FirstVisibleIndex = -1;
            FirstMeasuredIndex = 0;
            LastVisibleIndex = -1;
            LastMeasuredIndex = measuredCount - 1;

            if (measuredCount > 0)
            {
                if (this.Type == LayoutType.Column)
                {
                    var medium = stackHeight / measuredCount;

                    if (MeasureItemsStrategy == MeasuringStrategy.MeasureVisible && measuredCount < itemsCount)
                    {
                        var estimatedTotalHeight = medium * itemsCount;
                        stackHeight = estimatedTotalHeight;
                    }
                    else
                    {
                        stackHeight = medium * itemsCount;
                    }
                }
                else if (this.Type == LayoutType.Row)
                {
                    var medium = stackWidth / measuredCount;

                    if (MeasureItemsStrategy == MeasuringStrategy.MeasureVisible && measuredCount < itemsCount)
                    {
                        var estimatedTotalWidth = medium * itemsCount;
                        stackWidth = estimatedTotalWidth;
                    }
                    else
                    {
                        stackWidth = medium * itemsCount;
                    }
                }
            }

            _listAdditionalMeasurements = 0;

            if (template != null)
            {
                ChildrenFactory.ReleaseTemplateInstance(template);
            }

            // Start background measurement if using MeasureVisible strategy
            if (MeasureItemsStrategy == MeasuringStrategy.MeasureVisible
                && measuredCount < itemsCount)
            {
                if (_pendingMeasurements.Count == 0)
                {
                    StartBackgroundMeasurement(rectForChildrenPixels, scale, measuredCount);
                }
                else
                {
                    Debug.WriteLine($"[MeasureList] have unapplied measurements, wil not continue measuring in background.");
                }
            }

            // Debug: Report actual measurement results
            if (MeasureItemsStrategy == MeasuringStrategy.MeasureVisible)
            {
                Debug.WriteLine($"[MeasureList] COMPLETED: Actually measured {measuredCount} items, estimated total size: {(Type == LayoutType.Column ? stackHeight : stackWidth):F1}px. Background measurement started for remaining {itemsCount - measuredCount} items.");
            }

            return ScaledSize.FromPixels(stackWidth, stackHeight, scale);
        }

        return ScaledSize.FromPixels(rectForChildrenPixels.Width, rectForChildrenPixels.Height, scale);
    }

    /// <summary>
    /// Updates estimated content size based on current measurements
    /// </summary>
    private void UpdateEstimatedContentSize(float scale)
    {
        if (_measuredItems.Count == 0)
            return;

        var totalItems = ItemsSource?.Count ?? 0;
        if (totalItems == 0)
            return;

        var measuredHeights = _measuredItems.Values
            .Where(x => x.Cell.WasMeasured)
            .Select(x => x.Cell.Measured.Pixels.Height)
            .Where(h => h > 0)
            .ToList();

        if (measuredHeights.Count > 0 && Type == LayoutType.Column)
        {
            var averageHeight = measuredHeights.Average();
            var estimatedTotalHeight = averageHeight * totalItems;

            // Update the measured size if estimate is larger
            if (estimatedTotalHeight > MeasuredSize.Pixels.Height)
            {
                SetMeasured(MeasuredSize.Pixels.Width, estimatedTotalHeight, false, false, scale);
                Debug.WriteLine($"[UpdateEstimatedContentSize] Updated estimated height to {estimatedTotalHeight:F1}px based on {measuredHeights.Count} measured items (avg: {averageHeight:F1}px)");
            }
        }
    }

    /// <summary>
    /// Measures a batch of items in background thread
    /// </summary>
    private List<MeasuredItemInfo> MeasureBatchInBackground(SKRect constraints, float scale, int startIndex, int count, float startX, float startY, int startRow, int startCol, CancellationToken cancellationToken)
    {
        var measuredBatch = new List<MeasuredItemInfo>();

        SkiaControl template = null;
        bool useOneTemplate = IsTemplated && RecyclingTemplate != RecyclingTemplate.Disabled;
        var cellsToRelease = new List<SkiaControl>();

        try
        {
            if (useOneTemplate)
            {
                template = ChildrenFactory.GetTemplateInstance();
            }

            var columnsCount = (Split > 0) ? Split : 1;
            var columnWidth = ComputeColumnWidth(columnsCount);

            // Initialize positioning variables from parameters (thread-safe)
            float currentX = startX;
            float currentY = startY;
            int row = startRow;
            int col = startCol;
            float rowHeight = 0f;

            float availableWidth = columnWidth;
            float availableHeight = float.PositiveInfinity;

            if (this.Type == LayoutType.Row)
            {
                availableHeight = columnWidth;
                availableWidth = float.PositiveInfinity;
            }

            for (int i = 0; i < count && !cancellationToken.IsCancellationRequested; i++)
            {
                var itemIndex = startIndex + i;

                // Check if already measured
                if (_measuredItems.ContainsKey(itemIndex))
                    continue;

                // Add spacing for this position
                if (Type == LayoutType.Column && i > 0)
                {
                    currentY += GetSpacingForIndex(row, scale);
                }

                var child = ChildrenFactory.GetViewForIndex(itemIndex, template, 0, true);
                if (template == null && child != null)
                {
                    cellsToRelease.Add(child);
                }

                if (child?.CanDraw == true)
                {
                    // Create proper destination rect with actual positioning
                    var rectForChild = new SKRect(
                        currentX,
                        currentY,
                        currentX + availableWidth,
                        currentY + availableHeight
                    );

                    var cell = new ControlInStack
                    {
                        ControlIndex = itemIndex,
                        Column = col,
                        Row = row,
                        Destination = rectForChild
                    };

                    var measured = MeasureAndArrangeCell(rectForChild, cell, child, constraints, scale);
                    cell.Measured = measured;
                    cell.WasMeasured = true;

                    // Update max row height
                    if (measured.Pixels.Height > rowHeight)
                        rowHeight = measured.Pixels.Height;

                    measuredBatch.Add(new MeasuredItemInfo
                    {
                        Cell = cell,
                        LastAccessed = DateTime.UtcNow,
                        IsInViewport = false
                    });

                    // Move to next column
                    col++;
                    if (col >= columnsCount)
                    {
                        // Complete row - move to next row
                        row++;
                        col = 0;
                        currentX = 0f;
                        currentY += rowHeight + (float)(Spacing * scale);
                        rowHeight = 0f;
                    }
                    else
                    {
                        // Move to next column horizontally
                        currentX += columnWidth + (float)(Spacing * scale);
                    }

                    Debug.WriteLine($"[MeasureBatchInBackground] Measured item {itemIndex} at ({cell.Destination.Left:F1},{cell.Destination.Top:F1}): {measured.Pixels.Width:F1}x{measured.Pixels.Height:F1}");
                }
            }
        }
        finally
        {
            if (template != null)
            {
                ChildrenFactory.ReleaseTemplateInstance(template);
            }
            foreach (var cell in cellsToRelease)
            {
                ChildrenFactory.ReleaseViewInUse(cell.ContextIndex, cell);
            }
        }

        return measuredBatch;
    }

    /// <summary>
    /// Integrates measured batch into the main structure
    /// </summary>
    private void IntegrateMeasuredBatch(List<MeasuredItemInfo> measuredBatch, float scale)
    {
        if (measuredBatch?.Count > 0)
        {
            foreach (var item in measuredBatch)
            {
                _measuredItems[item.Cell.ControlIndex] = item;
            }

            // Update LastMeasuredIndex
            var maxIndex = measuredBatch.Max(x => x.Cell.ControlIndex);
            if (maxIndex > LastMeasuredIndex)
            {
                LastMeasuredIndex = maxIndex;
            }

            // Stage for rendering pipeline integration
            lock (_pendingLock)
            {
                _pendingMeasurements.Add(measuredBatch);
            }

            // Recalculate estimated content size
            //UpdateEstimatedContentSize(scale);

            Debug.WriteLine($"[IntegrateMeasuredBatch] Integrated {measuredBatch.Count} items, total measured: {_measuredItems.Count}, last index: {LastMeasuredIndex}");
        }
    }

    /// <summary>
    /// Applies background measurement results to StackStructure - called from rendering pipeline
    /// </summary>
    public void ApplyBackgroundMeasurementResult()
    {
        List<List<MeasuredItemInfo>> batchesToProcess = null;

        // Get all pending batches atomically
        lock (_pendingLock)
        {
            if (_pendingMeasurements.Count == 0)
                return;

            // Copy and clear in one atomic operation
            batchesToProcess = new List<List<MeasuredItemInfo>>(_pendingMeasurements);
            _pendingMeasurements.Clear();
        }

        // Process outside the lock for maximum performance
        var allRows = new List<List<ControlInStack>>();
        var columnsCount = (Split > 0) ? Split : 1;

        foreach (var batch in batchesToProcess)
        {
            // Group batch items into rows based on columnsCount
            var currentRow = new List<ControlInStack>(columnsCount);

            foreach (var item in batch)
            {
                currentRow.Add(item.Cell);

                // Complete row when we reach columnsCount
                if (currentRow.Count >= columnsCount)
                {
                    allRows.Add(currentRow);
                    currentRow = new List<ControlInStack>(columnsCount);
                }
            }

            // Add incomplete row if it has items
            if (currentRow.Count > 0)
            {
                allRows.Add(currentRow);
            }
        }

        // Append to StackStructure if we have rows to add
        if (allRows.Count > 0)
        {
            if (StackStructure == null)
            {
                StackStructure = new LayoutStructure(allRows);
            }
            else
            {
                StackStructure.Append(allRows);
            }

            // Update content size with progressive accuracy as we approach the end
            UpdateProgressiveContentSize();

            Debug.WriteLine($"[ApplyBackgroundMeasurementResult] Applied {allRows.Count} rows from {batchesToProcess.Count} batches to StackStructure. Measured: {MeasuredItemsPercentage:P1}");
        }
    }

    /// <summary>
    /// Updates content size with progressive accuracy as we approach measuring all items
    /// </summary>
    private void UpdateProgressiveContentSize()
    {
        if (StackStructure == null || ItemsSource?.Count == 0)
            return;

        var totalItems = ItemsSource.Count;
        var measuredCount = LastMeasuredIndex + 1;
        var progress = MeasuredItemsPercentage;

        if (Type == LayoutType.Column)
        {
            // Calculate actual measured height from structure
            var actualMeasuredHeight = 0f;
            var measuredItems = StackStructure.GetChildren().Take(measuredCount);
            foreach (var item in measuredItems)
            {
                actualMeasuredHeight += item.Measured.Pixels.Height;
            }

            // Add spacing between items
            var spacingHeight = (measuredCount - 1) * (float)(Spacing * RenderingScale);
            actualMeasuredHeight += spacingHeight;

            float newContentHeight;

            if (progress >= 1.0f)
            {
                // 100% measured - use exact size
                newContentHeight = actualMeasuredHeight;
                Debug.WriteLine($"[UpdateProgressiveContentSize] 100% measured - exact height: {newContentHeight:F1}px");
            }
            else if (progress >= 0.9f)
            {
                // 90%+ measured - use precise estimate with small buffer
                var averageHeight = actualMeasuredHeight / measuredCount;
                var estimatedTotal = averageHeight * totalItems;
                var buffer = estimatedTotal * 0.05f; // 5% buffer for final items
                newContentHeight = estimatedTotal + buffer;
                Debug.WriteLine($"[UpdateProgressiveContentSize] {progress:P1} measured - precise estimate: {newContentHeight:F1}px (avg: {averageHeight:F1}px)");
            }
            else if (progress >= 0.5f)
            {
                // 50%+ measured - blend between estimate and large buffer
                var averageHeight = actualMeasuredHeight / measuredCount;
                var estimatedTotal = averageHeight * totalItems;
                var buffer = estimatedTotal * (0.5f - progress * 0.4f); // Decreasing buffer as we approach 90%
                newContentHeight = estimatedTotal + buffer;
                Debug.WriteLine($"[UpdateProgressiveContentSize] {progress:P1} measured - blended estimate: {newContentHeight:F1}px (buffer: {buffer:F1}px)");
            }
            else
            {
                // Less than 50% measured - use large estimate to allow scrolling
                var averageHeight = actualMeasuredHeight / measuredCount;
                var estimatedTotal = averageHeight * totalItems;
                var buffer = estimatedTotal * 1.0f; // 100% buffer for early measurements
                newContentHeight = estimatedTotal + buffer;
                Debug.WriteLine($"[UpdateProgressiveContentSize] {progress:P1} measured - early estimate: {newContentHeight:F1}px (large buffer)");
            }

            // Only update if the new size is different enough to matter
            var currentHeight = MeasuredSize.Pixels.Height;
            if (Math.Abs(newContentHeight - currentHeight) > 10f) // 10px threshold
            {
                SetMeasured(MeasuredSize.Pixels.Width, newContentHeight, false, false, RenderingScale);
                Debug.WriteLine($"[UpdateProgressiveContentSize] Updated height from {currentHeight:F1}px to {newContentHeight:F1}px");
            }
        }
        else if (Type == LayoutType.Row)
        {
            // Similar logic for horizontal scrolling
            var actualMeasuredWidth = 0f;
            var measuredItems = StackStructure.GetChildren().Take(measuredCount);
            foreach (var item in measuredItems)
            {
                actualMeasuredWidth += item.Measured.Pixels.Width;
            }

            var spacingWidth = (measuredCount - 1) * (float)(Spacing * RenderingScale);
            actualMeasuredWidth += spacingWidth;

            float newContentWidth;

            if (progress >= 1.0f)
            {
                newContentWidth = actualMeasuredWidth;
            }
            else if (progress >= 0.9f)
            {
                var averageWidth = actualMeasuredWidth / measuredCount;
                var estimatedTotal = averageWidth * totalItems;
                newContentWidth = estimatedTotal + (estimatedTotal * 0.05f);
            }
            else if (progress >= 0.5f)
            {
                var averageWidth = actualMeasuredWidth / measuredCount;
                var estimatedTotal = averageWidth * totalItems;
                var buffer = estimatedTotal * (0.5f - progress * 0.4f);
                newContentWidth = estimatedTotal + buffer;
            }
            else
            {
                var averageWidth = actualMeasuredWidth / measuredCount;
                var estimatedTotal = averageWidth * totalItems;
                newContentWidth = estimatedTotal + (estimatedTotal * 1.0f);
            }

            var currentWidth = MeasuredSize.Pixels.Width;
            if (Math.Abs(newContentWidth - currentWidth) > 10f)
            {
                SetMeasured(newContentWidth, MeasuredSize.Pixels.Height, false, false, RenderingScale);
                Debug.WriteLine($"[UpdateProgressiveContentSize] Updated width from {currentWidth:F1}px to {newContentWidth:F1}px");
            }
        }
    }

    /// <summary>
    /// Applies sliding window cleanup to maintain memory limits
    /// </summary>
    private void ApplySlidingWindowCleanup()
    {
        if (_measuredItems.Count <= SLIDING_WINDOW_SIZE)
            return;

        var currentViewportStart = Math.Max(0, FirstVisibleIndex - BEHIND_BUFFER);
        var currentViewportEnd = LastVisibleIndex + AHEAD_BUFFER;

        var itemsToRemove = _measuredItems
            .Where(kvp => kvp.Key < currentViewportStart || kvp.Key > currentViewportEnd)
            .OrderBy(kvp => kvp.Value.LastAccessed)
            .Take(_measuredItems.Count - SLIDING_WINDOW_SIZE)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var indexToRemove in itemsToRemove)
        {
            _measuredItems.TryRemove(indexToRemove, out _);
        }

        if (itemsToRemove.Count > 0)
        {
            Debug.WriteLine($"[ApplySlidingWindowCleanup] Removed {itemsToRemove.Count} measured items, kept {_measuredItems.Count} items in memory");
        }
    }


    /// <summary>
    /// Background measurement implementation with sliding window
    /// </summary>
    private async Task BackgroundMeasureItems(SKRect constraints, float scale, int startIndex, CancellationToken cancellationToken)
    {
        var totalItems = ItemsSource.Count;
        var currentBatchStart = startIndex;
        var maxIterations = Math.Max(1, (totalItems / MEASUREMENT_BATCH_SIZE) + 10); // Safety limit
        var iterationCount = 0;

        Debug.WriteLine($"[BackgroundMeasureItems] Starting background measurement from index {startIndex} of {totalItems} total items");

        while (currentBatchStart < totalItems && !cancellationToken.IsCancellationRequested && iterationCount < maxIterations)
        {

            lock (_pendingLock)
            {
                if (_pendingMeasurements.Count > 0)
                {
                    break;
                }
            }

            iterationCount++;
            var batchEnd = Math.Min(currentBatchStart + MEASUREMENT_BATCH_SIZE, totalItems);
            var itemsToMeasure = batchEnd - currentBatchStart;

            // Safety check to prevent infinite loops
            if (itemsToMeasure <= 0)
            {
                Debug.WriteLine($"[BackgroundMeasureItems] WARNING: No items to measure in batch {currentBatchStart}-{batchEnd}, breaking loop");
                break;
            }

            Debug.WriteLine($"[BackgroundMeasureItems] Measuring batch {currentBatchStart}-{batchEnd - 1} ({itemsToMeasure} items) [iteration {iterationCount}/{maxIterations}]");

            // Get positioning data on main thread (thread-safe read)
            var structure = LatestStackStructure;
            var (startX, startY, startRow, startCol) = GetNextItemPositionForIncremental(structure);

            // Measure batch on background thread
            var measuredBatch = await Task.Run(() => MeasureBatchInBackground(
                constraints, scale, currentBatchStart, itemsToMeasure, startX, startY, startRow, startCol, cancellationToken), cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                Debug.WriteLine($"[BackgroundMeasureItems] Cancellation requested, stopping at batch {currentBatchStart}");
                break;
            }

            // Integrate results on background thread (safe for reading/staging)
            if (!cancellationToken.IsCancellationRequested)
            {
                IntegrateMeasuredBatch(measuredBatch, scale);
                ApplySlidingWindowCleanup();
            }

            _backgroundMeasurementProgress = batchEnd;

            // Move to next batch - CRITICAL: This was missing!
            currentBatchStart = batchEnd;

            // Small delay to prevent overwhelming the system
            await Task.Delay(10, cancellationToken);
        }

        if (iterationCount >= maxIterations)
        {
            Debug.WriteLine($"[BackgroundMeasureItems] WARNING: Hit maximum iteration limit ({maxIterations}), stopping background measurement");
        }

        Debug.WriteLine($"[BackgroundMeasureItems] Completed background measurement up to index {_backgroundMeasurementProgress}");
    }

    /// <summary>
    /// Renders Templated Column/Row todo in some cases..
    /// </summary>
    /// <param name="structure"></param>
    /// <param name="context"></param>
    /// <param name="destination"></param>
    /// <param name="scale"></param>
    /// <returns></returns>
    protected virtual int DrawList(
        DrawingContext ctx,
        LayoutStructure structure)
    {
        if (!IsTemplated || IsDisposing)
            return 0;

        //StackStructure was creating inside Measure.
        //While scrolling templated its not called again (checked).

        List<SkiaControlWithRect> tree = new();
        bool wasVisible = false;
        var needrebuild = templatesInvalidated;
        int countRendered = 0;
        int visibleIndex = -1;
        int visibleIndexEnd = -1;

        if (structure != null)
        {
            var cellsToRelease = new List<SkiaControl>();

            try
            {
                //draw children manually
                var inflate = (float)this.VirtualisationInflated * ctx.Scale;
                var visibleArea = GetOnScreenVisibleArea(ctx, new(inflate, inflate));

                var currentIndex = -1;
                foreach (var cell in structure.GetChildrenAsSpans())
                {
                    currentIndex++;

                    if (!cell.WasMeasured)
                    {
                        continue;
                    }

                    if (cell.Destination == SKRect.Empty || cell.Measured.Pixels.IsEmpty)
                    {
                        cell.IsVisible = false;
                    }
                    else
                    {
                        //cell.Destination is what was measured, and we got x,y offsets from a parent, like scroll

                        var x = ctx.Destination.Left + cell.Destination.Left;
                        var y = ctx.Destination.Top + cell.Destination.Top;

                        cell.Drawn.Set(x, y, x + cell.Destination.Width, y + cell.Destination.Height);

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
                                cell.IsVisible = cell.Drawn.IntersectsWith(visibleArea.Pixels);
                            }
                        }
                        else
                        {
                            cell.IsVisible = true;
                        }
                    }

                    if (cell.IsVisible)
                    {
                        if (visibleIndex < 0 && currentIndex > visibleIndex)
                        {
                            visibleIndex = currentIndex;
                        }

                        var child = ChildrenFactory.GetViewForIndex(cell.ControlIndex, null,
                            GetSizeKey(cell.Measured.Pixels));

                        cellsToRelease.Add(child);

                        if (child == null) //ChildrenFactory.GetChildAt was unable to return child?..
                        {
                            return countRendered;
                        }

                        if (child is SkiaControl control && child.IsVisible)
                        {
                            if (child.NeedMeasure)
                            {
                                if (!child.WasMeasured || GetSizeKey(child.MeasuredSize.Pixels) !=
                                    GetSizeKey(cell.Measured.Pixels))
                                {
                                    child.Measure((float)cell.Area.Width, (float)cell.Area.Height, ctx.Scale);
                                }
                            }

                            SKRect destinationRect;
                            if (IsTemplated && RecyclingTemplate != RecyclingTemplate.Disabled)
                            {
                                //when context changes we need all available space for remeasuring cell
                                destinationRect = new SKRect(cell.Drawn.Left, cell.Drawn.Top,
                                    cell.Drawn.Left + cell.Area.Width, cell.Drawn.Top + cell.Area.Bottom);
                            }
                            else
                            {
                                destinationRect = new SKRect(cell.Drawn.Left, cell.Drawn.Top, cell.Drawn.Right,
                                    cell.Drawn.Bottom);
                            }


                            if (IsRenderingWithComposition)
                            {
                                if (DirtyChildrenInternal.Contains(child))
                                {
                                    DrawChild(ctx.WithDestination(destinationRect), child);
                                    countRendered++;
                                }
                                else
                                {
                                    //skip drawing but need arrange :(
                                    //todo set virtual offset between drawnrect and the new
                                    child.Arrange(destinationRect, child.SizeRequest.Width, child.SizeRequest.Height,
                                        ctx.Scale);
                                }
                            }
                            else
                            {
                                DrawChild(ctx.WithDestination(destinationRect), child);
                                countRendered++;
                            }

                            //gonna use that for gestures and for item inside viewport detection and for hotreload children tree
                            tree.Add(new SkiaControlWithRect(control,
                                destinationRect,
                                control.DrawingRect,
                                currentIndex,
                                -1, // Default freeze index
                                control.BindingContext)); // Capture current binding context
                        }
                    }

                    if (!cell.IsVisible)
                    {
                        if (visibleIndexEnd < 0 && currentIndex > visibleIndexEnd)
                        {
                            visibleIndexEnd = currentIndex - 1;
                        }

                        ChildrenFactory.MarkViewAsHidden(cell.ControlIndex);
                    }
                }
            }
            finally
            {
                foreach (var cell in cellsToRelease)
                {
                    ChildrenFactory.ReleaseViewInUse(cell.ContextIndex, cell);
                }
            }
        }

        FirstVisibleIndex = visibleIndex;
        LastVisibleIndex = visibleIndexEnd;

        if (needrebuild && countRendered > 0)
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

        return countRendered;
    }

    private MeasuredListCells _measuredCells;

    public int EstimatedTotalItems => ItemsSource?.Count ?? 0;

    // Returns how far we have measured content in units (vertical or horizontal)
    public double GetMeasuredContentEnd()
    {
        var structure = LatestStackStructure;
        if (structure != null)
        {
            var last = LatestStackStructure.GetChildren().LastOrDefault();
            if (last != null)
            {
                if (Type == LayoutType.Column)
                {
                    return last.Destination.Top / RenderingScale;
                }

                if (Type == LayoutType.Row)
                {
                    return last.Destination.Left / RenderingScale;
                }
            }
        }

        return double.PositiveInfinity;
    }

    /// <summary>
    /// Gets estimated total content size for virtualized lists with unmeasured items
    /// </summary>
    public ScaledSize GetEstimatedContentSize(float scale)
    {
        if (!IsTemplated || ItemsSource == null || ItemsSource.Count == 0)
            return MeasuredSize;

        var itemsCount = ItemsSource.Count;
        var measuredCount = LastMeasuredIndex + 1;
        
        if (measuredCount >= itemsCount)
            return MeasuredSize; // All items measured, use actual size

        var structure = LatestStackStructure;
        if (structure == null || measuredCount == 0)
        {
            // No items measured yet, use rough estimate
            var defaultItemHeight = 60f * scale; // Fallback estimate
            var estimatedHeight = itemsCount * defaultItemHeight;
            return ScaledSize.FromPixels(MeasuredSize.Pixels.Width, estimatedHeight, scale);
        }

        if (Type == LayoutType.Column)
        {
            // Calculate average height from measured items
            var measuredHeight = 0f;
            var measuredItems = structure.GetChildren().Take(measuredCount);
            foreach (var item in measuredItems)
            {
                measuredHeight += item.Measured.Pixels.Height;
            }
            
            var averageHeight = measuredHeight / measuredCount;
            var estimatedTotalHeight = averageHeight * itemsCount;
            
            Debug.WriteLine($"[GetEstimatedContentSize] Measured {measuredCount}/{itemsCount} items, avg height: {averageHeight:F1}px, estimated total: {estimatedTotalHeight:F1}px");
            
            return ScaledSize.FromPixels(MeasuredSize.Pixels.Width, estimatedTotalHeight, scale);
        }
        else if (Type == LayoutType.Row)
        {
            // Calculate average width from measured items
            var measuredWidth = 0f;
            var measuredItems = structure.GetChildren().Take(measuredCount);
            foreach (var item in measuredItems)
            {
                measuredWidth += item.Measured.Pixels.Width;
            }
            
            var averageWidth = measuredWidth / measuredCount;
            var estimatedTotalWidth = averageWidth * itemsCount;
            
            Debug.WriteLine($"[GetEstimatedContentSize] Measured {measuredCount}/{itemsCount} items, avg width: {averageWidth:F1}px, estimated total: {estimatedTotalWidth:F1}px");
            
            return ScaledSize.FromPixels(estimatedTotalWidth, MeasuredSize.Pixels.Height, scale);
        }

        return MeasuredSize;
    }


    private (float x, float y, int row, int col) GetNextItemPositionForIncremental(LayoutStructure structure)
    {
        if (structure.GetCount() == 0)
        {
            // No items measured yet
            return (0f, 0f, 0, 0);
        }

        var lastItem = structure.GetChildren().Last();

        int lastRow = lastItem.Row;
        int lastCol = lastItem.Column;
        int nextRow = lastRow;
        int nextCol = lastCol + 1;

        int columnsCount = (Split > 0) ? Split : 1;
        if (nextCol >= columnsCount)
        {
            // start a new row
            nextRow = lastRow + 1;
            nextCol = 0;
        }

        float startX = 0f;
        float startY = 0f;

        if (this.Type == LayoutType.Column)
        {
            startY = ComputeBottomOfRow(structure, lastRow) + (float)(Spacing * RenderingScale);
        }
        else
        {
            startX = ComputeRightOfColumn(structure, lastRow) + (float)(Spacing * RenderingScale);
        }

        // If we are placing item in the same row must find the position after last col
        if (nextCol > 0)
        {
            float columnWidth = ComputeColumnWidth(columnsCount);
            startX = nextCol * (columnWidth + (float)(Spacing * RenderingScale));
        }

        return (startX, startY, nextRow, nextCol);
    }

    private float ComputeColumnWidth(int columnsCount)
    {
        if (this.Type == LayoutType.Column)
        {
            return (float)Math.Round(columnsCount > 1
                ? (MeasuredSize.Pixels.Width - (columnsCount - 1) * Spacing * RenderingScale) / columnsCount
                : MeasuredSize.Pixels.Width);
        }
        else
        {
            return MeasuredSize.Pixels.Width;
        }
    }


    private float ComputeRightOfColumn(LayoutStructure structure, int row)
    {
        var cell = structure.GetRow(row).Last();
        var right = cell.Area.Left + cell.Measured.Pixels.Width;
        return right;
    }

    private float ComputeBottomOfRow(LayoutStructure structure, int row)
    {
        // Find the max bottom of all items in that row
        float maxBottom = 0f;
        foreach (var cell in structure.GetRow(row))
        {
            var bottom = cell.Area.Top + cell.Measured.Pixels.Height;
            if (bottom > maxBottom)
                maxBottom = bottom;
        }

        return maxBottom;
    }

    private void AppendRowsToStructureMeasured(List<List<ControlInStack>> rows)
    {
        var structure = LatestStackStructure.Clone();
        structure.Append(rows);
        StackStructureMeasured = structure;
    }

    public int MeasureAdditionalItems(int batchSize, int aheadCount, float scale)
    {
        if (ItemsSource == null || ItemsSource.Count == 0)
            return 0;

        int startIndex = LastMeasuredIndex + 1;
        int endIndex = Math.Min(startIndex + batchSize + aheadCount, ItemsSource.Count);

        Debug.WriteLine($"[MeasureAdditionalItems] INCREMENTAL: Measuring items {startIndex}-{endIndex - 1} (batch: {batchSize}, ahead: {aheadCount})");

        if (startIndex >= endIndex)
            return 0;

        int countToMeasure = endIndex - startIndex;
        if (countToMeasure <= 0)
            return 0;

        var structure = LatestMeasuredStackStructure.Clone();
        var (startX, startY, startRow, startCol) = GetNextItemPositionForIncremental(structure);
        int columnsCount = (Split > 0) ? Split : 1;

        float columnWidth = ComputeColumnWidth(columnsCount);
        float availableWidth = columnWidth;
        float availableHeight = float.PositiveInfinity;

        if (this.Type == LayoutType.Row)
        {
            availableHeight = columnWidth;
            availableWidth = float.PositiveInfinity;
        }

        var rows = new List<List<ControlInStack>>();
        var cols = new List<ControlInStack>(columnsCount);
        float currentX = startX;
        float currentY = startY;
        float rowHeight = 0f;

        int currentIndex = startIndex;
        int row = startRow;
        int col = startCol;

        float rowWidth = 0;
        var stackHeight = 0.0f;
        var stackWidth = 0.0f;

        SkiaControl template = null;
        bool useOneTemplate = IsTemplated && RecyclingTemplate != RecyclingTemplate.Disabled;

        if (useOneTemplate)
        {
            template = ChildrenFactory.GetTemplateInstance();
        }

        // Measure!
        var cellsToRelease = new List<SkiaControl>();

        try
        {
            while (currentIndex < endIndex)
            {
                stackHeight += GetSpacingForIndex(row, scale);

                var child = ChildrenFactory.GetViewForIndex(currentIndex, template, 0, true);
                if (template == null)
                {
                    cellsToRelease.Add(child);
                }

                if (child == null)
                {
                    return 0;
                }

                var rectForChild = new SKRect(
                    currentX,
                    currentY,
                    currentX + availableWidth,
                    currentY + availableHeight
                );

                var cell = new ControlInStack { ControlIndex = currentIndex, Destination = rectForChild, };

                var measured = MeasureAndArrangeCell(rectForChild, cell, child, rectForChild, scale);
                cols.Add(cell);

                // Update max row height
                if (measured.Pixels.Height > rowHeight)
                    rowHeight = measured.Pixels.Height;

                rowWidth += measured.Pixels.Width + GetSpacingForIndex(col, scale);

                // Move to next column
                col++;
                if (col >= columnsCount)
                {
                    // The row is complete
                    // Add this completed row to newRows
                    rows.Add(cols);

                    stackHeight += rowHeight;
                    stackWidth +=

                        // start next row
                        row++;
                    col = 0;
                    currentX = 0f;
                    currentY += rowHeight + (float)(Spacing * RenderingScale);
                    rowWidth = 0;
                    rowHeight = 0;
                    cols = new List<ControlInStack>(columnsCount);
                }
                else
                {
                    // Move to next column horizontally
                    currentX += columnWidth + (float)(Spacing * RenderingScale);
                }

                if (rowWidth > stackWidth)
                    stackWidth = rowWidth;

                currentIndex++;
            }

            structure.Append(rows);
            StackStructureMeasured = structure;

            LastMeasuredIndex = startIndex + countToMeasure - 1;

            SKSize newSizePixels;
            var existingHeight = MeasuredSize.Pixels.Height;
            var existingWidth = MeasuredSize.Pixels.Width;

            if (Type == LayoutType.Column)
            {
                float spacingPixels = (float)(Spacing * scale);

                //first additional measurement
                if (_listAdditionalMeasurements == 0)
                {
                    //do not use approx size we have
                    stackHeight = structure.GetChildren().Sum(x => x.Measured.Pixels.Height) +
                        spacingPixels * structure.MaxRows - 1;
                }
                else
                {
                    if (_listAdditionalMeasurements == 1)
                    {
                        //add some more space to be able to scroll
                        stackHeight += 1500 * scale;
                    }

                    if (endIndex == ItemsSource.Count)
                    {
                        stackHeight -= 1500 * scale;
                    }

                    stackHeight += existingHeight + spacingPixels;
                }

                if (existingWidth > stackWidth)
                    stackWidth = existingWidth;

                newSizePixels = new(stackWidth, stackHeight);

                SetMeasured(newSizePixels.Width, newSizePixels.Height, false, false, scale);
            }

            _listAdditionalMeasurements++;
            
            Debug.WriteLine($"[MeasureAdditionalItems] COMPLETED: Measured {countToMeasure} additional items, now measured up to index {LastMeasuredIndex} of {ItemsSource.Count} total");

            return countToMeasure;
        }
        finally
        {
            if (template != null)
            {
                ChildrenFactory.ReleaseTemplateInstance(template);
            }
            else
            {
                foreach (var cell in cellsToRelease)
                {
                    ChildrenFactory.ReleaseViewInUse(cell.ContextIndex, cell);
                }
            }
        }
    }
}

public record MeasuredListCell(ControlInStack Cell, int Index);

public class MeasuredListCells : ReadOnlyCollection<MeasuredListCell>
{
    public MeasuredListCells(IList<MeasuredListCell> list) : base(list)
    {
    }
}
