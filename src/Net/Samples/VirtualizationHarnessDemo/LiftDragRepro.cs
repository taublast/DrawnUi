using AppoMobi.Specials;
using SkiaSharp;
using AppoMobi.Gestures;
using DrawnUi.Draw;
using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// Lift-and-drop reorder, the Android style: the dragged row is drawn by a floating "ghost" in an overlay
/// above the list while its real row goes blank, leaving a gap that travels as the list reorders. Released
/// inside the list the row stays where the gap is; released outside it goes back where it started. Either
/// way the ghost then glides into that slot instead of vanishing. The ghost is positioned with Left/Top,
/// which offset a control's CACHE, so it must be cached.
/// </summary>
public static class LiftDragRepro
{
    const float RowHeight = 30;
    const float RowSpacing = 2;
    const float DropSeconds = 0.14f;

    class Item
    {
        public int Id { get; init; }
        public override string ToString() => $"#{Id}";
    }

    static int _dragIndex = -1;
    static float _dragTravel;
    static float _dragStride;
    static float _dragLastOffset;
    static float _dragPointerY;
    static SkiaScroll _dragScroll;
    static SkiaLayout _rows;
    static Func<int, int, bool> _dragMove;

    static SkiaShape _ghost;
    static SkiaLabel _ghostLabel;
    static Item _dragItem;
    static int _dragStartIndex = -1;
    static float _dragGrabOffset;
    static float _dropFrom, _dropTo, _dropProgress;
    static bool _dropping;

    const float DragEdgeZone = 44;
    const float DragScrollStep = 8;

    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("============ LIFT AND DROP (ghost row) ============");
        try
        {
            int bad = 0;
            bad += RunCore("drop inside the list", cancel: false);
            bad += RunCore("drop outside the list", cancel: true);
            Console.WriteLine(bad == 0
                ? "=> PASS (ghost carries the row, drop lands or cancels)"
                : $"=> FAIL ({bad} checks failed)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  CRASH: {ex}");
        }

