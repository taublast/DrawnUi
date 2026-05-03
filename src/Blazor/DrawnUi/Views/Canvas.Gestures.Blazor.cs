using AppoMobi.Gestures;

namespace DrawnUi.Views;

public partial class Canvas : IGestureListener
{
    public bool InputTransparent => false;

    private readonly object _gestureLock = new();
    private readonly Dictionary<long, TouchActionEventArgs> _previousTouchArgs = new();
    private readonly Dictionary<long, TouchActionEventArgs> _pointerDownArgs = new();
    private readonly HashSet<long> _activeTouchIds = new();

    public HashSet<ISkiaGestureListener> ReceivedInput { get; } = new();

    public Dictionary<Guid, ISkiaGestureListener> HadInput { get; } = new();

    public event EventHandler? Tapped;

    protected bool IsSavedGesture(TouchActionResult type)
    {
        return type == TouchActionResult.Panning || type == TouchActionResult.Wheel || type == TouchActionResult.Up;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddHadInput(ISkiaGestureListener consumed)
    {
        HadInput.TryAdd(consumed.Uid, consumed);
    }

    protected virtual void ProcessGestures(SkiaGesturesParameters args)
    {
        lock (LockIterateListeners)
        {
            if (args.Type == TouchActionResult.Down && HadInput.Count > 0)
            {
                HadInput.Clear();
            }

            var checkHover = args.Type == TouchActionResult.Pointer;
            var hadHover = false;
            ISkiaGestureListener consumed = null;
            ISkiaGestureListener alreadyConsumed = null;

            IsHiddenInViewTree = false;
            var manageChildFocus = false;
            var touchLocation = new SKPoint(args.Event.Location.X, args.Event.Location.Y);
            var secondPass = true;

            if (HadInput.Count > 0 && IsSavedGesture(args.Type))
            {
                var adjust = new GestureEventProcessingInfo(touchLocation, SKPoint.Empty, SKPoint.Empty, null);

                foreach (var hadInput in HadInput.Values)
                {
                    if (!hadInput.CanDraw || hadInput.InputTransparent || hadInput.GestureListenerRegistrationTime == null)
                    {
                        continue;
                    }

                    consumed = hadInput.OnSkiaGestureEvent(args, adjust);
                    if (consumed != null)
                    {
                        alreadyConsumed ??= consumed;
                        if (args.Type != TouchActionResult.Up)
                        {
                            secondPass = false;
                            AddHadInput(consumed);
                            break;
                        }
                    }
                }
            }

            if (secondPass)
            {
                var adjust = new GestureEventProcessingInfo(touchLocation, SKPoint.Empty, SKPoint.Empty, alreadyConsumed);

                foreach (var listener in GestureListeners.GetListeners())
                {
                    if (listener == null || !listener.CanDraw || listener.InputTransparent)
                    {
                        continue;
                    }

                    if (HadInput.ContainsKey(listener.Uid) && IsSavedGesture(args.Type))
                    {
                        continue;
                    }

                    if (listener == FocusedChild)
                        manageChildFocus = true;

                    var forChild = true;
                    if (args.Type != TouchActionResult.Up)
                    {
                        forChild = ((SkiaControl)listener).HitIsInside(args.Event.StartingLocation.X, args.Event.StartingLocation.Y) ||
                                   listener == FocusedChild;
                    }

                    if (!forChild)
                    {
                        continue;
                    }

                    if (manageChildFocus && listener == FocusedChild)
                    {
                        manageChildFocus = false;
                    }

                    var maybeConsumed = listener.OnSkiaGestureEvent(args, adjust);
                    if (maybeConsumed != null)
                    {
                        consumed = maybeConsumed;
                    }

                    if (consumed != null)
                    {
                        if (args.Type != TouchActionResult.Up)
                        {
                            AddHadInput(consumed);
                        }

                        break;
                    }
                }
            }

            if (TouchEffect.LogEnabled)
            {
                if (consumed == null)
                {
                    if (args.Event.Pointer != null)
                    {
                        Super.Log($"[Touch] {args.Type} ({args.Event.Pointer}) at {args.Event.Location} not consumed");
                    }
                    else
                    {
                        Super.Log($"[Touch] {args.Type} ({args.Event.NumberOfTouches}) at {args.Event.Location} not consumed");
                    }
                }
                else
                {
                    if (args.Event.Pointer != null)
                    {
                        Super.Log($"[Touch] {args.Type} ({args.Event.Pointer}) consumed by {consumed} at {args.Event.Location}");
                    }
                    else
                    {
                        Super.Log($"[Touch] {args.Type} ({args.Event.NumberOfTouches}) consumed by {consumed} at {args.Event.Location}");
                    }
                }
            }

            if (args.Type == TouchActionResult.Up && HadInput.Count > 0)
            {
                HadInput.Clear();
            }

            if (args.Type == TouchActionResult.Up || FocusedChild != null)
            {
                if (manageChildFocus || (FocusedChild != null && consumed != FocusedChild && !FocusedChild.LockFocus))
                {
                    FocusedChild = consumed;
                }
            }

            if (ReceivedInput.Count > 0)
            {
                ReceivedInput.Clear();
            }

            if (_gestureEffect != null)
            {
                if (consumed == null && args.Type == TouchActionResult.Panning)
                {
                    _gestureEffect.WIllLock = ShareLockState.Unlocked;
                }
                else if (consumed != null &&
                         (args.Type == TouchActionResult.Panning || args.Type == TouchActionResult.Wheel))
                {
                    _gestureEffect.WIllLock = ShareLockState.Locked;
                }
            }

            if (checkHover && !hadHover)
            {
                HasHover = null;
            }
        }
    }

    public bool SignalInput(ISkiaGestureListener listener, TouchActionResult gestureType)
    {
        if (ReceivedInput.Contains(listener))
            return false;

        if (IsSavedGesture(gestureType))
        {
            ReceivedInput.Add(listener);
        }

        return true;
    }

    public virtual void OnGestureEvent(TouchActionType type, TouchActionEventArgs args1, TouchActionResult touchAction)
    {
        if (!CanDraw)
        {
            NeedCheckParentVisibility = true;
            Repaint();
            return;
        }

        if (touchAction == TouchActionResult.Tapped)
        {
            Tapped?.Invoke(this, EventArgs.Empty);
        }

        if (touchAction == TouchActionResult.Down && _gestureEffect != null)
        {
            _gestureEffect.WIllLock = ShareLockState.Initial;
        }

        var args = SkiaGesturesParameters.Create(touchAction, args1);

        // Blazor interop needs the lock state updated before JS decides whether
        // the current browser event should stay local or bubble to the page.
        try
        {
            ProcessGestures(args);
        }
        catch (Exception e)
        {
            Super.Log(e);
        }

        Repaint();

    }

    public void OnTouchAction(TouchActionEventArgs args)
    {
        if (Gestures == GesturesMode.Disabled)
        {
            return;
        }

        lock (_gestureLock)
        {
            var action = args.Type;
            var pointerId = args.Id;

            if (action == TouchActionType.Pressed)
            {
                _activeTouchIds.Add(pointerId);
                args.NumberOfTouches = _activeTouchIds.Count;
                args.StartingLocation = args.Location;
                args.IsInsideView = true;
                args.IsInContact = true;
                args.Distance = new TouchActionEventArgs.DistanceInfo();
                _pointerDownArgs[pointerId] = args;
                _previousTouchArgs[pointerId] = args;
                OnGestureEvent(action, args, TouchActionResult.Down);
                return;
            }

            args.NumberOfTouches = _activeTouchIds.Count;

            _previousTouchArgs.TryGetValue(pointerId, out var previousArgs);
            if (previousArgs != null)
            {
                TouchActionEventArgs.FillDistanceInfo(args, previousArgs);
            }

            if (_pointerDownArgs.TryGetValue(pointerId, out var downArgs))
            {
                args.StartingLocation = downArgs.StartingLocation;
            }
            else
            {
                args.StartingLocation = args.Location;
            }

            if (action == TouchActionType.Wheel)
            {
                OnGestureEvent(action, args, TouchActionResult.Wheel);
                return;
            }

            if (action == TouchActionType.Pointer)
            {
                OnGestureEvent(action, args, TouchActionResult.Pointer);
                _previousTouchArgs[pointerId] = args;
                return;
            }

            if (action == TouchActionType.Moved || action == TouchActionType.PanStarted || action == TouchActionType.PanChanged || action == TouchActionType.PanEnded)
            {
                if ((args.Distance.Delta.X != 0 || args.Distance.Delta.Y != 0))
                {
                    OnGestureEvent(action, args, TouchActionResult.Panning);
                }

                _previousTouchArgs[pointerId] = args;
                return;
            }

            if (action == TouchActionType.Released || action == TouchActionType.Cancelled || action == TouchActionType.Exited)
            {
                args.IsInContact = args.NumberOfTouches > 1;

                if (!args.IsInContact && downArgs != null && action == TouchActionType.Released && !downArgs.PreventDefault)
                {
                    var threshold = TouchEffect.TappedCancelMoveThresholdPoints * Math.Max(0.1f, TouchEffect.Density);
                    if (Math.Abs(args.Distance.Total.X) < threshold && Math.Abs(args.Distance.Total.Y) < threshold)
                    {
                        OnGestureEvent(action, args, TouchActionResult.Tapped);
                    }
                }

                OnGestureEvent(action, args, TouchActionResult.Up);
                _previousTouchArgs.Remove(pointerId);
                _pointerDownArgs.Remove(pointerId);
                _activeTouchIds.Remove(pointerId);
            }
        }
    }
}
