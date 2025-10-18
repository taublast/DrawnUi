//reusing some code from #dotnetmaui Layout

using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace DrawnUi.Draw;

public partial class SkiaLayout
{
    public virtual void OnViewportWasChanged(ScaledRect viewport)
    {
        //RenderingViewport = new(viewport.Pixels);
        if (MeasureItemsStrategy == MeasuringStrategy.MeasureVisible)
        {
            if (WillDrawFromFreshItemssSource == 0 && ContentSize.IsEmpty && ItemsSource.Count > 0)
            {
                InvalidateMeasure();
            }
        }

        //cells will get OnScrolled
        ViewportWasChanged = true;
    }

    /// <summary>
    /// Determines whether LoadMore should be triggered based on viewport position and measurement state.
    /// This prevents race conditions by considering background measurement progress.
    /// </summary>
    public virtual bool ShouldTriggerLoadMore(ScaledRect viewport)
    {
        // No items source or not templated - can't load more
        if (!IsTemplated || ItemsSource == null || ItemsSource.Count == 0)
            return false;

        // Still measuring existing items in background - don't load more yet
        if (_isBackgroundMeasuring && _backgroundMeasurementProgress < ItemsSource.Count - 1)
        {
            Debug.WriteLine(
                $"[ShouldTriggerLoadMore] Still measuring items (progress: {_backgroundMeasurementProgress}/{ItemsSource.Count}), not triggering LoadMore");
            return false;
        }

        // Haven't finished measuring all existing items - don't load more yet
        if (LastMeasuredIndex < ItemsSource.Count - 1)
        {
            Debug.WriteLine(
                $"[ShouldTriggerLoadMore] Haven't measured all items yet (LastMeasuredIndex: {LastMeasuredIndex}/{ItemsSource.Count}), not triggering LoadMore");
            return false;
        }

        // Check if viewport is actually at the end of measured content
        return IsViewportAtEndOfMeasuredContent(viewport);
    }

    /// <summary>
    /// Checks if we allow scroll to load more
    /// </summary>
    protected virtual bool IsViewportAtEndOfMeasuredContent(ScaledRect viewport)
    {
        if (StackStructure == null || StackStructure.Length == 0)
            return false;

        if (MeasureItemsStrategy == MeasuringStrategy.MeasureVisible)
        {
            return LastMeasuredIndex == ItemsSource.Count - 1;
        }

        return true;
    }

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
    protected float MeasuredItemsPercentage =>
        ItemsSource?.Count > 0 ? (float)(LastMeasuredIndex + 1) / ItemsSource.Count : 0f;

    [DebuggerDisplay("{Type} {Count} at {StartIndex}, ")]
    /// <summary>
    /// Represents a pending structure change to be applied during rendering
    /// </summary>
    public class StructureChange
    {
        public StructureChange(StructureChangeType type, long stamp)
        {
            Type = type;
            Stamp = stamp;
        }

        public long Stamp { get; set; }
        public StructureChangeType Type { get; set; }
        public Vector2? OffsetOthers { get; set; }
        public int StartIndex { get; set; }
        public int Count { get; set; }
        public List<object> Items { get; set; } // For Add/Replace
        public int TargetIndex { get; set; } // For Move
        public List<MeasuredItemInfo> MeasuredItems { get; set; } // For BackgroundMeasurement
        public int? InsertAtIndex { get; set; } // Where to insert in existing structure
        public bool IsInsertOperation { get; set; } // Flag for insert vs append
        public bool IsVisible { get; set; } // For VisibilityChange

        // Background measurement offset compensation data
        public BackgroundMeasurementStartingPosition StartingPosition { get; set; }
    }

    /// <summary>
    /// Stores the starting position data when background measurement begins
    /// Used to detect and compensate for position changes due to visibility changes
    /// </summary>
    public class BackgroundMeasurementStartingPosition
    {
        public int LastRow { get; set; }
        public int LastCol { get; set; }
        public float ExpectedStartX { get; set; }
        public float ExpectedStartY { get; set; }
        public LayoutType LayoutType { get; set; }
    }

    /// <summary>
    /// Context for background measurement operations
    /// </summary>
    public class BackgroundMeasurementContext
    {
        public int? InsertAtIndex { get; set; }
        public int? InsertCount { get; set; }
        public int StartMeasuringFrom { get; set; }
        public bool IsInsertOperation => InsertAtIndex.HasValue;
        public bool IsSingleItemRemeasurement { get; set; }
        public int? SingleItemIndex { get; set; }
        public int? EndMeasuringAt { get; set; }
    }

    /// <summary>
    /// Types of structure changes that can be applied
    /// </summary>
    public enum StructureChangeType
    {
        Add,
        Remove,
        Replace,
        Move,
        Reset,
        BackgroundMeasurement,
        VisibilityChange,
        SingleItemUpdate
    }

    // Background measurement support
    private CancellationTokenSource _backgroundMeasurementCts;
    private Task _backgroundMeasurementTask;
    private readonly object _measurementLock = new object();
    private volatile bool _isBackgroundMeasuring = false;

    // Sliding window configuration
    private const int SLIDING_WINDOW_SIZE = 300; // Keep 300 measured items max
    private const int MEASUREMENT_BATCH_SIZE = 20; // Measure 20 items per batch
    private const int AHEAD_BUFFER = 100; // Measure 100 items ahead of visible area
    private const int BEHIND_BUFFER = 50; // Keep 50 items behind visible area

    // Track measurement state
    private readonly ConcurrentDictionary<int, MeasuredItemInfo> _measuredItems = new();
    private volatile int _backgroundMeasurementProgress = 0;

    // Universal structure changes staging for rendering pipeline integration
    private readonly object _structureChangesLock = new();
    private readonly List<StructureChange> _pendingStructureChanges = new();

    // Hybrid shifting system
    private const int DIRECT_SHIFT_THRESHOLD = 1000;
    private readonly Dictionary<int, int> _indexOffsets = new();
    private readonly HashSet<int> _removedIndices = new();

