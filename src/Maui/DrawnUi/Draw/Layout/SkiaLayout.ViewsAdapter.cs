using System.Collections;
using static SkiaSharp.HarfBuzz.SKShaper;

namespace DrawnUi.Draw;

public record CellWIthHeight(float Height, SkiaControl view);

/// <summary>
/// Top level class for working with ItemTemplates. Holds visible views.
/// </summary>
public class ViewsAdapter : IDisposable
{
    public static bool LogEnabled = false;

    #region INITIALIZE

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

        var changedData = _dataContexts != dataContexts;

        var needReset = args.Action == NotifyCollectionChangedAction.Reset
                        || (layoutChanged || _templatedViewsPool == null || _dataContexts != dataContexts ||
                            CheckTemplateChanged());

        if (needReset)
        {
            //temporarily fixed to android until issue found
            lock (_lockTemplates)
            {
                //kill provider ability to provide deprecated templates
                _wrappers.Clear();
                _forScale = _parent.RenderingScale;
                _forSplit = _parent.Split;
                _dataContexts = null;
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

            //looks like only itemssource has changed, resize pool, keep old templates, be fast
            InitializeSoft(layoutChanged, dataContexts, poolSize);
        }
    }

    void InitializeSoft(bool layoutChanged, IList dataContexts, int poolSize)
    {
        if (LogEnabled)
            Super.Log("[ViewsAdapter] InitializeSoft");

        lock (_lockTemplates)
        {
            TemplesInvalidating = false;

            _templatedViewsPool.MaxSize = poolSize;
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

        SetTemplatesAvailable(dataContexts);
    }

    void SetTemplatesAvailable(IList dataContexts)
    {
        _dataContexts = dataContexts;
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
            var kill = _templatedViewsPool;

            lock (lockVisible)
            {
                _cellsInUseViews.Clear();
            }

            _templatedViewsPool = new TemplatedViewsPool(template, poolSize, (k) => { _parent?.DisposeObject(k); });

            // Clear standalone pool when template changes
            kill?.ClearStandalonePool();

            if (UsesGenericPool)
            {
                FillPool(reserve, dataContexts);
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

            _dataContexts = dataContexts;
            TemplatesInvalidated = false; //enable TemplatesAvailable otherwise beackground measure will fail
            _parent.MeasureLayout(
                new(_parent._lastMeasuredForWidth, _parent._lastMeasuredForHeight, _parent.RenderingScale), true);
        }

        SetTemplatesAvailable(dataContexts);
    }

    #endregion

    #region SHIFT

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

            // Update the dictionary with new indexes
            foreach (var (oldIndex, newIndex, view) in itemsToUpdate)
            {
                _cellsInUseViews.Remove(oldIndex);
                _cellsInUseViews[newIndex] = view;

                // Update the view's context index
                view.ContextIndex = newIndex;

                if (LogEnabled)
                {
                    Super.Log($"[ViewsAdapter] Shifted view {view.Uid} from index {oldIndex} to {newIndex}");
                }
            }
        }
    }

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
        _dataContexts = newDataContexts; //todo check if we need this here???

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

    public void ReleaseViewInUse(int index, SkiaControl view)
    {
        if (view == null)
            return;

        lock (lockVisible)
        {
            _cellsInUseViews[index] = view;
        }
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
                        lock (lockVisible)
                        {
                            if (template == null && !isMeasuring &&
                                _cellsInUseViews.TryGetValue(index, out SkiaControl ready))
                            {
                                if (LogEnabled)
                                {
                                    Super.Log(
                                        $"[ViewsAdapter] {_parent.Tag} for index {index} returned a INUSE view {ready.Uid}  ({ready.ContextIndex})");
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

                            var view = GetOrCreateViewForIndexInternal(index, height, template);

                            if (view == null)
                            {
                                return null; //maybe pool is full, anyway unexpected
                            }

                            AttachView(view, index, isMeasuring);

                            //save visible view for future use only if template is not provided
                            if (template == null)
                            {
                                if (!_cellsInUseViews.Values.Contains(view))
                                {
                                    _cellsInUseViews.TryAdd(index, view);
                                }

                                //if (view is ISkiaCell notify) //todo move to draw and couple lofic with cell
                                //{
                                //    notify.OnAppearing();
                                //}
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

    public SkiaControl GetOrCreateViewForIndexInternal(int index, float height = 0, SkiaControl template = null)
    {
        if (_templatedViewsPool == null || _dataContexts == null)
        {
            throw new InvalidOperationException("Templates have not been initialized.");
        }

        if (index < 0 || index >= _dataContexts.Count)
        {
            return null;
        }

        if (template == null)
        {
            template = _templatedViewsPool.Get(height);

            if (LogEnabled && template != null)
            {
                Super.Log(
                    $"[ViewsAdapter] {_parent.Tag} for index {index} returned from POOL {template.Uid} with height={height}");
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
        if (_parent.RecyclingTemplate != RecyclingTemplate.Disabled)
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
                if (index < _dataContexts?.Count)
                {
                    // Double-check before setting binding context
                    if (!view.IsDisposed && !view.IsDisposing)
                    {

                        var context = _dataContexts[index];

                        if (index == 0 || view.ContextIndex != index || view.BindingContext != context)
                        {
                            if (!isMeasuring)
                            {
                                view.Parent = _parent;
                            }

                            view.ContextIndex = index;
                            var ctx = view.BindingContext;
                            view.BindingContext = context; // ← where crashes could happen
                            if (ctx != context)
                            {
                                view.NeedMeasure = true;
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
        if (IsDisposed)
            return;

        if (size > 0)
        {
            while (_templatedViewsPool.Size < size && _templatedViewsPool.Size < _templatedViewsPool.MaxSize)
            {
                _templatedViewsPool.Reserve();
            }
        }
    }

    #endregion

    public string GetDebugInfo()
    {
        if (_dataContexts == null)
        {
            return "ViewsAdapter empty";
        }

        return
            $"ItemsSource size {_dataContexts.Count}, using cells {_cellsInUseViews.Count}/{PoolSize + _cellsInUseViews.Count}";
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

        if (_parent.ItemsSource != null)
        {
            return _parent.ItemsSource.Count;
        }

        return 0;
    }

    private TemplatedViewsPool _templatedViewsPool;
    private IList _dataContexts;

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

/// <summary>
/// To iterate over virtual views
/// </summary>
public class ViewsIterator : IEnumerable<SkiaControl>, IDisposable
{
    private TemplatedViewsPool _templatedViewsPool;
    private IList _dataContexts;
    private IEnumerable<SkiaControl> _views;
    private DataContextIterator _iterator;
    private readonly LayoutType? _layoutType;

    public bool IsTemplated { get; }

    public TemplatedViewsPool TemplatedViewsPool => _templatedViewsPool;
    public IList DataContexts => _dataContexts;
    public IEnumerable<SkiaControl> Views => _views;

    public ViewsIterator(TemplatedViewsPool templatedViewsPool, IList dataContexts, LayoutType? layoutType)
    {
        _templatedViewsPool = templatedViewsPool;
        _dataContexts = dataContexts;
        _layoutType = layoutType;
        IsTemplated = true;
    }

    public void SetViews(IEnumerable<SkiaControl> views)
    {
        _views = views;
    }

    public ViewsIterator(IEnumerable<SkiaControl> views)
    {
        _views = views;
        IsTemplated = false;
    }

    public IEnumerator<SkiaControl> GetEnumerator()
    {
        _iterator = new DataContextIterator(this, _layoutType);
        return _iterator;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Dispose()
    {
        if (_iterator != null)
        {
            _iterator.Dispose();
            _iterator = null;
        }
    }
}

public class DataContextIterator : IEnumerator<SkiaControl>
{
    private readonly ViewsIterator _viewsProvider;
    private int _currentIndex;
    private SkiaControl _view;
    private IEnumerator<SkiaControl> _viewEnumerator;
    private readonly LayoutType? _layoutType;

    //added this to use more that 1 view at a time
    private readonly Queue<SkiaControl> _viewsInUse;

    int GetSizeKey(SkiaControl view)
    {
        if (_layoutType.HasValue)
        {
            int hKey = 0;
            if (_layoutType == LayoutType.Column)
            {
                hKey = (int)Math.Round(view.MeasuredSize.Pixels.Height);
            }
            else if (_layoutType == LayoutType.Row)
            {
                hKey = (int)Math.Round(view.MeasuredSize.Pixels.Width);
            }

            return hKey;
        }

        return 0;
    }

    public DataContextIterator(ViewsIterator viewsProvider, LayoutType? layoutType)
    {
        _layoutType = layoutType;
        _viewsProvider = viewsProvider;
        _currentIndex = -1;
        _viewsInUse = new Queue<SkiaControl>();

        if (!_viewsProvider.IsTemplated)
        {
            _viewEnumerator = _viewsProvider.Views.GetEnumerator();
        }

        _layoutType = layoutType;
    }

    public SkiaControl Current => _view;

    object IEnumerator.Current => Current;

    public bool MoveNext()
    {
        if (_viewsProvider.IsTemplated)
        {
            // Dequeue and return the oldest view if we're at capacity.
            if (_viewsInUse.Count >= _viewsProvider.TemplatedViewsPool.MaxSize)
            {
                var oldestView = _viewsInUse.Dequeue();

                _viewsProvider.TemplatedViewsPool.Return(oldestView, GetSizeKey(oldestView));
            }

            _currentIndex++;

            if (_currentIndex < _viewsProvider.DataContexts.Count)
            {
                _view = _viewsProvider.TemplatedViewsPool.Get();

                if (_view == null)
                    return false;

                _viewsInUse.Enqueue(_view); // Keep track of the views in use.

                _view.ContextIndex = _currentIndex;
                _view.BindingContext = _viewsProvider.DataContexts[_currentIndex];
                return true;
            }

            return false;
        }

        bool hasMore = _viewEnumerator.MoveNext();
        if (hasMore)
        {
            _view = _viewEnumerator.Current;
        }

        return hasMore;
    }

    public void Reset()
    {
        if (_viewsProvider.IsTemplated)
        {
            if (_currentIndex >= 0 && _currentIndex < _viewsProvider.DataContexts.Count)
            {
                _viewsProvider.TemplatedViewsPool.Return(_view, GetSizeKey(_view));
            }

            _currentIndex = -1;
        }
        else
        {
            _viewEnumerator.Reset();
        }
    }

    public void Dispose()
    {
        if (_viewsProvider.IsTemplated && _currentIndex >= 0 && _currentIndex < _viewsProvider.DataContexts.Count)
        {
            _viewsProvider.TemplatedViewsPool.Return(_view, GetSizeKey(_view));
        }
    }
}

public class TemplatedViewsPool : IDisposable
{
    public Func<object> CreateTemplate { get; protected set; }
    public int MaxSize { get; set; }
    public bool IsDisposing;
    private bool _disposed = false;
    private readonly Action<IDisposable> _dispose;

    // New: track height-based pools
    // Key: Rounded integer height, Value: Stack of controls for that height
    private Dictionary<int, Stack<SkiaControl>> _heightPools = new();
    private int _maxDistinctHeights = 10; // or configurable
    private readonly Stack<SkiaControl> _genericPool; // fallback pool for cells without specific height
    private Stack<SkiaControl> _standalonePool = new();
    private readonly object _syncLock = new object();

    public TemplatedViewsPool(Func<object> initialViewModel, int maxSize, Action<IDisposable> dispose)
    {
        CreateTemplate = initialViewModel;
        MaxSize = maxSize;
        _dispose = dispose;
        _genericPool = new Stack<SkiaControl>();
    }

    public int Size
    {
        get
        {
            lock (_syncLock)
            {
                // Total size = sum of all height pools + generic
                int total = _genericPool.Count;
                foreach (var kvp in _heightPools)
                    total += kvp.Value.Count;
                return total;
            }
        }
    }

    public int MaxDistinctHeights
    {
        get => _maxDistinctHeights;
        set
        {
            lock (_syncLock)
            {
                _maxDistinctHeights = value;
            }
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        lock (_syncLock)
        {
            IsDisposing = true;
            if (!_disposed && disposing)
            {
                DisposeStack(_genericPool);
                foreach (var kvp in _heightPools)
                {
                    DisposeStack(kvp.Value);
                }

                _disposed = true;
            }
        }
    }

    private void DisposeStack(Stack<SkiaControl> stack)
    {
        while (stack.Count > 0)
        {
            var c = stack.Pop();
            if (c != null)
            {
                c.ContextIndex = -1;
                c.Dispose();
            }
        }
    }

    public void Dispose()
    {
        IsDisposing = true;
        Dispose(true);
    }

    SkiaControl CreateFromTemplate()
    {
        if (IsDisposing)
            return null;

        var create = CreateTemplate();
        if (ViewsAdapter.LogEnabled)
            Super.Log("[ViewsAdapter] created new view !");

        if (create is SkiaControl element)
        {
            return element;
        }

        var ctrl = (SkiaControl)create;
        ctrl.ContextIndex = -1;
        return ctrl;
    }

    public void Reserve()
    {
        lock (_syncLock)
        {
            if (IsDisposing)
                return;

            if (_genericPool.Count < MaxSize)
            {
                try
                {
                    _genericPool.Push(CreateFromTemplate());
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e);
                    throw;
                }
            }
        }
    }

    public SkiaControl GetStandalone()
    {
        lock (_syncLock)
        {
            if (IsDisposing)
                return null;

            if (_standalonePool.Count > 0)
            {
                return _standalonePool.Pop();
            }

            var ret = CreateFromTemplate();
            ret.IsParentIndependent = true;
            return ret;
        }
    }

    public void ReturnStandalone(SkiaControl viewModel)
    {
        lock (_syncLock)
        {
            if (IsDisposing)
                return;

            _standalonePool.Push(viewModel);
        }
    }

    public void ClearStandalonePool()
    {
        lock (_syncLock)
        {
            while (_standalonePool.Count > 0)
            {
                var ctrl = _standalonePool.Pop();
                if (ctrl != null)
                {
                    ctrl.ContextIndex = -1;
                    ctrl.Dispose();
                }
            }
        }
    }

    public SkiaControl Get(float height = 0)
    {
        lock (_syncLock)
        {
            if (IsDisposing)
                return null;

            if (height == 0)
            {
                if (_genericPool.Count > 0)
                {
                    var generic = _genericPool.Pop();
                    if (!generic.IsDisposed)
                        return generic;
                }

                if (Size < MaxSize)
                    return CreateFromTemplate();

                return null;
            }

            int hKey = (int)Math.Round(height);
            if (!_heightPools.TryGetValue(hKey, out var stack))
            {
                if (_heightPools.Count < MaxDistinctHeights)
                {
                    stack = new();
                    _heightPools[hKey] = stack;
                }
            }

            SkiaControl view = null;

            if (stack != null && stack.Count > 0)
            {
                view = stack.Pop();
            }
            else
            {
                //maybe generic available?
                if (_genericPool.Count > 0)
                    view = _genericPool.Pop();

                //create new for size
                if (view == null && Size < MaxSize)
                {
                    view = CreateFromTemplate();
                }
            }

            return view;
        }
    }

    public void Return(SkiaControl viewModel, int hKey)
    {
        if (viewModel == null)
            return;

        lock (_syncLock)
        {
            if (IsDisposing)
                return;

            if (Size < MaxSize)
            {
                if (hKey != 0)
                {
                    if (!_heightPools.TryGetValue(hKey, out var stack))
                    {
                        if (_heightPools.Count < MaxDistinctHeights)
                        {
                            stack = new Stack<SkiaControl>();
                            _heightPools[hKey] = stack;
                        }
                    }

                    if (stack != null)
                    {
                        stack.Push(viewModel);
                        return;
                    }
                }

                _genericPool.Push(viewModel);
            }
            else
            {
                _dispose?.Invoke(viewModel);
            }
        }
    }
}