        Console.WriteLine("===================================================");
    }

    static int RunCore(string phase, bool cancel)
    {
        Console.WriteLine($"  --- {phase} ---");
        _dragIndex = -1;
        _dragItem = null;
        _dragScroll = null;
        _dropping = false;

        using var host = new HeadlessCanvasHost(900, 700, scale: 1f, background: Colors.Black);

        var items = new ObservableRangeCollection<Item>(
            Enumerable.Range(1, 30).Select(i => new Item { Id = i }));
        var model = new List<Item>(items);
        var holder = new Holder();

        SkiaScroll scroll = null;
        SkiaLayout rows = null;

        host.Canvas.Content = new SkiaLayer
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaLayout
                {
                    Type = LayoutType.Grid,
                    Padding = new Thickness(12),
                    VerticalOptions = LayoutOptions.Fill,
                    Children =
                    {
                        new SkiaScroll
                        {
                            VerticalOptions = LayoutOptions.Fill,
                            Content = new SkiaLayout
                            {
                                Type = LayoutType.Column,
                                Spacing = RowSpacing,
                                HorizontalOptions = LayoutOptions.Fill,
                                ItemsSource = items,
                                ItemTemplate = new DataTemplate(() =>
                                {
                                    SkiaControl grip = null;

                                    return new SkiaShape
                                    {
                                        Type = ShapeType.Rectangle,
                                        CornerRadius = 4,
                                        BackgroundColor = Color.FromArgb("#333333"),
                                        HeightRequest = RowHeight,
                                        HorizontalOptions = LayoutOptions.Fill,
                                        Children = { Grip().Assign(out grip) }
                                    }
                                    .Adapt(me => MakeDraggable(grip, me, () => holder.Scroll, () => model, items));
                                }),
                            }.Assign(out rows)
                        }.Assign(out scroll).SetGrid(0, 0),
                    }
                }.WithColumnDefinitions("240"),

                new SkiaLayer
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    InputTransparent = true,
                    Children =
                    {
                        new SkiaShape
                        {
                            Type = ShapeType.Rectangle,
                            CornerRadius = 4,
                            BackgroundColor = Color.FromArgb("#3A5A8A"),
                            HorizontalOptions = LayoutOptions.Start,
                            VerticalOptions = LayoutOptions.Start,
                            IsVisible = false,
                            UseCache = SkiaCacheType.Image,
                            Children =
                            {
                                new SkiaLabel { FontSize = 13, TextColor = Colors.White }.Assign(out _ghostLabel),
                            }
                        }.Assign(out _ghost),
                    }
                },
            }
        };

        holder.Scroll = scroll;
        _rows = rows;
        _view = items;

        for (int i = 0; i < 300 && rows.LastVisibleIndex < 0; i++) { host.RenderFrame(16); Thread.Sleep(3); }
        host.AdvanceFrames(80, 16);

        var viewport = scroll.DrawingRect;
        var rect = RowRect(2);
        if (rect == null)
        {
            Console.WriteLine("   FAIL row 2 is not on screen");
            return 1;
        }

        var dragged = items[2];
        var startOrder = items.ToList();
        var robot = new GestureRobot(host);

        robot.PointerDown(rect.Value.Left + 11, rect.Value.MidY); // over the grip, not the row body
        var lifted = _ghost.IsVisible;
        var ghostTopAtLift = (float)_ghost.Top;

        // drag down four rows, then either stay over the list or leave it sideways
        var targetY = rect.Value.MidY + 4 * (RowHeight + RowSpacing);
        robot.PointerMoveTo(rect.Value.Left + 11, targetY);
        robot.PointerHold(4, PumpDrag);
        var ghostTopWhileDragging = (float)_ghost.Top;
        var indexWhileDragging = _dragIndex;

        if (cancel)
        {
            robot.PointerMoveTo(viewport.Right + 120, targetY); // out of the list
            robot.PointerHold(4, PumpDrag);
        }

        robot.PointerUp();
        for (int f = 0; f < 30; f++)
        {
            host.RenderFrame(16);
            if (_dropping)
                PumpDrop(0.016f);
        }

        int bad = 0;
        var landed = items.IndexOf(dragged);
        Console.WriteLine($"  lifted={lifted} ghostTop {ghostTopAtLift:0.0} -> {ghostTopWhileDragging:0.0}, index while dragging={indexWhileDragging}, landed={landed}");

        if (!lifted)
        {
            bad++;
            Console.WriteLine("   FAIL the ghost never appeared on press");
        }

        if (Math.Abs(ghostTopWhileDragging - ghostTopAtLift) < 10)
        {
            bad++;
            Console.WriteLine("   FAIL the ghost did not follow the pointer");
        }

        if (indexWhileDragging != 6)
        {
            bad++;
            Console.WriteLine($"   FAIL the gap reached index {indexWhileDragging}, expected 6");
        }

        var expected = cancel ? 2 : 6;
        if (landed != expected)
        {
            bad++;
            Console.WriteLine($"   FAIL {dragged} landed at {landed}, expected {expected}");
        }

        if (_ghost.IsVisible)
        {
            bad++;
            Console.WriteLine("   FAIL the ghost is still on screen after the drop settled");
        }

        if (_dragItem != null)
        {
            bad++;
            Console.WriteLine("   FAIL the drag state was not cleared");
        }

        if (!model.SequenceEqual(items))
        {
            bad++;
            Console.WriteLine("   FAIL the persisted list drifted from the displayed order");
        }

        if (cancel && !startOrder.SequenceEqual(items))
        {
            bad++;
            Console.WriteLine("   FAIL a cancelled drop changed the order");
        }

        return bad;
    }

    class Holder
    {
        public SkiaScroll Scroll;
    }

    static SKRect? RowRect(int index)
    {
        var tree = _rows?.RenderTree;
        if (tree == null || index < 0)
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
        VerticalOptions = LayoutOptions.Fill,
        Children = { Bar(), Bar(), Bar() }
    };

    static SkiaShape Bar() => new()
    {
        Type = ShapeType.Rectangle,
        HeightRequest = 2,
        WidthRequest = 14,
        BackgroundColor = Colors.Gray,
        HorizontalOptions = LayoutOptions.Center,
    };

    static void MakeDraggable(SkiaControl grip, SkiaControl row, Func<SkiaScroll> scroll,
        Func<IList<Item>> model, ObservableRangeCollection<Item> view)
    {
        grip.ConsumeGestures += (sender, e) =>
        {
            switch (e.Args.Type)
            {
                case TouchActionResult.Down:
                    EndDrag();
                    _dragIndex = row.BindingContext is Item item ? view.IndexOf(item) : -1;
                    e.Consumed = _dragIndex >= 0;
                    if (!e.Consumed)
                        break;

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

                    Lift(row, e.Args.Event.Location.Y / (float)grip.RenderingScale);
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
                    var outside = _dragScroll != null
                                  && !_dragScroll.DrawingRect.Contains(e.Args.Event.Location.X, e.Args.Event.Location.Y);
                    if (wasDragging)
                        StartDrop(outside, model());

                    EndDrag();
                    break;
            }
        };
    }

    static void Lift(SkiaControl row, float pointerYPts)
    {
        if (row.BindingContext is not Item item)
            return;

        var rect = RowRect(_dragIndex);
        if (rect == null)
            return;

        var scale = (float)row.RenderingScale;
        if (scale <= 0)
            scale = 1;

        var topPts = rect.Value.Top / scale;
        _dragItem = item;
        _dragStartIndex = _dragIndex;
        _dragGrabOffset = pointerYPts - topPts;
        _dropping = false;

        _ghostLabel.Text = item.ToString();
        _ghost.WidthRequest = rect.Value.Width / scale;
        _ghost.HeightRequest = rect.Value.Height / scale;
        _ghost.Left = rect.Value.Left / scale;
        _ghost.Top = topPts;
        _ghost.IsVisible = true;
    }

    static void CarryGhost()
    {
        if (_dragItem == null || _dropping)
            return;

        var scale = (float)_ghost.RenderingScale;
        if (scale <= 0)
            scale = 1;

        _ghost.Top = _dragPointerY / scale - _dragGrabOffset;
    }

    static void StartDrop(bool cancelled, IList<Item> list)
    {
        if (_dragItem == null)
            return;

        if (cancelled && _dragStartIndex >= 0 && _dragIndex >= 0 && _dragIndex != _dragStartIndex)
        {
            var moved = list[_dragIndex];
            list.RemoveAt(_dragIndex);
            list.Insert(_dragStartIndex, moved);
            _view.Move(_dragIndex, _dragStartIndex);
            _dragIndex = _dragStartIndex;
        }

        var rect = RowRect(_dragIndex);
        if (rect == null)
        {
            FinishDrop();
            return;
        }

        var scale = (float)_ghost.RenderingScale;
        if (scale <= 0)
            scale = 1;

        _dropFrom = (float)_ghost.Top;
        _dropTo = rect.Value.Top / scale;
        _dropProgress = 0;
        _dropping = true;
    }

    static ObservableRangeCollection<Item> _view;

    public static void PumpDrop(float seconds)
    {
        if (!_dropping)
            return;

        _dropProgress += seconds / DropSeconds;
        if (_dropProgress >= 1)
        {
            FinishDrop();
            return;
        }

        var eased = 1 - (float)Math.Pow(1 - _dropProgress, 3);
        _ghost.Top = _dropFrom + (_dropTo - _dropFrom) * eased;
    }

    static void FinishDrop()
    {
        _dropping = false;
        _dragItem = null;
        _dragStartIndex = -1;
        if (_ghost != null)
            _ghost.IsVisible = false;
    }

    public static void PumpDrag()
    {
        if (_dragIndex < 0 || _dragMove == null)
            return;

        AutoScrollStep();
        CarryGhost();

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

        if (_dragScroll != null)
        {
            _dragScroll.RespondsToGestures = true;
            _dragScroll = null;
        }
    }
}