    /// <summary>
    /// Information about a measured item for sliding window management
    /// </summary>
    public class MeasuredItemInfo
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
    private void StartBackgroundMeasurement(SKRect constraints, float scale, int startFromIndex,
        BackgroundMeasurementContext context = null)
    {
        if (!IsTemplated || ItemsSource == null || ItemsSource.Count <= startFromIndex)
            return;

        // Check if we're already measuring this range or beyond to prevent duplicates
        lock (_measurementLock)
        {
            if (_isBackgroundMeasuring)
            {
                // If we're already measuring at or beyond this index, skip duplicate measurement
                if (_backgroundMeasurementProgress >= startFromIndex)
                {
                    Debug.WriteLine(
                        $"[StartBackgroundMeasurement] Already measuring beyond index {startFromIndex} (progress: {_backgroundMeasurementProgress}), skipping duplicate measurement");
                    return;
                }

                // If we're measuring a range that would overlap with the requested range
                // Cancel the existing measurement to avoid conflicts
                Debug.WriteLine(
                    $"[StartBackgroundMeasurement] Current measurement progress {_backgroundMeasurementProgress} < {startFromIndex}, cancelling to restart from new position");
            }
        }

        // Cancel any existing background measurement
        CancelBackgroundMeasurement();

        lock (_measurementLock)
        {
            _backgroundMeasurementCts = new CancellationTokenSource();
            _isBackgroundMeasuring = true;
        }

        var cancellationToken = _backgroundMeasurementCts.Token;

        Tasks.StartDelayed(TimeSpan.FromMilliseconds(50), () =>
        {
            _backgroundMeasurementTask = Task.Run(async () =>
            {
                try
                {
                    await BackgroundMeasureItems(constraints, scale, startFromIndex, cancellationToken, context);
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
        });
    }

    /// <summary>
    /// Remeasures a single item in the background and updates it in the existing structure
    /// </summary>
    public void RemeasureSingleItemInBackground(int itemIndex)
    {
        if (!IsTemplated || ItemsSource == null || itemIndex < 0 || itemIndex >= ItemsSource.Count)
        {
            Debug.WriteLine(
                $"[RemeasureSingleItemInBackground] Invalid parameters: IsTemplated={IsTemplated}, ItemsSource={ItemsSource?.Count}, itemIndex={itemIndex}");
            return;
        }

        // Create context for single-item measurement
        var context = new BackgroundMeasurementContext
        {
            IsSingleItemRemeasurement = true,
            SingleItemIndex = itemIndex,
            StartMeasuringFrom = itemIndex,
            EndMeasuringAt = itemIndex
        };

        // Get current constraints from last measurement
        var constraints = new SKRect(0, 0, _lastMeasuredForWidth, _lastMeasuredForHeight);
        var scale = RenderingScale;

        // Start targeted background measurement
        StartBackgroundMeasurement(constraints, scale, itemIndex, context);

        Debug.WriteLine(
            $"[RemeasureSingleItemInBackground] Started background remeasurement for item at index {itemIndex}");
    }

    private int _listAdditionalMeasurements;

    protected long MeasureStamp;

    /// <summary>
    /// Enhanced MeasureList with background measurement support
    /// </summary>
    public virtual ScaledSize MeasureList(SKRect rectForChildrenPixels, float scale)
    {
        // Cancel any ongoing background measurement when starting fresh measurement
        CancelBackgroundMeasurement();

        MeasureStamp++;

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
            var visibleArea =
                base.GetOnScreenVisibleArea(new(null, rectForChildrenPixels, scale), new(inflate, inflate));

            if (visibleArea.Pixels.Height < 1 || visibleArea.Pixels.Width < 1)
            {
                return ScaledSize.CreateEmpty(scale);
            }

            WillMeasureFromFreshItemssSource = false;

            // Fix: Use Split property instead of hardcoded columnsCount = 1
            var columnsCount = (Split > 0) ? Split : 1;
            var rowsCount = itemsCount;
            if (Type == LayoutType.Row)
            {
                rowsCount = 1;
                if (Split <= 0)
                    columnsCount = itemsCount;
            }

            var rows = new List<List<ControlInStack>>();
            var columns = new List<ControlInStack>(columnsCount);

            int index = -1;
            var cellsToRelease = new List<SkiaControl>();

            // Calculate effective row count based on columnsCount and itemsCount
            var effectiveRowsCount = Type == LayoutType.Column
                ? (int)Math.Ceiling((double)itemsCount / columnsCount)
                : rowsCount;


            try
            {
                int notVisible = 0;

                // Initial measurement loop (same as before)
                for (var row = 0; row < effectiveRowsCount; row++)
                {
                    if (stopMeasuring || index + 2 > itemsCount)
                        break;

                    var rowMaxHeight = 0.0f;
                    var maxWidth = 0.0f;

                    // Calculate actual columns for this row (handle DynamicColumns)
                    var actualColumnsForRow = columnsCount;
                    if (DynamicColumns && Type == LayoutType.Column)
                    {
                        var remainingItems = itemsCount - (row * columnsCount);
                        if (remainingItems < columnsCount)
                            actualColumnsForRow = Math.Max(1, remainingItems);
                    }

                    float widthPerColumn;
                    if (Type == LayoutType.Column)
                    {
                        widthPerColumn = (float)Math.Round(actualColumnsForRow > 1
                            ? (rectForChildrenPixels.Width - (actualColumnsForRow - 1) * Spacing * scale) / actualColumnsForRow
                            : rectForChildrenPixels.Width);
                    }
                    else
                    {
                        widthPerColumn = rectForChildrenPixels.Width;
                    }

                    int column;
                    for (column = 0; column < actualColumnsForRow; column++)
                    {
                        try
                        {
                            if (index + 1 >= itemsCount)
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
                                measured = MeasureAndArrangeCell(rectFitChild, cell, child, rectForChildrenPixels,
                                    scale);

                                if (!visibleArea.Pixels.IntersectsWithInclusive(cell.Destination))
                                {
                                    notVisible++;
                                    if (notVisible > 2)
                                    {
                                        stopMeasuring = true;
                                        break;
                                    }
                                }

                                cell.Measured = measured;
                                cell.WasMeasured = true;

                                // Store in sliding window cache
                                _measuredItems[cell.ControlIndex] = new MeasuredItemInfo
                                {
                                    Cell = cell, LastAccessed = DateTime.UtcNow, IsInViewport = true
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

                LayoutCell(secondPass.Child.MeasuredSize, secondPass.Cell, secondPass.Child, autoRect,
                    secondPass.Scale);
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
                    if (measuredCount < itemsCount)
                    {
                        var averageSize = stackHeight / measuredCount;
                        stackHeight += averageSize;
                    }
                }
                else if (this.Type == LayoutType.Row)
                {
                    if (measuredCount < itemsCount)
                    {
                        var averageSize = stackWidth / measuredCount;
                        stackWidth += averageSize;
                    }
                }
            }

            _listAdditionalMeasurements = 0;

            if (template != null)
            {
                ChildrenFactory.ReleaseTemplateInstance(template);
            }

            // Start background measurement if using MeasureVisible strategy
            //todo DISABLED dont need here
            //if (MeasureItemsStrategy == MeasuringStrategy.MeasureVisible
            //    && measuredCount < itemsCount)
            //{
            //    if (_pendingStructureChanges.Count == 0)
            //    {
            //        StartBackgroundMeasurement(rectForChildrenPixels, scale, measuredCount);
            //    }
            //    else
            //    {
            //        Debug.WriteLine($"[MeasureList] have unapplied measurements, wil not continue measuring in background.");
            //    }
            //}

            // Debug: Report actual measurement results
            if (MeasureItemsStrategy == MeasuringStrategy.MeasureVisible)
            {
                Debug.WriteLine(
                    $"[MeasureList] COMPLETED: Measured {measuredCount} items, estimated total size: {(Type == LayoutType.Column ? stackHeight : stackWidth):F1}px. Background measurement started for remaining {itemsCount - measuredCount} items.");
            }

            return ScaledSize.FromPixels(stackWidth, stackHeight, scale);
        }

        return ScaledSize.FromPixels(rectForChildrenPixels.Width, rectForChildrenPixels.Height, scale);
    }

    /// <summary>
    /// Measures a batch of items in background thread
    /// </summary>
    private List<MeasuredItemInfo> MeasureBatchInBackground(SKRect constraints, float scale, int startIndex, int count,
        float startX, float startY, int startRow, int startCol, CancellationToken cancellationToken)
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

                // Add spacing only when starting a new row (not for columns within the same row)
                if (Type == LayoutType.Column && col == 0 && i > 0)
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
                        ControlIndex = itemIndex, Column = col, Row = row, Destination = rectForChild
                    };

                    var measured = MeasureAndArrangeCell(rectForChild, cell, child, constraints, scale);
                    cell.Measured = measured;
                    cell.WasMeasured = true;

                    // Update max row height
                    if (measured.Pixels.Height > rowHeight)
                        rowHeight = measured.Pixels.Height;

                    measuredBatch.Add(new MeasuredItemInfo
                    {
                        Cell = cell, LastAccessed = DateTime.UtcNow, IsInViewport = false
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

                    //Debug.WriteLine($"[MeasureBatchInBackground] Measured item {itemIndex} at ({cell.Destination.Left:F1},{cell.Destination.Top:F1}): {measured.Pixels.Width:F1}x{measured.Pixels.Height:F1}");
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
    private void IntegrateMeasuredBatch(List<MeasuredItemInfo> measuredBatch, float scale,
        BackgroundMeasurementContext context = null,
        BackgroundMeasurementStartingPosition startingPosition = null)
    {
        if (measuredBatch?.Count > 0)
        {
            var count = 0;
            foreach (var item in measuredBatch)
            {
                _measuredItems[item.Cell.ControlIndex] = item;
                count++;
            }

            // Stage for rendering pipeline integration
            lock (_structureChangesLock)
            {
                _pendingStructureChanges.Add(new StructureChange(StructureChangeType.BackgroundMeasurement, MeasureStamp)
                {
                    Type = StructureChangeType.BackgroundMeasurement,
                    MeasuredItems = measuredBatch,
                    InsertAtIndex = context?.InsertAtIndex,
                    IsInsertOperation = context?.IsInsertOperation ?? false,
                    StartingPosition = startingPosition, // CRITICAL: Store starting position for offset compensation
                    Count = count
                });
            }

            // Recalculate estimated content size
            //UpdateEstimatedContentSize(scale);

            //Debug.WriteLine($"[IntegrateMeasuredBatch] Integrated {measuredBatch.Count} items, total measured: {_measuredItems.Count}, last index: {LastMeasuredIndex}");
        }
    }

    public override bool NeedMeasure
    {
        get { return base.NeedMeasure; }
        set
        {
            if (value && IsTemplated)
            {
                Debug.WriteLine("Recycled cells stack NeedMeasure");
            }

            base.NeedMeasure = value;
        }
    }

    /// <summary>
    /// Applies all pending structure changes to StackStructure - called from rendering pipeline
    /// </summary>
    public void ApplyStructureChanges()
    {
        List<StructureChange> changesToProcess = null;

        // Get all pending changes atomically
        lock (_structureChangesLock)
        {
            if (LatestStackStructure == null || _pendingStructureChanges.Count == 0)
                return;

            // Copy and clear in one atomic operation
            changesToProcess = new List<StructureChange>(_pendingStructureChanges);
            _pendingStructureChanges.Clear();
        }

        // Process all changes outside the lock for maximum performance

        foreach (var change in changesToProcess)
        {
            if (change.Stamp != MeasureStamp)
            {
                continue; //fixes fast remeasuring artifacts
            }

            switch (change.Type)
            {
                case StructureChangeType.BackgroundMeasurement:
                    if (change.Count == 0)
                    {
                        continue;
                    }

                    ApplyBackgroundMeasurementChange(change);
                    break;

                case StructureChangeType.Add:
                    if (change.Count == 0)
                    {
                        continue;
                    }

                    ApplyAddChange(change);
                    break;

                case StructureChangeType.Remove:
                    if (change.Count == 0)
                    {
                        continue;
                    }

                    ApplyRemoveChange(change);
                    break;

                case StructureChangeType.Replace:
                    if (change.Count == 0)
                    {
                        continue;
                    }

                    ApplyReplaceChange(change);
                    break;

                case StructureChangeType.Move:
                    if (change.Count == 0)
                    {
                        continue;
                    }

                    if (change.Count == 0)
                    {
                        continue;
                    }

                    ApplyMoveChange(change);
                    break;

                case StructureChangeType.Reset:
                    ApplyResetChange();
                    break;

                case StructureChangeType.VisibilityChange:
                    if (change.Count == 0)
                    {
                        continue;
                    }

                    ApplyVisibilityChange(change);
                    break;

                case StructureChangeType.SingleItemUpdate:
                    ApplySingleItemUpdateChange(change);
                    break;

                default:
                    Debug.WriteLine($"[ApplyStructureChanges] Unknown change type: {change.Type}");
                    break;
            }
        }

        //Debug.WriteLine($"[StackStructure] Applied {changesToProcess.Count} structure changes. Measured: {MeasuredItemsPercentage:P1}");
    }

    /// <summary>
    /// Applies background measurement changes to StackStructure
    /// FIXED: Now applies structure modifications atomically with LastMeasuredIndex updates
    /// </summary>
    private void ApplyBackgroundMeasurementChange(StructureChange change)
    {
        if (change.MeasuredItems?.Count > 0)
        {
            // CRITICAL: Check for position changes and apply offset compensation
            if (change.StartingPosition != null)
            {
                ApplyOffsetCompensationForBackgroundMeasurement(change);
            }

            List<List<ControlInStack>> allRows;
            
            if (change.IsInsertOperation && change.InsertAtIndex.HasValue)
            {
                // Prepare measurements for insertion at specific position
                allRows = InsertMeasurementsAtPosition(change.MeasuredItems, change.InsertAtIndex.Value);
            }
            else
            {
                // Prepare measurements for appending to end
                allRows = AppendMeasurementsToEnd(change.MeasuredItems);
            }

            // ATOMIC STRUCTURE MODIFICATION - Apply structure changes and update index together
            if (allRows?.Count > 0)
            {
                if (change.IsInsertOperation && change.InsertAtIndex.HasValue)
                {
                    // Insert rows at the correct position in existing structure
                    if (StackStructure == null)
                    {
                        StackStructure = new LayoutStructure(allRows);
                    }
                    else
                    {
                        InsertRowsAtPosition(allRows, change.InsertAtIndex.Value);
                    }
                }
                else
                {
                    // Append rows to end
                    if (StackStructure == null)
                    {
                        StackStructure = new LayoutStructure(allRows);
                    }
                    else
                    {
                        StackStructure.Append(allRows);
                    }
                }

                // Update LastMeasuredIndex atomically with structure changes
                var maxIndex = change.MeasuredItems.Max(x => x.Cell.ControlIndex);
                if (maxIndex > LastMeasuredIndex)
                {
                    LastMeasuredIndex = maxIndex;
                }

                UpdateProgressiveContentSize();

                //Debug.WriteLine($"[StackStructure] Applied {allRows.Count} rows atomically, LastMeasuredIndex: {LastMeasuredIndex}");
            }
        }
    }

    /// <summary>
    /// Applies offset compensation for background measurements when position changes occurred
    /// This handles the race condition where visibility changes offset the structure
    /// while background measurements were calculated with the original positions
    /// </summary>
    private void ApplyOffsetCompensationForBackgroundMeasurement(StructureChange change)
    {
        var startingPos = change.StartingPosition;
        var currentStructure = LatestStackStructure;

        if (currentStructure == null || currentStructure.GetCount() == 0)
            return;

        // Get the current position where new items should be placed - USE INDEX-BASED APPROACH
        // Use the first item's index from the measured batch to get the correct position
        var firstItemIndex = change.MeasuredItems[0].Cell.ControlIndex;
        var (currentStartX, currentStartY, currentRow, currentCol) = GetPositionForIndexDirect(firstItemIndex);

        // Calculate the offset difference
        float deltaX = currentStartX - startingPos.ExpectedStartX;
        float deltaY = currentStartY - startingPos.ExpectedStartY;

        // Apply offset to all measured items if there's a significant difference
        if (Math.Abs(deltaX) > 0.1f || Math.Abs(deltaY) > 0.1f)
        {
            Debug.WriteLine(
                $"[ApplyOffsetCompensation] Detected position change - Expected: ({startingPos.ExpectedStartX:F1},{startingPos.ExpectedStartY:F1}) -> Current: ({currentStartX:F1},{currentStartY:F1}), Delta: ({deltaX:F1},{deltaY:F1})");

            foreach (var item in change.MeasuredItems)
            {
                var cell = item.Cell;

                // Apply offset to both Area and Destination
                cell.Area = new SKRect(
                    cell.Area.Left + deltaX,
                    cell.Area.Top + deltaY,
                    cell.Area.Right + deltaX,
                    cell.Area.Bottom + deltaY
                );

                cell.Destination = new SKRect(
                    cell.Destination.Left + deltaX,
                    cell.Destination.Top + deltaY,
                    cell.Destination.Right + deltaX,
                    cell.Destination.Bottom + deltaY
                );

                // Update row/col if needed (this is more complex for column layout)
                if (startingPos.LayoutType == LayoutType.Column)
                {
                    // For column layout, check if we need to update row based on Y position
                    // This is simplified - in reality, we'd need to recalculate based on actual structure
                    if (Math.Abs(deltaY) > 0.1f)
                    {
                        // Update row/col based on position change
                        cell.Row = currentRow + (cell.Row - startingPos.LastRow);
                        cell.Column = currentCol + (cell.Column - startingPos.LastCol);
                    }
                }
                else
                {
                    // For row layout, check if we need to update column based on X position
                    if (Math.Abs(deltaX) > 0.1f)
                    {
                        cell.Column = currentCol + (cell.Column - startingPos.LastCol);
                        cell.Row = currentRow + (cell.Row - startingPos.LastRow);
                    }
                }
            }

            Debug.WriteLine(
                $"[ApplyOffsetCompensation] Applied offset compensation to {change.MeasuredItems.Count} items");
        }
    }

    /// <summary>
    /// Prepares measurements for insertion at a specific position in existing structure
    /// FIXED: No longer modifies StackStructure directly - returns rows for atomic application
    /// </summary>
    private List<List<ControlInStack>> InsertMeasurementsAtPosition(List<MeasuredItemInfo> measuredItems, int insertAtIndex)
    {
        var allRows = new List<List<ControlInStack>>();
        var columnsCount = (Split > 0) ? Split : 1;
        var currentRow = new List<ControlInStack>(columnsCount);

        for (int i = 0; i < measuredItems.Count; i++)
        {
            var item = measuredItems[i];
            currentRow.Add(item.Cell);

            // Check if this is the last item and we should apply DynamicColumns logic
            bool isLastItem = (i == measuredItems.Count - 1);
            bool shouldCompleteRow = currentRow.Count >= columnsCount;

            // Apply DynamicColumns logic: if this is the last item and we have fewer items than columnsCount,
            // complete the row even if it's not full (when DynamicColumns = true)
            if (DynamicColumns && isLastItem && currentRow.Count < columnsCount)
            {
                shouldCompleteRow = true;
            }

            if (shouldCompleteRow)
            {
                allRows.Add(currentRow);
                currentRow = new List<ControlInStack>(columnsCount);
            }
        }

        // Add incomplete row if it has items (fallback for non-DynamicColumns case)
        if (currentRow.Count > 0)
        {
            allRows.Add(currentRow);
        }

        //Debug.WriteLine($"[StackStructure] Prepared {allRows.Count} rows for insertion at index {insertAtIndex}");

        return allRows;
    }

    /// <summary>
    /// Prepares measurements for appending to the end of existing structure
    /// FIXED: No longer modifies StackStructure directly - returns rows for atomic application
    /// </summary>
    private List<List<ControlInStack>> AppendMeasurementsToEnd(List<MeasuredItemInfo> measuredItems)
    {
        var allRows = new List<List<ControlInStack>>();
        var columnsCount = (Split > 0) ? Split : 1;
        var currentRow = new List<ControlInStack>(columnsCount);

        for (int i = 0; i < measuredItems.Count; i++)
        {
            var item = measuredItems[i];
            currentRow.Add(item.Cell);

            // Check if this is the last item and we should apply DynamicColumns logic
            bool isLastItem = (i == measuredItems.Count - 1);
            bool shouldCompleteRow = currentRow.Count >= columnsCount;

            // Apply DynamicColumns logic: if this is the last item and we have fewer items than columnsCount,
            // complete the row even if it's not full (when DynamicColumns = true)
            if (DynamicColumns && isLastItem && currentRow.Count < columnsCount)
            {
                shouldCompleteRow = true;
            }

            if (shouldCompleteRow)
            {
                allRows.Add(currentRow);
                currentRow = new List<ControlInStack>(columnsCount);
            }
        }

        // Add incomplete row if it has items (fallback for non-DynamicColumns case)
        if (currentRow.Count > 0)
        {
            allRows.Add(currentRow);
        }

        //Debug.WriteLine($"[StackStructure] Prepared {allRows.Count} rows for appending");
        return allRows;
    }

    /// <summary>
    /// Inserts rows at a specific position in the StackStructure
    /// </summary>
    private void InsertRowsAtPosition(List<List<ControlInStack>> newRows, int insertAtIndex)
    {
        // For now, we'll use a simplified approach since DynamicGrid doesn't have direct insert
        // We'll rebuild the structure with the new rows inserted at the correct position

        var existingCells = StackStructure.GetChildren().ToList();
        var allCells = new List<ControlInStack>();

        // Add cells before insert position
        allCells.AddRange(existingCells.Where(c => c.ControlIndex < insertAtIndex));

        // Add new cells
        foreach (var row in newRows)
        {
            allCells.AddRange(row);
        }

        // Add cells after insert position (with shifted indices)
        var cellsAfter = existingCells.Where(c => c.ControlIndex >= insertAtIndex).ToList();
        foreach (var cell in cellsAfter)
        {
            cell.ControlIndex += newRows.SelectMany(r => r).Count(); // Shift indices
        }

        allCells.AddRange(cellsAfter);

        // Rebuild structure with all cells
        var rebuiltRows = new List<List<ControlInStack>>();
        var columnsCount = (Split > 0) ? Split : 1;
        var currentRow = new List<ControlInStack>(columnsCount);
        var orderedCells = allCells.OrderBy(c => c.ControlIndex).ToList();

        for (int i = 0; i < orderedCells.Count; i++)
        {
            var cell = orderedCells[i];
            currentRow.Add(cell);

            // Check if this is the last item and we should apply DynamicColumns logic
            bool isLastItem = (i == orderedCells.Count - 1);
            bool shouldCompleteRow = currentRow.Count >= columnsCount;

            // Apply DynamicColumns logic: if this is the last item and we have fewer items than columnsCount,
            // complete the row even if it's not full (when DynamicColumns = true)
            if (DynamicColumns && isLastItem && currentRow.Count < columnsCount)
            {
                shouldCompleteRow = true;
            }

            if (shouldCompleteRow)
            {
                rebuiltRows.Add(currentRow);
                currentRow = new List<ControlInStack>(columnsCount);
            }
        }

        // Add incomplete row if it has items (fallback for non-DynamicColumns case)
        if (currentRow.Count > 0)
        {
            rebuiltRows.Add(currentRow);
        }

        // Replace the entire structure
        StackStructure = new LayoutStructure(rebuiltRows);

        //Debug.WriteLine($"[StackStructure] Rebuilt with {newRows.Count} rows inserted at index {insertAtIndex}");
    }

    /// <summary>
    /// Triggers insert-aware background measurement for new items
    /// </summary>
    private void TriggerInsertAwareBackgroundMeasurement(int insertAtIndex, int insertCount)
    {
        if (!IsTemplated || ItemsSource == null)
            return;

        // Create context for insert operation
        var context = new BackgroundMeasurementContext
        {
            InsertAtIndex = insertAtIndex, InsertCount = insertCount, StartMeasuringFrom = insertAtIndex
        };

        // Get current constraints from last measurement
        var constraints = new SKRect(0, 0, _lastMeasuredForWidth, _lastMeasuredForHeight);
        var scale = RenderingScale;

        // Start background measurement with insert context
        StartBackgroundMeasurement(constraints, scale, insertAtIndex, context);

        //Debug.WriteLine($"[StackStructure] Started insert-aware background measurement for {insertCount} items at index {insertAtIndex}");
    }

    /// <summary>
    /// Applies Add changes to StackStructure
    /// </summary>
    private void ApplyAddChange(StructureChange change)
    {
        //Debug.WriteLine($"[StackStructure] Adding {change.Count} items at index {change.StartIndex}");

        if (MeasureItemsStrategy == MeasuringStrategy.MeasureVisible)
        {
            if (change.StartIndex <= LastMeasuredIndex)
            {
                // Insert in middle of measured items - shift existing measurements
                ShiftMeasurementIndices(change.StartIndex, change.Count);

                // Trigger insert-aware background measurement for new items
                TriggerInsertAwareBackgroundMeasurement(change.StartIndex, change.Count);

                //Debug.WriteLine($"[StackStructure] MeasureVisible: Shifted measurements and triggered insert-aware background measurement");
            }
            else
            {
                // Adding at end - background measurement should continue from LastMeasuredIndex + 1
                // This handles LoadMore scenario where items are added at the end but background measurement
                // should continue sequentially from where it left off
                //Debug.WriteLine($"[StackStructure] MeasureVisible strategy - LoadMore add at end (index {change.StartIndex}), background measurement continues from {LastMeasuredIndex + 1}");

                // No need to shift measurements, background measurement will handle the gap naturally
                // by continuing from LastMeasuredIndex + 1 and eventually reaching the newly added items
            }
        }
        else
        {
            // For sync strategies: Need to shift existing measurements and measure new items
            if (change.StartIndex <= LastMeasuredIndex)
            {
                // Adding in middle of measured items - shift existing measurements
                ShiftMeasurementIndices(change.StartIndex, change.Count);
                //Debug.WriteLine($"[StackStructure] Shifted measurements for sync strategy");
            }
            // Note: New items will be measured on-demand during normal layout
        }

        UpdateProgressiveContentSize();
    }

    /// <summary>
    /// Applies Remove changes to StackStructure
    /// </summary>
    private void ApplyRemoveChange(StructureChange change)
    {
        //Debug.WriteLine($"[StackStructure] Removing {change.Count} items at index {change.StartIndex}");

        // Remove items from measurement cache and shift indices
        for (int i = change.StartIndex; i < change.StartIndex + change.Count; i++)
        {
            _measuredItems.TryRemove(i, out _);
        }

        // Shift remaining measurements
        ShiftMeasurementIndices(change.StartIndex + change.Count, -change.Count);

        // Remove corresponding rows from StackStructure
        if (StackStructure != null)
        {
            RemoveItemsFromStackStructure(change.StartIndex, change.Count);
        }

        //Debug.WriteLine($"[StackStructure] Removed {change.Count} items and shifted measurements");
        UpdateProgressiveContentSize();
    }

    /// <summary>
    /// Applies Replace changes to StackStructure
    /// </summary>
    private void ApplyReplaceChange(StructureChange change)
    {
        //Debug.WriteLine($"[StackStructure] Replacing {change.Count} items at index {change.StartIndex}");

        // For Replace: Split into Remove + Add in same frame
        var removeChange = new StructureChange(StructureChangeType.Remove, MeasureStamp)
        {
            StartIndex = change.StartIndex, Count = change.Count
        };

        var addChange = new StructureChange(StructureChangeType.Add, MeasureStamp)
        {
            StartIndex = change.StartIndex,
            Count = change.Count,
            Items = change.Items
        };

        // Apply remove then add
        ApplyRemoveChange(removeChange);
        ApplyAddChange(addChange);

        //Debug.WriteLine($"[StackStructure] Split replace into remove + add operations");
    }

    /// <summary>
    /// Applies Move changes to StackStructure
    /// </summary>
    private void ApplyMoveChange(StructureChange change)
    {
        //Debug.WriteLine($"[StackStructure] Moving item from index {change.StartIndex} to {change.TargetIndex}");
        // TODO: Implement move logic that reorders structure
        UpdateProgressiveContentSize();
    }

    /// <summary>
    /// Applies Reset changes to StackStructure
    /// </summary>
    protected void ApplyResetChange()
    {
        //Debug.WriteLine($"[StackStructure] Resetting all");

        // Clear everything for reset
        StackStructure = null;
        _measuredItems.Clear();
        _indexOffsets.Clear();
        _removedIndices.Clear();
        LastMeasuredIndex = -1;
        FirstMeasuredIndex = -1;
        UpdateProgressiveContentSize();
    }

    /// <summary>
    /// Applies visibility changes to StackStructure
    /// FIXED: Now processes visibility changes in sequential groups to prevent gaps
    /// that occur when non-consecutive items change visibility
    /// </summary>
    private void ApplyVisibilityChange(StructureChange change)
    {
        var structure = LatestMeasuredStackStructure;
        if (structure == null || change.Count == 0)
            return;

        //Debug.WriteLine($"[ApplyVisibilityChange] Processing {change.Count} cells starting at {change.StartIndex}, visibility: {change.IsVisible}");

        // Process visibility changes in sequential groups to prevent gaps
        ProcessVisibilityChangesInSequentialGroups(structure, change);

        UpdateProgressiveContentSize();
        Repaint();
    }

    /// <summary>
    /// Processes visibility changes in sequential groups to prevent gaps between items
    /// when non-consecutive items change visibility
    /// </summary>
    private void ProcessVisibilityChangesInSequentialGroups(LayoutStructure structure, StructureChange change)
    {
        var changedCells = new List<(int index, ControlInStack cell, bool wasChanged)>();

        // First pass: identify all cells that actually changed and collect their info
        for (int i = change.StartIndex; i < change.StartIndex + change.Count; i++)
        {
            var cell = structure.GetForIndex(i);
            if (cell == null) continue;

            bool wasChanged = false;

            if (!change.IsVisible && !cell.IsCollapsed)
            {
                // BECOMING GHOST  
                cell.IsCollapsed = true;
                wasChanged = true;
            }
            else if (change.IsVisible && cell.IsCollapsed)
            {
                // BECOMING VISIBLE 
                cell.IsCollapsed = false;
                wasChanged = true;
            }

            changedCells.Add((i, cell, wasChanged));
        }

        // Second pass: process sequential groups of changes
        var groups = GroupSequentialChanges(changedCells.Where(c => c.wasChanged).ToList());

        foreach (var group in groups)
        {
            // Calculate offset for this group
            float groupDeltaWidth = 0;
            float groupDeltaHeight = 0;
            ControlInStack lastCellInGroup = null;

            foreach (var (index, cell, _) in group)
            {
                if (!change.IsVisible && cell.IsCollapsed)
                {
                    // Cell became ghost
                    groupDeltaWidth += -cell.Destination.Width;
                    groupDeltaHeight += -cell.Destination.Height;
                }
                else if (change.IsVisible && !cell.IsCollapsed)
                {
                    // Cell became visible
                    groupDeltaWidth += cell.Destination.Width;
                    groupDeltaHeight += cell.Destination.Height;
                }

                lastCellInGroup = cell;
            }

            // Apply offset for this group to all subsequent cells
            if (lastCellInGroup != null && (Math.Abs(groupDeltaWidth) > 0.1f || Math.Abs(groupDeltaHeight) > 0.1f))
            {
                OffsetSubsequentCells(structure, lastCellInGroup, groupDeltaWidth, groupDeltaHeight);
            }
        }
    }

    /// <summary>
    /// Groups sequential changes together to process them as batches
    /// Example: changes at indices [1,2,3,7,8,12] become groups [[1,2,3], [7,8], [12]]
    /// </summary>
    private List<List<(int index, ControlInStack cell, bool wasChanged)>> GroupSequentialChanges(
        List<(int index, ControlInStack cell, bool wasChanged)> changes)
    {
        var groups = new List<List<(int index, ControlInStack cell, bool wasChanged)>>();

        if (changes.Count == 0)
            return groups;

        // Sort by index to ensure proper grouping
        changes.Sort((a, b) => a.index.CompareTo(b.index));

        var currentGroup = new List<(int index, ControlInStack cell, bool wasChanged)> { changes[0] };

        for (int i = 1; i < changes.Count; i++)
        {
            var currentChange = changes[i];
            var previousChange = changes[i - 1];

            // If current index is sequential to the previous, add to current group
            if (currentChange.index == previousChange.index + 1)
            {
                currentGroup.Add(currentChange);
            }
            else
            {
                // Non-sequential, start a new group
                groups.Add(currentGroup);
                currentGroup = new List<(int index, ControlInStack cell, bool wasChanged)> { currentChange };
            }
        }

        // Add the last group
        groups.Add(currentGroup);

        return groups;
    }

    /// <summary>
    /// Applies a single item update to StackStructure
    /// </summary>
    private void ApplySingleItemUpdateChange(StructureChange change)
    {
        if (change.MeasuredItems?.Count == 1 && change.StartIndex >= 0)
        {
            var newMeasurement = change.MeasuredItems[0];
            var itemIndex = change.StartIndex;

            // Get old measurement for comparison
            MeasuredItemInfo oldMeasurement = null;
            _measuredItems.TryGetValue(itemIndex, out oldMeasurement);

            // Update measurement in dictionary
            _measuredItems[itemIndex] = newMeasurement;

            // Find and update the cell in StackStructure
            if (StackStructure != null)
            {
                var cell = StackStructure.GetForIndex(itemIndex);
                if (cell != null)
                {
                    // Calculate size difference for shifting
                    float deltaWidth = 0;
                    float deltaHeight = 0;

                    if (change.OffsetOthers != null)
                    {
                        deltaWidth = change.OffsetOthers.Value.X;
                        deltaHeight = change.OffsetOthers.Value.Y;
                    }
                    else if (oldMeasurement != null)
                    {
                        deltaWidth = newMeasurement.Cell.Measured.Pixels.Width -
                                     oldMeasurement.Cell.Measured.Pixels.Width;
                        deltaHeight = newMeasurement.Cell.Measured.Pixels.Height -
                                      oldMeasurement.Cell.Measured.Pixels.Height;
                    }

                    // Update cell with new measurement
                    cell.Measured = newMeasurement.Cell.Measured;

                    // CRITICAL: Update the destination rectangle to match the new size
                    // This is what was missing - we need to resize the cell's destination
                    cell.Destination = new SKRect(
                        cell.Destination.Left,
                        cell.Destination.Top,
                        cell.Destination.Left + newMeasurement.Cell.Measured.Pixels.Width,
                        cell.Destination.Top + newMeasurement.Cell.Measured.Pixels.Height
                    );

                    // Shift subsequent items if size changed significantly
                    if (Math.Abs(deltaWidth) > 0.1f || Math.Abs(deltaHeight) > 0.1f)
                    {
                        // Use the existing OffsetSubsequentCells method
                        OffsetSubsequentCells(StackStructure, cell, deltaWidth, deltaHeight);

                        //Debug.WriteLine($"[StackStructure] changed single item {itemIndex}, shifted cells by {deltaWidth}x{deltaHeight}");
                    }
                }
                else
                {
                    Debug.WriteLine($"[StackStructure] Could not find cell for index {itemIndex} in structure");
                }
            }

            // Update content size
            UpdateProgressiveContentSize();

            // Trigger repaint to show changes
            Repaint();

            //Debug.WriteLine($"[StackStructure] Updated measurement for item {itemIndex}");
        }
    }

    #region Hybrid Measurement Shifting

    /// <summary>
    /// Shifts measurement indices using hybrid approach based on collection size
    /// </summary>
    private void ShiftMeasurementIndices(int startIndex, int offset)
    {
        var affectedCount = _measuredItems.Keys.Count(k => k >= startIndex);

        if (affectedCount <= DIRECT_SHIFT_THRESHOLD)
        {
            // Small collection - direct shifting (simple & fast)
            DirectShiftMeasurements(startIndex, offset);
        }
        else
        {
            // Large collection - offset mapping (scalable)
            OffsetMapMeasurements(startIndex, offset);
        }

        // Update measurement indices
        if (offset < 0) // Removal
        {
            if (LastMeasuredIndex >= startIndex)
            {
                LastMeasuredIndex = Math.Max(startIndex - 1, LastMeasuredIndex + offset);
            }
        }
        else // Addition
        {
            if (LastMeasuredIndex >= startIndex)
            {
                LastMeasuredIndex += offset;
            }
        }

        Debug.WriteLine(
            $"[ShiftMeasurementIndices] Shifted {affectedCount} items from index {startIndex} by {offset}. LastMeasuredIndex: {LastMeasuredIndex}");
    }

    /// <summary>
    /// Direct shifting for small collections
    /// </summary>
    private void DirectShiftMeasurements(int startIndex, int offset)
    {
        var itemsToShift = _measuredItems
            .Where(kvp => kvp.Key >= startIndex)
            .OrderBy(kvp => offset > 0 ? -kvp.Key : kvp.Key) // Avoid conflicts during shifting
            .ToList();

        foreach (var (oldIndex, item) in itemsToShift)
        {
            _measuredItems.TryRemove(oldIndex, out _);
            var newIndex = oldIndex + offset;
            if (newIndex >= 0)
            {
                item.Cell.ControlIndex = newIndex;
                _measuredItems[newIndex] = item;

                // Update StackStructure indices
                if (StackStructure != null)
                {
                    UpdateStackStructureIndex(oldIndex, newIndex);
                }
            }
        }

        Debug.WriteLine($"[DirectShiftMeasurements] Directly shifted {itemsToShift.Count} measurements");
    }

    /// <summary>
    /// Offset mapping for large collections
    /// </summary>
    private void OffsetMapMeasurements(int startIndex, int offset)
    {
        if (offset < 0) // Removal
        {
            // Mark removed indices
            for (int i = startIndex; i < startIndex - offset; i++)
            {
                _removedIndices.Add(i);
                _measuredItems.TryRemove(i, out _); // Remove from cache
            }
        }

        // Add offset for all subsequent indices
        var offsetKey = startIndex + Math.Max(0, -offset);
        _indexOffsets[offsetKey] = _indexOffsets.GetValueOrDefault(offsetKey, 0) + offset;

        Debug.WriteLine(
            $"[OffsetMapMeasurements] Added offset {offset} for indices >= {offsetKey}. Removed: {-Math.Min(0, offset)} indices");
    }

    /// <summary>
    /// Updates a specific index in StackStructure
    /// </summary>
    private void UpdateStackStructureIndex(int oldIndex, int newIndex)
    {
        if (StackStructure == null) return;

        foreach (var cell in StackStructure.GetChildren())
        {
            if (cell.ControlIndex == oldIndex)
            {
                cell.ControlIndex = newIndex;
                return;
            }
        }
    }

    /// <summary>
    /// Gets the actual index considering offset mapping
    /// </summary>
    private int GetActualIndex(int originalIndex)
    {
        if (_removedIndices.Contains(originalIndex)) return -1;

        var offset = 0;
        foreach (var kvp in _indexOffsets.Where(kvp => originalIndex >= kvp.Key))
        {
            offset += kvp.Value;
        }

        return originalIndex + offset;
    }

    /// <summary>
    /// Removes items from StackStructure
    /// </summary>
    private void RemoveItemsFromStackStructure(int startIndex, int count)
    {
        if (StackStructure == null) return;

        // Find and remove cells with indices in the removal range
        var cellsToRemove = StackStructure.GetChildren()
            .Where(cell => cell.ControlIndex >= startIndex && cell.ControlIndex < startIndex + count)
            .ToList();

        // Remove cells from the grid structure
        foreach (var cell in cellsToRemove)
        {
            // Since DynamicGrid doesn't have a direct remove method, we need to rebuild
            // For now, we'll mark them as removed by setting ControlIndex to -1
            cell.ControlIndex = -1;
        }

        Debug.WriteLine(
            $"[RemoveItemsFromStackStructure] Marked {cellsToRemove.Count} items for removal from structure");
    }

    #endregion

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
            // Calculate actual measured height using first/last item positions (O(1) optimization)
            var actualMeasuredHeight = 0f;
            var visibleItemsCount = measuredCount; // Assume all measured items are visible

            if (measuredCount > 0 && StackStructure.Length > 0)
            {
                // Get first item, skip collapsed if needed
                ControlInStack firstVisibleItem = StackStructure[0];
                var firstIndex = 0;
                while (firstIndex < measuredCount && firstIndex < StackStructure.Length &&
                       StackStructure[firstIndex].IsCollapsed)
                {
                    firstVisibleItem = StackStructure[++firstIndex];
                    visibleItemsCount--; // Subtract collapsed items
                }

                // Get last item, skip collapsed if needed
                ControlInStack lastVisibleItem = StackStructure[Math.Min(measuredCount - 1, StackStructure.Length - 1)];
                var lastIndex = Math.Min(measuredCount - 1, StackStructure.Length - 1);
                while (lastIndex >= 0 && StackStructure[lastIndex].IsCollapsed)
                {
                    lastVisibleItem = StackStructure[--lastIndex];
                    visibleItemsCount--; // Subtract collapsed items
                }

                // Use first/last item positions for O(1) calculation
                if (firstVisibleItem != null && lastVisibleItem != null && !firstVisibleItem.IsCollapsed &&
                    !lastVisibleItem.IsCollapsed)
                {
                    actualMeasuredHeight = lastVisibleItem.Destination.Bottom - firstVisibleItem.Destination.Top;
                }
            }

            float newContentHeight;

            if (progress >= 1.0f)
            {
                // 100% measured - use exact size
                newContentHeight = actualMeasuredHeight;

                //Debug.WriteLine($"[SkiaLayout] 100% measured - exact height: {newContentHeight:F1}px");
            }
            else if (visibleItemsCount == 0)
            {
                // No items measured yet - use a minimal estimate
                // Use default item height or a reasonable fallback
                var defaultItemHeight = 60f * RenderingScale; // Reasonable default
                newContentHeight = Math.Min(totalItems * defaultItemHeight, 10000f); // Cap at 10k pixels

                //Debug.WriteLine($"[SkiaLayout] No items measured - using default estimate: {newContentHeight:F1}px");
            }
            else
            {
                // Use actual measured height from LatestStackStructure (what renderer uses)
                var averageHeight = actualMeasuredHeight / visibleItemsCount;
                newContentHeight = actualMeasuredHeight;//+ averageHeight;

                //Debug.WriteLine($"[SkiaLayout] {progress:P1} measured - estimate: {newContentHeight:F1}px");
            }

            // CRITICAL: Never allow content size to shrink dramatically during scrolling
            // This prevents the "huge empty space" issue when scrolling fast
            var currentHeight = MeasuredSize.Pixels.Height;

            if (Math.Abs(newContentHeight - currentHeight) > 1f)
            {
                SetMeasured(MeasuredSize.Pixels.Width, newContentHeight, false, false, RenderingScale);

                //Debug.WriteLine($"[SkiaLayout] Updated content COLUMN {100.0 * progress:0}% height from {currentHeight:F1}px to {newContentHeight:F1}px");
            }
        }
        else if (Type == LayoutType.Row)
        {
            // Calculate actual measured width using first/last item positions (O(1) optimization)
            var actualMeasuredWidth = 0f;
            var visibleItemsCount = measuredCount; // Assume all measured items are visible

            if (measuredCount > 0 && StackStructure.Length > 0)
            {
                // Get first item, skip collapsed if needed
                ControlInStack firstVisibleItem = StackStructure[0];
                var firstIndex = 0;
                while (firstIndex < measuredCount && firstIndex < StackStructure.Length &&
                       StackStructure[firstIndex].IsCollapsed)
                {
                    firstVisibleItem = StackStructure[++firstIndex];
                    visibleItemsCount--; // Subtract collapsed items
                }

                // Get last item, skip collapsed if needed
                ControlInStack lastVisibleItem = StackStructure[Math.Min(measuredCount - 1, StackStructure.Length - 1)];
                var lastIndex = Math.Min(measuredCount - 1, StackStructure.Length - 1);
                while (lastIndex >= 0 && StackStructure[lastIndex].IsCollapsed)
                {
                    lastVisibleItem = StackStructure[--lastIndex];
                    visibleItemsCount--; // Subtract collapsed items
                }

                // Use first/last item positions for O(1) calculation
                if (firstVisibleItem != null && lastVisibleItem != null && !firstVisibleItem.IsCollapsed &&
                    !lastVisibleItem.IsCollapsed)
                {
                    actualMeasuredWidth = lastVisibleItem.Destination.Right - firstVisibleItem.Destination.Left;
                }
            }

            float newContentWidth;

            if (progress >= 1.0f)
            {
                newContentWidth = actualMeasuredWidth;
            }
            else if (visibleItemsCount == 0)
            {
                var defaultItemWidth = 100f * RenderingScale; // Reasonable default
                newContentWidth = Math.Min(totalItems * defaultItemWidth, 10000f); // Cap at 10k pixels
            }
            else
            {
                var averageWidth = actualMeasuredWidth / visibleItemsCount;
                newContentWidth = actualMeasuredWidth;// + averageWidth;

                Debug.WriteLine(
                    $"[SkiaLayout] {progress:P1} measured - structure-based estimate: {newContentWidth:F1}px");
            }

            var currentWidth = MeasuredSize.Pixels.Width;

            if (Math.Abs(newContentWidth - currentWidth) > 1f)
            {
                SetMeasured(newContentWidth, MeasuredSize.Pixels.Height, false, false, RenderingScale);
                Debug.WriteLine(
                    $"[SkiaLayout] Updated content ROW {100.0 * progress:0}% width from {currentWidth:F1}px to {newContentWidth:F1}px");
            }
        }
    }

    /// <summary>
    /// ANDROID-STYLE: Never allow dramatic content size shrinkage during scrolling
    /// This prevents the "huge empty space" issue when scrolling fast to unmeasured areas
    /// </summary>
    private float ApplyStableSizeConstraints(float newSize, float currentSize)
    {
        if (currentSize > 0 && newSize < currentSize * 0.8f)
        {
            // If new estimate is more than 20% smaller, use gradual shrinking
            return Math.Max(newSize, currentSize * 0.9f);
        }

        return newSize;
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
            Debug.WriteLine(
                $"[ApplySlidingWindowCleanup] Removed {itemsToRemove.Count} measured items, kept {_measuredItems.Count} items in memory");
        }
    }


    /// <summary>
    /// Background measurement implementation with sliding window
    /// </summary>
    private async Task BackgroundMeasureItems(SKRect constraints, float scale, int startIndex,
        CancellationToken cancellationToken,
        BackgroundMeasurementContext context = null)
    {
        // Special case for single item remeasurement
        if (context?.IsSingleItemRemeasurement == true && context.SingleItemIndex.HasValue)
        {
            MeasureSingleItem(context.SingleItemIndex.Value, constraints, scale, cancellationToken, true);
            return;
        }

        var totalItems = ItemsSource.Count;
        var currentBatchStart = startIndex;
        var maxIterations = Math.Max(1, (totalItems / MEASUREMENT_BATCH_SIZE) + 10); // Safety limit
        var iterationCount = 0;

        Debug.WriteLine($"[MeasureVisible] Starting measurement from {startIndex} of {totalItems} total items");

        while (currentBatchStart < totalItems && !cancellationToken.IsCancellationRequested &&
               iterationCount < maxIterations)
        {
            lock (_structureChangesLock)
            {
                if (_pendingStructureChanges.Count > 0)
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
                Debug.WriteLine(
                    $"[MeasureVisible] WARNING: No items to measure in batch {currentBatchStart}-{batchEnd}, breaking loop");
                break;
            }

            Debug.WriteLine(
                $"[MeasureVisible] Measuring batch {currentBatchStart}-{batchEnd - 1} ({itemsToMeasure} items) [iteration {iterationCount}/{maxIterations}]");


            var (startX, startY, startRow, startCol) = GetPositionForIndexDirect(currentBatchStart);

            // Create starting position data for offset compensation
            var startingPosition = new BackgroundMeasurementStartingPosition
            {
                LastRow = startRow,
                LastCol = startCol,
                ExpectedStartX = startX,
                ExpectedStartY = startY,
                LayoutType = this.Type
            };

            // Measure batch on background thread
            var measuredBatch = await Task.Run(() => MeasureBatchInBackground(
                constraints, scale, currentBatchStart, itemsToMeasure, startX, startY, startRow, startCol,
                cancellationToken), cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                Debug.WriteLine($"[MeasureVisible] Cancellation requested, stopping at batch {currentBatchStart}");
                break;
            }

            // Integrate results on background thread (safe for reading/staging)
            if (!cancellationToken.IsCancellationRequested)
            {
                IntegrateMeasuredBatch(measuredBatch, scale, context, startingPosition);
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
            Debug.WriteLine(
                $"[MeasureVisible] WARNING: Hit maximum iteration limit ({maxIterations}), stopping background measurement");
        }

        Debug.WriteLine(
            $"[MeasureVisible] Completed background measurement up to index {_backgroundMeasurementProgress}");

        Repaint();
    }

    /// <summary>
    /// Measures a single item in the background and stages it for structure update.
    /// For MeasureVisible Only.
    /// </summary>
    public void MeasureSingleItem(int itemIndex, SKRect constraints, float scale, CancellationToken cancellationToken,
        bool inBackground)
    {
        try
        {
            if (MeasureItemsStrategy != MeasuringStrategy.MeasureVisible)
            {
                return;
            }

            //Debug.WriteLine($"[StackStructure] Starting measurement for item at index {itemIndex}");

            SkiaControl template = null;

            try
            {
                // Get child for this specific index
                var child = ChildrenFactory.GetViewForIndex(itemIndex, template, 0, true);


                if (child == null || !child.CanDraw)
                {
                    Debug.WriteLine(
                        $"[BackgroundMeasureSingleItem] Failed to get child or child cannot draw for item {itemIndex}");
                    return;
                }

                // Create cell structure for measurement
                var cell = new ControlInStack { ControlIndex = itemIndex, View = child };

                // Measure the item (simplified measurement for single item)
                var availableWidth = constraints.Width;
                var availableHeight = float.PositiveInfinity; // Allow natural height

                var measured = MeasureChild(child, availableWidth, availableHeight, scale);
                cell.Measured = measured;
                cell.WasMeasured = true;

                // Create measured item info
                var measuredItem = new MeasuredItemInfo
                {
                    Cell = cell, LastAccessed = DateTime.UtcNow, IsInViewport = true
                };

                // Stage for rendering pipeline with special single-item flag
                if (!cancellationToken.IsCancellationRequested)
                {
                    lock (_structureChangesLock)
                    {
                        _pendingStructureChanges.Add(new StructureChange(StructureChangeType.SingleItemUpdate, MeasureStamp)
                        {
                            StartIndex = itemIndex,
                            Count = 1,
                            MeasuredItems = new List<MeasuredItemInfo> { measuredItem }
                        });
                    }

                    //Debug.WriteLine($"[BackgroundMeasureSingleItem] Staged single item update for index {itemIndex}, measured size: {measured.Pixels.Width}x{measured.Pixels.Height}");
                }
            }
            finally
            {
                if (template != null)
                {
                    ChildrenFactory.ReleaseTemplateInstance(template);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BackgroundMeasureSingleItem] Error measuring item {itemIndex}: {ex.Message}");
        }
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

            Debug.WriteLine(
                $"[GetEstimatedContentSize] Measured {measuredCount}/{itemsCount} items, avg height: {averageHeight:F1}px, estimated total: {estimatedTotalHeight:F1}px");

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

            Debug.WriteLine(
                $"[GetEstimatedContentSize] Measured {measuredCount}/{itemsCount} items, avg width: {averageWidth:F1}px, estimated total: {estimatedTotalWidth:F1}px");

            return ScaledSize.FromPixels(estimatedTotalWidth, MeasuredSize.Pixels.Height, scale);
        }

        return MeasuredSize;
    }


    /// <summary>
    /// DEPRECATED: Use GetPositionForIndexDirect instead to avoid row/col coordinate confusion
    /// </summary>
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

    /// <summary>
    /// Default item height when no measurements are available
    /// </summary>
    private float DefaultItemHeight => 60f * RenderingScale;

    /// <summary>
    /// Calculate position for a specific index directly without relying on structure last item
    /// This prevents row/col coordinate confusion and ensures alignment with ItemsSource indices
    /// </summary>
    private (float x, float y, int row, int col) GetPositionForIndexDirect(int itemIndex)
    {
        int columnsCount = (Split > 0) ? Split : 1;

        // Calculate row/col directly from index - pure mathematical calculation
        int row = itemIndex / columnsCount;
        int col = itemIndex % columnsCount;

        // Calculate position based on row/col
        float columnWidth = ComputeColumnWidth(columnsCount);
        float spacing = (float)(Spacing * RenderingScale);

        float x = col * (columnWidth + spacing);

        // For Y position, try to use actual measured heights if available
        float y = 0f;
        if (StackStructure != null && StackStructure.Length > 0 && row > 0)
        {
            // Check if we're at the first column of a row
            if (col == 0)
            {
                // First column: calculate Y based on previous row bottom + spacing
                float previousRowBottom = ComputeBottomOfRow(StackStructure, row - 1);
                y = previousRowBottom + spacing;
            }
            else
            {
                // Same row, different column - use the Y position of the first column in this row
                var firstColumnInRow = StackStructure.Get(0, row);
                if (firstColumnInRow != null)
                {
                    y = firstColumnInRow.Destination.Top;
                }
                else
                {
                    // Fallback: calculate without extra spacing since we're in the same row
                    float previousRowBottom = ComputeBottomOfRow(StackStructure, row - 1);
                    y = previousRowBottom + spacing;
                }
            }
        }
        else
        {
            // Fallback to estimated height
            y = row * (DefaultItemHeight + spacing);
        }

        return (x, y, row, col);
    }

    private float ComputeColumnWidth(int columnsCount)
    {
        if (this.Type == LayoutType.Column)
        {
            // Use content width (excluding margins/padding) like initial measurement does
            var contentWidth = GetContentWidthForBackgroundMeasurement();
            return (float)Math.Round(columnsCount > 1
                ? (contentWidth - (columnsCount - 1) * Spacing * RenderingScale) / columnsCount
                : contentWidth);
        }
        else
        {
            return GetContentWidthForBackgroundMeasurement();
        }
    }

    /// <summary>
    /// Gets the content width excluding margins and padding, equivalent to rectForChildrenPixels.Width
    /// used in initial measurement. This ensures background measurement uses the same available width.
    /// </summary>
    private float GetContentWidthForBackgroundMeasurement()
    {
        var scale = RenderingScale;
        var constraintLeft = (UsePadding.Left + Margins.Left) * scale;
        var constraintRight = (UsePadding.Right + Margins.Right) * scale;

        return (float)Math.Round(MeasuredSize.Pixels.Width - (constraintRight + constraintLeft));
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

        Debug.WriteLine(
            $"[MeasureAdditionalItems] INCREMENTAL: Measuring items {startIndex}-{endIndex - 1} (batch: {batchSize}, ahead: {aheadCount})");

        if (startIndex >= endIndex)
            return 0;

        int countToMeasure = endIndex - startIndex;
        if (countToMeasure <= 0)
            return 0;

        var structure = LatestMeasuredStackStructure.Clone();
        var (startX, startY, startRow, startCol) = GetPositionForIndexDirect(startIndex);
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

            Debug.WriteLine(
                $"[MeasureAdditionalItems] COMPLETED: Measured {countToMeasure} additional items, now measured up to index {LastMeasuredIndex} of {ItemsSource.Count} total");

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
