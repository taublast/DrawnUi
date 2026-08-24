using System.Collections;

namespace DrawnUi.Draw
{
    /// <summary>
    /// Top level class for working with ItemTemplates. Holds visible views.
    /// </summary>
    public partial class ViewsAdapter : IDisposable
    {
        public static bool LogEnabled = false;

        #region FILL POOL

        // Add these fields to the ViewsAdapter class:
        private CancellationTokenSource _backgroundFillCancellation;
        private readonly object _fillLock = new object();

        /// <summary>
        /// Safely fills the pool in background with cancellation support. Cancels any previous background filling operation.
        /// </summary>
        /// <param name="size">Number of views to pre-create in the pool</param>
        /// <returns>Task that completes when pool filling is done or cancelled</returns>
        public Task FillPoolInBackgroundAsync(int size)
        {
            if (IsDisposed || _templatedViewsPool == null)
                return Task.CompletedTask;

            lock (_fillLock)
            {
                // Cancel any existing background operation
                _backgroundFillCancellation?.Cancel();
                _backgroundFillCancellation?.Dispose();

                // Create new cancellation token for this operation
                _backgroundFillCancellation = new CancellationTokenSource();
            }

            var token = _backgroundFillCancellation.Token;

            return Task.Run(async () =>
            {
                try
                {
                    await FillPoolWithCancellation(size, token);
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancelled, no need to log
                }
                catch (Exception e)
                {
                    if (LogEnabled)
                        Super.Log($"[ViewsAdapter] Background pool filling failed: {e}");
                }
            }, token);
        }

        /// <summary>
        /// Fills the pool with cancellation support. On BROWSER (single-threaded WASM) it yields to the
        /// browser event loop every few creations so the warm-up never freezes the render/input thread;
        /// on other heads the loop completes synchronously (the returned Task is already done), so the
        /// desktop warm-up behaviour is unchanged.
        /// </summary>
        /// <param name="size">Target pool size</param>
        /// <param name="cancellationToken">Token to cancel the operation</param>
        private async Task FillPoolWithCancellation(int size, CancellationToken cancellationToken)
        {
            if (IsDisposed || _templatedViewsPool == null)
                return;

            if (size <= 0)
                return;

            // The pool cap may have been baked BEFORE the first layout pass (viewport-derived ceiling
            // computed with unknown visible indices). This fill runs AFTER the first paint — recompute
            // from live state so the warm target actually fits; otherwise the loop exits at the stale
            // tiny cap and ReserveTemplates silently under-fills (same recompute the draw path does when
            // the pool refuses a rent).
            if (_parent != null)
            {
                var limit = _parent.GetTemplatesPoolLimitPublic();
                if (limit > _templatedViewsPool.MaxSize)
                    _templatedViewsPool.MaxSize = limit;
            }

            while (_templatedViewsPool.Size < size &&
                   _templatedViewsPool.CreatedCount < _templatedViewsPool.MaxSize &&
                   !IsDisposed &&
                   !_templatedViewsPool.IsDisposing)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    _templatedViewsPool.Reserve();
                }
                catch (Exception e)
                {
                    if (LogEnabled)
                        Super.Log($"[ViewsAdapter] Failed to reserve view during background fill: {e}");
                    break;
                }

