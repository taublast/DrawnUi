//reusing some code from #dotnetmaui Layout

using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace DrawnUi.Draw;

public partial class SkiaLayout
{
    public virtual void OnViewportWasChanged(ScaledRect viewport, ScaledPoint offset)
    {
        //RenderingViewport = new(viewport.Pixels);
        if (MeasureItemsStrategy == MeasuringStrategy.MeasureVisible)
        {
            if (WillDrawFromFreshItemssSource == 0 && ContentSize.IsEmpty && EffectiveItemsSource.Count > 0)
            {
                InvalidateMeasure();
            }
        }

        ParentViewport = viewport;
        _lastVisibilityOffset = offset;

        // built-in source window: derive batch/cap from the real viewport once visible range is known,
        // and drive internal slides from the visible range (independent of the scroll's LoadMoreCommand)
        if (_itemsWindow != null)
        {
            // NEVER feed the draw-updated Visible*Local fields here: plane-blit frames (SkiaCachedStack)
            // skip the live DrawStack that refreshes them — riding a covered plane they freeze at the
            // last live frame, the slide margin is never detected and the window never slides: the
            // scroll walls DEAD at the window edge (WASM wheel repro: m pinned at window end, offset
            // clamped forever). The structure + this event's offset are always current — recompute.
            // NO slide decisions on stale or catch-up data: if the measured structure does not reach
            // the viewport (mid engage-reset / rebase re-measure), the computed range is the measure
            // FRONTIER, not the viewport — feeding it to CheckSlide fired spurious backward slides
            // that walked the window and offset away right after an engage-on-grow.
            if (ComputeVisibleRangeFromStructure(offset, viewport, out var f, out var l))
            {
                _itemsWindow.AutoTune(f, l);
                _itemsWindow.CheckSlide(f, l);
            }
        }

        //cells will get OnScrolled
        ViewportWasChanged = true;
    }

    /// <summary>
    /// Visible LOCAL index range computed fresh from the measured structure and the current scroll
    /// offset (content space: viewport top = -offset). Independent of the draw pipeline, so it stays
    /// correct on frames that never run the live DrawStack (plane blits). Linear scan bounded by the
    /// window cap. False when the structure is empty or nothing measured intersects the viewport.
    /// </summary>
    bool ComputeVisibleRangeFromStructure(ScaledPoint offset, ScaledRect viewport, out int first, out int last)
    {
        first = -1;
        last = -1;

        var structure = GetStackStructure();
        if (structure == null)
            return false;

        float vpTop = -offset.Pixels.Y;
        float vpBottom = vpTop + viewport.Pixels.Height;

        int lastMeasured = -1;
        // INDEXED, not foreach: the public FirstVisibleIndex/LastVisibleIndex getters call this from
        // ANY thread (app timers, DebugString, bindings) while measure/build mutates the structure.
        // Dictionary/List enumerators are version-checked, so GetChildren() there threw
        // "Collection was modified" and killed the app (device: DrawnCells DebugString timer).
        // Index access reads add-ordered storage without a version check: a concurrent mutation can
        // only make the scan see a shorter list or a stale cell, both harmless for a read-only range.
        int count = structure.Length;
        for (int i = 0; i < count; i++)
        {
            var cell = structure[i];
            if (cell == null || !cell.WasMeasured)
                continue;
            lastMeasured = cell.ControlIndex;
            if (cell.Destination.Bottom <= vpTop)
                continue;
            if (cell.Destination.Top >= vpBottom)
                break; // index-ordered: nothing below can intersect

            if (first < 0)
                first = cell.ControlIndex;
            last = cell.ControlIndex;
        }

        // Viewport rode PAST the materialized content (virtual-extent travel over a windowed source):
        // nothing intersects, but the slide logic must still see "we are at the end" to keep chasing.
        // ONLY when the structure is COMPLETE: during a re-measure catch-up (engage-on-grow reset,
        // rebase) "past the structure" means the FRONTIER hasn't reached the viewport yet — reporting
        // the frontier as visible fired spurious backward slides (frontier index reads as "at head").
        if (last < 0 && lastMeasured >= 0 && vpTop > 0
            && lastMeasured >= (EffectiveItemsSource?.Count ?? 0) - 1)
        {
            first = lastMeasured;
            last = lastMeasured;
        }

        return last >= 0;
    }

    /// <summary>
    /// Set by OnViewportWasChanged from Parent
    /// </summary>
    protected ScaledRect ParentViewport { get; set; }

    /// <summary>
    /// When true, auto-LoadMore (both edges) is frozen. A windowed source holds this for the span of a
    /// programmatic jump (rebase + ordered scroll + settle) so paging can't mutate ItemsSource mid-jump.
    /// Released by the consumer once the target lands. Replaces the former CellsStack-subclass field.
    /// </summary>
    public bool SuppressLoadMore { get; set; }

    /// <summary>
    /// Raised after a background-measured batch OR a backward head-insert has been APPLIED to the structure.
    /// The trim-after-measure hook for windowed sources (cap the opposite end once the new batch landed),
    /// without subclassing the layout.
    /// </summary>
    public event Action MeasurementApplied;

    internal void RaiseMeasurementApplied() => MeasurementApplied?.Invoke();

    /// <summary>
    /// Determines whether LoadMore should be triggered based on viewport position and measurement state.
    /// This prevents race conditions by considering background measurement progress.
    /// </summary>
    public virtual bool ShouldTriggerLoadMore(ScaledRect viewport)
    {
        return ShouldTriggerLoadMore(viewport, LoadMoreDirection.Bottom);
    }

    /// <summary>
    /// Determines whether LoadMore should be triggered for the requested edge direction,
    /// while honoring layout measurement readiness.
    /// </summary>
    public virtual bool ShouldTriggerLoadMore(ScaledRect viewport, LoadMoreDirection direction)
    {
        // Consumer-driven freeze: held true for the duration of a programmatic jump (rebase + ordered
        // scroll + its settle). An auto-LoadMore mid-jump would mutate ItemsSource and drift the pending
        // target. Windowed sources (e.g. a sliding window over a huge backing list) set this around a jump.
        if (SuppressLoadMore)
            return false;

        // Never auto-LoadMore while an ordered ScrollToIndex is in flight: it would head-insert/append a
        // batch and shift the view off the still-settling target. Generic for any windowed/jump scenario;
        // formerly required subclassing the layout to enforce.
        if (Parent is SkiaScroll orderedScroll &&
            (orderedScroll.OrderedScrollToIndexIsSet || orderedScroll.OrderedScrollTo.IsValid))
            return false;

        // No items source or not templated - can't load more
        if (!IsTemplated || EffectiveItemsSource == null || EffectiveItemsSource.Count == 0 || _isBackgroundMeasuring || UpdateLocks>0)
            return false;

        // Tiled-planes virtualization: bottom/top detection comes from the ESTIMATE (plane) content bounds
        // — the scroll already gated this by InternalViewportOffset vs _scrollMinY +/- LoadMoreOffset, i.e.
        // the plane edge, not the real viewport. The per-item measurement gates below don't apply (tiles
        // estimate positions for every item, they aren't measured up-front into StackStructure), so allow it.
        if (Parent is SkiaScroll planesScroll && planesScroll.UseVirtual)
            return true;

        // Backward (top) loads must not be gated by TAIL measurement progress: after a window
        // rebase/jump the tail may still be measuring in background while the user is already
        // at the start of the window asking for items above it.
        if (direction == LoadMoreDirection.Top)
        {
            if (!IsViewportAtStartOfMeasuredContent(viewport))
                return false;

            // Built-in source window: page in already-available older items INTERNALLY;
            // the app's LoadMoreTopCommand fires only when the window sits at the source head.
            if (_itemsWindow != null && _itemsWindow.CanSlideBackward)
            {
                _itemsWindow.RequestSlideBackward();
                return false;
            }

            return true;
        }

        // Still measuring existing items in background - don't load more yet
        if (_isBackgroundMeasuring && _backgroundMeasurementProgress < EffectiveItemsSource.Count - 1)
        {
            Debug.WriteLine($"[ShouldTriggerLoadMore] Still measuring items (progress: {_backgroundMeasurementProgress}/{EffectiveItemsSource.Count}), not triggering LoadMore");
            return false;
        }

        // Haven't finished measuring all existing items - don't load more yet
        if (LastMeasuredIndexLocal < EffectiveItemsSource.Count - 1)
        {
            //Debug.WriteLine($"[ShouldTriggerLoadMore] Haven't measured all items yet (LastMeasuredIndexLocal: {LastMeasuredIndexLocal}/{ItemsSource.Count}), not triggering LoadMore");
            return false;
        }

        if (!IsViewportAtEndOfMeasuredContent(viewport))
            return false;

        // Built-in source window: page in already-available newer items INTERNALLY;
        // the app's LoadMoreCommand fires only when the window sits at the source tail.
        if (_itemsWindow != null && _itemsWindow.CanSlideForward)
        {
            _itemsWindow.RequestSlideForward();
            return false;
        }

        return true;
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
            return LastMeasuredIndexLocal == EffectiveItemsSource.Count - 1;
        }

