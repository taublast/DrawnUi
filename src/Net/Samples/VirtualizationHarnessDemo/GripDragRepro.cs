using AppoMobi.Specials;
using SkiaSharp;
using AppoMobi.Gestures;
using DrawnUi.Draw;
using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// Mirrors the FiltersCamera content editor's reorder-by-grip rows: a vertical SkiaScroll over a
/// templated SkiaLayout, each cell carrying a drag grip on the left. Dragging the grip must reorder
/// the collection one position per row-height travelled and must NOT scroll the list.
/// The interesting part is gesture routing: a vertical pan inside a vertical scroll belongs to the
/// scroll, which only offers Panning to children when the direction is wrong for it or when it is
/// not responding to gestures — so the grip's Down stands the scroll down for the drag.
/// </summary>
public static class GripDragRepro
{
    const float RowHeight = 28;
    const float RowSpacing = 3;
    const float Stride = RowHeight + RowSpacing;

    class Item
    {
        public int Id { get; init; }
        public override string ToString() => $"#{Id}";
    }

    // one drag at a time, exactly like the editor page holds it
    static int _dragIndex = -1;
    static float _dragTravel;
    static float _dragStride;
    static float _dragLastOffset;
    static float _dragPointerY;
    static SkiaScroll _dragScroll;
    static SkiaControl _dragRow;
    static Func<int, int, bool> _dragMove;
    static int _commits;

    const float DragEdgeZone = 44;
    const float DragScrollStep = 8;

    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("============= DRAG GRIP TO REORDER =============");
        try
        {
            int bad = 0;
            bad += RunCore("drag down, at top", scrollFirst: false, steps: 3);
            bad += RunCore("drag down, scrolled into the middle", scrollFirst: true, steps: 3);
            bad += RunCore("drag up, scrolled into the middle", scrollFirst: true, steps: -4);
            bad += RunEdgeScroll();
            Console.WriteLine(bad == 0
                ? "=> PASS (grip drag reorders, scroll stays put)"
                : $"=> FAIL ({bad} checks failed)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  CRASH: {ex}");
        }

