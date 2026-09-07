using AppoMobi.Gestures;

namespace DrawnUi.Views;

public partial class Canvas
{
    public HashSet<ISkiaGestureListener> ReceivedInput { get; } = new();

    public Dictionary<Guid, ISkiaGestureListener> HadInput { get; } = new();

    // FocusedChild as of the last Down — detects focus claimed during the current gesture.
    private ISkiaGestureListener _focusedChildAtDown;

    public event EventHandler? Tapped;

    protected bool IsSavedGesture(TouchActionResult type)
    {
        return type == TouchActionResult.Panning || type == TouchActionResult.Wheel || type == TouchActionResult.Up;
    }

    /// <summary>
    /// Not used in pur .NET yet
    /// </summary>
    public GesturesMode Gestures { get; set; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddHadInput(ISkiaGestureListener consumed)
    {
        HadInput.TryAdd(consumed.Uid, consumed);
    }

    protected virtual void ProcessNetGestures(SkiaGesturesParameters args)
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
            ISkiaGestureListener? consumed = null;
            ISkiaGestureListener? alreadyConsumed = null;

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
                    {
                        manageChildFocus = true;
                    }

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

            if (_checkHover && !_hadHover)
            {
                HasHover = null;
            }
        }
    }

    private bool SignalNetInput(ISkiaGestureListener listener, TouchActionResult gestureType)
    {
        if (ReceivedInput.Contains(listener))
            return false;

        if (IsSavedGesture(gestureType))
        {
            ReceivedInput.Add(listener);
        }

        return true;
    }

    private void HandleNetGestureEvent(TouchActionType type, TouchActionEventArgs args1, TouchActionResult touchAction)
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

        var args = SkiaGesturesParameters.Create(touchAction, args1, RenderingScale);

        try
        {
            ProcessNetGestures(args);
        }
        catch (Exception e)
        {
            Super.Log(e);
        }

        // ContextMenu: a control that took the request marked the (possibly rescaled) event Handled;
        // the host reads the original args to decide preventDefault
        if (touchAction == TouchActionResult.ContextMenu && args.Event != null && args.Event != args1)
        {
            args1.Handled = args.Event.Handled;
        }

        Repaint();
    }
}
