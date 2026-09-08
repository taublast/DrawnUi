using AppoMobi.Specials;
using SkiaSharp;
using AppoMobi.Gestures;
using DrawnUi.Draw;
using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// The FiltersCamera content editor's actual SHAPE, not just its list: a three-column grid, the list
/// being a Fill SkiaScroll sandwiched between auto-sized siblings inside a stack, and a status label in
/// another column whose text changes when the drag commits. Reported symptom: after dropping a row that
/// was dragged from the top to the end of the list, the viewport lands somewhere in the middle instead
/// of staying where the drop happened.
/// </summary>
public static class EditorShapedDragRepro
{
    const float RowHeight = 28;
    const float RowSpacing = 3;

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
    static SkiaControl _dragRow;
    static Func<int, int, bool> _dragMove;
    static SkiaLabel _status;
    static int _commits;

    const float DragEdgeZone = 44;
    const float DragScrollStep = 8;

    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("========== EDITOR-SHAPED DRAG (drop position) ==========");
        try
        {
            var bad = RunCore();
            Console.WriteLine(bad == 0
                ? "=> PASS (the drop position survives the release)"
                : $"=> FAIL ({bad} checks failed)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  CRASH: {ex}");
        }

        Console.WriteLine("========================================================");
    }

    static int RunCore()
    {
        _dragIndex = -1;
        _dragScroll = null;
        _commits = 0;

        using var host = new HeadlessCanvasHost(1100, 800, scale: 1f, background: Colors.Black);

        var items = new ObservableRangeCollection<Item>(
            Enumerable.Range(1, 44).Select(i => new Item { Id = i }));
        var model = new List<Item>(items);

        SkiaLayout list = null;
        SkiaScroll scroll = null;
        var holder = new Holder();

        host.Canvas.Content = new SkiaLayout
        {
            Type = LayoutType.Grid,
            Padding = new Thickness(12),
            ColumnSpacing = 12,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                // column 0: presets (not dragged here, just occupies the shape)
                new SkiaLayout
                {
                    Type = LayoutType.Column,
                    Spacing = 6,
                    VerticalOptions = LayoutOptions.Fill,
                    Children = { Header(), Filler(120) },
                }.SetGrid(0, 0),

                // column 1: the list, a Fill scroll between auto-sized siblings
                new SkiaLayout
                {
                    Type = LayoutType.Column,
                    Spacing = 6,
                    VerticalOptions = LayoutOptions.Fill,
                    Children =
                    {
                        Header(),
                        Filler(34),
                        Filler(34),
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
                                    .Adapt(me => MakeDraggable(grip, me, () => holder.Scroll, () => model, items));
                                }),
                            }.Assign(out list)
                        }.Assign(out scroll),
                        Header(),
                        Filler(38),
                        Filler(28),
                    }
                }.SetGrid(1, 0),

                // column 2: the status label, whose text changes on every commit
                new SkiaLayout
                {
                    Type = LayoutType.Column,
                    Spacing = 8,
                    VerticalOptions = LayoutOptions.Fill,
                    Children =
                    {
                        Header(),
                        Filler(34),
                        Filler(72),
                        new SkiaLabel
                        {
                            FontSize = 11,
                            TextColor = Colors.Gray,
                            UseCache = SkiaCacheType.Operations,
                        }.Assign(out _status),
                    }
                }.SetGrid(2, 0),
            }
        }
        .WithColumnDefinitions("200,*,320");

        holder.Scroll = scroll;

        for (int i = 0; i < 300 && list.LastVisibleIndex < 0; i++) { host.RenderFrame(16); Thread.Sleep(3); }
        host.AdvanceFrames(80, 16);

        var viewport = scroll.DrawingRect;
        Console.WriteLine($"  scroll viewport = [{viewport.Top:0}..{viewport.Bottom:0}], rows on screen {list.FirstVisibleIndex}..{list.LastVisibleIndex}, items {items.Count}");

        var rect = RectOf(list, 1);
        if (rect == null)
        {
            Console.WriteLine("   FAIL row 1 is not on screen");
            return 1;
        }

        var dragged = items[1];
        var robot = new GestureRobot(host);
        robot.PointerDown(rect.Value.Left + 11, rect.Value.MidY);
        robot.PointerMoveTo(rect.Value.Left + 11, viewport.Bottom - 10);
        robot.PointerHold(200, PumpDrag);

        var offsetAtDrop = scroll.ViewportOffsetY;
        var indexAtDrop = _dragIndex;

        robot.PointerUp();
        var afterUp = scroll.ViewportOffsetY;
        host.AdvanceFrames(60, 16);
        var settled = scroll.ViewportOffsetY;

        Console.WriteLine($"  {dragged}: index {indexAtDrop} at drop, now {items.IndexOf(dragged)}");
        Console.WriteLine($"  offY drop={offsetAtDrop:0.0} afterUp={afterUp:0.0} settled={settled:0.0}, vis=[{list.FirstVisibleIndex}..{list.LastVisibleIndex}], commits={_commits}");

        int bad = 0;
        if (Math.Abs(settled - offsetAtDrop) > 1f)
        {
            bad++;
            Console.WriteLine($"   FAIL letting go moved the list by {settled - offsetAtDrop:0.0}px");
        }

        if (items.IndexOf(dragged) != items.Count - 1)
        {
            bad++;
            Console.WriteLine($"   FAIL the row reached {items.IndexOf(dragged)}, not the end ({items.Count - 1})");
        }

        return bad;
    }

    class Holder
    {
        public SkiaScroll Scroll;
    }

    static SkiaControl Header() => new SkiaLabel("HEADER") { FontSize = 11, TextColor = Colors.Gray };

    static SkiaControl Filler(float height) => new SkiaShape
    {
        Type = ShapeType.Rectangle,
        HeightRequest = height,
        BackgroundColor = Color.FromArgb("#222222"),
        HorizontalOptions = LayoutOptions.Fill,
    };

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
        Children = { Bar(), Bar(), Bar() }
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
                        Commit();

                    break;
            }
        };
    }

    /// <summary>Mirrors the page: save, reload, then a status label in another column changes text.</summary>
    static void Commit()
    {
        _commits++;
        if (_status != null)
            _status.Text = $"saved catalog.json {DateTime.Now:HH:mm:ss}";
    }

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
