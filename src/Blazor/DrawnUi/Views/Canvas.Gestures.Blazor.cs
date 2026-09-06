using AppoMobi.Gestures;
using static System.Runtime.InteropServices.JavaScript.JSType;

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

    // FocusedChild as of the last Down — detects focus claimed during the current gesture.
    private ISkiaGestureListener _focusedChildAtDown;

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
            if (args.Type == TouchActionResult.Down)
            {
                if (HadInput.Count > 0)
                {
                    HadInput.Clear();
                }
                // Focus state at gesture start: if a control claims focus DURING this gesture (editor
                // self-focusing on Down -> keyboard spacer relayout shifts the layout), the completed
                // Tapped must not override that fresh claim.
                _focusedChildAtDown = FocusedChild;
            }

            _checkHover = args.Type == TouchActionResult.Pointer;
            _hadHover = false;
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
                        var hitPoint = args.Type == TouchActionResult.Pointer
                            ? args.Event.Location
                            : args.Event.StartingLocation;

                        forChild = ((SkiaControl)listener).HitIsInside(hitPoint.X, hitPoint.Y) ||
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

            if (args.Type != TouchActionResult.Pointer)
            {
                // FOCUS RULES. Controls CLAIM focus themselves (editor self-focuses on its Down); the
                // canvas only decides on the COMPLETED tap. Down/Panning/Up never change focus here —
                // clearing focus on Down closed the keyboard mid-gesture, the spacer relayout moved the
                // send button away from under the pointer and its Tapped never fired (text not sent).
                // On Tapped: move focus to the consumer (ReportFocus keeps the current focus when the
                // consumer does not accept it — tapping a button must not steal the editor's keyboard),
                // or clear it on a tap over nothing = the outside-tap keyboard dismiss. Skip both when
                // focus was claimed during THIS gesture (FocusedChild changed since Down): the claim is
                // fresher than the tap's landing spot, which a keyboard relayout may have invalidated.
                if (manageChildFocus)
                {
                    FocusedChild = consumed;
                }
                else if (args.Type == TouchActionResult.Tapped
                         && FocusedChild != null && consumed != FocusedChild
                         && !FocusedChild.LockFocus
                         && FocusedChild == _focusedChildAtDown)
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

            if (_checkHover && !_hadHover)
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

        //Console.WriteLine($"CANVAS {RenderingScale:F1}/{args1.Scale:F1} touch: {touchAction} {args1.Location}");

        if (touchAction == TouchActionResult.Tapped)
        {
            Tapped?.Invoke(this, EventArgs.Empty);
        }

        if (touchAction == TouchActionResult.Down && _gestureEffect != null)
        {
            _gestureEffect.WIllLock = ShareLockState.Initial;
        }

        var args = SkiaGesturesParameters.Create(touchAction, args1, RenderingScale);

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

        // ContextMenu: a control that took the request marked the (possibly rescaled) event Handled;
        // the gesture effect reads the original args to decide preventDefault
        if (touchAction == TouchActionResult.ContextMenu && args.Event != null && args.Event != args1)
        {
            args1.Handled = args.Event.Handled;
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
