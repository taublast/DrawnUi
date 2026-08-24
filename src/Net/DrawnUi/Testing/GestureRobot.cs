using System.Drawing;
using AppoMobi.Gestures;
using DrawnUi.Draw;

namespace DrawnUi.Testing;

/// <summary>
/// High-level, deterministic gesture driver for a <see cref="HeadlessCanvasHost"/>.
/// Synthesises the same Down → Panning(×N) → Up sequence the platform layer produces, but with an
/// injected monotonic timestamp clock so panning deltas and fling velocity are fully reproducible
/// (no wall-clock dependency). Coordinates are supplied in points; the robot converts to pixels
/// using the host's rendering scale, matching <c>TouchActionEventArgs</c> (pixel space).
/// </summary>
public sealed class GestureRobot
{
    private readonly HeadlessCanvasHost _host;
    private readonly float _scale;
    private long _pointerId;
    private DateTime _clock = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public GestureRobot(HeadlessCanvasHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _scale = host.Scale <= 0 ? 1f : host.Scale;
    }

    /// <summary>Single tap at the given point (no movement): Down → Tapped → Up.</summary>
    public void Tap(double x, double y, double frameMs = 16.0)
    {
        var id = ++_pointerId;
        var px = ToPixels(x, y);

        var down = MakeArgs(id, TouchActionType.Pressed, px, px);
        down.IsInContact = true;
        Send(TouchActionType.Pressed, down, TouchActionResult.Down, frameMs);

        Advance(frameMs);
        var up = MakeArgs(id, TouchActionType.Released, px, px);
        TouchActionEventArgs.FillDistanceInfo(up, down);
        // movement is zero → emit Tapped first (mirrors platform path), then Up
        Send(TouchActionType.Released, up, TouchActionResult.Tapped, 0);
        Send(TouchActionType.Released, up, TouchActionResult.Up, frameMs);
    }

    /// <summary>
    /// Drags from <paramref name="from"/> to <paramref name="to"/> over <paramref name="durationMs"/>,
    /// split into <paramref name="steps"/> interpolated Panning events, then releases. The release
    /// carries the velocity implied by the last step, so the target may fling afterwards.
    /// Call <see cref="SettleFling(SkiaScroll,int,double,float)"/> to run the fling to rest.
    /// </summary>
    public void Pan(PointF from, PointF to, double durationMs = 200, int steps = 12)
        => Pan(from, to, durationMs, steps, 0);

    /// <summary>
    /// Pan with a hold before release: the finger stays at the end point for <paramref name="holdMs"/>
    /// so the release velocity is ~zero (slow-drag-and-release, not a flick).
    /// </summary>
    public void Pan(PointF from, PointF to, double durationMs, int steps, double holdMs)
    {
        if (steps < 1) steps = 1;
        var id = ++_pointerId;
        var dt = durationMs / steps;

        var fromPx = ToPixels(from.X, from.Y);
        var toPx = ToPixels(to.X, to.Y);

        var down = MakeArgs(id, TouchActionType.Pressed, fromPx, fromPx);
        down.IsInContact = true;
        Send(TouchActionType.Pressed, down, TouchActionResult.Down, dt);

        var prev = down;
        for (var i = 1; i <= steps; i++)
        {
            Advance(dt);
            var t = i / (float)steps;
            var loc = new PointF(
                fromPx.X + (toPx.X - fromPx.X) * t,
                fromPx.Y + (toPx.Y - fromPx.Y) * t);

            var move = MakeArgs(id, TouchActionType.Moved, loc, fromPx);
            TouchActionEventArgs.FillDistanceInfo(move, prev);

            if (move.Distance.Delta.X != 0 || move.Distance.Delta.Y != 0)
                Send(TouchActionType.Moved, move, TouchActionResult.Panning, dt);
            else
                _host.RenderFrame(dt);

            prev = move;
        }

        Advance(dt + holdMs);
        if (holdMs > 0)
        {
            // VelocityAccumulator ages samples by REAL time: sleep so the release sees a stale window -> ~zero velocity
            System.Threading.Thread.Sleep((int)holdMs);
        }
        var up = MakeArgs(id, TouchActionType.Released, toPx, fromPx);
        up.IsInContact = false;
        TouchActionEventArgs.FillDistanceInfo(up, prev);
        Send(TouchActionType.Released, up, TouchActionResult.Up, dt);
    }

