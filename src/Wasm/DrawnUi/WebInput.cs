using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using AppoMobi.Gestures;
using DrawnUi.Views;

namespace DrawnUi.Draw;

/// <summary>
/// Input handling for DrawnUI.Web - routes JS input to DrawnUI gesture system
/// </summary>
[SupportedOSPlatform("browser")]
public static partial class WebInput
{
    private static TouchActionEventArgs? _pointerDownArgs;
    private static TouchActionEventArgs? _previousArgs;
    private static readonly HashSet<long> ActiveTouchIds = new();

    /// <summary>
    /// The canvas that will receive input events
    /// </summary>
    public static Canvas? TargetCanvas { get; set; }

    /// <summary>
    /// Current rendering scale
    /// </summary>
    public static float RenderingScale { get; set; } = 1.0f;

    /// <summary>
    /// Called by JS when pointer goes down
    /// </summary>
    [JSExport]
    public static void OnPointerDown(int pointerId, double x, double y, int button, int buttons)
    {
        var scale = Math.Max(0.1f, RenderingScale);
        var location = new PointF((float)x * scale, (float)y * scale);
        var args = MakeTouchArgs(pointerId, TouchActionType.Pressed, location);

        ActiveTouchIds.Add(pointerId);
        args.NumberOfTouches = ActiveTouchIds.Count;
        args.StartingLocation = location;
        args.IsInsideView = true;
        args.IsInContact = true;
        args.Distance = new TouchActionEventArgs.DistanceInfo();

        _pointerDownArgs = args;
        _previousArgs = args;

        TargetCanvas?.OnGestureEvent(TouchActionType.Pressed, args, TouchActionResult.Down);
    }

    /// <summary>
    /// Called by JS when pointer moves
    /// </summary>
    [JSExport]
    public static void OnPointerMove(int pointerId, double x, double y, int buttons)
    {
        var scale = Math.Max(0.1f, RenderingScale);
        var location = new PointF((float)x * scale, (float)y * scale);
        var isDragging = buttons != 0;
        var actionType = isDragging ? TouchActionType.Moved : TouchActionType.Pointer;
        var args = MakeTouchArgs(pointerId, actionType, location);

        args.NumberOfTouches = ActiveTouchIds.Count;

        if (_previousArgs != null)
        {
            TouchActionEventArgs.FillDistanceInfo(args, _previousArgs);
        }

        if (_pointerDownArgs != null)
        {
            args.StartingLocation = _pointerDownArgs.StartingLocation;
        }
        else
        {
            args.StartingLocation = location;
        }

        if (actionType == TouchActionType.Pointer)
        {
            TargetCanvas?.OnGestureEvent(actionType, args, TouchActionResult.Pointer);
            _previousArgs = args;
            return;
        }

        if (args.Distance.Delta.X != 0 || args.Distance.Delta.Y != 0)
        {
            TargetCanvas?.OnGestureEvent(actionType, args, TouchActionResult.Panning);
        }

        _previousArgs = args;
    }

    /// <summary>
    /// Called by JS when pointer goes up
    /// </summary>
    [JSExport]
    public static void OnPointerUp(int pointerId, double x, double y, int button, int buttons)
    {
        var scale = Math.Max(0.1f, RenderingScale);
        var location = new PointF((float)x * scale, (float)y * scale);
        var args = MakeTouchArgs(pointerId, TouchActionType.Released, location);

        ActiveTouchIds.Remove(pointerId);
        args.NumberOfTouches = ActiveTouchIds.Count;
        args.IsInContact = ActiveTouchIds.Count > 0;

        if (_previousArgs != null)
        {
            TouchActionEventArgs.FillDistanceInfo(args, _previousArgs);
        }

        if (_pointerDownArgs != null)
        {
            args.StartingLocation = _pointerDownArgs.StartingLocation;

            // Check for tap
            var threshold = TouchEffect.TappedCancelMoveThresholdPoints * Math.Max(0.1f, TouchEffect.Density);
            if (Math.Abs(args.Distance.Total.X) < threshold && Math.Abs(args.Distance.Total.Y) < threshold)
            {
                TargetCanvas?.OnGestureEvent(TouchActionType.Released, args, TouchActionResult.Tapped);
            }
        }

        TargetCanvas?.OnGestureEvent(TouchActionType.Released, args, TouchActionResult.Up);

        _pointerDownArgs = null;
        _previousArgs = null;
    }

