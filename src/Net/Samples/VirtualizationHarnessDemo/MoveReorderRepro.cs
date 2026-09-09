using AppoMobi.Specials;
using DrawnUi.Draw;
using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// Gate for ObservableCollection.Move on a templated SkiaLayout (list reorder: an editor moving a row
/// up/down, drag-and-drop). Move used to fall through to InitializeTemplates + Invalidate, rebuilding
/// every cell to end up with the same rows in another order; a reorder deep in a list also had to
/// survive without the scroll drifting. Asserts, after a Move made while scrolled into the middle:
/// the viewport offset is unchanged, the content height is unchanged, and the cells on screen carry
/// the collection's NEW order. Runs over both measuring strategies.
/// </summary>
public static class MoveReorderRepro
{
    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("================= MOVE (list reorder) =================");
        try
        {
            int bad = 0;
            bad += RunCore(MeasuringStrategy.MeasureVisible, variableHeights: true, down: true);
            bad += RunCore(MeasuringStrategy.MeasureVisible, variableHeights: true, down: false);
            bad += RunCore(MeasuringStrategy.MeasureFirst, variableHeights: false, down: true);
            bad += RunCore(MeasuringStrategy.MeasureFirst, variableHeights: false, down: false);
            Console.WriteLine(bad == 0
                ? "=> PASS (reorder applied in place, scroll and content height held)"
                : $"=> FAIL ({bad} checks failed)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  CRASH: {ex}");
        }