    /// <summary>Convenience overload taking raw coordinates.</summary>
    public void Pan(double fromX, double fromY, double toX, double toY, double durationMs = 200, int steps = 12)
        => Pan(new PointF((float)fromX, (float)fromY), new PointF((float)toX, (float)toY), durationMs, steps);

    /// <summary>
    /// A fast flick: pans a short distance over a short time to impart high velocity, then settles
    /// the resulting fling against <paramref name="scroll"/>. Returns the number of settle frames used.
    /// </summary>
    public int Fling(PointF from, PointF to, SkiaScroll scroll, double durationMs = 80, int steps = 6,
        int maxFrames = 600, double frameMs = 16.0)
    {
        Pan(from, to, durationMs, steps);
        return SettleFling(scroll, maxFrames, frameMs);
    }

    /// <summary>
    /// Renders frames until the scroll's viewport offset stops changing (fling/bounce settled),
    /// or <paramref name="maxFrames"/> is reached. Returns the number of frames rendered.
    /// </summary>
    public int SettleFling(SkiaScroll scroll, int maxFrames = 600, double frameMs = 16.0, float epsilon = 0.05f)
    {
        var stableNeeded = 3;
        var stable = 0;
        var prevX = scroll.ViewportOffsetX;
        var prevY = scroll.ViewportOffsetY;

        for (var i = 0; i < maxFrames; i++)
        {
            _host.RenderFrame(frameMs);

            var dx = Math.Abs(scroll.ViewportOffsetX - prevX);
            var dy = Math.Abs(scroll.ViewportOffsetY - prevY);
            prevX = scroll.ViewportOffsetX;
            prevY = scroll.ViewportOffsetY;

            if (dx < epsilon && dy < epsilon)
            {
                if (++stable >= stableNeeded)
                    return i + 1;
            }
            else
            {
                stable = 0;
            }
        }

        return maxFrames;
    }

    /// <summary>
    /// Emits a mouse-wheel event (<c>delta</c> px) at the given point. Note: <see cref="SkiaScroll"/>
    /// routes the wheel to zoom unless <c>ZoomLocked</c> is true — set that to make the wheel scroll.
    /// </summary>
    public void WheelScroll(double x, double y, float delta, double frameMs = 16.0)
    {
        var id = ++_pointerId;
        var px = ToPixels(x, y);
        var args = MakeArgs(id, TouchActionType.Wheel, px, px);
        args.Wheel = new WheelEventArgs { Delta = delta, Scale = 1f, Center = px };
        Send(TouchActionType.Wheel, args, TouchActionResult.Wheel, frameMs);
    }

    private TouchActionEventArgs MakeArgs(long id, TouchActionType type, PointF pixelLocation, PointF startingPixel)
    {
        // Scale == host scale so SkiaGesturesParameters.Create does NOT rescale our pixel coords.
        var args = new TouchActionEventArgs(id, type, pixelLocation, null, _scale)
        {
            Timestamp = _clock,
            NumberOfTouches = 1,
            StartingLocation = startingPixel,
            IsInsideView = true
        };
        return args;
    }

    private void Send(TouchActionType type, TouchActionEventArgs args, TouchActionResult result, double frameMs)
    {
        _host.Canvas.OnGestureEvent(type, args, result);
        // Gesture delivery is queued in ExecuteBeforeDraw and flushed on the next frame.
        _host.RenderFrame(frameMs);
    }

    private void Advance(double ms) => _clock = _clock.AddMilliseconds(ms);

    private PointF ToPixels(double x, double y) => new((float)(x * _scale), (float)(y * _scale));
}