    /// <summary>
    /// Called by JS on the browser contextmenu event (right click, long press on touch, keyboard Menu key).
    /// Returns true when a control took it (the JS side then calls preventDefault); false = browser menu shows.
    /// </summary>
    [JSExport]
    public static bool OnContextMenu(double x, double y, string pointerType)
    {
        var scale = Math.Max(0.1f, RenderingScale);
        var location = new PointF((float)x * scale, (float)y * scale);
        var args = MakeTouchArgs(0, TouchActionType.ContextMenu, location);
        args.IsInsideView = true;
        args.StartingLocation = location;
        args.Distance = new TouchActionEventArgs.DistanceInfo();
        args.Pointer = new PointerData
        {
            Button = MouseButton.Right,
            ButtonNumber = 2,
            State = MouseButtonState.Released,
            DeviceType = pointerType switch { "touch" => PointerDeviceType.Touch, "pen" => PointerDeviceType.Pen, _ => PointerDeviceType.Mouse },
        };

        TargetCanvas?.OnGestureEvent(TouchActionType.ContextMenu, args, TouchActionResult.ContextMenu);

        return args.Handled;
    }

    /// <summary>
    /// Called by JS when pointer is cancelled
    /// </summary>
    [JSExport]
    public static void OnPointerCancel(int pointerId)
    {
        var location = new PointF(0, 0);
        var args = MakeTouchArgs(pointerId, TouchActionType.Cancelled, location);

        ActiveTouchIds.Remove(pointerId);
        args.NumberOfTouches = ActiveTouchIds.Count;
        args.IsInContact = false;

        TargetCanvas?.OnGestureEvent(TouchActionType.Cancelled, args, TouchActionResult.Up);

        _pointerDownArgs = null;
        _previousArgs = null;
    }

    /// <summary>
    /// Called by JS when a mouse wheel / trackpad scroll occurs.
    /// Mirrors the Net <c>GestureRobot.WheelScroll</c> contract: emits a
    /// <see cref="TouchActionType.Wheel"/> gesture carrying <see cref="WheelEventArgs"/>.
    /// SkiaScroll uses only the sign of <c>Wheel.Delta</c> for scrolling; zoom controls
    /// use its magnitude. Browser <c>deltaY &gt; 0</c> (scroll down) maps to a negative
    /// Delta so content scrolls down (DrawnUI decreases ViewportOffsetY when scrolling down).
    /// </summary>
    /// <param name="deltaX">Horizontal wheel delta (browser units).</param>
    /// <param name="deltaY">Vertical wheel delta (browser units).</param>
    /// <param name="deltaMode">0 = pixel, 1 = line, 2 = page.</param>
    /// <param name="x">Cursor X in CSS pixels.</param>
    /// <param name="y">Cursor Y in CSS pixels.</param>
    [JSExport]
    public static void OnWheel(double deltaX, double deltaY, int deltaMode, double x, double y)
    {
        // Normalize line/page modes to an approximate pixel magnitude (sign preserved).
        var factor = deltaMode switch
        {
            1 => 40.0,  // lines
            2 => 800.0, // pages
            _ => 1.0,   // pixels
        };
        var dominant = Math.Abs(deltaY) >= Math.Abs(deltaX) ? deltaY : deltaX;

        var scale = Math.Max(0.1f, RenderingScale);
        var location = new PointF((float)x * scale, (float)y * scale);
        var args = MakeTouchArgs(0, TouchActionType.Wheel, location);
        args.NumberOfTouches = Math.Max(1, ActiveTouchIds.Count);
        args.Wheel = new WheelEventArgs
        {
            // Negate: browser down (positive) → DrawnUI scroll down (negative Delta).
            Delta = (float)(-dominant * factor),
            Scale = 1f,
            Center = location,
        };

        TargetCanvas?.OnGestureEvent(TouchActionType.Wheel, args, TouchActionResult.Wheel);
    }

    /// <summary>
    /// Called by JS on keydown. <paramref name="code"/> is the DOM <c>KeyboardEvent.code</c>
    /// (e.g. "ArrowLeft", "Space", "Enter"), parsed into <see cref="InputKey"/>.
    /// </summary>
    [JSExport]
    public static void OnKeyDown(string code)
    {
        KeyboardManager.KeyboardPressed(MapCode(code));
    }

    /// <summary>
    /// Called by JS on keyup. See <see cref="OnKeyDown"/> for the code contract.
    /// </summary>
    [JSExport]
    public static void OnKeyUp(string code)
    {
        KeyboardManager.KeyboardReleased(MapCode(code));
    }

    private static InputKey MapCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return InputKey.Unknown;

        return Enum.TryParse<InputKey>(code, ignoreCase: false, out var mapped)
            ? mapped
            : InputKey.Unknown;
    }

    private static TouchActionEventArgs MakeTouchArgs(long pointerId, TouchActionType type, PointF location)
    {
        var args = new TouchActionEventArgs(
            pointerId,
            type,
            location,
            null,
            Math.Max(0.1f, RenderingScale));

        args.IsInsideView = true;
        args.NumberOfTouches = ActiveTouchIds.Count;

        if (_pointerDownArgs != null)
        {
            args.StartingLocation = _pointerDownArgs.StartingLocation;
        }
        else
        {
            args.StartingLocation = location;
        }

        return args;
    }
}