        Console.WriteLine("=======================================================");
    }

    class Item
    {
        public int Id { get; init; }
        public float Height { get; init; }
        public override string ToString() => $"#{Id}";
    }

    static int RunCore(MeasuringStrategy strategy, bool variableHeights, bool down)
    {
        Console.WriteLine($"  --- {strategy}, moving {(down ? "down" : "up")} ---");

        using var host = new HeadlessCanvasHost(440, 920, scale: 1f, background: Colors.Black);

        var items = new ObservableRangeCollection<Item>(
            Enumerable.Range(1, 60).Select(i => new Item
            {
                Id = i,
                Height = variableHeights ? 44 + (i % 7) * 13 : 64
            }));

        SkiaLayout list = null;
        SkiaScroll scroll = null;
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
                        Spacing = 6,
                        Padding = new Thickness(10, 12),
                        HorizontalOptions = LayoutOptions.Fill,
                        RecyclingTemplate = RecyclingTemplate.Enabled,
                        MeasureItemsStrategy = strategy,
                        Virtualisation = VirtualisationType.Enabled,
                        ItemsSource = items,
                        ItemTemplate = new DataTemplate(() => new SkiaShape
                        {
                            Type = ShapeType.Rectangle,
                            CornerRadius = 6,
                            BackgroundColor = Colors.White,
                            HorizontalOptions = LayoutOptions.Fill,
                        }.Adapt(me => me.ApplyingBindingContext += (s, e) =>
                        {
                            if (me.BindingContext is Item bound)
                                me.HeightRequest = bound.Height;
                        })),
                    }.Assign(out list)
                }.Assign(out scroll)
            }
        };

        Settle(host, list, items.Count);

        // scroll into the middle: a reorder at the top of a fresh list proves nothing about drift
        var robot = new GestureRobot(host);
        for (int f = 0; f < 5; f++)
        {
            robot.Pan(220, 800, 220, 300, durationMs: 60, steps: 5);
            host.AdvanceFrames(10, 16);
            Thread.Sleep(4);
        }

        host.AdvanceFrames(40, 16);

        var offsetBefore = scroll.ViewportOffsetY;
        var contentBefore = scroll.ContentSize.Pixels.Height;
        int first = Math.Max(0, list.FirstVisibleIndex);
        int from = down ? first + 1 : first + 4;
        int to = down ? first + 4 : first + 1;
        if (Math.Max(from, to) >= items.Count)
        {
            Console.WriteLine("  SKIP (not enough items on screen)");
            return 0;
        }

        Console.WriteLine($"  before: offY={offsetBefore:0.0} contentH={contentBefore:0.0} vis=[{list.FirstVisibleIndex}..{list.LastVisibleIndex}] moving {items[from]} from {from} to {to}");

        items.Move(from, to);

        // FIRST frames after the move: a reorder must land without a visibly wrong intermediate state
        host.AdvanceFrames(2, 16);
        int early = AssertOrder(list, items, "early");
        host.AdvanceFrames(80, 16);

        var offsetAfter = scroll.ViewportOffsetY;
        var contentAfter = scroll.ContentSize.Pixels.Height;
        Console.WriteLine($"  after:  offY={offsetAfter:0.0} contentH={contentAfter:0.0} vis=[{list.FirstVisibleIndex}..{list.LastVisibleIndex}]");

        int bad = 0;

        if (Math.Abs(offsetAfter - offsetBefore) > 1f)
        {
            bad++;
            Console.WriteLine($"   FAIL scroll drifted by {offsetAfter - offsetBefore:0.0}px (a reorder must not move the viewport)");
        }

        if (Math.Abs(contentAfter - contentBefore) > 1f)
        {
            bad++;
            Console.WriteLine($"   FAIL content height changed by {contentAfter - contentBefore:0.0}px (nothing was added or removed)");
        }

        bad += early;
        bad += AssertOrder(list, items, "settled");

        // FAR MOVE: both indices are far above the viewport, so nothing on screen changes and the rows
        // that DID change are only revisited later. A structure left holding the pre-move heights heals
        // silently while they are off screen; scroll back and the region must still be laid out right.
        items.Move(2, 30);
        host.AdvanceFrames(40, 16);

        for (int f = 0; f < 12; f++)
        {
            robot.Pan(220, 300, 220, 860, durationMs: 60, steps: 5);
            host.AdvanceFrames(10, 16);
            Thread.Sleep(4);
        }

        host.AdvanceFrames(60, 16);
        Console.WriteLine($"  far move 2->30, scrolled back: offY={scroll.ViewportOffsetY:0.0} vis=[{list.FirstVisibleIndex}..{list.LastVisibleIndex}]");
        bad += AssertOrder(list, items, "far move");

        return bad;
    }

    /// <summary>Every cell on screen must carry the item the collection now holds at that index.</summary>
    static int AssertOrder(SkiaLayout list, IList<Item> items, string phase)
    {
        var tree = list.RenderTree;
        if (tree == null)
        {
            Console.WriteLine("   FAIL no render tree");
            return 1;
        }

        int bad = 0, checkedCells = 0;
        foreach (var cell in tree)
        {
            if (cell?.FreezeBindingContext is not Item bound)
                continue;

            int index = cell.FreezeIndex; // ContextIndex: the data index the view is bound to
            if (index < 0 || index >= items.Count)
                continue;

            checkedCells++;
            if (!ReferenceEquals(items[index], bound))
            {
                if (bad < 6)
                    Console.WriteLine($"   FAIL index {index} shows {bound} but the collection holds {items[index]}");
                bad++;
                continue;
            }

            // the row must also be laid out at ITS item's height: binding alone would pass while the
            // structure still held the pre-move heights, drawing every cell in the wrong slot
            var height = cell.Rect.Height;
            if (Math.Abs(height - bound.Height) > 1f)
            {
                if (bad < 6)
                    Console.WriteLine($"   FAIL index {index} ({bound}) drawn {height:0.0}px tall, item is {bound.Height:0.0}px");
                bad++;
            }
        }

        Console.WriteLine($"  order[{phase}]: checked {checkedCells} cells, {bad} wrong");
        return bad;
    }

    static void Settle(HeadlessCanvasHost host, SkiaLayout list, int count)
    {
        for (int i = 0; i < 300 && list.LastVisibleIndex < 0; i++)
        {
            host.RenderFrame(16);
            Thread.Sleep(3);
        }

        for (int i = 0; i < 200; i++)
        {
            host.RenderFrame(16);
            Thread.Sleep(2);
            if (list.LastMeasuredIndex >= count - 1)
                break;
        }
    }
}