                // Small delay to prevent blocking the thread pool
                if (_templatedViewsPool.Size % 5 == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();

#if BROWSER
                    // WASM has no worker thread — this loop runs on the render/input thread. Yield to the
                    // browser event loop (real timer) so rAF + input pump between cell ctors instead of the
                    // ~225-cell warm-up locking the page. Non-BROWSER never compiles this: the method
                    // completes synchronously, identical to the old void loop.
                    await Task.Delay(1, cancellationToken);
#endif
                }
            }
        }

        /// <summary>
        /// Cancels any ongoing background pool filling operation
        /// </summary>
        public void CancelBackgroundPoolFilling()
        {
            lock (_fillLock)
            {
                _backgroundFillCancellation?.Cancel();
            }
        }

        // Update the existing FillPool method to support cancellation:
        /// <summary>
        /// Use to manually pre-create views from item templates so when we suddenly need more templates they would already be ready, avoiding lag spike,
        /// This will respect pool MaxSize in order not to overpass it.
        /// </summary>
        /// <param name="size">Target pool size</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        public void FillPool(int size, CancellationToken cancellationToken = default)
        {
            if (IsDisposed)
                return;

            if (size > 0)
            {
                while (_templatedViewsPool.Size < size &&
                       _templatedViewsPool.CreatedCount < _templatedViewsPool.MaxSize &&
                       !cancellationToken.IsCancellationRequested)
                {
                    _templatedViewsPool.Reserve();
                }
            }
        }

        #endregion

        #region INITIALIZE

        /// <summary>
        /// Builds an immutable snapshot (fixed array) of the live collection. MUST be called on the mutating
        /// (UI) thread, serialized with the collection change: `new object[Count]` + `CopyTo` is NOT atomic, so
        /// a concurrent mutation between the two (the Android render-thread/main-thread split) tears the copy —
        /// ArgumentOutOfRangeException or a torn array binding cells to wrong/-1 indices (overlapping cells).
        /// Cost: one shallow copy of item references per change - cheap.
        /// </summary>
        private static IList CreateDataContextsSnapshot(IList source)
        {
            if (source == null)
                return null;

            var snapshot = new object[source.Count];
            source.CopyTo(snapshot, 0);
            return snapshot;
        }

        /// <summary>
        /// Public capture point for the immutable data-contexts snapshot. Call on the MUTATING (UI) thread,
        /// synchronously inside the CollectionChanged handler, then hand the returned array to the deferred /
        /// render-thread init (InitializeSoft / ApplyInsertShift / ApplyRemoveShift) via their preCaptured
        /// parameter so those never re-read the live collection off-thread. Fixes the Android tear where the
        /// render thread snapshotted ItemsSource while the main thread mutated it.
        /// </summary>
        public IList CaptureContextsSnapshot(IList source) => CreateDataContextsSnapshot(source);

        /// <summary>
        /// Returns the data context currently held at the given index in the render snapshot, or null.
        /// Used to capture a scroll anchor before a collection change refreshes the snapshot.
        /// </summary>
        public object GetContextAt(int index)
        {
            var contexts = _dataContexts;
            if (contexts != null && index >= 0 && index < contexts.Count)
                return contexts[index];
            return null;
        }

        /// <summary>
        /// Swaps in a fresh immutable snapshot from the just-mutated live collection and refreshes any
        /// per-thread iterators so they stop pointing at the previous snapshot. Must run on the mutating
        /// (UI) thread, serialized with the collection mutation that triggered it.
        /// </summary>
        private void RefreshDataContexts(IList source, IList preCaptured = null)
        {
            _dataContextsSource = source;
            // preCaptured: an immutable snapshot already taken on the mutating thread (serialized with the
            // change). Use it verbatim instead of copying the live source here — this method can run on the
            // render thread (deferred InitializeSoft / ApplyInsertShift), where reading live source races the
            // main-thread mutation. Falls back to a live copy only for same-thread callers that pass none.
            _dataContexts = preCaptured ?? CreateDataContextsSnapshot(source);

            // Pool MaxSize is CALIBRATED ONCE (full InitializeTemplates) and never re-derived here:
            // re-setting it per snapshot swap made the ceiling churn with every windowed slide
            // (trim/add pairs re-sizing the pool each refresh = disposal/creation spikes). Demand-driven
            // growth still exists: GetViewForIndex recomputes the honest ceiling and retries once when
            // the pool refuses a realized cell. Tradeoff: a trimmed window keeps its high-water pool.
            //
            // TRIED AND REVERTED (2026-07-20): correcting the cap DOWNWARD once when the source leaves the
            // cache-all regime (>256 items, where the honest ceiling stops being "one view per item"). It
            // does shrink the pool, but the drain is not free: the surplus is disposed through Return, and
            // the very next event is usually the window engage dropping ~300 contexts to 128 — so ~170
            // control disposals land in ONE frame, a visible spike exactly at the window cut (device).
            // Any retry must drain the surplus over several frames, not on the return path.

            lock (_lockTemplates)
            {
                foreach (var wrapper in _wrappers.Values)
                {
                    wrapper.SetDataContexts(_dataContexts);
                }
            }
        }

        /// <summary>
        /// Main method to initialize templates, can use InitializeTemplatesInBackground as an option.
        /// </summary>
        /// <param name="template"></param>
        /// <param name="dataContexts"></param>
        /// <param name="poolSize"></param>
        /// <param name="reserve">Pre-create number of views to avoid lag spikes later, useful to do in backgound.</param>
        public void InitializeTemplates(NotifyCollectionChangedEventArgs args, Func<object> template, IList dataContexts,
            int poolSize, int reserve = 0)
        {
            if (IsDisposed || _parent != null && _parent.IsDisposing)
                return;


            //Debug.WriteLine("[CELLS] InitializeTemplates");
            if (template == null)
            {
                TemplatesInvalidated = false;
                TemplesInvalidating = false;
                return;
            }

            bool CheckTemplateChanged()
            {
                var ret = _templatedViewsPool.CreateTemplate != template
                    //|| _parent.SplitMax != _forColumns || _parent.MaxRows != _forRows
                    ;
                return ret;
            }

            var
                layoutChanged = //todo cannot really optimize as can have same nb of cells, same references for  _dataContexts != dataContexts but different contexts
                    _parent.RenderingScale != _forScale || _parent.Split != _forSplit;

            var changedData = _dataContextsSource != dataContexts;

            var needReset = args.Action == NotifyCollectionChangedAction.Reset
                            || (layoutChanged || _templatedViewsPool == null || _dataContextsSource != dataContexts ||
                                CheckTemplateChanged());

            if (LogEnabled)
                Super.Log($"[ViewsAdapter] InitializeTemplates {args.Action} count={dataContexts?.Count} needReset={needReset} busy={TemplatesBusy} invalidating={TemplesInvalidating}");

            if (needReset)
            {
                CancelBackgroundPoolFilling();

                //temporarily fixed to android until issue found
                lock (_lockTemplates)
                {
                    //kill provider ability to provide deprecated templates
                    _wrappers.Clear();
                    _forScale = _parent.RenderingScale;
                    _forSplit = _parent.Split;
                    _dataContexts = null;
                    _dataContextsSource = null;
                    AddedMore = 0;
                }

                InitializeFull(false, template, dataContexts, poolSize, reserve); //.ConfigureAwait(false);
            }
            else
            {
                CleanupInvalidCachedViews();
                bool result = false;
                switch (args.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        result = HandleAdd(args, dataContexts);
                        break;

                    case NotifyCollectionChangedAction.Remove:
                        result = HandleRemove(args, dataContexts);
                        break;

                    case NotifyCollectionChangedAction.Replace:
                        result = HandleReplace(args, dataContexts);
                        break;

                    case NotifyCollectionChangedAction.Move:
                        result = HandleMove(args, dataContexts);
                        break;
                }

                if (LogEnabled)
                {
                    Super.Log(
                        $"[ViewsAdapter] Handle SmartCollectionChange: {args.Action} result {result}");
                }

                // Refresh the immutable snapshot from the just-mutated live collection so the render
                // thread sees the new items without ever indexing the live collection (issue #300).
                RefreshDataContexts(dataContexts);

                //looks like only itemssource has changed, resize pool, keep old templates, be fast
                InitializeSoft(layoutChanged, dataContexts, poolSize);
            }
        }

        /// <summary>
        /// TRUE when the hosting scroll is neither animating nor under the finger, so work that costs a
        /// frame (mass cell disposal after the pool ceiling drops) cannot land on a moving frame.
        /// No hosting scroll = treat as at rest.
        /// </summary>
        private bool IsListAtRest()
        {
            if (_parent?.Parent is SkiaScroll scroll)
                return !scroll.IsScrolling && !scroll.IsUserPanning;

            return true;
        }

        public void InitializeSoft(bool layoutChanged, IList dataContexts, int poolSize, IList preCaptured = null)
        {
            if (LogEnabled)
                Super.Log("[ViewsAdapter] InitializeSoft");

            lock (_lockTemplates)
            {
                TemplesInvalidating = false;

                // GROW ANYTIME, SHRINK ONLY AT REST.
                // Raising the ceiling is free. LOWERING it makes every over-cap Return dispose, and
                // poolSize is derived from the CURRENT EffectiveItemsSource — for a windowed source that
                // is just the resident SLICE, so the window engage/trim lowered it by ~70 cells and
                // disposed them all inside the very frame that was already doing the swap (the lag spike
                // at the cut, "cells 18/200 -> 16/131"). Never lowering is not the answer either: a
                // non-recycling (cache-all) windowed list then parks a cell for every context ever
                // scrolled instead of releasing the trimmed ones, and that memory growth is its own
                // stutter. So the shrink still happens — just never on a moving frame.
                if (poolSize > _templatedViewsPool.MaxSize)
                {
                    _templatedViewsPool.MaxSize = poolSize;
                }
                else if (poolSize > 0 && poolSize < _templatedViewsPool.MaxSize && IsListAtRest())
                {
                    _templatedViewsPool.MaxSize = poolSize;
                }
                if (layoutChanged)
                {
                    foreach (var view in _cellsInUseViews.Values)
                    {
                        view.InvalidateChildrenTree();
                    }
                }

                //MarkAllViewsAsHidden(); //todo think
                Monitor.PulseAll(_lockTemplates);
            }

            // Refresh the data-contexts snapshot from the (mutated) source. Without this a structure-preserving
            // Add/Remove (e.g. LoadMore append) updates the pool size but leaves _dataContexts at the old count,
            // so GetViewForIndex returns null for the new indices -> blank cells/planes. Runs on the mutating
            // (UI) thread, serialized with the collection change that triggered it. When preCaptured is supplied
            // (deferred render-thread drain), publish that mutation-thread snapshot instead of copying live.
            if (dataContexts != null)
                RefreshDataContexts(dataContexts, preCaptured);
        }

        void SetTemplatesAvailable(IList dataContexts)
        {
            RefreshDataContexts(dataContexts);
            TemplatesInvalidated = false;
            TemplatesBusy = false;
            _parent.OnTemplatesAvailable();
        }

        async void InitializeFull(bool measure, Func<object> template, IList dataContexts, int poolSize, int reserve = 0)
        {
            if (LogEnabled)
                Super.Log("[ViewsAdapter] InitializeFull");

            lock (_lockTemplates)
            {
                CancelBackgroundPoolFilling();

                var kill = _templatedViewsPool;

                lock (lockVisible)
                {
                    foreach (var view in _cellsInUseViews.Values)
                    {
                        view.Dispose();
                    }

                    _cellsInUseViews.Clear();
                }

                _templatedViewsPool?.Dispose();

                _templatedViewsPool = new TemplatedViewsPool(template, poolSize, (k) => { _parent?.DisposeObject(k); });

                // Clear standalone pool when template changes
                kill?.ClearStandalonePool();

                if (UsesGenericPool)
                {
                    // Warm only a SMALL batch synchronously so the FIRST frame already has cells. The rest of
                    // the prefill (up to ItemTemplatePoolSize for RecyclingTemplate.Disabled) is created on a
                    // BACKGROUND thread AFTER the visible measured cells are painted — kicked from
                    // DrawStackVisibleChildren's first-fresh-draw hook. Filling the whole prefill synchronously
                    // here blocked the first draw -> long blank startup.
                    var mult = _parent.Split > 0 ? _parent.Split : 1;
                    FillPool(Math.Min(reserve, mult * 3), dataContexts);
                }

                if (kill != null)
                {
                    kill.IsDisposing = true;

                    _parent?.DisposeObject(kill);
                }

                TemplesInvalidating = false;

                Monitor.PulseAll(_lockTemplates);
            }

            if (measure)
            {
                TemplatesBusy = true;

                while (_parent.IsMeasuring)
                {
                    await Task.Delay(10);
                }

                RefreshDataContexts(dataContexts);
                TemplatesInvalidated = false; //enable TemplatesAvailable otherwise beackground measure will fail
                _parent.MeasureLayout(
                    new(_parent._lastMeasuredForWidth, _parent._lastMeasuredForHeight, _parent.RenderingScale), true);
            }

            SetTemplatesAvailable(dataContexts);
        }

        #endregion

        #region SHIFT

        /// <summary>
        /// Synchronously aligns the adapter with an index shift the layout structure is applying
        /// right now (e.g. head insert for backward LoadMore): rekeys the views currently in use
        /// (so a visible cell keeps its view and binding — rebinding becomes a no-op) and swaps in
        /// a fresh data-contexts snapshot. Without this the structure indices move immediately on
        /// the render thread while the adapter waits for a POSTED InitializeSoft — for one frame
        /// visible indices resolve to pre-insert items: wrong contexts flash and their draw-time
        /// remeasure poisons the structure with wrong heights.
        /// Call from the render thread, atomically with the structure index shift, after the
        /// source mutation has completed.
        /// </summary>
        public void ApplyInsertShift(IList source, int startIndex, int count, IList preCaptured = null)
        {
            ShiftCachedViewIndexes(startIndex, count);
            RefreshDataContexts(source, preCaptured);
        }

        /// <summary>
        /// Synchronously aligns the adapter with a removal the layout structure is applying right
        /// now (window trim for a bounded in-memory ItemsSource): releases views bound to removed
        /// items, rekeys views after the removed range, and swaps in a fresh data-contexts snapshot.
        /// ShiftCachedViewIndexes alone is wrong here — views INSIDE the removed range would be
        /// shifted onto surviving indices instead of released.
        /// Call from the render thread, atomically with the structure change, after the source
        /// mutation has completed.
        /// </summary>
        public void ApplyRemoveShift(IList source, int startIndex, int count, IList preCaptured = null)
        {
            lock (lockVisible)
            {
                foreach (var kvp in _cellsInUseViews.ToArray())
                {
                    if (kvp.Key >= startIndex && kvp.Key < startIndex + count)
                    {
                        _cellsInUseViews.Remove(kvp.Key);
                        kvp.Value.ContextIndex = -1;
                        ReleaseViewToPool(kvp.Value);
                    }
                }
            }

            ShiftCachedViewIndexes(startIndex + count, -count);
            RefreshDataContexts(source, preCaptured);
        }

        /// <summary>
        /// Shifts cached view indexes when items are inserted/removed
        /// </summary>
        /// <param name="startIndex">Index where change occurred</param>
        /// <param name="offset">Positive for insertions, negative for removals</param>
        private void ShiftCachedViewIndexes(int startIndex, int offset)
        {
            if (offset == 0) return;

            lock (lockVisible)
            {
                var itemsToUpdate = new List<(int oldIndex, int newIndex, SkiaControl view)>();

                // Find all cached views that need index shifting
                foreach (var kvp in _cellsInUseViews.ToArray())
                {
                    var currentIndex = kvp.Key;
                    var view = kvp.Value;

                    if (currentIndex >= startIndex)
                    {
                        var newIndex = currentIndex + offset;
                        if (newIndex >= 0) // Only keep valid indexes
                        {
                            itemsToUpdate.Add((currentIndex, newIndex, view));
                        }
                        else
                        {
                            // Remove views that would have negative indexes
                            _cellsInUseViews.Remove(currentIndex);
                            ReleaseViewToPool(view);
                        }
                    }
                }

                // Update the dictionary in TWO passes. Doing remove+add interleaved (as before) corrupts the
                // map when the source and destination index ranges OVERLAP — which they always do on a head
                // CUT: a shift of -count maps keys [count .. N) onto [0 .. N-count), overlapping in
                // [count .. N-count). Dictionary enumeration order is arbitrary, so writing [newIndex]=view
                // could land on an old key that has NOT been moved yet, overwriting that survivor. The
                // overwritten cell is dropped from the map but stays parented and is never pooled -> an
                // orphan-with-Parent, and the pool total silently shrinks by ~overlap count every cut.
                //
                // PASS 1: remove every shifting old key first, so the shifted set cannot clobber itself.
                foreach (var (oldIndex, _, _) in itemsToUpdate)
                {
                    _cellsInUseViews.Remove(oldIndex);
                }

                // PASS 2: write the new keys. Any remaining collision is with a NON-shifted entry
                // (key < startIndex); recycle the displaced cell instead of orphaning it.
                foreach (var (oldIndex, newIndex, view) in itemsToUpdate)
                {
                    if (_cellsInUseViews.TryGetValue(newIndex, out var displaced) && !ReferenceEquals(displaced, view))
                    {
                        ReleaseViewToPool(displaced, true);
                    }

                    _cellsInUseViews[newIndex] = view;
                    view.ContextIndex = newIndex;

                    if (LogEnabled)
                    {
                        Super.Log($"[ViewsAdapter] Shifted view {view.Uid} from index {oldIndex} to {newIndex}");
                    }
                }
            }
        }

        /// <summary>
        /// Read-only: is a realized (in-use) view currently mapped to this item index? A background plane
        /// renderer calls <see cref="GetViewForIndex"/> off the render thread and bails the whole draw if it
        /// returns null (pool not yet realized after a grow/rekey). Checking this on the render thread before
        /// dispatching an off-thread bake lets a self-caching layout fall back to a synchronous record for the
        /// few frames until the views are realized — avoiding a baked-in empty band.
        /// </summary>
        public bool IsViewRealizedForIndex(int index)
        {
            lock (lockVisible)
                return _cellsInUseViews.ContainsKey(index);
        }

        /// <summary>
        /// Read-only peek at the realized (in-use) view mapped to this item index, or null. Unlike
        /// <see cref="GetViewForIndex"/> this never binds, rents or mutates pool state — safe for
        /// render-thread gates that need to inspect a view's cache readiness (e.g. a self-caching
        /// layout refusing to record a plane while an ImageDoubleBuffered cell hasn't baked yet).
        /// </summary>
        public SkiaControl PeekRealizedViewForIndex(int index)
        {
            lock (lockVisible)
                return _cellsInUseViews.TryGetValue(index, out var view) ? view : null;
        }

        #region PREPARED VIEWS (see SkiaLayout.UsePreparedViews)

        /// <summary>
        /// True when the REAL view for this index is already bound to its current data context and measured
        /// (either realized in-use or parked in the context-indexed pool), so drawing it will not trigger a
        /// render-thread measure. Read-only — never rents or binds.
        /// </summary>
        /// <summary>
        /// True when ANY index in [first..last] is not prepared (see <see cref="IsViewPrepared"/>).
        /// Single-lock range variant for per-frame checks on the plane blit path — do not call
        /// IsViewPrepared in a loop there (one lock acquisition per index).
        /// </summary>
        internal bool AnyUnpreparedInRange(int first, int last)
        {
            var contexts = _dataContexts;
            if (contexts == null)
                return false;

            // Pass 1 under lockVisible: in-use views. Pool peeks happen OUTSIDE it (PeekContext takes the
            // pool's own lock — never nest the two, same discipline as IsViewPrepared).
            List<object> poolChecks = null;
            lock (lockVisible)
            {
                for (int index = first; index <= last; index++)
                {
                    if (index < 0 || index >= contexts.Count)
                        continue;
                    var context = contexts[index];
                    if (context == null)
                        continue;

                    if (_cellsInUseViews.TryGetValue(index, out var inUse) && inUse != null)
                    {
                        if (inUse.NeedMeasure || inUse.IsPreparingOffthread
                            || !ReferenceEquals(inUse.BindingContext, context))
                            return true;
                        continue;
                    }

                    (poolChecks ??= new List<object>()).Add(context);
                }
            }

            if (poolChecks != null)
            {
                foreach (var context in poolChecks)
                {
                    var pooled = _templatedViewsPool?.PeekContext(context);
                    if (pooled == null || pooled.NeedMeasure || pooled.IsPreparingOffthread)
                        return true;
                }
            }

            return false;
        }

        public bool IsViewPrepared(int index)
        {
            var contexts = _dataContexts;
            if (contexts == null || index < 0 || index >= contexts.Count)
                return true; // out of range = nothing to prepare

            var context = contexts[index];
            if (context == null)
                return true;

            lock (lockVisible)
            {
                if (_cellsInUseViews.TryGetValue(index, out var inUse) && inUse != null)
                {
                    return !inUse.NeedMeasure && !inUse.IsPreparingOffthread
                           && ReferenceEquals(inUse.BindingContext, context);
                }
            }

            var pooled = _templatedViewsPool?.PeekContext(context);
            if (pooled != null)
                return !pooled.NeedMeasure && !pooled.IsPreparingOffthread;

            return false;
        }

        /// <summary>
        /// Rents/resolves the REAL view for off-thread preparation. Returns the live in-use instance when it
        /// is bound to the right context but still unmeasured (the render thread shows its placeholder
        /// meanwhile — guarded by IsPreparingOffthread), otherwise rents from the pool (exact context match,
        /// free spare, or a new instance) and binds it here on the worker thread — same pattern the
        /// background structure measurement has always used for pool-owned (never drawn) cells.
        /// Returns null when there is nothing to prepare. fromPool=true means the caller must return the
        /// view via <see cref="ReleaseMeasuringView"/> after measuring.
        /// </summary>
        public SkiaControl GetViewForPreparation(int index, out bool fromPool)
        {
            fromPool = false;

            if (IsDisposed || _templatedViewsPool == null)
                return null;

            var contexts = _dataContexts;
            if (contexts == null || index < 0 || index >= contexts.Count)
                return null;

            var context = contexts[index];
            if (context == null)
                return null;

            lock (lockVisible)
            {
                if (_cellsInUseViews.TryGetValue(index, out var inUse) && inUse != null)
                {
                    if (!inUse.IsDisposed && !inUse.IsDisposing
                        && ReferenceEquals(inUse.BindingContext, context)
                        && inUse.NeedMeasure && !inUse.IsPreparingOffthread)
                    {
                        return inUse; // measure the live instance in place
                    }

                    return null; // in use and already prepared / mid-prep / stale context (draw path owns rebinding)
                }
            }

            var view = _templatedViewsPool.GetForPreparation(context, _isTagEvictable ??= IsTagEvictable);
            if (view == null || view.IsDisposed || view.IsDisposing)
                return null;

            if (ReferenceEquals(view.BindingContext, context) && !view.NeedMeasure)
            {
                // already prepared — park it back
                _templatedViewsPool.Return(view, 0);
                return null;
            }

            fromPool = true;
            AttachView(view, index, true);
            return view;
        }

        private Func<object, bool> _isTagEvictable;

        /// <summary>
        /// Eviction judge for <see cref="TemplatedViewsPool.GetForPreparation"/>: a pool cell tagged by a
        /// context that is NO LONGER in the current data window (trimmed out by a windowed source) is dead
        /// weight and may be reclaimed for speculative preparation. A cell tagged by a LIVE context is
        /// protected — evicting it un-prepares that index and feeds the eviction-carousel repaint loop
        /// (see GetForPreparation doc). Reference scan: data items are matched by instance, never Equals.
        /// </summary>
        private bool IsTagEvictable(object taggedContext)
        {
            var contexts = _dataContexts;
            if (contexts == null)
                return true;

            for (int i = 0; i < contexts.Count; i++)
            {
                if (ReferenceEquals(contexts[i], taggedContext))
                    return false;
            }

            return true;
        }

        #endregion

        /// <summary>
        /// Updates binding context for a specific cached view
        /// </summary>
        /// <param name="index">Index of the view to update</param>
        /// <param name="newContext">New binding context</param>
        private void UpdateCachedViewContext(int index, object newContext)
        {
            lock (lockVisible)
            {
                if (_cellsInUseViews.TryGetValue(index, out SkiaControl view))
                {
                    if (!view.IsDisposed && !view.IsDisposing)
                    {
                        view.BindingContext = newContext;

                        if (LogEnabled)
                        {
                            Super.Log($"[ViewsAdapter] Updated context for view {view.Uid} at index {index}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Enhanced collection change handling with validation and better error handling
        /// </summary>
        public bool _HandleSmartCollectionChange(NotifyCollectionChangedEventArgs args, IList newDataContexts,
            int poolSize, int reserve = 0)
        {
            if (IsDisposed || !_parent.IsTemplated)
                return false;

            // Validate the new data contexts
            if (newDataContexts == null)
            {
                if (LogEnabled)
                    Super.Log("[ViewsAdapter] HandleSmartCollectionChange: newDataContexts is null");
                return false;
            }

            if (LogEnabled)
            {
                Super.Log(
                    $"[ViewsAdapter] HandleSmartCollectionChange: {args.Action}, old data count: {_dataContexts?.Count ?? -1}, new data count: {newDataContexts.Count}");
            }

            try
            {
                // Cleanup any invalid cached views before processing
                CleanupInvalidCachedViews();

                bool result = false;
                switch (args.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        result = HandleAdd(args, newDataContexts);
                        break;

                    case NotifyCollectionChangedAction.Remove:
                        result = HandleRemove(args, newDataContexts);
                        break;

                    case NotifyCollectionChangedAction.Replace:
                        result = HandleReplace(args, newDataContexts);
                        break;

                    case NotifyCollectionChangedAction.Move:
                        result = HandleMove(args, newDataContexts);
                        break;

                    case NotifyCollectionChangedAction.Reset:
                        // Always do full reset for this
                        if (LogEnabled)
                            Super.Log(
                                "[ViewsAdapter] HandleSmartCollectionChange: Reset action, falling back to full reset");
                        return false;

                    default:
                        if (LogEnabled)
                            Super.Log(
                                $"[ViewsAdapter] HandleSmartCollectionChange: Unknown action {args.Action}, falling back to full reset");
                        return false;
                }

                if (LogEnabled)
                {
                    Super.Log(
                        $"[ViewsAdapter] HandleSmartCollectionChange: {args.Action} result: {result}, final data count: {_dataContexts?.Count ?? -1}");
                }

                return result;
            }
            catch (Exception e)
            {
                Super.Log($"[ViewsAdapter] Smart collection change failed: {e}");

                // If smart handling fails, clean up and fall back to full reset
                lock (lockVisible)
                {
                    foreach (var view in _cellsInUseViews.Values)
                    {
                        view.Dispose();
                    }

                    _cellsInUseViews.Clear();
                }

                return false;
            }
            finally
            {
                // Always validate consistency after changes (in debug builds)
                if (LogEnabled && !ValidateCacheConsistency())
                {
                    Super.Log("[ViewsAdapter] Cache consistency validation failed after smart change");
                }
            }
        }

        private bool HandleAdd(NotifyCollectionChangedEventArgs args, IList newDataContexts)
        {
            if (args.NewItems == null || args.NewStartingIndex < 0)
                return false;

            var insertIndex = args.NewStartingIndex;
            var insertCount = args.NewItems.Count;

            // Collection changes are applied deferred: a Clear() followed by AddRange() on the same frame
            // lands here as a Reset that already rebuilt the adapter from the REFILLED source, then this
            // Add. The in-use views at the insert range are then already bound to exactly these new items:
            // shifting them would move them past the end and starve the pool (blank cells).
            // Shift only when the views sitting there hold something else (a real insert in front of them).
            var alreadyBound = false;
            lock (lockVisible)
            {
                var matched = 0;
                for (int i = 0; i < insertCount; i++)
                {
                    if (_cellsInUseViews.TryGetValue(insertIndex + i, out var view))
                    {
                        if (!ReferenceEquals(view.BindingContext, args.NewItems[i]))
                        {
                            matched = 0;
                            break;
                        }
                        matched++;
                    }
                }
                alreadyBound = matched > 0;
            }
            if (alreadyBound)
            {
                if (LogEnabled)
                    Super.Log($"[ViewsAdapter] Add of {insertCount} at {insertIndex}: views already bound to these items, no shift");
                return true;
            }

            // Shift existing cached views
            ShiftCachedViewIndexes(insertIndex, insertCount);

            if (LogEnabled)
            {
                Super.Log(
                    $"[ViewsAdapter] Added {insertCount} items at index {insertIndex}, new data count: {_dataContexts.Count}");
            }

            return true;
        }

        private bool HandleRemove(NotifyCollectionChangedEventArgs args, IList newDataContexts)
        {
            if (args.OldItems == null || args.OldStartingIndex < 0)
                return false;

            var removeIndex = args.OldStartingIndex;
            var removeCount = args.OldItems.Count;

            // Remove cached views that are being deleted
            lock (lockVisible)
            {
                for (int i = removeIndex; i < removeIndex + removeCount; i++)
                {
                    if (_cellsInUseViews.TryGetValue(i, out SkiaControl view))
                    {
                        _cellsInUseViews.Remove(i);
                        ReleaseViewToPool(view, true); // Reset the view
                    }
                }
            }

            ShiftCachedViewIndexes(removeIndex + removeCount, -removeCount);

            if (LogEnabled)
            {
                Super.Log($"[ViewsAdapter] Removed {removeCount} items from index {removeIndex}");
            }

            return true;
        }

        private bool HandleReplace(NotifyCollectionChangedEventArgs args, IList newDataContexts)
        {
            if (args.NewItems == null || args.OldItems == null ||
                args.NewStartingIndex < 0 || args.NewItems.Count != args.OldItems.Count)
                return false;

            var startIndex = args.NewStartingIndex;

            // Update data contexts first
            RefreshDataContexts(newDataContexts);

            // Update cached views with new contexts
            for (int i = 0; i < args.NewItems.Count; i++)
            {
                var index = startIndex + i;
                var newContext = args.NewItems[i];
                UpdateCachedViewContext(index, newContext);
            }

            if (LogEnabled)
            {
                Super.Log($"[ViewsAdapter] Replaced {args.NewItems.Count} items at index {startIndex}");
            }

            return true;
        }

        private bool HandleMove(NotifyCollectionChangedEventArgs args, IList newDataContexts)
        {
            if (args.NewItems == null || args.NewItems.Count != 1 ||
                args.OldStartingIndex < 0 || args.NewStartingIndex < 0)
                return false;

            var oldIndex = args.OldStartingIndex;
            var newIndex = args.NewStartingIndex;

            if (oldIndex == newIndex)
                return true; // No actual move

            lock (lockVisible)
            {
                // Get the view being moved
                _cellsInUseViews.TryGetValue(oldIndex, out SkiaControl movingView);

                // Update all affected indexes
                if (oldIndex < newIndex)
                {
                    // Moving forward: shift items between oldIndex+1 and newIndex backward
                    for (int i = oldIndex + 1; i <= newIndex; i++)
                    {
                        if (_cellsInUseViews.TryGetValue(i, out SkiaControl view))
                        {
                            _cellsInUseViews.Remove(i);
                            _cellsInUseViews[i - 1] = view;
                            view.ContextIndex = i - 1;
                        }
                    }
                }
                else
                {
                    // Moving backward: shift items between newIndex and oldIndex-1 forward
                    for (int i = oldIndex - 1; i >= newIndex; i--)
                    {
                        if (_cellsInUseViews.TryGetValue(i, out SkiaControl view))
                        {
                            _cellsInUseViews.Remove(i);
                            _cellsInUseViews[i + 1] = view;
                            view.ContextIndex = i + 1;
                        }
                    }
                }

                // Place the moved view in its new position
                if (movingView != null)
                {
                    _cellsInUseViews.Remove(oldIndex);
                    _cellsInUseViews[newIndex] = movingView;
                    movingView.ContextIndex = newIndex;
                }
            }

            if (LogEnabled)
            {
                Super.Log($"[ViewsAdapter] Moved item from index {oldIndex} to {newIndex}");
            }

            return true;
        }

        /// <summary>
        /// Validates that cached views are in sync with data contexts
        /// </summary>
        private bool ValidateCacheConsistency()
        {
            if (_dataContexts == null) return true;

            lock (lockVisible)
            {
                foreach (var kvp in _cellsInUseViews)
                {
                    var index = kvp.Key;
                    var view = kvp.Value;

                    // Check if index is valid
                    if (index < 0 || index >= _dataContexts.Count)
                    {
                        if (LogEnabled)
                        {
                            Super.Log(
                                $"[ViewsAdapter] Invalid index {index} in cache, data count: {_dataContexts.Count}");
                        }

                        return false;
                    }

                    // Check if binding context matches
                    var expectedContext = _dataContexts[index];
                    if (view.BindingContext != expectedContext)
                    {
                        if (LogEnabled)
                        {
                            Super.Log($"[ViewsAdapter] Context mismatch at index {index}");
                        }

                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Cleans up invalid cached views
        /// </summary>
        private void CleanupInvalidCachedViews()
        {
            if (_dataContexts == null) return;

            lock (lockVisible)
            {
                var toRemove = new List<int>();

                foreach (var kvp in _cellsInUseViews)
                {
                    var index = kvp.Key;
                    var view = kvp.Value;

                    if (index < 0 || index >= _dataContexts.Count ||
                        view.IsDisposed || view.IsDisposing)
                    {
                        toRemove.Add(index);
                    }
                }

                foreach (var index in toRemove)
                {
                    if (_cellsInUseViews.TryGetValue(index, out SkiaControl view))
                    {
                        _cellsInUseViews.Remove(index);
                        ReleaseViewToPool(view, true);
                    }
                }

                if (LogEnabled && toRemove.Count > 0)
                {
                    Super.Log($"[ViewsAdapter] Cleaned up {toRemove.Count} invalid cached views");
                }
            }
        }

        #endregion


        public void ReleaseViewInUse(SkiaControl cell)
        {
            if (cell != null)
            {
                ReleaseViewInUseForIndex(cell.ContextIndex, cell);
            }
        }

        public void ReleaseViewInUseForIndex(int index, SkiaControl view)
        {
            if (view == null)
                return;

            lock (lockVisible)
            {
                // This installs a freshly-measured transient cell at 'index'. Measuring (isMeasuring)
                // skips the in-use reuse branch in GetViewForIndex, so re-measuring an index that ALREADY
                // has a live realized cell pops a fresh pool cell. A blind indexer overwrite here would
                // drop the live cell out of the map without returning it to the pool -> orphan: the cell
                // is neither pooled nor in-use, so the pool total silently shrinks after every re-measure
                // (e.g. the window CUT). Net effect: pool starves and we re-create cells (lag) later.
                //
                // Resolve the conflict without losing a cell: if a DIFFERENT live cell already owns this
                // index, keep it (it carries the valid draw cache) and return the incoming transient to
                // the pool. Only overwrite when the slot is empty or holds a dead cell.
                if (_cellsInUseViews.TryGetValue(index, out var existing) && !ReferenceEquals(existing, view))
                {
                    if (existing != null && !existing.IsDisposed && !existing.IsDisposing)
                    {
                        ReleaseViewToPool(view);
                        return;
                    }
                }

                _cellsInUseViews[index] = view;
            }
        }

        /// <summary>
        /// Releases a view realized transiently for on-demand gesture hit-testing (GetViewForIndex). Removes
        /// it from the in-use map and returns it to the GENERIC pool (hKey 0) — symmetric with the height:0
        /// gets the tiled-planes renderer uses, so gesture hit-testing never strands cells in a height bucket
        /// and starves tile rendering.
        /// </summary>
        public void ReleaseMeasuringView(SkiaControl view)
        {
            if (view == null || _templatedViewsPool == null)
                return;

            lock (lockVisible)
            {
                var idx = view.ContextIndex;
                if (idx >= 0 && _cellsInUseViews.TryGetValue(idx, out var inUse) && ReferenceEquals(inUse, view))
                    _cellsInUseViews.Remove(idx);
            }

            _templatedViewsPool.Return(view, 0);
        }

        /// <summary>
        /// Returns the live (in-use, bound, measured) cell currently realized for this index, or null. Does NOT
        /// re-attach or measure — used by the tiled-planes renderer to keep resident-tile cells alive for gestures
        /// and to release them only when no tile references the index anymore.
        /// </summary>
        public SkiaControl GetCellInUseOrNull(int index)
        {
            lock (lockVisible)
            {
                _cellsInUseViews.TryGetValue(index, out var view);
                return view;
            }
        }

        /// <summary>Releases an in-use cell by index back to the GENERIC pool (hKey 0), if one is realized.</summary>
        public void ReleaseCellByIndex(int index)
        {
            SkiaControl view;
            lock (lockVisible)
            {
                if (!_cellsInUseViews.TryGetValue(index, out view))
                    return;
                _cellsInUseViews.Remove(index);
            }

            _templatedViewsPool?.Return(view, 0);
        }

        /// <summary>
        /// Creates view from template and returns already existing view for a specific index.
        /// This uses cached views and tends to return same views matching index they already used.
        /// When cell recycling is off this will be a perfect match at all times.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="template"></param>
        /// <param name="height"></param>
        /// <param name="isMeasuring"></param>
        /// <returns></returns>
        public SkiaControl GetViewForIndex(int index, SkiaControl template = null, float height = 0,
            bool isMeasuring = false)
        {
            if (IsDisposed)
                return null;

            try
            {
                if (index >= 0)
                {
                    //lock (lockVisible)
                    {
                        if (_parent.IsTemplated)
                        {
                            //lock (lockVisible)
                            {
                                if (template == null && !isMeasuring &&
                                    _cellsInUseViews.TryGetValue(index, out SkiaControl ready))
                                {
                                    if (LogEnabled)
                                    {
                                        Super.Log(
                                            $"[ViewsAdapter] {_parent.Tag} for index {index} returned a IN USE view {ready.Uid}  ({ready.ContextIndex})");
                                    }

                                    if (ready != null && !ready.IsDisposing)
                                    {
                                        AttachView(ready, index, isMeasuring);
                                        return ready;
                                    }

                                    //lol unexpected happened
                                    _cellsInUseViews.Remove(index);
                                    ReleaseViewToPool(ready);
                                }

                                var view = GetOrCreateViewForIndexInternal(index, height, template, isMeasuring);

                                if (view == null)
                                {
                                    // Pool refused (cap reached). The viewport-derived recycling ceiling is
                                    // computed from visible indexes that may have been unknown at the last
                                    // contexts swap (startup fallback) — recompute from LIVE state and retry
                                    // once, so real realized demand always fits and never draws a hole.
                                    var limit = _parent.GetTemplatesPoolLimitPublic();
                                    if (limit > _templatedViewsPool.MaxSize)
                                    {
                                        _templatedViewsPool.MaxSize = limit;
                                        view = GetOrCreateViewForIndexInternal(index, height, template, isMeasuring);
                                    }

                                    if (view == null)
                                        return null; //pool full at the honest ceiling
                                }

                                AttachView(view, index, isMeasuring);

                                //save visible view for future use only if template is not provided
                                if (template == null)
                                {
                                    // Track this realized cell. AttachView already PARENTED it. If the slot is
                                    // already owned (concurrent realize for the same index — GetViewForIndex is
                                    // not locked), the cell we just fetched is a loser DUPLICATE: it is parented
                                    // but would never enter the map and the draw path has no finally to release
                                    // it -> permanent orphan-with-Parent (pool total silently shrinks every CUT).
                                    // Recycle the loser (un-parent + pool) and hand back the established owner so
                                    // the index is drawn by a single, tracked instance.
                                    if (!_cellsInUseViews.TryAdd(index, view))
                                    {
                                        if (_cellsInUseViews.TryGetValue(index, out var owner)
                                            && owner != null && !ReferenceEquals(owner, view) && !owner.IsDisposing)
                                        {
                                            ReleaseViewToPool(view, true);
                                            AttachView(owner, index, isMeasuring);
                                            return owner;
                                        }
                                    }
                                }

                                return view;
                            }
                        }
                        else
                        {
                            var children = _parent.GetUnorderedSubviews();
                            if (index < children.Count())
                                return children.ElementAt(index);
                        }

                        return null;
                    }
                }
            }
            catch (Exception e)
            {
                Super.Log(e);
            }

            return null;
        }

        public SkiaControl GetOrCreateViewForIndexInternal(int index, float height = 0, SkiaControl template = null,
            bool isMeasuring = false)
        {
            // Capture the snapshot reference once: it can be swapped on the UI thread at any moment.
            // Reading the local guarantees a consistent bounds-check + index against the same array.
            var contexts = _dataContexts;

            if (_templatedViewsPool == null || contexts == null)
            {
                throw new InvalidOperationException("Templates have not been initialized.");
            }

            if (index < 0 || index >= contexts.Count)
            {
                return null;
            }

            if (template == null)
            {
                // Get the binding context for this index
                var bindingContext = contexts[index];
                template = _templatedViewsPool.Get(height, bindingContext);

                if (template != null)
                {
                    var contextMatch = template.BindingContext != null && template.BindingContext.Equals(bindingContext);

                    if (LogEnabled)
                    Super.Log(
                        $"[ViewsAdapter] {_parent.Tag} for index {index} returned from POOL {template.Uid} with height={height} (bindingContext match: {contextMatch})");

                    // Wrong-context cell handed back. In the DRAW path this is a real bug — that cell would
                    // render with the wrong data unless re-bound. In the MEASURE path it is inherent and
                    // harmless: measure deliberately bypasses the in-use cell (GetViewForIndex gates the
                    // _cellsInUseViews reuse on !isMeasuring) and borrows a SCRATCH cell on the background
                    // thread; the measured SIZE lands in _measuredItems regardless of which scratch cell
                    // computed it, and the index is skipped on the next sweep. So only flag it for draw.
                    if (!isMeasuring && template.BindingContext != null && template.BindingContext != bindingContext)
                    {
                        var stop = 1;                         //312 311
                    }
                }
            }
            else
            {
                if (LogEnabled)
                {
                    Super.Log($"[ViewsAdapter] {_parent.Tag} for index {index} used passed tpl {template.Uid}");
                }
            }

            return template;
        }

        /// <summary>
        /// Retuns view to the POOL and set parent to null. Doesn't set BindingContext to null !
        /// </summary>
        /// <param name="view"></param>
        /// <param name="reset"></param>
        public void ReleaseViewToPool(SkiaControl view, bool reset = false)
        {
            if (view == null)
                return;

            //lock (_lockTemplates)
            {
                if (reset)
                {
                    view.SetParent(null);
                    //viewModel.BindingContext = null;
                }

                _templatedViewsPool.Return(view, GetSizeKey(view));
            }
        }


        bool UsesGenericPool
        {
            get
            {
                // Prepared-views pipeline REQUIRES the context-indexed generic reservoir: a cell that was
                // bound+measured off-thread for a context must be reclaimable by that exact context at draw,
                // which height buckets cannot do. This also turns RecyclingTemplate.Enabled into
                // "recycling with context affinity": a small pool cycles cells, but a cell revisited while
                // still tagged skips the rebind+remeasure entirely.
                if (_parent != null && _parent.UsePreparedViews)
                    return true;

                if (_parent != null && _parent.RecyclingTemplate != RecyclingTemplate.Disabled)
                {
                    if (
                        _parent.MeasureItemsStrategy != MeasuringStrategy.MeasureFirst &&
                        (_parent.Type == LayoutType.Column || _parent.Type == LayoutType.Row))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        int GetSizeKey(SkiaControl view)
        {
            int hKey = 0;
            if (_parent.RecyclingTemplate != RecyclingTemplate.Disabled && !UsesGenericPool)
            {
                if (_parent.Type == LayoutType.Column)
                {
                    hKey = (int)Math.Round(view.MeasuredSize.Pixels.Height);
                }
                else if (_parent.Type == LayoutType.Row)
                {
                    hKey = (int)Math.Round(view.MeasuredSize.Pixels.Width);
                }
            }

            return hKey;
        }


        public bool IsDisposed;
        private readonly SkiaLayout _parent;

        public ViewsAdapter(SkiaLayout parent)
        {
            _parent = parent;
        }

        public void Dispose()
        {
            CancelBackgroundPoolFilling();

            lock (_fillLock)
            {
                _backgroundFillCancellation?.Dispose();
                _backgroundFillCancellation = null;
            }

            DisposeViews();

            IsDisposed = true;
        }

        object lockVisible = new();

        protected void UpdateVisibleViews()
        {
            lock (lockVisible)
            {
                if (IsDisposed)
                    return;

                foreach (var view in _cellsInUseViews.Values.ToList())
                {
                    view.InvalidateInternal();
                }
            }
        }

        protected void DisposeVisibleViews()
        {
            lock (lockVisible)
            {
                if (IsDisposed)
                    return;

                foreach (var view in _cellsInUseViews.Values)
                {
                    view.Dispose();
                }

                _cellsInUseViews.Clear();
            }
        }

        protected void DisposeViews()
        {
            if (IsDisposed)
                return;

            _templatedViewsPool?.Dispose();
            DisposeVisibleViews();
        }

        protected virtual void AttachView(SkiaControl view, int index, bool isMeasuring)
        {
            if (IsDisposed || view == null)
                return;

            try
            {
                if (view.IsDisposed || view.IsDisposing)
                {
                    if (LogEnabled)
                        Super.Log($"[ViewsAdapter] Skipping disposed view {view.Uid} for index {index}");
                    return;
                }

                view.IsParentIndependent = true;

                try
                {
                    // Capture the snapshot reference once (it can be swapped on the UI thread mid-attach).
                    var contexts = _dataContexts;
                    if (index < contexts?.Count)
                    {
                        // Double-check before setting binding context
                        if (!view.IsDisposed && !view.IsDisposing)
                        {
                            var context = contexts[index];

                            if (!isMeasuring)
                            {
                                view.Parent = _parent;
                            }

                            if (view.ContextIndex != index || view.BindingContext != context) //index == 0 ||
                            {
                                view.ContextIndex = index;
                                var ctx = view.BindingContext;
                                view.BindingContext = context; // ← where crashes could happen
                                if (ctx != context)
                                {
                                    if (_parent.MeasureItemsStrategy != MeasuringStrategy.MeasureFirst)
                                    {
                                        view.NeedMeasure = true;
                                    }
                                    // caches still hold the previous context's pixels — stale-serve must
                                    // not blit them at the new slot (cleared on next fresh RenderObject)
                                    if (ctx != null)
                                    {
                                        view.PixelsForeign = true;
                                    }
                                }

                                if (!isMeasuring)
                                {
                                    _parent.OnViewAttached();
                                }
                            }
                        }
                    }
                }
                catch (ObjectDisposedException ex)
                {
                    // View disposed between checks
                    if (LogEnabled)
                        Trace.WriteLine(
                            $"[ViewsAdapter] View {view.Uid} disposed during binding context set: {ex.Message}");
                }
            }
            catch (ObjectDisposedException ex)
            {
                if (LogEnabled)
                    Super.Log($"[ViewsAdapter] View disposed during AttachView: {ex.Message}");
            }
            catch (Exception e)
            {
                Super.Log($"[ViewsAdapter] AttachView failed: {e}");
            }
        }

        #region POOL

        /// <summary>
        /// Sets the recycling pool ceiling to exactly <paramref name="size"/> (may raise OR lower it, so the
        /// tiled-planes auto-sizer can replace the default item-count cap with a geometry-based one). Lowering
        /// just lets surplus returns dispose down over time. Used together with FillPool to pre-warm.
        /// </summary>
        public void SetPoolMaxSize(int size)
        {
            if (_templatedViewsPool == null || IsDisposed || size < 1)
                return;

            _templatedViewsPool.MaxSize = size;
        }

        public int PoolMaxSize
        {
            get
            {
                if (_templatedViewsPool == null)
                {
                    return int.MinValue;
                }

                return _templatedViewsPool.MaxSize;
            }
        }

        public int PoolSize
        {
            get
            {
                if (_templatedViewsPool == null)
                {
                    return int.MinValue;
                }

                return _templatedViewsPool.Size;
            }
        }

        /// <summary>
        /// Holds visible prepared views with appropriate context, index is inside ItemsSource
        /// </summary>
        private readonly Dictionary<int, SkiaControl> _cellsInUseViews = new(256);


        public void MarkAllViewsAsHidden()
        {
            lock (lockVisible)
            {
                // Add all visible views back to the recycling pool (e.g., _viewModelPool.Return(hiddenView))
                foreach (var hiddenView in _cellsInUseViews.Values)
                    ReleaseViewToPool(hiddenView);

                _cellsInUseViews.Clear();
            }
        }

        public void MarkViewAsHidden(int index)
        {
            lock (lockVisible)
            {
                if (IsDisposed)
                    return;

                if (_parent.IsTemplated && _parent.RecyclingTemplate == RecyclingTemplate.Enabled)
                {
                    if (_cellsInUseViews.ContainsKey(index))
                    {
                        if (_cellsInUseViews.TryGetValue(index, out SkiaControl hiddenView))
                        {
                            //if (hiddenView is ISkiaCell notify)
                            //{
                            //    notify.OnDisappeared();
                            //}

                            _cellsInUseViews.Remove(index);
                            ReleaseViewToPool(hiddenView);
                        }

                        //Debug.WriteLine($"[InUse] {_dicoCellsInUse.Keys.Select(k => k.ToString()).Aggregate((current, next) => $"{current},{next}")}");
                    }
                }
            }
        }

        public int AddedMore { get; protected set; }

        /// <summary>
        /// Keep pool size with `n` templated more oversized, so when we suddenly need more templates they would already be ready, avoiding lag spike,
        /// This method is likely to reserve templated views once on layout size changed.
        /// </summary>
        /// <param name="oversize"></param>
        public void AddMoreToPool(int oversize)
        {
            if (IsDisposed)
                return;

            if (_templatedViewsPool != null && AddedMore < oversize)
            {
                var add = oversize - AddedMore;
                AddedMore = oversize;
                if (add > 0)
                {
                    for (int i = 0; i < add; i++)
                    {
                        _templatedViewsPool?.Reserve();
                    }
                }
            }
        }

        /// <summary>
        /// Use to manually pre-create views from item templates so when we suddenly need more templates they would already be ready, avoiding lag spike,
        /// This will respect pool MaxSize in order not to overpass it.
        /// </summary>
        /// <param name="size"></param>
        public void FillPool(int size, IList context)
        {
            if (IsDisposed)
                return;

            FillPool(size);

            if (context == null)
            {
                return;
            }

            //todo?..
        }

        /// <summary>
        /// Use to manually pre-create views from item templates so when we suddenly need more templates they would already be ready, avoiding lag spike,
        /// This will respect pool MaxSize in order not to overpass it.
        /// </summary>
        /// <param name="size"></param>
        public void FillPool(int size)
        {
            if (IsDisposed || !_parent.IsTemplated)
                return;

            if (size > 0)
            {
                while (_templatedViewsPool.Size < size && _templatedViewsPool.CreatedCount < _templatedViewsPool.MaxSize)
                {
                    _templatedViewsPool.Reserve();
                }
            }
        }


        /// <summary>
        /// Invalidates all existing cells currently stored in the recycling pools (generic, sized, and standalone).
        /// Use this when container constraints change so pooled cells are forced to re-measure next time they are reused.
        /// </summary>
        public void InvalidateAllPooledCells()
        {
            if (IsDisposed)
                return;

            lock (_lockTemplates)
            {
                foreach (var kvp in _cellsInUseViews.ToArray())
                {
                    _cellsInUseViews.Remove(kvp.Key);
                    ReleaseViewToPool(kvp.Value);
                }

                _templatedViewsPool?.InvalidateAll();
            }
        }

        #endregion

        public int TotalCellsCount
        {
            get
            {
                return PoolSize + _cellsInUseViews.Count;
            }
        }

        public string GetDebugInfo()
        {
            if (_dataContexts == null)
            {
                return "ViewsAdapter empty";
            }

            return
                $"Data:  {_dataContexts.Count}, cells {_cellsInUseViews.Count}/{TotalCellsCount}";
        }

        public int GetChildrenCount()
        {
            if (IsDisposed)
                return 0;


            if (!_parent.IsTemplated)
            {
                var children = _parent.GetUnorderedSubviews();

                return children.Count();
            }

            // Use the immutable snapshot count, not the live ItemsSource.Count: the layout pass indexes
            // _dataContexts, so its length must match what the render thread can safely read (issue #300).
            var contexts = _dataContexts;
            if (contexts != null)
            {
                return contexts.Count;
            }

            return 0;
        }

        private TemplatedViewsPool _templatedViewsPool;

        // Immutable snapshot of the bound collection that the render thread indexes lock-free.
        // We never store the live ItemsSource here: the GL render thread reads _dataContexts[index]
        // concurrently while the UI thread mutates the bound collection (Add/RemoveAt/Insert). Indexing
        // a live List/ObservableCollection across threads tears the backing array between the bounds
        // check and the indexer -> SIGSEGV in mono on the GLThread (issue #300). The snapshot is a fixed
        // object[] rebuilt on the mutating thread and swapped in atomically (reference assignment), so a
        // render-thread read always sees one whole consistent array, never a half-resized one.
        private volatile IList _dataContexts;

        // The live collection reference, kept ONLY for identity comparison (detecting a new ItemsSource
        // instance). Never indexed from the render thread.
        private IList _dataContextsSource;

        protected readonly object _lockTemplates = new object();

        private readonly Dictionary<int, ViewsIterator> _wrappers =
            new();

        public bool TemplesInvalidating;

        private int times = 0;

        public bool TemplatesInvalidated
        {
            get => _templatesInvalidated;
            set
            {
                lock (_lockTemplates)
                {
                    _templatesInvalidated = value;
                }
            }
        }


        public bool TemplatesBusy;
        private bool _templatesInvalidated;
        private float _forScale;
        private int _forSplit;


        public void UpdateViews(IEnumerable<SkiaControl> views = null)
        {
            if (_parent.IsTemplated)
            {
                UpdateVisibleViews();
            }
        }


        /// <summary>
        /// An important check to consider before consuming templates especially if you initialize templates in background
        /// </summary>
        public bool TemplatesAvailable
        {
            get
            {
                return (_templatedViewsPool != null
                        && _dataContexts != null)
                       && !TemplatesInvalidated;
            }
        }

        public ViewsIterator GetViewsIterator()
        {
            lock (_lockTemplates)
            {
                if (_parent.IsTemplated)
                {
                    if (!TemplatesAvailable)
                    {
                        throw new InvalidOperationException("Templates have not been initialized.");
                    }

                    int threadId = Thread.CurrentThread.ManagedThreadId;

                    if (!_wrappers.TryGetValue(threadId, out ViewsIterator wrapper))
                    {
                        if (_parent.RecyclingTemplate != RecyclingTemplate.Disabled)
                            wrapper = new ViewsIterator(_templatedViewsPool, _dataContexts, _parent.Type);
                        else
                            wrapper = new ViewsIterator(_templatedViewsPool, _dataContexts, null);

                        _wrappers.TryAdd(threadId, wrapper);
                    }

                    return wrapper;
                }
                else
                {
                    int threadId = Thread.CurrentThread.ManagedThreadId;

                    var children = _parent.GetUnorderedSubviews();

                    if (!_wrappers.TryGetValue(threadId, out ViewsIterator iterator))
                    {
                        iterator = new ViewsIterator(children);

                        _wrappers.TryAdd(threadId, iterator);
                    }
                    else
                    {
                        iterator.SetViews(children);
                    }

                    return iterator;
                }
            }
        }

        public void DisposeWrapper()
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;

            if (_wrappers.TryGetValue(threadId, out ViewsIterator wrapper))
            {
                _wrappers.Remove(threadId);
                wrapper.Dispose();
            }
        }


        public void PrintDebugVisible()
        {
            lock (lockVisible)
            {
                Trace.WriteLine($"Visible views {_cellsInUseViews.Count}:");
                foreach (var view in _cellsInUseViews.Values)
                {
                    Trace.WriteLine(
                        $"└─ {view} {view.Width:0.0}x{view.Height:0.0} pts ({view.MeasuredSize.Pixels.Width:0.0}x{view.MeasuredSize.Pixels.Height:0.0} px) ctx: {view.BindingContext}");
                }
            }
        }


        public SkiaControl GetTemplateInstance()
        {
            lock (_lockTemplates)
            {
                if (_templatedViewsPool == null || _dataContexts == null)
                {
                    throw new InvalidOperationException("Templates have not been initialized.");
                }

                SkiaControl view = _templatedViewsPool.GetStandalone();
                view.IsParentIndependent = true;
                view.NeedMeasure = true;
                return view;
            }
        }

        /// <summary>
        /// Returns standalone view, used for measuring to its own separate pool.
        /// </summary>
        /// <param name="viewModel"></param>
        /// <param name="reset"></param>
        public void ReleaseTemplateInstance(SkiaControl viewModel, bool reset = false)
        {
            if (viewModel == null)
                return;

            //lock (_lockTemplates)
            {
                if (reset)
                {
                    viewModel.SetParent(null);
                    //viewModel.BindingContext = null;
                }

                _templatedViewsPool.ReturnStandalone(viewModel);
            }
        }
    }
}