        Console.WriteLine("================================================");
    }

    static int RunCore(string phase, bool scrollFirst, int steps)
    {
        Console.WriteLine($"  --- {phase} ---");
        _dragIndex = -1;
        _dragScroll = null;
        _commits = 0;

        using var host = new HeadlessCanvasHost(440, 920, scale: 1f, background: Colors.Black);

        var items = new ObservableRangeCollection<Item>(
            Enumerable.Range(1, 60).Select(i => new Item { Id = i }));
        var model = new List<Item>(items); // the editor keeps the persisted list beside the observable

        var built = BuildList(host, items, model);
        var list = built.List;
        var scroll = built.Scroll;

        for (int i = 0; i < 300 && list.LastVisibleIndex < 0; i++) { host.RenderFrame(16); Thread.Sleep(3); }
        host.AdvanceFrames(60, 16);

        if (scrollFirst)
        {
            var robotPre = new GestureRobot(host);
            for (int f = 0; f < 2; f++)
            {
                robotPre.Pan(300, 800, 300, 400, durationMs: 60, steps: 5);
                host.AdvanceFrames(10, 16);
                Thread.Sleep(4);
            }

            host.AdvanceFrames(60, 16);
        }

        int target = Math.Max(0, list.FirstVisibleIndex) + (steps > 0 ? 2 : 6);
        var rect = RectOf(list, target);
        if (rect == null)
        {
            Console.WriteLine($"   FAIL row {target} is not on screen");
            return 1;
        }

        var dragged = items[target];
        var offsetBefore = scroll.ViewportOffsetY;
        var gripX = rect.Value.Left + 11;
        var startY = rect.Value.MidY;
        Console.WriteLine($"  dragging {dragged} at index {target} by {steps} rows, grip at ({gripX:0},{startY:0}), offY={offsetBefore:0.0}");

        var robot = new GestureRobot(host);
        robot.Pan(new System.Drawing.PointF(gripX, startY),
            new System.Drawing.PointF(gripX, startY + steps * Stride),
            durationMs: 400, steps: 24, holdMs: 60);

        host.AdvanceFrames(60, 16);

        int bad = 0;
        var landed = items.IndexOf(dragged);
        var offsetAfter = scroll.ViewportOffsetY;

        Console.WriteLine($"  landed at index {landed} (wanted {target + steps}), offY={offsetAfter:0.0}, commits={_commits}");

        if (landed != target + steps)
        {
            bad++;
            Console.WriteLine($"   FAIL {dragged} moved {landed - target} positions, expected {steps}");
        }

        if (Math.Abs(offsetAfter - offsetBefore) > 1f)
        {
            bad++;
            Console.WriteLine($"   FAIL the list scrolled by {offsetAfter - offsetBefore:0.0}px during the drag");
        }

        // a pan that does NOT start on a grip must still scroll, not reorder
        var orderBeforePan = items.ToList();
        var offsetBeforePan = scroll.ViewportOffsetY;
        var bodyX = rect.Value.Left + 200;
        var bodyFrom = offsetBeforePan < -50 ? 400 : 700;   // pan whichever way has room to move
        var bodyTo = offsetBeforePan < -50 ? 700 : 400;
        new GestureRobot(host).Pan(new System.Drawing.PointF(bodyX, bodyFrom),
            new System.Drawing.PointF(bodyX, bodyTo), durationMs: 120, steps: 8, holdMs: 40);
        host.AdvanceFrames(60, 16);

        if (!orderBeforePan.SequenceEqual(items))
        {
            bad++;
            Console.WriteLine("   FAIL a pan on the row body reordered the list (it must only scroll)");
        }

        if (Math.Abs(scroll.ViewportOffsetY - offsetBeforePan) < 1f)
        {
            bad++;
            Console.WriteLine("   FAIL a pan on the row body did not scroll the list");
        }

        if (_commits != 1)
        {
            bad++;
            Console.WriteLine($"   FAIL saved {_commits} times, a drag must write once on release");
        }

        // the persisted list must match the observable one, and nothing may be lost
        if (model.Count != items.Count || !model.SequenceEqual(items))
        {
            bad++;
            Console.WriteLine("   FAIL the persisted list drifted from the displayed order");
        }

        if (!scroll.RespondsToGestures)
        {
            bad++;
            Console.WriteLine("   FAIL the scroll was left deaf to gestures after the drag");
        }

        // and it must still scroll normally afterwards. Pan whichever way has room: the "scrolled into
        // the middle" phase ends near the bottom, where a further pan up legitimately cannot move.
        var beforeScrollBack = scroll.ViewportOffsetY;
        if (beforeScrollBack < -50)
            new GestureRobot(host).Pan(300, 500, 300, 800, durationMs: 80, steps: 6);
        else
            new GestureRobot(host).Pan(300, 800, 300, 500, durationMs: 80, steps: 6);
        host.AdvanceFrames(60, 16);
        if (Math.Abs(scroll.ViewportOffsetY - beforeScrollBack) < 1f)
        {
            bad++;
            Console.WriteLine("   FAIL the list no longer scrolls after a drag");
        }

        return bad;
    }

    /// <summary>
    /// From the top of the list, drag a row down to the bottom edge and HOLD: the list must scroll itself
    /// and the row must keep travelling with it, so it can be dropped somewhere that was never on screen.
    /// </summary>
    static int RunEdgeScroll()
    {
        Console.WriteLine("  --- hold at the bottom edge, drag off screen ---");
        _dragIndex = -1;
        _dragScroll = null;
        _commits = 0;

        using var host = new HeadlessCanvasHost(440, 920, scale: 1f, background: Colors.Black);

        var items = new ObservableRangeCollection<Item>(
            Enumerable.Range(1, 200).Select(i => new Item { Id = i }));
        var model = new List<Item>(items);

        var built = BuildList(host, items, model);
        var list = built.List;
        var scroll = built.Scroll;

        for (int i = 0; i < 300 && list.LastVisibleIndex < 0; i++) { host.RenderFrame(16); Thread.Sleep(3); }
        host.AdvanceFrames(80, 16);

        var rect = RectOf(list, 1);
        if (rect == null)
        {
            Console.WriteLine("   FAIL row 1 is not on screen");
            return 1;
        }

        var dragged = items[1];
        var visibleAtStart = list.LastVisibleIndex;
        Console.WriteLine($"  starting at offY={scroll.ViewportOffsetY:0.0}, last visible row is {visibleAtStart}");

        var robot = new GestureRobot(host);
        robot.PointerDown(rect.Value.Left + 11, rect.Value.MidY);
        robot.PointerMoveTo(rect.Value.Left + 11, 900); // into the bottom edge zone
        robot.PointerHold(240, PumpDrag);               // hold: only the ticker keeps it going
        robot.PointerUp();
        host.AdvanceFrames(40, 16);

        int bad = 0;
        var landed = items.IndexOf(dragged);
        Console.WriteLine($"  landed at index {landed}, offY={scroll.ViewportOffsetY:0.0}, vis=[{list.FirstVisibleIndex}..{list.LastVisibleIndex}], commits={_commits}");

        if (landed <= visibleAtStart)
        {
            bad++;
            Console.WriteLine($"   FAIL the row only reached {landed}, never past the rows that were on screen at the start ({visibleAtStart})");
        }

        if (scroll.ViewportOffsetY >= -1)
        {
            bad++;
            Console.WriteLine("   FAIL the list never scrolled while the pointer rested in the edge zone");
        }

        if (_commits != 1)
        {
            bad++;
            Console.WriteLine($"   FAIL saved {_commits} times, expected once on release");
        }

        if (model.Count != items.Count || !model.SequenceEqual(items))
        {
            bad++;
            Console.WriteLine("   FAIL the persisted list drifted from the displayed order");
        }

        if (!scroll.RespondsToGestures)
        {
            bad++;
            Console.WriteLine("   FAIL the scroll was left deaf to gestures after the drag");
        }

        return bad;
    }

    class Built
    {
        public SkiaLayout List;
        public SkiaScroll Scroll;
    }

    static Built BuildList(HeadlessCanvasHost host, ObservableRangeCollection<Item> items, IList<Item> model)
    {
        var built = new Built();

        host.Canvas.Content = new SkiaLayer
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaScroll
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Content = new SkiaLayout
                    {
                        Type = LayoutType.Column,
                        Spacing = RowSpacing,
                        HorizontalOptions = LayoutOptions.Fill,
                        RecyclingTemplate = RecyclingTemplate.Enabled,
                        ItemsSource = items,
                        ItemTemplate = new DataTemplate(() =>
                        {
                            SkiaControl grip = null;

                            return new SkiaLayout
                            {
                                Type = LayoutType.Grid,
                                ColumnSpacing = 4,
                                HeightRequest = RowHeight,
                                HorizontalOptions = LayoutOptions.Fill,
                                Children =
                                {
                                    Grip().Assign(out grip).SetGrid(0, 0),
                                    new SkiaShape
                                    {
                                        Type = ShapeType.Rectangle,
                                        CornerRadius = 4,
                                        BackgroundColor = Colors.DimGray,
                                        HorizontalOptions = LayoutOptions.Fill,
                                        VerticalOptions = LayoutOptions.Fill,
                                    }.SetGrid(1, 0),
                                }
                            }
                            .WithColumnDefinitions("22,*")
                            .Adapt(me => MakeDraggable(grip, me, () => built.Scroll, () => model, items));
                        }),
                    }.Assign(out built.List)
                }.Assign(out built.Scroll)
            }
        };

        return built;
    }

    static SKRect? RectOf(SkiaLayout list, int index)
    {
        var tree = list.RenderTree;
        if (tree == null)
            return null;

        foreach (var cell in tree)
        {
            if (cell.FreezeIndex == index)
                return cell.Rect;
        }

        return null;
    }

    static SkiaControl Grip() => new SkiaLayout
    {
        Type = LayoutType.Column,
        Spacing = 3,
        WidthRequest = 22,
        HorizontalOptions = LayoutOptions.Fill,
        VerticalOptions = LayoutOptions.Center,
        Children =
        {
            Bar(), Bar(), Bar(),
        }
    };

    static SkiaShape Bar() => new()
    {
        Type = ShapeType.Rectangle,
        CornerRadius = 1,
        HeightRequest = 2,
        WidthRequest = 14,
        BackgroundColor = Colors.Gray,
        HorizontalOptions = LayoutOptions.Center,
    };

    /// <summary>Same wiring as ContentEditorPage.MakeDraggable.</summary>
    static void MakeDraggable(SkiaControl grip, SkiaControl row, Func<SkiaScroll> scroll,
        Func<IList<Item>> model, ObservableRangeCollection<Item> view)
    {
        if (grip == null || row == null)
            return;

        grip.ConsumeGestures += (sender, e) =>
        {
            switch (e.Args.Type)
            {
                case TouchActionResult.Down:
                    EndDrag();
                    _dragIndex = row.BindingContext is Item item ? view.IndexOf(item) : -1;
                    e.Consumed = _dragIndex >= 0;
                    row.Opacity = e.Consumed ? 0.55 : 1;
                    if (!e.Consumed)
                        break;

                    _dragRow = row;
                    _dragTravel = 0;
                    _dragStride = (float)(row.MeasuredSize.Units.Height + RowSpacing);
                    _dragPointerY = e.Args.Event.Location.Y;
                    _dragScroll = scroll();
                    _dragLastOffset = (float)(_dragScroll?.ViewportOffsetY ?? 0);
                    _dragMove = (from, to) =>
                    {
                        var list = model();
                        if (list == null || to < 0 || to >= view.Count || to >= list.Count)
                            return false;

                        var moved = list[from];
                        list.RemoveAt(from);
                        list.Insert(to, moved);
                        view.Move(from, to);
                        return true;
                    };

                    if (_dragScroll != null)
                        _dragScroll.RespondsToGestures = false;

                    break;

                case TouchActionResult.Panning:
                    if (_dragIndex < 0)
                        break;

                    _dragPointerY = e.Args.Event.Location.Y;
                    _dragTravel += e.Args.Event.Distance.Delta.Y / (float)grip.RenderingScale;
                    PumpDrag();
                    e.Consumed = true;
                    break;

                case TouchActionResult.Up:
                    var wasDragging = _dragIndex >= 0;
                    EndDrag();
                    if (wasDragging)
                        _commits++;

                    break;
            }
        };
    }

    /// <summary>Stands in for the page's dispatcher tick plus its Panning handler.</summary>
    public static void PumpDrag()
    {
        if (_dragIndex < 0 || _dragMove == null)
            return;

        AutoScrollStep();

        if (_dragScroll != null)
        {
            var offset = (float)_dragScroll.ViewportOffsetY;
            _dragTravel += _dragLastOffset - offset;
            _dragLastOffset = offset;
        }

        while (_dragStride > 0 && Math.Abs(_dragTravel) >= _dragStride)
        {
            var step = Math.Sign(_dragTravel);
            if (!_dragMove(_dragIndex, _dragIndex + step))
            {
                _dragTravel = 0;
                break;
            }

            _dragIndex += step;
            _dragTravel -= step * _dragStride;
        }
    }

    static void AutoScrollStep()
    {
        if (_dragScroll == null)
            return;

        var scale = (float)_dragScroll.RenderingScale;
        if (scale <= 0)
            return;

        var viewport = _dragScroll.DrawingRect;
        var zone = DragEdgeZone * scale;

        var direction = 0f;
        if (_dragPointerY < viewport.Top + zone)
            direction = 1;
        else if (_dragPointerY > viewport.Bottom - zone)
            direction = -1;

        if (direction == 0)
            return;

        _dragScroll.ScrollTo((float)_dragScroll.ViewportOffsetX,
            (float)_dragScroll.ViewportOffsetY + direction * DragScrollStep, 0, true);
    }

    static void EndDrag()
    {
        _dragIndex = -1;
        _dragMove = null;

        if (_dragRow != null)
        {
            _dragRow.Opacity = 1;
            _dragRow = null;
        }

        if (_dragScroll != null)
        {
            _dragScroll.RespondsToGestures = true;
            _dragScroll = null;
        }
    }
}