        return true;
    }

    /// <summary>
    /// Checks if top-edge load more can be triggered.
    /// </summary>
    protected virtual bool IsViewportAtStartOfMeasuredContent(ScaledRect viewport)
    {
        if (StackStructure == null || StackStructure.Length == 0)
            return false;

        if (MeasureItemsStrategy == MeasuringStrategy.MeasureVisible)
        {
            // Head of the content must be measured (no gap at index 0). While a head-insert
            // (backward LoadMore) is in flight, indices 0..N-1 are vacated until its commit,
            // which naturally serializes backward loads — no retrigger until content landed.
            return _measuredItems.ContainsKey(0);
        }

        return true;
    }

    public bool IsBackgroundMeasuring => _isBackgroundMeasuring;
    public int BackgroundMeasurementProgress => _backgroundMeasurementProgress;
    public int TotalMeasuredItems => _measuredItems.Count;

    // *Local = window/pipeline space (indices into EffectiveItemsSource). Without the built-in
    // window these equal the public values; the pipeline always works in this space.
    public int FirstMeasuredIndexLocal { get; protected set; }

    public int LastMeasuredIndexLocal
    {
        get;
        protected set;
    }
    public int FirstVisibleIndexLocal { get; protected set; }
    public int LastVisibleIndexLocal { get; protected set; }

    // PUBLIC API speaks ItemsSource (global) space: when the built-in window (ItemsWindow) is
    // engaged, local indices are offset by WindowStart; -1 sentinels pass through unmapped.
    //
    // PUBLIC "visible" = REALLY intersecting the viewport, computed fresh from structure + offset on
    // read. The protected *Local fields carry the DRAWN band instead (viewport + overscan, plane
    // record width) — internal machinery (slide margins, AutoTune) is calibrated to that and plane
    // blit frames don't refresh it; exposing it publicly reported "18 visible" for 8 on screen.

    /// <summary>Index of the first item REALLY visible in the viewport, in ItemsSource space.</summary>
    public int FirstVisibleIndex
    {
        get
        {
            if (ComputeVisibleRangeFromStructure(_lastVisibilityOffset, ParentViewport, out var f, out _))
                return f + (_itemsWindow?.WindowStart ?? 0);
            return FirstVisibleIndexLocal < 0 ? FirstVisibleIndexLocal : FirstVisibleIndexLocal + (_itemsWindow?.WindowStart ?? 0);
        }
    }

    /// <summary>Index of the last item REALLY visible in the viewport, in ItemsSource space.</summary>
    public int LastVisibleIndex
    {
        get
        {
            if (ComputeVisibleRangeFromStructure(_lastVisibilityOffset, ParentViewport, out _, out var l))
                return l + (_itemsWindow?.WindowStart ?? 0);
            return LastVisibleIndexLocal < 0 ? LastVisibleIndexLocal : LastVisibleIndexLocal + (_itemsWindow?.WindowStart ?? 0);
        }
    }

    /// <summary>Latest scroll offset seen by OnViewportWasChanged — feeds the public visibility reads.</summary>
    protected ScaledPoint _lastVisibilityOffset;

    // TECHNICAL accessors: the DRAWN band (viewport + overscan, what the pipeline/plane actually
    // painted last), ItemsSource space. For machinery-level consumers (edge/frontier checks) that
    // were calibrated to these semantics — NOT what a user sees on screen (use *VisibleIndex).
    /// <summary>First index of the last DRAWN band (includes overscan), ItemsSource space.</summary>
    public int FirstVisibleIndexDrawn =>
        FirstVisibleIndexLocal < 0 ? FirstVisibleIndexLocal : FirstVisibleIndexLocal + (_itemsWindow?.WindowStart ?? 0);

    /// <summary>Last index of the last DRAWN band (includes overscan), ItemsSource space.</summary>
    public int LastVisibleIndexDrawn =>
        LastVisibleIndexLocal < 0 ? LastVisibleIndexLocal : LastVisibleIndexLocal + (_itemsWindow?.WindowStart ?? 0);

    /// <summary>Highest measured index, in ItemsSource space.</summary>
    public int LastMeasuredIndex =>
        LastMeasuredIndexLocal < 0 ? LastMeasuredIndexLocal : LastMeasuredIndexLocal + (_itemsWindow?.WindowStart ?? 0);

    /// <summary>Lowest measured index, in ItemsSource space.</summary>
    public int FirstMeasuredIndex =>
        FirstMeasuredIndexLocal < 0 ? FirstMeasuredIndexLocal : FirstMeasuredIndexLocal + (_itemsWindow?.WindowStart ?? 0);

    /// <summary>
    /// Percentage of items that have been measured (0.0 to 1.0)
    /// </summary>
    protected float MeasuredItemsPercentage =>
        EffectiveItemsSource?.Count > 0 ? (float)(LastMeasuredIndexLocal + 1) / EffectiveItemsSource.Count : 0f;

    /// <summary>
    /// Represents a pending structure change to be applied during rendering
    /// </summary>
        [DebuggerDisplay("{Type} {Count} at {StartIndex}, ")]
    public class StructureChange
    {
        public StructureChange(StructureChangeType type, long stamp)
        {
            Type = type;
            Stamp = stamp;
        }

        public long Stamp { get; set; }

        /// <summary>
        /// Value of _itemsShiftEpoch this change was computed under; background measurement
        /// changes from an older epoch reference pre-shift indices/positions and must be dropped.
        /// </summary>
        public int Epoch { get; set; }

        public StructureChangeType Type { get; set; }
        public Vector2? OffsetOthers { get; set; }
        public int StartIndex { get; set; }
        public int Count { get; set; }

        /// <summary>
        /// For Replace: number of OLD items being replaced. May differ from Count (new items)
        /// when a range Replace shrinks or grows the collection (e.g. windowed list trim/jump).
        /// ApplyReplaceChange removes OldCount entries then adds Count entries; reusing Count for
        /// both halves leaves stale structure entries on a shrinking Replace.
        /// </summary>
        public int OldCount { get; set; }

        public List<object> Items { get; set; } // For Add/Replace

        /// <summary>
        /// Immutable data-contexts snapshot captured on the MUTATING (UI) thread when this change was staged,
        /// so the deferred render-thread apply (ApplyInsertShift/ApplyRemoveShift) publishes it instead of
        /// re-reading the live ItemsSource off-thread (the Android tear -> overlapping cells / crash).
        /// </summary>
        public System.Collections.IList ContextsSnapshot { get; set; }

        public int TargetIndex { get; set; } // For Move
        public List<MeasuredItemInfo> MeasuredItems { get; set; } // For BackgroundMeasurement
        public int? InsertAtIndex { get; set; } // Where to insert in existing structure
        public bool IsInsertOperation { get; set; } // Flag for insert vs append
        public bool IsVisible { get; set; } // For VisibilityChange

        /// <summary>
        /// Remove was at the collection tail at the moment it was raised (window trim).
        /// Must be detected at staging time: by the time the change is applied the live
        /// ItemsSource may already contain a subsequent prepend from the same frame,
        /// making StartIndex == Count checks unreliable.
        /// </summary>
        public bool TailRemoval { get; set; }

        /// <summary>
        /// Remove was emitted as the remove-half of a Replace, NOT a window head/tail trim.
        /// The trim fast paths assume cells beyond the removed block are real survivors that keep
        /// their positions; for a Replace that is false (the range is being swapped), so they must
        /// be skipped or the structure keeps stale survivors and rebases content into empty space.
        /// </summary>
        public bool SkipTrimFastPath { get; set; }

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

    public int BackgroundMeasurementBatchSize { get; set; } = 10; // Measure per batch

    // DEBUG: per-batch delay in background measurement. Crank up (e.g. 300) to simulate slow-device
    // (Android) measurement so it is still in flight when the viewport reaches a LoadMore point — the
    // condition under which the offset-compensation / background-render structure race reproduces.
    public static int DebugBackgroundMeasureDelayMs = 0;

    // DEBUG: when true, DrawStack paints a colored left-edge stripe + top seam line on each visible cell,
    // colored by ControlInStack.DebugMeasureBatch (0 = initial measure, 1..N = background batches). Lets you
    // SEE where background-measured cells were glued in and whether a seam is mis-positioned/overlapping.
    public static bool DebugDrawMeasureBatches;
    private int _debugMeasureBatch;

    // Track measurement state
    private readonly ConcurrentDictionary<int, MeasuredItemInfo> _measuredItems = new();
    private volatile int _backgroundMeasurementProgress = 0;

    // Bumped by ShiftMeasurementIndices; guards against straggler background batches
    // (measured under old indices/positions) landing after a shift. See StructureChange.Epoch.
    private volatile int _itemsShiftEpoch;

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

    #region ITEM-KEYED MEASUREMENT MEMO

    // _measuredItems is keyed by LOCAL ControlIndex, so a full-collection Replace (windowed list jump)
    // MUST clear it (ResetMeasurementForReplace) — the same slots now host different data. That throws
    // away perfectly good sizes and forces a full remeasure of the new window every jump.
    //
    // This memo is keyed by the DATA ITEM (object identity) + the constraint WIDTH it was measured under,
    // so it survives window swaps and view recycling: revisit a region and its cells are seeded from here
    // instead of remeasured. LRU-capped (default 1000) so it cannot grow unbounded.
    //
    // Contract: an entry is valid only while the item's content AND the available width are unchanged.
    // Re-measuring an item (e.g. RemeasureSingleItemInBackground after image load, or a foreground measure
    // under a new width) overwrites its entry via StoreMemoSize, so refreshes happen naturally. A consumer
    // that mutates an item's content in place without triggering a remeasure must call ClearMeasurementCache.

    private sealed class MeasureMemoEntry
    {
        public object Item;
        public float Width;
        public ScaledSize Measured;
    }

    private readonly object _memoLock = new();
    private readonly Dictionary<object, LinkedListNode<MeasureMemoEntry>> _memo = new();
    private readonly LinkedList<MeasureMemoEntry> _memoLru = new(); // most-recently-used at head

    /// <summary>
    /// Max number of item-keyed measured sizes retained across window swaps / recycling (LRU-evicted).
    /// Set to 0 (or less) to disable the memo entirely. Default 1000.
    /// </summary>
    public int MeasurementCacheCapacity { get; set; } = 1000;

    /// <summary>Drop all memoized item sizes (call after mutating item content without a remeasure).</summary>
    public void ClearMeasurementCache()
    {
        lock (_memoLock)
        {
            _memo.Clear();
            _memoLru.Clear();
        }
    }

    private bool TryGetMemoSize(object item, float width, out ScaledSize size)
    {
        size = ScaledSize.Default;
        if (item == null || MeasurementCacheCapacity <= 0)
            return false;

        lock (_memoLock)
        {
            if (_memo.TryGetValue(item, out var node) && Math.Abs(node.Value.Width - width) < 0.5f)
            {
                _memoLru.Remove(node);
                _memoLru.AddFirst(node);
                size = node.Value.Measured;
                return true;
            }
        }

        return false;
    }

    private void StoreMemoSize(object item, float width, ScaledSize measured)
    {
        if (item == null || MeasurementCacheCapacity <= 0)
            return;
        if (width <= 0 || float.IsInfinity(width) || measured == null || measured.IsEmpty)
            return;

        // Clone: MeasureChild may return the (recycled) control's own MeasuredSize instance, which gets
        // mutated when that control is re-measured for another item — storing the live ref would alias.
        var snapshot = measured.Clone();

        lock (_memoLock)
        {
            if (_memo.TryGetValue(item, out var node))
            {
                node.Value.Width = width;
                node.Value.Measured = snapshot;
                _memoLru.Remove(node);
                _memoLru.AddFirst(node);
            }
            else
            {
                node = new LinkedListNode<MeasureMemoEntry>(
                    new MeasureMemoEntry { Item = item, Width = width, Measured = snapshot });
                _memo[item] = node;
                _memoLru.AddFirst(node);

                while (_memo.Count > MeasurementCacheCapacity && _memoLru.Last != null)
                {
                    var last = _memoLru.Last;
                    _memoLru.RemoveLast();
                    _memo.Remove(last.Value.Item);
                }
            }
        }
    }

    // Reads the data item at a window-local index for memo keying, off the render thread. ItemsSource may
    // mutate concurrently; a stale/out-of-range read just misses the memo (epoch guard drops stale batches).
    private object GetItemForMemo(int index)
    {
        var source = EffectiveItemsSource;
        if (source == null || index < 0 || index >= source.Count)
            return null;
        try
        {
            return source[index];
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region PREPARED VIEWS PIPELINE

    // Render thread never measures a templated cell in this mode: cells are bound+measured ahead of
    // scrolling by CellPreparationService (dedicated worker), and an unprepared cell that still slips
    // into the viewport draws its placeholder (skeleton) at the structure's reserved slot until the
    // worker catches up. This kills the rhythmic fling spikes caused by first-appearing cells
    // sync-measuring on the render thread (a single cell measure can exceed a frame on Debug).

    /// <summary>
    /// Prepared-views pipeline: never measure templated cells on the render thread; prepare
    /// (bind+measure) the REAL cell views on a background worker ahead of scrolling instead, drawing
    /// placeholder skeletons (<see cref="SkiaControl.DrawPlaceholder"/>) for the frames a cell is not
    /// ready yet. Built-in for MeasureItemsStrategy=MeasureVisible — a subclass that must fall back to
    /// legacy render-thread cell measuring can override this to return false. Works for both recycling
    /// modes: the pool becomes a context-indexed reservoir (see ViewsAdapter.UsesGenericPool) — with
    /// RecyclingTemplate.Disabled every context keeps its own prepared cell; with recycling enabled a
    /// small pool cycles cells with context affinity, the preparation worker rebinding evicted cells to
    /// upcoming contexts ahead of the scroll.
    /// </summary>
    public virtual bool UsePreparedViews => MeasureItemsStrategy == MeasuringStrategy.MeasureVisible;

    internal bool UsePreparedViewsActive =>
        UsePreparedViews && IsTemplated
                         && MeasureItemsStrategy == MeasuringStrategy.MeasureVisible;

    /// <summary>
    /// Diagnostics: how many times DrawStack measured a templated cell synchronously on the render
    /// thread. With <see cref="UsePreparedViews"/> active this must stay flat during scrolling —
    /// harness repros assert on it.
    /// </summary>
    public long CountRenderThreadCellMeasures;

    /// <summary>
    /// Called on the CellPreparationService worker thread each time a cell finishes off-thread
    /// preparation (bound + measured, ready to draw real content). Default no-op. A plane-caching
    /// subclass overrides this to mark its recorded plane STALE: the plane may have that cell baked
    /// in as a skeleton, and without a re-record it keeps blitting the skeleton long after the cell
    /// is ready (visible as placeholders while scrolling at moderate speed).
    /// </summary>
    protected virtual void OnCellPreparedOffthread()
    {
    }

    /// <summary>
    /// True when any currently visible templated cell is not prepared (unmeasured, mid-preparation,
    /// or bound to a stale context). Used by the plane blit path at REST to detect skeletons frozen
    /// into the plane (cells that entered the viewport during pure-blit deceleration frames never met
    /// a live DrawStack) so it can force one live frame to heal them. Cheap: single-lock range scan.
    /// </summary>
    internal bool AnyVisibleCellUnprepared()
    {
        if (!UsePreparedViewsActive)
            return false;

        var first = FirstVisibleIndexLocal;
        var last = LastVisibleIndexLocal;
        if (first < 0 || last < first)
            return false;

        return ChildrenFactory.AnyUnpreparedInRange(first, last);
    }

    /// <summary>
    /// Diagnostics: GAP-RESCUE inline measures. With <see cref="UsePreparedViews"/> active, a visible cell
    /// the preparation worker has NOT prepared in time (fast fling, jump landing, recycling rebind) is
    /// bound+measured synchronously on the render thread instead of showing a skeleton/hole — the same
    /// cost the non-prepared direct-draw path pays for EVERY appearing cell, paid here only when the
    /// worker lost the race. Counted separately so harness repros can still assert the prepared pipeline
    /// itself stays measure-free during warm scrolling.
    /// </summary>
    public long CountGapRescueMeasures;

    // Visible cells found unprepared during the current draw pass (render thread only) — they get
    // top priority in the want-list so their skeletons materialize first.
    private readonly List<int> _prepVisibleUnprepared = new();

    // ControlIndexes of the cells the last draw treated as VISIBLE (occupying the viewport). Written on
    // the render thread in DrawStackVisibleChildren, read by ApplyStackMeasureResult to reject publishing
    // an incoming structure whose VISIBLE slots aren't measured yet (would blank on screen) while still
    // allowing normal MeasureVisible publishes where only OFF-SCREEN cells are unmeasured.
    private readonly HashSet<int> _lastVisibleControlIndexes = new();

    // Scratch: measured ControlIndexes of the CURRENT live structure, rebuilt only on frames that produced
    // a measure. Lets ApplyStackMeasureResult verify the current structure is a valid fallback for the
    // visible slots before holding it (see there). Reused to avoid per-publish allocation.
    private readonly HashSet<int> _measuredCurrentScratch = new();
    private int _prepPrevFirstVisible;
    private bool _prepPostedNonEmpty;

    /// <summary>
    /// Called by CellPreparationService on its worker thread: bind (pool-owned cells only — never a
    /// live drawn instance) and measure the REAL view for this index, feeding the item-keyed size memo
    /// so the background structure pass memo-hits instead of re-measuring. Same constraints as the
    /// background structure measurement so sizes agree.
    /// </summary>
    internal void PrepareCellOffthread(int index)
    {
        if (IsDisposed || IsDisposing || !UsePreparedViewsActive)
            return;

        var source = EffectiveItemsSource;
        if (source == null || index < 0 || index >= source.Count)
            return;

        var adapter = ChildrenFactory;
        if (adapter == null || adapter.IsDisposed || !adapter.TemplatesAvailable)
            return;

        var view = adapter.GetViewForPreparation(index, out var fromPool);
        if (view == null)
            return;

        var columnsCount = (Split > 0) ? Split : 1;
        var columnWidth = ComputeColumnWidth(columnsCount);
        float w = columnWidth, h = float.PositiveInfinity;
        if (Type == LayoutType.Row)
        {
            w = float.PositiveInfinity;
            h = columnWidth;
        }

        if (w <= 0 && h <= 0)
        {
            if (fromPool)
                adapter.ReleaseMeasuringView(view);
            return;
        }

        // Mutual exclusion with the render thread's GAP-RESCUE inline measure: exactly one side may
        // measure this instance. Loser skips — the index stays wanted and is re-posted next pass.
        if (Interlocked.CompareExchange(ref view.MeasureClaim, 1, 0) != 0)
        {
            if (fromPool)
                adapter.ReleaseMeasuringView(view);
            return;
        }

        ScaledSize measured = null;
        view.IsPreparingOffthread = true;
        try
        {
            measured = MeasureChild(view, w, h, RenderingScale);
        }
        catch (Exception e)
        {
            Super.Log(e);
        }
        finally
        {
            view.IsPreparingOffthread = false;
            Interlocked.Exchange(ref view.MeasureClaim, 0);
        }

        if (measured != null && !measured.IsEmpty)
        {
            var item = GetItemForMemo(index);
            if (item != null && ReferenceEquals(view.BindingContext, item))
                StoreMemoSize(item, w, measured);

            OnCellPreparedOffthread();
        }

        if (fromPool)
            adapter.ReleaseMeasuringView(view); // parks it bound+measured in the context-indexed pool

        Repaint();
    }

    /// <summary>
    /// Render thread, once per draw pass: builds the priority want-list (visible unprepared first, then
    /// ahead of the scroll direction, then behind) and posts it to CellPreparationService. Posts nothing
    /// while everything nearby is prepared (steady state = zero allocations here).
    /// </summary>
    private void PostCellPreparationWants()
    {
        var first = FirstVisibleIndexLocal;
        var last = LastVisibleIndexLocal;
        var count = EffectiveItemsSource?.Count ?? 0;

        if (first < 0 || last < 0 || count == 0)
        {
            if (_prepPostedNonEmpty)
            {
                CellPreparationService.Post(this, null);
                _prepPostedNonEmpty = false;
            }

            return;
        }

        var visibleCount = Math.Max(1, last - first + 1);
        var forward = first >= _prepPrevFirstVisible; // scrolling toward higher indices
        _prepPrevFirstVisible = first;

        // The prep horizon must FIT the pool: demanding more prepared cells than the pool can hold
        // makes every preparation evict another wanted context's cell — musical-chairs churn where
        // cells constantly rebind/rebake and nothing ever stays prepared (visible as jaggy scroll).
        // Budget = pool ceiling minus what the viewport itself consumes, minus a small reserve for
        // transient double-realizations.
        var poolBudget = ChildrenFactory.PoolMaxSize - visibleCount - 4;
        if (poolBudget < 0) poolBudget = 0;
        var ahead = Math.Min(visibleCount * 2, (poolBudget * 2) / 3);
        var behind = Math.Min(visibleCount, poolBudget - ahead);

        List<int> want = null;

        void AddIfUnprepared(int i)
        {
            if (i < 0 || i >= count || ChildrenFactory.IsViewPrepared(i))
                return;
            (want ??= new List<int>()).Add(i);
        }

        foreach (var i in _prepVisibleUnprepared)
        {
            (want ??= new List<int>()).Add(i);
        }

        if (forward)
        {
            for (int i = last + 1; i <= last + ahead; i++) AddIfUnprepared(i);
            for (int i = first - 1; i >= first - behind; i--) AddIfUnprepared(i);
        }
        else
        {
            for (int i = first - 1; i >= first - ahead; i--) AddIfUnprepared(i);
            for (int i = last + 1; i <= last + behind; i++) AddIfUnprepared(i);
        }

        if (want != null)
        {
            CellPreparationService.Post(this, want);
            _prepPostedNonEmpty = true;
        }
        else if (_prepPostedNonEmpty)
        {
            CellPreparationService.Post(this, null);
            _prepPostedNonEmpty = false;
        }
    }

    #endregion

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
        if (!IsTemplated || EffectiveItemsSource == null || EffectiveItemsSource.Count <= startFromIndex)
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

        CancellationTokenSource myCts;
        lock (_measurementLock)
        {
            myCts = new CancellationTokenSource();
            _backgroundMeasurementCts = myCts;
            _isBackgroundMeasuring = true;
        }

        var cancellationToken = myCts.Token;

        Tasks.StartDelayed(TimeSpan.FromMilliseconds(50), () =>
        {
#if BROWSER
            // ST WASM: never launch the pass via Task.Run — the queued item can starve indefinitely
            // under continuous rAF + input load, freezing the measurement frontier mid-scroll (the
            // timer callback that got us here fires reliably; a threadpool item does not). Run the
            // async pass directly: the first batch executes inline here, subsequent batches yield
            // via the pass's own Task.Delay(1) timers.
            _backgroundMeasurementTask = RunBackgroundMeasurementPass(constraints, scale, startFromIndex, cancellationToken, context, myCts);
#else
            _backgroundMeasurementTask = Task.Run(async () =>
            {
                await RunBackgroundMeasurementPass(constraints, scale, startFromIndex, cancellationToken, context, myCts);
            });
#endif
        });
    }

    async Task RunBackgroundMeasurementPass(SKRect constraints, float scale, int startFromIndex,
        CancellationToken cancellationToken, BackgroundMeasurementContext context, CancellationTokenSource myCts)
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
                // Only the CURRENT pass may clear the flag. A cancelled stale task finishing late
                // used to clobber it to false while a newer pass was running — every observer then
                // saw "idle", cancelled the live pass and restarted it, which could livelock
                // measurement entirely (each restart killed within its own 50ms start delay).
                if (ReferenceEquals(_backgroundMeasurementCts, myCts))
                {
                    _isBackgroundMeasuring = false;
                }
            }
        }
    }

    /// <summary>
    /// Restarts background measurement from the first really-unmeasured index if it is idle.
    /// Used by the ordered-scroll gate: after a window jump the draw-side restart can starve behind
    /// pending structure changes, leaving the scroll target's geometry an estimate forever. Returns
    /// true when measurement is running or was started, false when there is nothing to measure or
    /// measurement cannot start yet (caller then falls back instead of waiting).
    /// </summary>
    public bool KickBackgroundMeasurement()
    {
        if (!IsTemplated || MeasureItemsStrategy != MeasuringStrategy.MeasureVisible
            || EffectiveItemsSource == null || EffectiveItemsSource.Count == 0)
            return false;

        if (_isBackgroundMeasuring)
            return true;

        if (HasPendingStructureChanges)
            return false; // apply first; the next draw's retry will kick again

        if (_lastMeasuredForWidth <= 0 && _lastMeasuredForHeight <= 0)
            return false; // never measured yet, no constraints to measure against

        var next = Math.Max(0, LastMeasuredIndexLocal + 1);
        while (next < EffectiveItemsSource.Count && _measuredItems.ContainsKey(next))
            next++;

        if (next >= EffectiveItemsSource.Count)
            return false; // everything is in the memo already

        var constraints = new SKRect(0, 0, _lastMeasuredForWidth, _lastMeasuredForHeight);
        StartBackgroundMeasurement(constraints, RenderingScale, next);
        return true;
    }

    /// <summary>
    /// Remeasures a single item in the background and updates it in the existing structure
    /// </summary>
    public void RemeasureSingleItemInBackground(int itemIndex)
    {
        if (!IsTemplated || EffectiveItemsSource == null || itemIndex < 0 || itemIndex >= EffectiveItemsSource.Count)
        {
            Debug.WriteLine(
                $"[RemeasureSingleItemInBackground] Invalid parameters: IsTemplated={IsTemplated}, EffectiveItemsSource={EffectiveItemsSource?.Count}, itemIndex={itemIndex}");
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

        if (IsTemplated && EffectiveItemsSource.Count > 0)
        {
            int measuredCount = 0;
            var itemsCount = EffectiveItemsSource.Count;
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
                base.GetOnScreenVisibleArea(
                    new(null, rectForChildrenPixels, scale,
                        new[] { new KeyValuePair<string, object>("InitialMeasureVisibleArea", true) }),
                    new(inflate, inflate));

            // When this list is the content of a planes (Managed) scroll, the scroll renders planes that
            // are TWO viewports tall and the FORWARD plane sits two viewports ahead. Measuring only the
            // visible viewport leaves the current plane's far half AND the forward (green) plane with no
            // measured items -> they render empty. Extend the measure area along the scroll axis to cover
            // the current plane plus the forward plane (~4 viewports) so both are filled up-front.
            if (Parent is SkiaScroll planesScroll && planesScroll.UseVirtual)
            {
                const float planesAheadViewports = 3f; // visible(1) + 3 => 4 viewports = current + forward plane
                var vp = planesScroll.Viewport.Pixels;
                var px = visibleArea.Pixels;
                if (Type == LayoutType.Column && vp.Height >= 1)
                    px = new SKRect(px.Left, px.Top, px.Right, px.Bottom + vp.Height * planesAheadViewports);
                else if (Type == LayoutType.Row && vp.Width >= 1)
                    px = new SKRect(px.Left, px.Top, px.Right + vp.Width * planesAheadViewports, px.Bottom);
                visibleArea = ScaledRect.FromPixels(px, scale);
            }

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
                    ChildrenFactory.ReleaseViewInUse(cell);
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

            FirstVisibleIndexLocal = -1;
            FirstMeasuredIndexLocal = 0;
            LastVisibleIndexLocal = -1;
            LastMeasuredIndexLocal = measuredCount - 1;

            if (measuredCount > 0)
            {
                if (this.Type == LayoutType.Column)
                {
                    if (measuredCount < itemsCount)
                    {
                        var averageSize = stackHeight / measuredCount;
                        // MeasureVisible: estimate ALL remaining items, not just one. A start-anchored
                        // inverted scroll derives its offset from the full content extent; a +1-avg
                        // under-estimate misplaces the anchor so even the measured visible cells land
                        // off-screen until background measurement completes the real extent.
                        if (MeasureItemsStrategy == MeasuringStrategy.MeasureVisible)
                            stackHeight += averageSize * (itemsCount - measuredCount);
                        else
                            stackHeight += averageSize;
                    }
                }
                else if (this.Type == LayoutType.Row)
                {
                    if (measuredCount < itemsCount)
                    {
                        var averageSize = stackWidth / measuredCount;
                        if (MeasureItemsStrategy == MeasuringStrategy.MeasureVisible)
                            stackWidth += averageSize * (itemsCount - measuredCount);
                        else
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

                ScaledSize measured;

                // Memo fast path: a known item size at this width seeds the cell WITHOUT instantiating or
                // measuring a view. Background measurement only needs sizes (off-screen, never drawn), so
                // this is the whole win after a window jump — revisited cells skip GetViewForIndex+Measure.
                var dataItem = GetItemForMemo(itemIndex);
                if (dataItem != null && TryGetMemoSize(dataItem, availableWidth, out var cachedSize))
                {
                    measured = cachedSize;
                    cell.Measured = measured;
                    cell.WasMeasured = true;
                    // Mirror MeasureAndArrangeCell: set BOTH Area and Destination. ComputeBottomOfRow (used to
                    // position the NEXT background batch) reads cell.Area.Top — leaving Area default (Top=0)
                    // makes every memo cell report bottom=height, so the next batch stacks at the top (overlap
                    // -> collapsed content -> blank). Area.Top must be the cell's real Y (rectForChild.Top).
                    cell.Area = rectForChild;
                    cell.Destination = new SKRect(currentX, currentY,
                        currentX + availableWidth, currentY + measured.Pixels.Height);
                }
                else
                {
                    var child = ChildrenFactory.GetViewForIndex(itemIndex, template, 0, true);
                    if (template == null && child != null)
                    {
                        cellsToRelease.Add(child);
                    }

                    if (child?.CanDraw != true)
                        continue;

                    measured = MeasureAndArrangeCell(rectForChild, cell, child, constraints, scale);
                    cell.Measured = measured;
                    cell.WasMeasured = true;
                }

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

                //Debug.WriteLine($"[MeasureBatchInBackground] Measured item {itemIndex} at ({cell.Destination.Left:F1},{cell.Destination.Top:F1}): {measured.Pixels.Width:F1}x{measured.Pixels.Height:F1}");
            }
        }
        finally
        {
            if (template != null)
            {
                ChildrenFactory.ReleaseTemplateInstance(template);
            }

            // Transient measuring cells (RecyclingTemplate.Disabled: template == null so these were
            // popped from the pool) must go BACK to the pool, on completion AND on cancel. Using
            // ReleaseViewInUse here parked them in _cellsInUseViews without a pool return; on a
            // cancel+restart the re-measure popped fresh cells and overwrote the map slot, orphaning
            // the parked cell (never pooled, never disposed) and draining the pool. ReleaseMeasuringView
            // removes the map entry and returns to the generic pool — symmetric with the pop.
            foreach (var cell in cellsToRelease)
            {
                ChildrenFactory.ReleaseMeasuringView(cell);
            }
        }

        return measuredBatch;
    }

    /// <summary>
    /// Integrates measured batch into the main structure
    /// </summary>
    private void IntegrateMeasuredBatch(List<MeasuredItemInfo> measuredBatch, float scale,
        BackgroundMeasurementContext context = null,
        BackgroundMeasurementStartingPosition startingPosition = null,
        int epoch = 0)
    {
        if (measuredBatch?.Count > 0)
        {
            // Indices were shifted while this batch was measuring: its ControlIndexes and
            // positions are stale — writing them would corrupt the measurement cache/structure.
            if (epoch != _itemsShiftEpoch)
            {
                Debug.WriteLine(
                    $"[IntegrateMeasuredBatch] DROPPED stale batch of {measuredBatch.Count} (epoch {epoch} != {_itemsShiftEpoch})");
                return;
            }

            var count = 0;
            foreach (var item in measuredBatch)
            {
                if (SkiaLayout.IsTraced(item.Cell.ControlIndex))
                    SkiaLayout.TraceIdx(item.Cell.ControlIndex, "BG-MEASURE-integrate",
                        $"measuredH={item.Cell.Measured.Pixels.Height:0} destTop={item.Cell.Destination.Top:0} destH={item.Cell.Destination.Height:0} epoch={epoch} insertAt={context?.InsertAtIndex}");

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
                    Count = count,
                    Epoch = epoch
                });
            }

            // Same reason as in StageStructureChange: the queue is drained by ApplyStructureChanges
            // inside Paint, and a cached layout never repaints on its own - freshly measured items
            // would stay invisible until something else invalidated the cache.
            if (UsingCacheType != SkiaCacheType.None)
            {
                InvalidateCache();
            }

            // Recalculate estimated content size
            //UpdateEstimatedContentSize(scale);

            //Debug.WriteLine($"[IntegrateMeasuredBatch] Integrated {measuredBatch.Count} items, total measured: {_measuredItems.Count}, last index: {LastMeasuredIndexLocal}");
        }
    }

    // guards the stale-stamp adapter resync post (one in flight is enough — it reads live state)
    private volatile bool _adapterResyncPosted;

    /// <summary>
    /// Applies all pending structure changes to StackStructure - called from rendering pipeline
    /// </summary>
    public void ApplyStructureChanges()
    {
        List<StructureChange> changesToProcess = null;

        // Get all pending changes atomically
        lock (_structureChangesLock)
        {
            if (GetStackStructure() == null || _pendingStructureChanges.Count == 0)
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
                // The stage handlers defer the adapter refresh for Remove/head-Add to THIS apply
                // (see HandleStructurePreservingRemove) — a dropped change must still sync the adapter
                // or its snapshot stays stale forever. NEVER reuse change.ContextsSnapshot here: it is
                // frozen at stage time, and re-applying an ancient one flips the adapter BACK to an old
                // window size (browser: Data oscillating 48/64/128 + pool churn spikes). Resync from
                // the CURRENT collection with a snapshot captured on the mutating (UI) thread.
                if (change.ContextsSnapshot != null &&
                    change.Type is StructureChangeType.Remove or StructureChangeType.Add &&
                    !_adapterResyncPosted)
                {
                    _adapterResyncPosted = true;
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _adapterResyncPosted = false;
                        var source = EffectiveItemsSource;
                        if (source == null || ChildrenFactory == null)
                            return;
                        var fresh = ChildrenFactory.CaptureContextsSnapshot(source);
                        ChildrenFactory.InitializeSoft(false, source, GetTemplatesPoolLimit(), fresh);
                    });
                }

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
    /// FIXED: Now applies structure modifications atomically with LastMeasuredIndexLocal updates
    /// </summary>
    protected virtual void ApplyBackgroundMeasurementChange(StructureChange change)
    {
        try
        {
            if (change.Epoch != _itemsShiftEpoch)
            {
                // Staged before a shift (head insert etc.) was applied: indices/positions are stale.
                Debug.WriteLine(
                    $"[ApplyBackgroundMeasurementChange] DROPPED stale change of {change.Count} (epoch {change.Epoch} != {_itemsShiftEpoch})");
                return;
            }

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

                    // Update LastMeasuredIndexLocal atomically with structure changes
                    var maxIndex = change.MeasuredItems.Max(x => x.Cell.ControlIndex);
                    if (maxIndex > LastMeasuredIndexLocal)
                    {
                        LastMeasuredIndexLocal = maxIndex;
                    }

                    UpdateProgressiveContentSize();

                    //Debug.WriteLine($"[StackStructure] Applied {allRows.Count} rows atomically, LastMeasuredIndexLocal: {LastMeasuredIndexLocal}");
                }
            }

        }
        catch (Exception e)
        {
            Super.Log(e);
        }
        finally
        {
            OnPropertyChanged(nameof(DebugString));
        }

        RaiseMeasurementApplied();
    }

    /// <summary>
    /// Applies offset compensation for background measurements when position changes occurred
    /// This handles the race condition where visibility changes offset the structure
    /// while background measurements were calculated with the original positions
    /// </summary>
    private void ApplyOffsetCompensationForBackgroundMeasurement(StructureChange change)
    {
        var startingPos = change.StartingPosition;
        var currentStructure = GetStackStructure();

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
        var debugBatch = System.Threading.Interlocked.Increment(ref _debugMeasureBatch); // DEBUG overlay tag

        for (int i = 0; i < measuredItems.Count; i++)
        {
            var item = measuredItems[i];
            item.Cell.DebugMeasureBatch = debugBatch;
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
        var debugBatch = System.Threading.Interlocked.Increment(ref _debugMeasureBatch); // DEBUG overlay tag

        for (int i = 0; i < measuredItems.Count; i++)
        {
            var item = measuredItems[i];
            item.Cell.DebugMeasureBatch = debugBatch;
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
        if (!IsTemplated || EffectiveItemsSource == null)
            return;

        // Create context for insert operation
        var context = new BackgroundMeasurementContext
        {
            InsertAtIndex = insertAtIndex,
            InsertCount = insertCount,
            StartMeasuringFrom = insertAtIndex
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

        // Head inserts and ALL MeasureFirst adds defer the adapter refresh to this apply — whichever
        // branch runs must sync exactly once, in stage order: branches calling ApplyInsertShift set
        // this; fallthroughs sync in finally with this change's own snapshot. MeasureVisible non-head
        // adds are stage-owned (see HandleStructurePreservingAdd) and excluded here.
        bool deferredSync = change.ContextsSnapshot != null &&
                            (change.StartIndex == 0 || MeasureItemsStrategy == MeasuringStrategy.MeasureFirst);
        bool adapterSynced = false;

        try
        {
            if (MeasureItemsStrategy == MeasuringStrategy.MeasureVisible)
            {
                if (change.StartIndex > LastMeasuredIndexLocal)
                {
                    return;
                }

                if (change.StartIndex == 0 && LastMeasuredIndexLocal >= 0)
                {
                    if (_pendingHeadRemove != null)
                    {
                        // degenerate overlap: a head insert raced an uncommitted head trim — their
                        // offset compensations cannot be merged, rebuild cleanly instead
                        _pendingHeadRemove = null;
                        Invalidate();
                        return;
                    }

                    // SERIALIZE concurrent head inserts (send-spam): a second StartIndex==0 must NEVER fall
                    // through to the middle-insert branch below — that path bumps _itemsShiftEpoch, which makes
                    // the in-flight head-insert commit drop (Epoch != _itemsShiftEpoch) and shifts indices twice,
                    // landing cells at wrong Destinations (the overlapping-bubble corruption). Keep it on the
                    // head path: flush a measured-and-staged block now, defer while one is still measuring.
                    if (_pendingHeadInsert != null)
                    {
                        // ready to commit — apply it inline so the structure is consistent before we shift again
                        CommitPendingHeadInsert();
                    }
                    if (_headInsertMeasuring)
                    {
                        // measurement still async: retry this exact change next frame, after it commits. The
                        // item exists in neither the adapter nor the structure yet (both gain it on re-run), so
                        // mark synced to skip the interim resync — no stale snapshot flip.
                        adapterSynced = true;
                        lock (_structureChangesLock)
                        {
                            _pendingStructureChanges.Add(change);
                        }
                        Repaint();
                        return;
                    }

                    // Head insert (backward LoadMore prepend): existing measurements keep their
                    // positions, only their indices shift — nothing moves on screen this frame.
                    // The new block is measured in background, then committed atomically together
                    // with scroll-offset compensation in CommitPendingStructureRebase, which the
                    // parent SkiaScroll calls BEFORE computing its frame offset.
                    // The adapter MUST shift in the same frame: rekey in-use views + fresh snapshot,
                    // otherwise this frame draws pre-insert contexts at the shifted indices.
                    ChildrenFactory.ApplyInsertShift(EffectiveItemsSource, change.StartIndex, change.Count, change.ContextsSnapshot);
                    adapterSynced = true;
                    ShiftMeasurementIndices(change.StartIndex, change.Count);
                    StartHeadInsertMeasurement(change.Items, change.Count, change.Stamp);
                    return;
                }

                if (change.StartIndex <= LastMeasuredIndexLocal)
                {
                    // Insert in middle of measured items - shift existing measurements
                    ShiftMeasurementIndices(change.StartIndex, change.Count);

                    // Trigger insert-aware background measurement for new items
                    TriggerInsertAwareBackgroundMeasurement(change.StartIndex, change.Count);

                    //Debug.WriteLine($"[StackStructure] MeasureVisible: Shifted measurements and triggered insert-aware background measurement");
                }
                else
                {
                    // Adding at end - background measurement should continue from LastMeasuredIndexLocal + 1
                    // This handles LoadMore scenario where items are added at the end but background measurement
                    // should continue sequentially from where it left off
                    Debug.WriteLine($"[StackStructure] MeasureVisible strategy - LoadMore add at end (index {change.StartIndex}), background measurement continues from {LastMeasuredIndexLocal + 1}");

                    // No need to shift measurements, background measurement will handle the gap naturally
                    // by continuing from LastMeasuredIndexLocal + 1 and eventually reaching the newly added items
                    return;
                }
            }
            else
            {
                // MeasureFirst = uniform rows: the whole change is ARITHMETIC (no rebuild/binds/measures)
                if (MeasureItemsStrategy == MeasuringStrategy.MeasureFirst
                    && TryApplyUniformAddMeasureFirst(change))
                {
                    adapterSynced = true; // helper ran ApplyInsertShift
                    return;
                }

                // For sync strategies: Need to shift existing measurements and measure new items
                if (change.StartIndex <= LastMeasuredIndexLocal)
                {
                    // Adding in middle of measured items - shift existing measurements
                    ShiftMeasurementIndices(change.StartIndex, change.Count);
                    //Debug.WriteLine($"[StackStructure] Shifted measurements for sync strategy");
                }
                // Note: New items will be measured on-demand during normal layout
            }

            UpdateProgressiveContentSize();
        }
        finally
        {
            // fallthrough for a deferred head insert (degenerate overlap, unmeasured head, helper
            // rejection): the stage handler skipped the refresh, no branch ran ApplyInsertShift —
            // sync the adapter to the mutated collection now or it serves a stale snapshot forever
            if (deferredSync && !adapterSynced)
            {
                ChildrenFactory.InitializeSoft(false, EffectiveItemsSource, GetTemplatesPoolLimit(),
                    change.ContextsSnapshot);
            }

            OnStructureChanged();
        }

    }

    /// <summary>
    /// MeasureFirst (uniform rows) Add fast path: every row is the first item's size + spacing, so an
    /// insert/append is pure arithmetic — followers slide down by rowsAdded*stride, new cells are placed
    /// by row/column multiplication. No template binds, no measures, no full structure-build loop (the
    /// freeze at window slides). Split-aware: a Split&gt;1 grid places new cells across their columns
    /// (row = index/Split, col = index%Split). Requires the change to be Split-aligned (count and index
    /// multiples of Split, guaranteed by IsSplitAlignedChange upstream) so survivor column parity is
    /// preserved by a pure row-shift; returns false otherwise to fall back to the generic path.
    /// </summary>
    private bool TryApplyUniformAddMeasureFirst(StructureChange change)
    {
        if (Type != LayoutType.Column || change.Count <= 0)
            return false;

        var split = Split > 0 ? Split : 1;

        // Split-aware arithmetic needs row-aligned inserts (col parity preserved by a pure Y-shift).
        if (split > 1 && (change.Count % split != 0 || change.StartIndex % split != 0))
            return false;

        lock (LockMeasure)
        {
            var cells = StackStructure?.GetChildren()
                .Where(c => c != null && c.ControlIndex >= 0)
                .OrderBy(c => c.ControlIndex).ToList();
            if (cells == null || cells.Count == 0)
                return false;

            var proto = cells[cells.Count - 1];
            if (!proto.WasMeasured || change.StartIndex > proto.ControlIndex + 1)
                return false; // no measured prototype / append beyond a gap — let the generic path handle it

            var spacingPixels = (float)Math.Round(Spacing * RenderingScale);
            float stride = proto.Destination.Height + spacingPixels;     // one ROW = cell height + spacing
            float colWidth = proto.Destination.Width;
            int rowsAdded = change.Count / split;                        // split==1 => == change.Count
            float blockShift = rowsAdded * stride;                       // followers slide by whole rows

            // Per-column X geometry read off the existing uniform grid (every cell in a column shares
            // its Left). New cells in a DIFFERENT column than the prototype MUST use their own column's
            // X — reusing the prototype's X was the single-column collapse (device 2026-07-19).
            var colLeft = new float[split];
            var colHasX = new bool[split];
            foreach (var c in cells)
            {
                int col = c.ControlIndex % split;
                if (!colHasX[col])
                {
                    colLeft[col] = c.Destination.Left;
                    colHasX[col] = true;
                }
            }
            for (int c = 0; c < split; c++)
                if (!colHasX[c])
                    colLeft[c] = colLeft[0] + c * (colWidth + spacingPixels); // fallback (all columns not yet resident)

            bool headInsert = change.StartIndex == 0;
            if (headInsert && _pendingHeadRemove != null)
                return false; // degenerate overlap with an uncommitted trim — let the generic path rebuild

            float insertTop;
            if (headInsert)
            {
                // HEAD INSERT (backward slide into trimmed space): new cells go ABOVE the current
                // content (negative space), survivors keep their absolute positions — painted pixels
                // identical this frame. Next frame CommitPendingHeadRemove translates everything down
                // by blockShift and compensates the scroll offset in the same frame (negative Shift).
                insertTop = cells[0].Destination.Top - blockShift;
                foreach (var cell in cells)
                {
                    cell.ControlIndex += change.Count; // count is Split-aligned: column parity preserved
                }
            }
            else
            {
                // middle/tail: the new block takes the first follower's slot, followers slide DOWN
                var follower = cells.FirstOrDefault(c => c.ControlIndex == change.StartIndex);
                insertTop = follower?.Destination.Top ?? (proto.Destination.Bottom + spacingPixels);

                foreach (var cell in cells)
                {
                    if (cell.ControlIndex >= change.StartIndex)
                    {
                        cell.ControlIndex += change.Count;
                        cell.Destination = new SKRect(cell.Destination.Left, cell.Destination.Top + blockShift,
                            cell.Destination.Right, cell.Destination.Bottom + blockShift);
                        cell.Area = new SKRect(cell.Area.Left, cell.Area.Top + blockShift,
                            cell.Area.Right, cell.Area.Bottom + blockShift);
                    }
                }
            }

            float protoHeight = proto.Destination.Height;
            for (int i = 0; i < change.Count; i++)
            {
                int finalIndex = change.StartIndex + i;
                int col = finalIndex % split;
                float top = insertTop + (i / split) * stride;
                var dest = new SKRect(colLeft[col], top, colLeft[col] + colWidth, top + protoHeight);

                cells.Add(new ControlInStack
                {
                    ControlIndex = finalIndex,
                    Column = col,
                    Row = finalIndex / split,
                    Destination = dest,
                    Area = dest,
                    Measured = proto.Measured,
                    WasMeasured = true,
                });
            }

            if (headInsert)
            {
                // negative Shift: commit translates cells DOWN by blockShift and moves the scroll
                // anchor by -blockShift/scale — same rebase mechanism as the head trim, mirrored
                _pendingHeadRemove = new HeadRemoveRebase
                {
                    Shift = -blockShift, Stamp = MeasureStamp, Epoch = _itemsShiftEpoch, Scale = RenderingScale
                };
            }

            cells.Sort((a, b) => a.ControlIndex.CompareTo(b.ControlIndex));
            // keep Column/Row consistent for any consumer that reads them (RebuildStructureFromCells
            // re-chunks by order, but DrawStack/measure paths read cell.Column for per-column layout)
            foreach (var c in cells)
            {
                c.Column = c.ControlIndex % split;
                c.Row = c.ControlIndex / split;
            }
            RebuildStructureFromCells(cells);

            if (LastMeasuredIndexLocal < cells[^1].ControlIndex)
            {
                LastMeasuredIndexLocal = cells[^1].ControlIndex;
            }

            // rekey in-use views + fresh contexts snapshot, same frame as the structure shift
            ChildrenFactory.ApplyInsertShift(EffectiveItemsSource, change.StartIndex, change.Count,
                change.ContextsSnapshot);

            UpdateProgressiveContentSize();
        }

        //Debug.WriteLine(
        //    $"[StackStructure] MeasureFirst uniform add: {change.Count} @ {change.StartIndex}, no rebuild");
        return true;
    }

    #region HEAD INSERT (backward LoadMore over a windowed ItemsSource)

    private sealed class HeadInsertRebase
    {
        public List<MeasuredItemInfo> Items;
        public long Stamp;
        public int Epoch;
        public float Scale;
    }

    private HeadInsertRebase _pendingHeadInsert;
    private volatile bool _headInsertMeasuring;

    /// <summary>
    /// A measured head-insert block or a head-remove trim is ready and must be committed by
    /// the parent scroll before it computes its next frame offset (see SkiaScroll.Draw).
    /// </summary>
    public bool HasPendingStructureRebase => _pendingHeadInsert != null || _pendingHeadRemove != null;

    /// <summary>
    /// True while a head insert is measuring or waiting for commit: tail background
    /// measurement must not run concurrently (it would integrate stale positions).
    /// </summary>
    public bool HeadInsertInFlight => _headInsertMeasuring || _pendingHeadInsert != null;

    /// <summary>
    /// True while ANY collection structure change is staged, measuring or waiting for its
    /// scroll-offset commit — i.e. the current StackStructure geometry is about to change.
    /// Consumers resolving positions from the structure (e.g. a ScrollToIndex order) should
    /// wait it out instead of computing from geometry that the commit will rebase.
    /// </summary>
    public bool HasPendingStructureChanges
    {
        get
        {
            if (HeadInsertInFlight || HeadRemoveInFlight)
                return true;

            lock (_structureChangesLock)
            {
                return _pendingStructureChanges.Count > 0;
            }
        }
    }

    /// <summary>
    /// Measures the prepended block [0..count) off-thread, laid out from y=0 like a fresh head,
    /// then stages it for atomic commit. Indices of existing measurements were already shifted.
    /// </summary>
    private void StartHeadInsertMeasurement(List<object> items, int count, long stamp)
    {
        if (items == null || items.Count != count)
        {
            Debug.WriteLine("[SkiaLayout] Head insert has no items payload, skipped");
            return;
        }

        var constraints = new SKRect(0, 0, _lastMeasuredForWidth, _lastMeasuredForHeight);
        var scale = RenderingScale;
        var epoch = _itemsShiftEpoch; // captured AFTER the head shift; another shift invalidates us

        _headInsertMeasuring = true;

        Debug.WriteLine(
            $"[SkiaLayout] Head insert measuring {count} items, constraints {constraints.Width:0.0}x{constraints.Height:0.0}px scale {scale:0.00}");

        Task.Run(() =>
        {
            try
            {
                var measured = MeasureHeadBatchDirect(items, constraints, scale);

                if (measured.Count == count && stamp == MeasureStamp && epoch == _itemsShiftEpoch)
                {
                    _pendingHeadInsert = new HeadInsertRebase
                    {
                        Items = measured, Stamp = stamp, Epoch = epoch, Scale = scale
                    };
                    Debug.WriteLine($"[SkiaLayout] Head insert measured {count} items, pending commit");
                }
                else
                {
                    Debug.WriteLine(
                        $"[SkiaLayout] Head insert measurement dropped (measured {measured.Count}/{count}, stamp {stamp}/{MeasureStamp})");
                }
            }
            catch (Exception e)
            {
                Super.Log(e);
            }
            finally
            {
                _headInsertMeasuring = false;
                Repaint();
            }
        });
    }

    /// <summary>
    /// Measures the inserted head items binding views DIRECTLY to the provided items.
    /// The adapter's data-contexts snapshot must NOT be used here: it is refreshed via a
    /// posted action (InitializeSoft) and can still hold the PRE-insert window when this runs,
    /// which would silently measure heights of the wrong rows (random gaps with uneven rows).
    /// </summary>
    private List<MeasuredItemInfo> MeasureHeadBatchDirect(List<object> items, SKRect constraints, float scale)
    {
        var measuredBatch = new List<MeasuredItemInfo>(items.Count);
        SkiaControl template = null;

        try
        {
            var columnsCount = (Split > 0) ? Split : 1;
            var columnWidth = ComputeColumnWidth(columnsCount);

            float currentX = 0f;
            float currentY = 0f;
            float rowHeight = 0f;
            int row = 0;
            int col = 0;

            float availableWidth = columnWidth;
            float availableHeight = float.PositiveInfinity;

            if (this.Type == LayoutType.Row)
            {
                availableHeight = columnWidth;
                availableWidth = float.PositiveInfinity;
            }

            for (int i = 0; i < items.Count; i++)
            {
                var rectForChild = new SKRect(
                    currentX,
                    currentY,
                    currentX + availableWidth,
                    currentY + availableHeight);

                var cell = new ControlInStack
                {
                    ControlIndex = i,
                    Column = col,
                    Row = row,
                    Destination = rectForChild
                };

                ScaledSize measured;

                // Memo fast path, mirroring the background batch (see MeasureBatchInBackground): a LoadNewer
                // head insert re-materializes items that were resident before the opposite-end trim, so their
                // sizes are usually already memoized. Skipping the template bind+measure here is what lets the
                // commit land before the fling reaches the uncommitted edge. Template is acquired lazily —
                // an all-hit batch never touches a view at all.
                if (TryGetMemoSize(items[i], availableWidth, out var cachedSize))
                {
                    measured = cachedSize;
                    cell.Measured = measured;
                    cell.WasMeasured = true;
                    // Mirror MeasureAndArrangeCell: Area = full slot, Destination = slot topped at currentY
                    // sized by the measured height (CommitPendingHeadInsert reads Destination.Top + height).
                    cell.Area = rectForChild;
                    cell.Destination = new SKRect(currentX, currentY,
                        currentX + availableWidth, currentY + measured.Pixels.Height);
                }
                else
                {
                    template ??= ChildrenFactory.GetTemplateInstance();
                    template.ContextIndex = i;
                    template.BindingContext = items[i];
                    template.NeedMeasure = true;

                    measured = MeasureAndArrangeCell(rectForChild, cell, template, constraints, scale);
                }

                if (measured.Pixels.Height > rowHeight)
                    rowHeight = measured.Pixels.Height;

                measuredBatch.Add(new MeasuredItemInfo
                {
                    Cell = cell,
                    LastAccessed = DateTime.UtcNow,
                    IsInViewport = false
                });

                col++;
                if (col >= columnsCount)
                {
                    row++;
                    col = 0;
                    currentX = 0f;
                    currentY += rowHeight + (float)(Spacing * scale);
                    rowHeight = 0f;
                }
                else
                {
                    currentX += columnWidth + (float)(Spacing * scale);
                }
            }
        }
        finally
        {
            if (template != null)
            {
                ChildrenFactory.ReleaseTemplateInstance(template);
            }
        }

        return measuredBatch;
    }

    /// <summary>
    /// Commits a measured head-insert block atomically: prepends its rows, translates all existing
    /// cells down by the block height, grows progressive content size and compensates the parent
    /// scroll offset by the same delta — so the frame painted right after shows identical pixels.
    /// Called by the parent SkiaScroll at the start of its Draw, before the frame offset is used.
    /// </summary>
    public void CommitPendingStructureRebase()
    {
        CommitPendingHeadInsert();
        CommitPendingHeadRemove();
    }

    private void CommitPendingHeadInsert()
    {
        var pending = Interlocked.Exchange(ref _pendingHeadInsert, null);
        if (pending == null)
            return;

        if (pending.Stamp != MeasureStamp || pending.Epoch != _itemsShiftEpoch
            || StackStructure == null || StackStructure.GetCount() == 0)
        {
            // a full remeasure or another index shift happened meanwhile
            Debug.WriteLine("[SkiaLayout] Head insert commit dropped, structure was rebuilt/shifted");
            return;
        }

        lock (LockMeasure)
        {
            var newCells = pending.Items;
            var scale = pending.Scale;
            var spacingPixels = (float)(Spacing * scale);

            // vertical space the new block occupies; next row starts after trailing spacing
            float blockBottom = 0f;
            foreach (var item in newCells)
            {
                var bottom = item.Cell.Destination.Top + item.Cell.Measured.Pixels.Height;
                if (bottom > blockBottom)
                    blockBottom = bottom;
            }

            var blockShift = blockBottom + spacingPixels;

            // translate every existing measured cell down (structure and measurement cache can
            // hold distinct ControlInStack instances — cover both, each instance once)
            var translated = new HashSet<ControlInStack>();

            void Translate(ControlInStack cell)
            {
                if (cell == null || !translated.Add(cell))
                    return;
                cell.Destination = new SKRect(cell.Destination.Left, cell.Destination.Top + blockShift,
                    cell.Destination.Right, cell.Destination.Bottom + blockShift);
                cell.Area = new SKRect(cell.Area.Left, cell.Area.Top + blockShift,
                    cell.Area.Right, cell.Area.Bottom + blockShift);
            }

            var existingCells = StackStructure.GetChildren().ToList();
            foreach (var cell in existingCells)
            {
                Translate(cell);
            }

            foreach (var kvp in _measuredItems)
            {
                if (kvp.Key >= newCells.Count)
                    Translate(kvp.Value.Cell);
            }

            // register the new measurements and rebuild the structure: new rows on top,
            // existing rows below, rows/columns renumbered by the constructor
            foreach (var item in newCells)
            {
                _measuredItems[item.Cell.ControlIndex] = item;
            }

            var allCells = new List<ControlInStack>(newCells.Count + existingCells.Count);
            allCells.AddRange(newCells.Select(x => x.Cell));
            allCells.AddRange(existingCells.OrderBy(c => c.ControlIndex));

            var columnsCount = (Split > 0) ? Split : 1;
            var rows = new List<List<ControlInStack>>();
            var currentRow = new List<ControlInStack>(columnsCount);
            foreach (var cell in allCells)
            {
                currentRow.Add(cell);
                if (currentRow.Count >= columnsCount)
                {
                    rows.Add(currentRow);
                    currentRow = new List<ControlInStack>(columnsCount);
                }
            }

            if (currentRow.Count > 0)
            {
                rows.Add(currentRow);
            }

            StackStructure = new LayoutStructure(rows);

            UpdateProgressiveContentSize();

            if (Parent is SkiaScroll scroll && !scroll.HasPendingScrollOrder)
            {
                // Position-preserving pin for backward LoadMore in history. Suppressed when an explicit
                // ScrollToIndex is pending (e.g. a just-sent message ordering ScrollToIndex(0)): pinning would
                // fight that scroll in the same frame and produce a nondeterministic 1-frame blink.
                scroll.OffsetVisibleAnchorY(-blockShift / scale);
            }

            Debug.WriteLine(
                $"[SkiaLayout] Head insert committed: {newCells.Count} items, shift {blockShift:0.0}px");
#if DEBUG
            // diagnostic: per-cell layout of the inserted block, "index:top+height"
            var sb = new System.Text.StringBuilder("[HEAD CELLS] ");
            foreach (var item in newCells)
            {
                sb.Append(item.Cell.ControlIndex).Append(':')
                    .Append(item.Cell.Destination.Top.ToString("0")).Append('+')
                    .Append(item.Cell.Measured.Pixels.Height.ToString("0")).Append(' ');
            }

            Debug.WriteLine(sb.ToString());
#endif
        }

        // The commit rebuilt the structure on a NON-collection-change frame (the staged Add fired last frame).
        // Subclasses that cache their own rendering (e.g. an Operations-plane stack) only invalidate on the
        // collection event, so without this the new cells stay undrawn until something else dirties a child
        // (e.g. a delivery-status update) — the just-sent message appears with a visible lag.
        OnStructureChanged();

        // A head insert lands through THIS path, not ApplyBackgroundMeasurementChange. Subclasses that hook
        // "items added & applied" (e.g. a windowed chat that trims the opposite end only after the batch is
        // committed) must be notified here too, otherwise a backward LoadMore (head insert) never triggers
        // their post-apply step and the window grows unbounded on that side.
        OnHeadInsertCommitted();
    }

    /// <summary>
    /// Fired after a backward-LoadMore head insert has been measured AND committed into the structure
    /// (position-preserving). Symmetric to <see cref="ApplyBackgroundMeasurementChange"/> for the forward/
    /// append path: both mark "new items are now applied". Default no-op.
    /// </summary>
    protected virtual void OnHeadInsertCommitted()
    {
        RaiseMeasurementApplied();
    }

    #endregion

    #region WINDOW TRIM (bounded in-memory ItemsSource: head/tail removal without reset)

    private sealed class HeadRemoveRebase
    {
        public float Shift;
        public long Stamp;
        public int Epoch;
        public float Scale;
    }

    private HeadRemoveRebase _pendingHeadRemove;

    /// <summary>
    /// True while a head trim waits for its pre-offset commit: tail background measurement
    /// must not start meanwhile, its staged positions would not be translated by the commit.
    /// </summary>
    public bool HeadRemoveInFlight => _pendingHeadRemove != null;

    /// <summary>
    /// Head trim (window cap after forward LoadMore): drop items [0..count) that are far above
    /// the viewport. Index-only this frame — adapter rekey, measurement cache shift, structure
    /// rows dropped — survivors keep their absolute positions, so painted pixels are identical.
    /// The dead space above is reclaimed next frame by CommitPendingHeadRemove, which the parent
    /// SkiaScroll calls BEFORE computing its frame offset: cells translate up and the viewport
    /// offset compensates in the same frame. No remeasure happens at any point.
    /// Returns false to fall back to the generic remove path.
    /// </summary>
    private bool ApplyHeadRemoveChange(StructureChange change)
    {
        lock (LockMeasure)
        {
            var allCells = StackStructure.GetChildren().Where(c => c.ControlIndex >= 0).ToList();
            var survivors = allCells.Where(c => c.ControlIndex >= change.Count)
                .OrderBy(c => c.ControlIndex).ToList();

            // need the first survivor measured in place to know how much dead space to reclaim
            if (survivors.Count == 0 || survivors[0].ControlIndex != change.Count)
            {
                Debug.WriteLine(
                    $"[SkiaLayout] Head remove fast path rejected: first survivor {survivors.FirstOrDefault()?.ControlIndex ?? -1} != {change.Count}");
                return false;
            }

            var blockShift = survivors[0].Destination.Top;

            // adapter must rekey in the same frame as the structure indices shift (released
            // views for removed items, survivors rekeyed, fresh snapshot) — see ApplyInsertShift
            ChildrenFactory.ApplyRemoveShift(EffectiveItemsSource, 0, change.Count, change.ContextsSnapshot);

            for (int i = 0; i < change.Count; i++)
            {
                _measuredItems.TryRemove(i, out _);
            }

            // rekeys _measuredItems and structure-cell indices, bumps the shift epoch
            // (drops in-flight background batches), clamps LastMeasuredIndexLocal.
            // NOTE: this renames STRUCTURE cells too (walks StackStructure), not just the memo —
            // valid for ALL strategies, do not rekey survivors again here.
            ShiftMeasurementIndices(change.Count, -change.Count);

            // structure keeps only survivors, positions untouched this frame
            RebuildStructureFromCells(survivors);

            _pendingHeadRemove = new HeadRemoveRebase
            {
                Shift = blockShift, Stamp = MeasureStamp, Epoch = _itemsShiftEpoch, Scale = RenderingScale
            };

            Debug.WriteLine(
                $"[SkiaLayout] Head remove applied: {change.Count} items, {blockShift:0.0}px pending reclaim");
        }

        Repaint();
        return true;
    }

    /// <summary>
    /// Tail trim (window cap before backward LoadMore): drop items at the collection tail,
    /// far below the viewport. Fully synchronous: survivors keep positions and indices, no
    /// offset compensation needed — only the content size shrinks at the bottom.
    /// Returns false to fall back to the generic remove path.
    /// </summary>
    private bool ApplyTailRemoveChange(StructureChange change)
    {
        lock (LockMeasure)
        {
            ChildrenFactory.ApplyRemoveShift(EffectiveItemsSource, change.StartIndex, change.Count, change.ContextsSnapshot);

            for (int i = change.StartIndex; i < change.StartIndex + change.Count; i++)
            {
                _measuredItems.TryRemove(i, out _);
            }

            // nothing exists after the removed tail so no indices shift, but in-flight
            // background batches may still target the removed range — invalidate them
            _itemsShiftEpoch++;

            if (LastMeasuredIndexLocal >= change.StartIndex)
            {
                LastMeasuredIndexLocal = change.StartIndex - 1;
            }

            var survivors = StackStructure.GetChildren()
                .Where(c => c.ControlIndex >= 0 && c.ControlIndex < change.StartIndex)
                .OrderBy(c => c.ControlIndex).ToList();

            RebuildStructureFromCells(survivors);

            UpdateProgressiveContentSize();

            Debug.WriteLine(
                $"[SkiaLayout] Tail remove applied: {change.Count} items from {change.StartIndex}");
        }

        Repaint();
        return true;
    }

    /// <summary>
    /// A head trim reclaimed dead space: the WHOLE content coordinate frame shifted vertically WITHOUT
    /// introducing new pixels (pure translation, no remeasure). Subclasses that keep their own coordinate-
    /// space-baked cache (e.g. a banded Operations plane) must RE-ANCHOR it by the same delta instead of
    /// re-recording — otherwise the stale plane is blitted at the pre-shift offset for a frame (a blink).
    /// deltaPixels is the signed amount destination.Top moves next frame (= +reclaimed block height).
    /// Default no-op. Runs on the render thread, before the parent scroll computes its frame offset.
    /// </summary>
    public virtual void OnContentTranslatedVertically(float deltaPixels)
    {
    }

    /// <summary>
    /// Reclaims the dead space left above the content by a head trim: translates all cells up
    /// by the removed block height and compensates the parent scroll offset by the same delta,
    /// in the same frame — identical pixels, shorter content. Called via
    /// CommitPendingStructureRebase from SkiaScroll.Draw before the frame offset is computed.
    /// </summary>
    private void CommitPendingHeadRemove()
    {
        var pending = Interlocked.Exchange(ref _pendingHeadRemove, null);
        if (pending == null)
            return;

        if (pending.Stamp != MeasureStamp)
        {
            // a full remeasure rebuilt all positions from scratch — no dead space left
            Debug.WriteLine("[SkiaLayout] Head remove commit dropped, structure was remeasured");
            return;
        }

        if (pending.Epoch != _itemsShiftEpoch || StackStructure == null || StackStructure.GetCount() == 0)
        {
            // another index shift landed between trim and commit: positions were translated
            // under different assumptions, the dead space cannot be reclaimed consistently —
            // force a clean rebuild instead of leaving a permanent gap above the content
            Debug.WriteLine("[SkiaLayout] Head remove commit dropped (shifted meanwhile), forcing rebuild");
            Invalidate();
            return;
        }

        lock (LockMeasure)
        {
            var blockShift = pending.Shift;

            // structure and measurement cache can hold distinct ControlInStack instances —
            // cover both, each instance once
            var translated = new HashSet<ControlInStack>();

            void Translate(ControlInStack cell)
            {
                if (cell == null || !translated.Add(cell))
                    return;
                cell.Destination = new SKRect(cell.Destination.Left, cell.Destination.Top - blockShift,
                    cell.Destination.Right, cell.Destination.Bottom - blockShift);
                cell.Area = new SKRect(cell.Area.Left, cell.Area.Top - blockShift,
                    cell.Area.Right, cell.Area.Bottom - blockShift);
            }

            foreach (var cell in StackStructure.GetChildren())
            {
                Translate(cell);
            }

            foreach (var kvp in _measuredItems)
            {
                Translate(kvp.Value.Cell);
            }

            UpdateProgressiveContentSize();

            if (Parent is SkiaScroll scroll)
            {
                scroll.OffsetVisibleAnchorY(blockShift / pending.Scale);
            }

            Debug.WriteLine($"[SkiaLayout] Head remove committed: reclaimed {blockShift:0.0}px");
        }

        // Pure-translation reclaim: cells moved -blockShift, the scroll offset compensated, so next frame
        // destination.Top moves +blockShift. Let a self-caching subclass re-anchor its coordinate-baked
        // plane by that delta (no re-record) instead of blitting it stale at the old offset (a blink).
        OnContentTranslatedVertically(pending.Shift);

        Repaint();
    }

    /// <summary>
    /// Rebuilds StackStructure from index-ordered cells, chunked by the current column count.
    /// Positions are taken as-is; the LayoutStructure constructor renumbers rows/columns.
    /// </summary>
    private void RebuildStructureFromCells(List<ControlInStack> orderedCells)
    {
        var columnsCount = (Split > 0) ? Split : 1;
        var rows = new List<List<ControlInStack>>();
        var currentRow = new List<ControlInStack>(columnsCount);
        foreach (var cell in orderedCells)
        {
            currentRow.Add(cell);
            if (currentRow.Count >= columnsCount)
            {
                rows.Add(currentRow);
                currentRow = new List<ControlInStack>(columnsCount);
            }
        }

        if (currentRow.Count > 0)
        {
            rows.Add(currentRow);
        }

        StackStructure = new LayoutStructure(rows);
    }

    #endregion

    /// <summary>
    /// Applies Remove changes to StackStructure
    /// </summary>
    private void ApplyRemoveChange(StructureChange change)
    {
        // the stage handler DEFERRED the adapter refresh to this apply (HandleStructurePreservingRemove):
        // every branch below ends in ApplyRemoveShift EXCEPT the no-structure fallthrough — net it
        bool adapterSynced = false;

        try
        {
            //Debug.WriteLine($"[StackStructure] Removing {change.Count} items at index {change.StartIndex}");

            if (MeasureItemsStrategy is MeasuringStrategy.MeasureVisible or MeasuringStrategy.MeasureFirst
                && StackStructure != null && StackStructure.GetCount() > 0
                && !HeadInsertInFlight && _pendingHeadRemove == null)
            {
                // Window-trim fast paths for a bounded in-memory ItemsSource (LoadMore both
                // directions with a capped window): structure-preserving, no remeasure, no reset.
                // MeasureFirst qualifies too: every cell is arithmetically measured, so the first
                // survivor is always "measured in place" as the head-remove commit requires.
                // Skipped for a Replace-remove: that is not a trim and has no real survivors.
                if (!change.SkipTrimFastPath)
                {
                    if (change.StartIndex == 0 && LastMeasuredIndexLocal >= 0 && ApplyHeadRemoveChange(change))
                    {
                        adapterSynced = true; // fast path ran ApplyRemoveShift
                        return;
                    }

                    if (change.TailRemoval && ApplyTailRemoveChange(change))
                    {
                        adapterSynced = true; // fast path ran ApplyRemoveShift
                        return;
                    }
                }
            }

            // Mid-window removal (e.g. "delete message"): REALLY remove the cells and close the gap.
            // The old stub only marked the removed cells ControlIndex=-1 — the dead slot kept its
            // pixels (permanent empty space on screen), followers kept their PRE-remove indices
            // (binding one item off) and the adapter was never rekeyed.
            //
            // Order matters: capture the removed structure cells BEFORE ShiftMeasurementIndices —
            // afterwards ALL index reindexing belongs to that shift (memo keys + structure cells,
            // shared instances renamed once). Re-decrementing here double-shifted shared instances
            // (duplicate indices -> two cells in one slot: the "delete first-of-day -> overlap" bug).
            List<ControlInStack> removedCells = null;
            if (StackStructure != null)
            {
                removedCells = StackStructure.GetChildren()
                    .Where(c => c != null &&
                                c.ControlIndex >= change.StartIndex &&
                                c.ControlIndex < change.StartIndex + change.Count)
                    .ToList();
            }

            // Remove items from measurement cache
            for (int i = change.StartIndex; i < change.StartIndex + change.Count; i++)
            {
                _measuredItems.TryRemove(i, out _);
            }

            // Shift remaining measurements + ALL structure cell indices (keys, LastMeasuredIndexLocal, epoch)
            ShiftMeasurementIndices(change.StartIndex + change.Count, -change.Count);

            if (StackStructure != null)
            {
                lock (LockMeasure)
                {
                    float delta = 0;
                    if (removedCells is { Count: > 0 })
                    {
                        var top = removedCells.Min(c => c.Destination.Top);
                        var bottom = removedCells.Max(c => c.Destination.Bottom);
                        var spacingPx = (float)(Spacing * RenderingScale);
                        delta = (bottom - top) + spacingPx;
                    }

                    var removedSet = removedCells is { Count: > 0 }
                        ? new HashSet<ControlInStack>(removedCells)
                        : null;

                    var translated = new HashSet<ControlInStack>();

                    void Translate(ControlInStack cell)
                    {
                        if (cell == null || delta == 0 || !translated.Add(cell))
                            return;
                        cell.Destination = new SKRect(cell.Destination.Left, cell.Destination.Top - delta,
                            cell.Destination.Right, cell.Destination.Bottom - delta);
                        cell.Area = new SKRect(cell.Area.Left, cell.Area.Top - delta,
                            cell.Area.Right, cell.Area.Bottom - delta);
                    }

                    // survivors = everything except the removed instances; followers (post-shift index
                    // >= StartIndex) slide up by the removed block height to close the gap
                    var survivors = new List<ControlInStack>();
                    foreach (var cell in StackStructure.GetChildren())
                    {
                        if (cell == null || cell.ControlIndex < 0)
                            continue;
                        if (removedSet != null && removedSet.Contains(cell))
                            continue;

                        if (cell.ControlIndex >= change.StartIndex)
                            Translate(cell);

                        survivors.Add(cell);
                    }

                    survivors.Sort((a, b) => a.ControlIndex.CompareTo(b.ControlIndex));

                    // memo can hold DISTINCT cell instances: keep their positions in sync too
                    foreach (var kvp in _measuredItems)
                    {
                        if (kvp.Key >= change.StartIndex)
                            Translate(kvp.Value?.Cell);
                    }

                    RebuildStructureFromCells(survivors);
                }

                // release removed views + rekey survivors + fresh contexts snapshot, atomically with
                // the structure shift (same contract as the trim paths)
                ChildrenFactory.ApplyRemoveShift(EffectiveItemsSource, change.StartIndex, change.Count,
                    change.ContextsSnapshot);
                adapterSynced = true;
            }

            //Debug.WriteLine($"[StackStructure] Removed {change.Count} items and shifted measurements");
            UpdateProgressiveContentSize();
        }
        finally
        {
            // no-structure fallthrough: no branch ran ApplyRemoveShift — the deferred refresh must
            // still land or the adapter serves a stale (pre-remove) snapshot forever
            if (!adapterSynced && change.ContextsSnapshot != null)
            {
                ChildrenFactory.InitializeSoft(false, EffectiveItemsSource, GetTemplatesPoolLimit(),
                    change.ContextsSnapshot);
            }

            OnStructureChanged();
        }

    }

    /// <summary>
    /// Applies Replace changes to StackStructure
    /// </summary>
    private void ApplyReplaceChange(StructureChange change)
    {
        //Debug.WriteLine($"[StackStructure] Replacing {change.Count} items at index {change.StartIndex}");

        var oldStructureCount = StackStructure?.GetCount() ?? 0;
        var removeCount = change.OldCount > 0 ? change.OldCount : change.Count;

        // Full-collection swap (windowed list jump: ReplaceRange clears + re-adds everything).
        // Remove+Add with index shifting would mis-attribute the old window's stale measurements to the
        // new items and can leave a stale LastMeasuredIndexLocal, so the scheduler thinks the new cells are
        // already measured and never lays them out -> scroll into empty space. Instead invalidate the
        // measurement state and let the next MeasureVisible pass rebuild + measure the new items.
        // Templates/pool are preserved (InitializeSoft already ran in HandleStructurePreservingReplace),
        // so NO adapter rebuild (InitializeTemplates) happens.
        if (change.StartIndex == 0 && oldStructureCount > 0 && removeCount >= oldStructureCount)
        {
            ResetMeasurementForReplace();
            return;
        }

        // Partial Replace: Split into Remove + Add in same frame.
        // Remove uses OldCount (entries actually present), Add uses Count (new items).
        // A range Replace that shrinks the collection has OldCount > Count; reusing Count for the
        // remove would leave stale structure entries and desync structure vs ItemsSource.
        // SkipTrimFastPath: a replace-remove is not a window trim (no real survivors beyond it).
        var removeChange = new StructureChange(StructureChangeType.Remove, MeasureStamp)
        {
            StartIndex = change.StartIndex,
            Count = removeCount,
            SkipTrimFastPath = true
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

    public virtual void OnStructureChanged()
    {
        OnPropertyChanged(nameof(DebugString));
        Repaint();
    }

    /// <summary>
    /// Applies Move changes to StackStructure
    /// </summary>
    private void ApplyMoveChange(StructureChange change)
    {
        //Debug.WriteLine($"[StackStructure] Moving item from index {change.StartIndex} to {change.TargetIndex}");
        // TODO: Implement move logic that reorders structure
        UpdateProgressiveContentSize();

        OnStructureChanged();
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
        LastMeasuredIndexLocal = -1;
        FirstMeasuredIndexLocal = -1;
        ChildrenFactory.TemplatesInvalidated = true;
        UpdateProgressiveContentSize();

        OnStructureChanged();
    }

    /// <summary>
    /// Invalidates measurement/structure state for a full-collection Replace (windowed list jump)
    /// WITHOUT invalidating templates. The adapter pool stays intact (InitializeSoft already ran),
    /// so no InitializeTemplates rebuild; the next MeasureVisible pass rebuilds the structure and
    /// measures the new items. Used instead of Remove+Add to avoid carrying the old window's stale
    /// measurements and LastMeasuredIndexLocal onto the new items (which renders into empty space).
    /// </summary>
    protected void ResetMeasurementForReplace()
    {
        // Stop any in-flight background pass and clear its progress: a stale _backgroundMeasurementProgress
        // from the old window both lies to the ScrollToIndex gate (it thinks far indices are measured and
        // resolves against an unmeasured cell) and blocks a restart (StartBackgroundMeasurement skips when
        // progress >= startFromIndex).
        CancelBackgroundMeasurement();
        _backgroundMeasurementProgress = -1;

        StackStructure = null;
        _measuredItems.Clear();
        _indexOffsets.Clear();
        _removedIndices.Clear();
        LastMeasuredIndexLocal = -1;
        FirstMeasuredIndexLocal = -1;
        _itemsShiftEpoch++; // drop any in-flight background measurement batches from the old window
        UpdateProgressiveContentSize();

        OnStructureChanged();

        // Structure was nulled: drive a measure pass so MeasureVisible rebuilds it and measures the new
        // items (the structure-preserving Replace handler only calls Update()/repaint, which alone would
        // never re-measure -> empty viewport). NeedMeasure=true via Invalidate; templates stay intact.
        Invalidate();
    }

    /// <summary>
    /// Applies visibility changes to StackStructure
    /// FIXED: Now processes visibility changes in sequential groups to prevent gaps
    /// that occur when non-consecutive items change visibility
    /// </summary>
    private void ApplyVisibilityChange(StructureChange change)
    {
        var structure = GetStackStructureForMeasuring();
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
        // A shift (head insert/remove) landed after this was measured: StartIndex now points at a DIFFERENT
        // item — applying would resize the wrong cell and shift everything below it from a wrong anchor
        // (transient overlap at the shift seam). Re-resolve the item's current index and remeasure fresh.
        if (change.Epoch != _itemsShiftEpoch)
        {
            var staleItem = change.Items?.Count == 1 ? change.Items[0] : null;
            int currentIndex = staleItem != null && EffectiveItemsSource != null ? EffectiveItemsSource.IndexOf(staleItem) : -1;
            //Debug.WriteLine($"[StackStructure] Stale single-item update (epoch {change.Epoch} != {_itemsShiftEpoch}), re-queued for current index {currentIndex}");
            if (currentIndex >= 0)
                RemeasureSingleItemInBackground(currentIndex);
            return;
        }

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
        // Invalidate any in-flight background measurement batch: it was measured against
        // pre-shift indices/positions and would corrupt the structure if integrated late
        // (cancellation is cooperative, a straggler batch can still try to land).
        _itemsShiftEpoch++;

        // ONE walk of the concurrent dictionary: snapshot affected entries here and hand them to the
        // shifter. Separate `.Keys.Count(...)` + LINQ `Where/OrderBy/ToList` walks measured 41+7ms
        // for 150 entries on-device (ConcurrentDictionary enumeration is expensive per-MoveNext) —
        // the bulk of every LoadMore trim frame.
        var affected = new List<KeyValuePair<int, MeasuredItemInfo>>();
        foreach (var kvp in _measuredItems)
        {
            if (kvp.Key >= startIndex)
                affected.Add(kvp);
        }

        if (affected.Count <= DIRECT_SHIFT_THRESHOLD)
        {
            // Small collection - direct shifting (simple & fast)
            DirectShiftMeasurements(startIndex, offset, affected);
        }
        else
        {
            // Large collection - offset mapping (scalable)
            OffsetMapMeasurements(startIndex, offset);
        }

        // Update measurement indices
        if (offset < 0) // Removal
        {
            if (LastMeasuredIndexLocal >= startIndex)
            {
                LastMeasuredIndexLocal = Math.Max(startIndex - 1, LastMeasuredIndexLocal + offset);
            }
        }
        else // Addition
        {
            if (LastMeasuredIndexLocal >= startIndex)
            {
                LastMeasuredIndexLocal += offset;
            }
        }

        Debug.WriteLine(
            $"[ShiftMeasurementIndices] Shifted {affected.Count} items from index {startIndex} by {offset}. LastMeasuredIndexLocal: {LastMeasuredIndexLocal}");
    }

    /// <summary>
    /// Direct shifting for small collections
    /// </summary>
    private void DirectShiftMeasurements(int startIndex, int offset,
        List<KeyValuePair<int, MeasuredItemInfo>> itemsToShift = null)
    {
        // caller usually supplies the affected snapshot (one dictionary walk total); fall back to
        // collecting here for other call sites
        if (itemsToShift == null)
        {
            itemsToShift = new List<KeyValuePair<int, MeasuredItemInfo>>();
            foreach (var kvp in _measuredItems)
            {
                if (kvp.Key >= startIndex)
                    itemsToShift.Add(kvp);
            }
        }

        // ascending for removals, descending for insertions — avoids key conflicts during rekeying
        if (offset > 0)
            itemsToShift.Sort((a, b) => b.Key.CompareTo(a.Key));
        else
            itemsToShift.Sort((a, b) => a.Key.CompareTo(b.Key));

        // Track instances renamed here so the structure pass below doesn't shift them AGAIN
        // (memo and structure usually share the same ControlInStack instances).
        var renamed = new HashSet<ControlInStack>();

        foreach (var (oldIndex, item) in itemsToShift)
        {
            _measuredItems.TryRemove(oldIndex, out _);
            var newIndex = oldIndex + offset;
            if (newIndex >= 0)
            {
                if (SkiaLayout.IsTraced(oldIndex) || SkiaLayout.IsTraced(newIndex))
                    SkiaLayout.TraceIdx(SkiaLayout.IsTraced(newIndex) ? newIndex : oldIndex, "SHIFT",
                        $"old={oldIndex} -> new={newIndex} (offset {offset}) measuredH={item.Cell?.Measured.Pixels.Height:0} startIndex={startIndex}");

                if (item.Cell != null)
                {
                    item.Cell.ControlIndex = newIndex;
                    renamed.Add(item.Cell);
                }

                _measuredItems[newIndex] = item;
            }
        }

        // Single O(n) pass for structure cells NOT covered by memo instances. The old per-item
        // UpdateStackStructureIndex was O(n^2) AND dead for shared instances: the instance was
        // renamed via item.Cell BEFORE the scan, so the scan never matched and always walked the
        // whole structure — measured 60ms of a 90ms trim frame on-device.
        if (StackStructure != null)
        {
            foreach (var cell in StackStructure.GetChildren())
            {
                if (cell == null || renamed.Contains(cell))
                    continue;
                if (cell.ControlIndex >= startIndex)
                {
                    var newIndex = cell.ControlIndex + offset;
                    if (newIndex >= 0)
                        cell.ControlIndex = newIndex;
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
        if (StackStructure == null || EffectiveItemsSource?.Count == 0)
            return;

        var totalItems = EffectiveItemsSource.Count;
        var measuredCount = LastMeasuredIndexLocal + 1;
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
                // Measured height of what we have + an ESTIMATE for the not-yet-measured tail, so the scroll
                // extent covers ALL items. Without the tail estimate the viewport clamps at the last measured
                // item (can't scroll/LoadMore past it -> never measures the rest = stuck). Refines as more
                // items measure (averageHeight converges).
                var averageHeight = actualMeasuredHeight / visibleItemsCount;
                var unmeasuredCount = Math.Max(0, totalItems - measuredCount);
                newContentHeight = actualMeasuredHeight + unmeasuredCount * averageHeight;

                //Debug.WriteLine($"[SkiaLayout] {progress:P1} measured - estimate: {newContentHeight:F1}px");
            }

            // CRITICAL: Never allow content size to shrink dramatically during scrolling
            // This prevents the "huge empty space" issue when scrolling fast
            var currentHeight = MeasuredSize.Pixels.Height;

            //if (SkiaLayout.DebugAssertStructure)
            //    Super.Log($"[CONTENTSIZE] total={totalItems} measured={measuredCount} progress={progress:0.000} visCnt={visibleItemsCount} measH={actualMeasuredHeight:0} new={newContentHeight:0} cur={currentHeight:0} structLen={StackStructure.Length}");

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
                // measured width + estimate for the unmeasured tail (see Column branch) so the scroll extent
                // covers all items instead of clamping at the last measured one.
                var averageWidth = actualMeasuredWidth / visibleItemsCount;
                var unmeasuredCount = Math.Max(0, totalItems - measuredCount);
                newContentWidth = actualMeasuredWidth + unmeasuredCount * averageWidth;

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

        var totalItems = EffectiveItemsSource.Count;
        var currentBatchStart = startIndex;
        var maxIterations = Math.Max(1, (totalItems / BackgroundMeasurementBatchSize) + 10); // Safety limit
        var iterationCount = 0;

        //Debug.WriteLine($"[MeasureVisible] Starting measurement from {startIndex} of {totalItems} total items");

        while (currentBatchStart < totalItems && !cancellationToken.IsCancellationRequested &&
               iterationCount < maxIterations)
        {
            lock (_structureChangesLock)
            {
                // Stop only for REAL structure mutations (their apply shifts positions this loop's
                // batches were computed from; epoch guards then drop stale batches on apply). Our OWN
                // staged BackgroundMeasurement batches queue here every iteration — breaking on them
                // starved measurement to one batch per draw-side restart (plus its 50ms start delay):
                // scroll jank from draw-time sync measures and ordered scrolls resolving against
                // estimates.
                var mustStop = false;
                foreach (var pending in _pendingStructureChanges)
                {
                    if (pending.Type != StructureChangeType.BackgroundMeasurement)
                    {
                        mustStop = true;
                        break;
                    }
                }

                if (mustStop)
                {
                    break;
                }
            }

            iterationCount++;
            var batchEnd = Math.Min(currentBatchStart + BackgroundMeasurementBatchSize, totalItems);
            var itemsToMeasure = batchEnd - currentBatchStart;

            // Safety check to prevent infinite loops
            if (itemsToMeasure <= 0)
            {
                Debug.WriteLine($"[MeasureVisible] WARNING: No items to measure in batch {currentBatchStart}-{batchEnd}, breaking loop");
                break;
            }

            //Debug.WriteLine($"[MeasureVisible] Measuring batch {currentBatchStart}-{batchEnd - 1} ({itemsToMeasure} items) [iteration {iterationCount}/{maxIterations}]");


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

            // Positions/indices above are only valid for the current shift epoch; if a shift
            // (e.g. head insert) lands while this batch measures, the batch must be dropped.
            var epoch = _itemsShiftEpoch;

#if BROWSER
            // ST WASM: a Task.Run item can STARVE INDEFINITELY under continuous rAF + input load (wheel
            // scroll + blits). Observed live: measurement frontier frozen for 13s mid-scroll — content
            // never grows, the scroll walls at the frontier ("scrolling stops on every measure"). The
            // pass already runs on the one thread anyway; call the batch inline, the Task.Delay(1)
            // below is the cooperative yield between batches.
            var measuredBatch = MeasureBatchInBackground(
                constraints, scale, currentBatchStart, itemsToMeasure, startX, startY, startRow, startCol,
                cancellationToken);
#else
            // Measure batch on background thread
            var measuredBatch = await Task.Run(() => MeasureBatchInBackground(
                constraints, scale, currentBatchStart, itemsToMeasure, startX, startY, startRow, startCol,
                cancellationToken), cancellationToken);
#endif

            if (cancellationToken.IsCancellationRequested)
            {
                Debug.WriteLine($"[MeasureVisible] Cancellation requested, stopping at batch {currentBatchStart}");
                break;
            }

            // Integrate results on background thread (safe for reading/staging)
            if (!cancellationToken.IsCancellationRequested)
            {
                IntegrateMeasuredBatch(measuredBatch, scale, context, startingPosition, epoch);
            }

            _backgroundMeasurementProgress = batchEnd;

            // Move to next batch
            currentBatchStart = batchEnd;

            // Let the render thread drain+apply this batch now instead of after the whole pass:
            // keeps cell positions fresh ahead of the viewport and keeps the ordered-scroll retry
            // (which only runs on draw) alive while it waits for the target to be measured.
            Repaint();

#if BROWSER
            // Single-threaded WASM has no real background: the awaited Task.Run above inlines on the ONE
            // (render + input) thread, so without an explicit real-timer yield this whole pass hogs it —
            // the "frozen page while measuring in background" at startup. Task.Delay(1) posts a browser
            // timer, so the event loop pumps rAF + input between batches (Task.Yield / Task.Run do NOT
            // reliably pump rAF under load — see the WASM Task.Run starvation lesson). Trickles the list
            // in cooperatively; the pass still measures the WHOLE list (far past the viewport), just
            // without freezing, which is exactly "measure more than visible at startup" minus the lock-up.
            await Task.Delay(1, cancellationToken);
#endif

            // Small delay to prevent overwhelming the system (DEBUG-tunable to simulate slow devices)
            if (DebugBackgroundMeasureDelayMs>0)
                await Task.Delay(DebugBackgroundMeasureDelayMs, cancellationToken);
        }

        if (iterationCount >= maxIterations)
        {
            Debug.WriteLine($"[MeasureVisible] WARNING: Hit maximum iteration limit ({maxIterations}), stopping background measurement");
        }

        //Debug.WriteLine($"[MeasureVisible] Completed background measurement up to index {_backgroundMeasurementProgress}");

        Repaint();
    }

    /// <summary>
    /// Stages a <see cref="StructureChangeType.SingleItemUpdate"/> for a cell whose view ALREADY measured
    /// itself during draw (self-growing content — e.g. a streaming AI bubble — served stale by the prepared
    /// pipeline). No measure happens here: the fresh size exists on the view; this bridges it into the
    /// structure through the same staged change <see cref="MeasureSingleItem"/> produces. Applied at the
    /// next DrawStack start (<see cref="ApplySingleItemUpdateChange"/>): slot resize + follower shift via
    /// OffsetSubsequentCells, BEFORE pass 1 — so positions, the gesture tree and any cached plane rebuild
    /// from one consistent chain. The explicit delta is captured against the CURRENT slot so the apply
    /// never depends on a background-measurement entry existing for this index.
    /// </summary>
    internal void StageSelfMeasuredCellUpdate(int itemIndex, ScaledSize freshMeasured, SKSize currentSlotPixels)
    {
        var cell = new ControlInStack
        {
            ControlIndex = itemIndex, Measured = freshMeasured, WasMeasured = true,
        };

        var info = new MeasuredItemInfo { Cell = cell, LastAccessed = DateTime.UtcNow, IsInViewport = true };
        var dataItem = GetItemForMemo(itemIndex);
        var delta = freshMeasured.Pixels - currentSlotPixels;

        lock (_structureChangesLock)
        {
            _pendingStructureChanges.Add(new StructureChange(StructureChangeType.SingleItemUpdate, MeasureStamp)
            {
                StartIndex = itemIndex,
                Count = 1,
                MeasuredItems = new List<MeasuredItemInfo> { info },
                OffsetOthers = new Vector2(delta.Width, delta.Height),
                Epoch = _itemsShiftEpoch,
                Items = dataItem != null ? new List<object> { dataItem } : null
            });
        }

        // see StageStructureChange: a cached layout must re-record for the queue to be drained
        if (UsingCacheType != SkiaCacheType.None)
        {
            InvalidateCache();
        }
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

            // Use an ISOLATED standalone instance (separate pool, never registered in _cellsInUseViews) so
            // this single-item remeasure cannot collide with the render thread's visible cell. Passing the
            // standalone as the template makes GetViewForIndex skip the shared in-use pool entirely — the
            // batch path already does this; the single-item path used template==null and raced the render
            // (an image-load remeasure could leave its measured layout in the visible cell's inner tree).
            var template = ChildrenFactory.GetTemplateInstance();
            if (template == null)
                return;

            // The index is only valid for the current shift epoch. A head insert/remove landing between
            // this measure and the render-thread apply shifts all indices — applying then would resize the
            // WRONG cell and OffsetSubsequentCells from it (transient seam overlap a plane bake can freeze).
            // Capture epoch + the data item so the apply can detect staleness and re-resolve.
            var epoch = _itemsShiftEpoch;
            var dataItem = GetItemForMemo(itemIndex);

            try
            {
                // Bind the standalone instance to this index for measuring (isolated from visible cells)
                var child = ChildrenFactory.GetViewForIndex(itemIndex, template, 0, true);


                if (child == null || !child.CanDraw)
                {
                    Debug.WriteLine(
                        $"[BackgroundMeasureSingleItem] Failed to get child or child cannot draw for item {itemIndex}");
                    return;
                }

                // Create cell structure for measurement. No View ref: the standalone is released below and
                // the apply path only consumes Cell.Measured (size), never Cell.View.
                var cell = new ControlInStack { ControlIndex = itemIndex };

                // Measure the item (simplified measurement for single item)
                var availableWidth = constraints.Width;
                var availableHeight = float.PositiveInfinity; // Allow natural height

                var measured = MeasureChild(child, availableWidth, availableHeight, scale);
                cell.Measured = measured;
                cell.WasMeasured = true;

                // Create measured item info
                var measuredItem = new MeasuredItemInfo
                {
                    Cell = cell,
                    LastAccessed = DateTime.UtcNow,
                    IsInViewport = true
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
                            MeasuredItems = new List<MeasuredItemInfo> { measuredItem },
                            Epoch = epoch,
                            Items = dataItem != null ? new List<object> { dataItem } : null
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
                            if (needrebuild
                                && UsingCacheType == SkiaCacheType.None
                                && Virtualisation == VirtualisationType.Smart
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
                                    // Use ArrangeCache to update cache's LastDestination for gesture coordinate translation
                                    child.ArrangeCache(destinationRect, child.SizeRequest.Width, child.SizeRequest.Height,
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
                                control.CreateHitRect(),
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
                    ChildrenFactory.ReleaseViewInUse(cell);
                }
            }
        }

        FirstVisibleIndexLocal = visibleIndex;
        LastVisibleIndexLocal = visibleIndexEnd;

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

        return countRendered;
    }

    private MeasuredListCells _measuredCells;

    /// <summary>Total items in ItemsSource space (the full source when the built-in window is engaged).</summary>
    public int EstimatedTotalItems => _itemsWindow?.Source?.Count ?? EffectiveItemsSource?.Count ?? 0;

    private double _lastMeasuredContentEnd = double.PositiveInfinity;

    // Returns how far we have measured content in units (vertical or horizontal)
    public double GetMeasuredContentEnd()
    {
        var structure = GetStackStructure();
        if (structure != null)
        {
            try
            {
                // Live structure — a background plane render can mutate it while we read the last cell.
                var last = structure.GetChildren().LastOrDefault();
                if (last != null)
                {
                    var value = Type == LayoutType.Row
                        ? last.Destination.Left / RenderingScale
                        : last.Destination.Top / RenderingScale;
                    _lastMeasuredContentEnd = value;
                    return value;
                }
            }
            catch (InvalidOperationException)
            {
                // structure mutated concurrently — use the cached value
                return _lastMeasuredContentEnd;
            }
        }

        return _lastMeasuredContentEnd;
    }

    /// <summary>
    /// Maps a content-from-top Y (pixels, in the layout's content-area space) to the item index whose REAL
    /// measured row contains it, using the measured structure (variable heights). Returns -1 if the Y is
    /// outside the measured content. Outputs the row's real top and height in pixels. Used by tiled-planes
    /// gesture hit-testing so a tap lands on the correct variable-height row instead of a uniform-slot guess.
    /// </summary>
    public int GetIndexAtContentY(double contentYpx, out float rowTopPx, out float rowHeightPx)
    {
        rowTopPx = 0;
        rowHeightPx = 0;
        var s = GetStackStructure();
        if (s == null)
            return -1;
        try
        {
            // Monotonic by index; a background plane render can mutate it while we read — tolerate (catch).
            foreach (var c in s.GetChildren())
            {
                if (c == null)
                    continue;
                float top = c.Destination.Top;
                float bot = c.Destination.Bottom;
                if (bot <= top)
                    continue;
                if (contentYpx >= top && contentYpx < bot)
                {
                    rowTopPx = top;
                    rowHeightPx = bot - top;
                    return c.ControlIndex;
                }
            }
        }
        catch (InvalidOperationException)
        {
            // structure mutated concurrently
        }
        return -1;
    }

    /// <summary>
    /// Gets estimated total content size for virtualized lists with unmeasured items
    /// </summary>
    public ScaledSize GetEstimatedContentSize(float scale)
    {
        if (!IsTemplated || EffectiveItemsSource == null || EffectiveItemsSource.Count == 0)
            return MeasuredSize;

        var itemsCount = EffectiveItemsSource.Count;
        var measuredCount = LastMeasuredIndexLocal + 1;

        if (measuredCount >= itemsCount)
            return MeasuredSize; // All items measured, use actual size

        var structure = GetStackStructure();
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
        var structure = GetStackStructureForMeasuring().Clone();
        structure.Append(rows);
        StackStructureMeasured = structure;
    }

    public int MeasureAdditionalItems(int batchSize, int aheadCount, float scale)
    {
        if (EffectiveItemsSource == null || EffectiveItemsSource.Count == 0)
            return 0;

        int startIndex = LastMeasuredIndexLocal + 1;
        int endIndex = Math.Min(startIndex + batchSize + aheadCount, EffectiveItemsSource.Count);

        Debug.WriteLine(
            $"[MeasureAdditionalItems] INCREMENTAL: Measuring items {startIndex}-{endIndex - 1} (batch: {batchSize}, ahead: {aheadCount})");

        if (startIndex > endIndex)
            return 0;

        int countToMeasure = endIndex - startIndex;
        if (countToMeasure <= 0)
            return 0;

        var structure = GetStackStructureForMeasuring().Clone();
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

            LastMeasuredIndexLocal = startIndex + countToMeasure - 1;

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
                    stackHeight = structure.GetChildren().Sum(x => x.Measured.Pixels.Height) + spacingPixels * structure.MaxRows - 1;
                }
                else
                {
                    if (_listAdditionalMeasurements == 1)
                    {
                        //add some more space to be able to scroll
                        stackHeight += 1500 * scale;
                    }

                    if (endIndex == EffectiveItemsSource.Count)
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
                $"[MeasureAdditionalItems] COMPLETED: Measured {countToMeasure} additional items, now measured up to index {LastMeasuredIndexLocal} of {EffectiveItemsSource.Count} total");

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
                    ChildrenFactory.ReleaseViewInUse(cell);
                }
            }
        }
        }

        /// <summary>
        /// Average measured item height in pixels, used to estimate item positions for the sliding window.
        /// </summary>
        private float _lastGoodAvgItemHeightPx;

        public float GetAverageItemHeightPixels(float scale)
        {
            var s = GetStackStructureForMeasuring();
            if (s != null)
            {
                try
                {
                    float sum = 0;
                    int n = 0;
                    // GetChildren() is the live structure; a background plane render can mutate it while we
                    // read. Tolerate that (catch below) and fall back to the last good value.
                    foreach (var k in s.GetChildren())
                    {
                        if (k != null && k.Measured.Pixels.Height > 0)
                        {
                            sum += k.Measured.Pixels.Height;
                            n++;
                        }
                    }
                    if (n > 0)
                    {
                        _lastGoodAvgItemHeightPx = sum / n;
                        return _lastGoodAvgItemHeightPx;
                    }
                }
                catch (InvalidOperationException)
                {
                    // structure mutated concurrently — use the cached value
                }
            }

            // Stable fallback: when the measured structure is momentarily empty (e.g. right after a LoadMore
            // re-init) keep the last good average so the tiled-planes slot doesn't jump — a slot jump would
            // re-render/position tiles against a different grid and show empty/misaligned bands.
            if (_lastGoodAvgItemHeightPx > 1f)
                return _lastGoodAvgItemHeightPx;

            return 60f * scale;
        }

        /// <summary>
        /// Smallest measured item height (pixels) across the latest measured structure. Used by the tiled
        /// planes pool auto-sizer for the worst case (the shortest cell packs the most rows into a band).
        /// Returns 0 if nothing measured yet.
        /// </summary>
        private float _lastGoodMinItemHeightPx;

        public float GetMinItemHeightPixels(float scale)
        {
            var s = GetStackStructureForMeasuring();
            if (s != null)
            {
                try
                {
                    float min = float.MaxValue;
                    // Live structure — tolerate concurrent mutation from a background plane render.
                    foreach (var k in s.GetChildren())
                    {
                        if (k != null && k.Measured.Pixels.Height > 0 && k.Measured.Pixels.Height < min)
                            min = k.Measured.Pixels.Height;
                    }
                    if (min != float.MaxValue)
                    {
                        _lastGoodMinItemHeightPx = min;
                        return min;
                    }
                }
                catch (InvalidOperationException)
                {
                    // structure mutated concurrently — use the cached value
                }
            }
            return _lastGoodMinItemHeightPx; // 0 if nothing measured yet
        }

        /// <summary>
        /// Builds a fresh structure for items whose estimated content position falls inside the band
        /// [<paramref name="bandTopPx"/>..<paramref name="bandBottomPx"/>] (content-from-top pixels), with
        /// cells taken from the recycling pool, correctly bound and positioned. This is the per-plane
        /// sliding window: only the band's items are realized (fits the pool), so any scroll position
        /// renders correct content. Caller MUST release the returned cells after painting.
        /// Does NOT touch the shared structure, MeasuredSize, or trigger invalidation.
        /// </summary>
        /// <summary>
        /// Builds an ESTIMATE-ONLY layout structure for a plane/tile's fixed content band
        /// [bandTopPx .. bandBottomPx]. It positions every item i on a consistent grid (i * (avg+spacing))
        /// with an estimated height (avg), and marks each cell measured. NO views are realized here:
        /// the SkiaScroll plane paint feeds this as PlaneOverrideStructure and DrawStack's normal PASS2
        /// owns the real GetViewForIndex / measure / draw / recycle lifecycle for the cells it draws.
        /// This avoids double-ownership of pooled views (which corrupts the recycling pool).
        /// The consistent grid also guarantees adjacent tiles align with no gaps/overlaps at boundaries.
        /// </summary>
        public LayoutStructure BuildPlaneWindowStructure(double bandTopPx, double bandBottomPx, float scale,
            float contentWidth)
        {
            var rows = new List<List<ControlInStack>>();

            if (!IsTemplated || EffectiveItemsSource == null || EffectiveItemsSource.Count == 0)
                return new LayoutStructure(rows);

            // Use the realized data-contexts snapshot count, not the live ItemsSource.Count: a cell built
            // for an index the render thread can't realize yet (post-LoadMore, before the snapshot refresh)
            // would fail GetViewForIndex in DrawStack PASS2 and blank the whole tile.
            int count = ChildrenFactory.GetChildrenCount();
            if (count <= 0)
                return new LayoutStructure(rows);
            float avg = GetAverageItemHeightPixels(scale);
            if (avg < 1f) avg = 60f * scale;

            // Use the caller-provided (stable) content width — MeasuredSize/DrawingRect are unreliable on
            // the background plane-preparation thread and can read as 0.
            float width = contentWidth > 1f ? contentWidth
                : (MeasuredSize.Pixels.Width > 1 ? MeasuredSize.Pixels.Width : DrawingRect.Width);
            if (width < 1f) return new LayoutStructure(rows);

            float spacing = (float)(Spacing * scale);
            double slot = avg + spacing;

            // Snapshot REAL measured positions/heights per index from the measured structure. The uniform
            // grid (idx*slot, height=avg) only works for equal-height cells; variable heights need each
            // cell's actual cumulative top + measured height, otherwise every cell gets clamped to avg and
            // positions desync (overlaps/gaps). Background plane-prep thread: tolerate concurrent mutation
            // (same pattern as GetAverageItemHeightPixels).
            var measured = new Dictionary<int, (float top, float height)>();
            int measuredMaxIndex = -1;
            double measuredTailTop = 0;   // content-top right after the highest measured cell
            var s = GetStackStructureForMeasuring();
            if (s != null)
            {
                try
                {
                    foreach (var k in s.GetChildren())
                    {
                        if (k == null) continue;
                        int ci = k.ControlIndex;
                        if (ci < 0 || ci >= count) continue;
                        float h = k.Measured.Pixels.Height;
                        if (h <= 0) continue;
                        float top = k.Destination.Top;
                        measured[ci] = (top, h);
                        if (ci > measuredMaxIndex)
                        {
                            measuredMaxIndex = ci;
                            measuredTailTop = top + h + spacing;   // positions are monotonic with index
                        }
                    }
                }
                catch (InvalidOperationException)
                {
                    // structure mutated concurrently — fall back to whatever we collected
                }
            }

            // Measure the band's UNMEASURED cells now so their slots are REAL (cumulative from the measured
            // tail), not avg estimates. The tile blit places cells at their Destination, so the slot MUST equal
            // the cell's real height or the tile's content won't fill its band and a seam/gap appears. This runs
            // on the render thread for a synchronous visible tile (the visible cells must be measured to draw
            // anyway, exactly like non-plane virtualization) and on the bg thread for ahead tiles. Local-only:
            // never mutates the shared structure / MeasuredSize (no measure->grow->invalidate->re-prepare loop).
            if (measuredMaxIndex < count - 1 && bandBottomPx + slot > measuredTailTop
                && RecyclingTemplate != RecyclingTemplate.Disabled)
            {
                SkiaControl template = ChildrenFactory.GetTemplateInstance();
                try
                {
                    double runTop = measuredMaxIndex >= 0 ? measuredTailTop : 0;
                    int mi = measuredMaxIndex + 1;
                    while (mi < count && runTop <= bandBottomPx + slot)
                    {
                        var child = ChildrenFactory.GetViewForIndex(mi, template, 0, true);
                        if (child == null)
                            break;

                        // Measure with UNBOUNDED height (Column) — constraining to avg would clip a tall cell
                        // to avg and paint it a line short.
                        var measureRect = new SKRect(0, (float)runTop, width, (float)runTop + float.PositiveInfinity);
                        var tmpCell = new ControlInStack { ControlIndex = mi, Destination = measureRect, Area = measureRect };
                        var m = MeasureAndArrangeCell(measureRect, tmpCell, child, measureRect, scale);
                        float h = m.Pixels.Height;
                        if (h <= 0) h = avg;

                        measured[mi] = ((float)runTop, h);
                        runTop += h + spacing;
                        mi++;
                    }
                    measuredMaxIndex = mi - 1;
                    measuredTailTop = runTop;
                }
                finally
                {
                    if (template != null)
                        ChildrenFactory.ReleaseTemplateInstance(template);
                }
            }

            // Real top + height for an index: measured value if known, else avg-estimate continuing from the
            // measured tail (or pure uniform grid if nothing measured yet).
            double TopFor(int index, out float height)
            {
                if (measured.TryGetValue(index, out var m))
                {
                    height = m.height;
                    return m.top;
                }
                height = avg;
                if (measuredMaxIndex >= 0 && index > measuredMaxIndex)
                    return measuredTailTop + (index - measuredMaxIndex - 1) * slot;
                return index * slot;
            }

            // Estimate a start index, then walk to the real first cell intersecting the band (heights vary,
            // so the estimate can be off — the walk converges locally, not a full O(count) scan).
            int start;
            if (measuredMaxIndex >= 0 && bandTopPx >= measuredTailTop)
                start = measuredMaxIndex + 1 + (int)Math.Floor((bandTopPx - measuredTailTop) / slot);
            else
                start = (int)Math.Floor(bandTopPx / slot);
            start = Math.Clamp(start, 0, count - 1);

            while (start > 0)
            {
                double t = TopFor(start, out _);
                if (t <= bandTopPx) break;
                start--;
            }
            while (start < count - 1)
            {
                double t = TopFor(start, out float hh);
                if (t + hh > bandTopPx) break;
                start++;
            }
            start = Math.Max(0, start - 1);   // overscan 1 above

            int idx = start;
            int rowNum = 0;

            while (idx < count)
            {
                double top = TopFor(idx, out float h);
                if (top > bandBottomPx + slot)   // overscan 1 below
                    break;

                var dest = new SKRect(0, (float)top, width, (float)top + h);
                var cell = new ControlInStack
                {
                    ControlIndex = idx,
                    Column = 0,
                    Row = rowNum,
                    Destination = dest,
                    Area = dest,
                    Measured = ScaledSize.FromPixels(width, h, scale),
                    WasMeasured = true,
                };

                rows.Add(new List<ControlInStack> { cell });

                idx++;
                rowNum++;
            }

            return new LayoutStructure(rows);
        }
    }

public record MeasuredListCell(ControlInStack Cell, int Index);

public class MeasuredListCells : ReadOnlyCollection<MeasuredListCell>
{
    public MeasuredListCells(IList<MeasuredListCell> list) : base(list)
    {
    }
}
