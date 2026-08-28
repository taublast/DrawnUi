using DrawnUi.Draw;
using Microsoft.Maui.Controls;
using SkiaSharp;
using Xunit;
using SkiaLayout = DrawnUi.Draw.SkiaLayout;

namespace UnitTests
{
    /// <summary>
    /// Column/Row/Wrap measure-vs-arrange sweep (2026-08-28). Each test pins a case where a child's arranged
    /// rect used to differ from the slot it was measured for: Split slots not advancing, templated cells
    /// re-aligned at draw time, main-axis Center children floating in the leftover stack rect, Fill children
    /// on an unbounded axis blowing up to infinity, auto stacks with only Fill children collapsing to 0,
    /// and Wrap flow cells aligning inside the leftover row strip.
    /// </summary>
    public class StackWrapSweepTests : DrawnTestsBase
    {
        public StackWrapSweepTests()
        {
            Super.Screen.Density = 1;
        }

        private static SkiaControl Box(float w = -1, float h = -1, LayoutOptions? ho = null, LayoutOptions? vo = null)
        {
            var c = new SkiaControl();
            if (w >= 0) c.WidthRequest = w;
            if (h >= 0) c.HeightRequest = h;
            if (ho != null) c.HorizontalOptions = ho.Value;
            if (vo != null) c.VerticalOptions = vo.Value;
            return c;
        }

        /// <summary>Absolute layout holding one fixed box — a child whose Fill size depends on its constraint.</summary>
        private static SkiaLayout Panel(float innerW, float innerH, LayoutOptions? ho = null, LayoutOptions? vo = null)
        {
            var l = new SkiaLayout { Type = LayoutType.Absolute, Children = new List<SkiaControl> { Box(innerW, innerH) } };
            if (ho != null) l.HorizontalOptions = ho.Value;
            if (vo != null) l.VerticalOptions = vo.Value;
            return l;
        }

        private static SkiaLayout Stack(LayoutType type, LayoutOptions ho, LayoutOptions vo, params SkiaControl[] kids) => new SkiaLayout
        {
            Type = type,
            Spacing = 0,
            HorizontalOptions = ho,
            VerticalOptions = vo,
            Children = kids.ToList(),
        };

        private static SkiaLayout Templated(int split, float spacing, Func<SkiaControl> template) => new SkiaLayout
        {
            Type = LayoutType.Column,
            Split = split,
            Spacing = spacing,
            HorizontalOptions = LayoutOptions.Fill,
            RecyclingTemplate = RecyclingTemplate.Disabled,
            MeasureItemsStrategy = MeasuringStrategy.MeasureAll,
            ItemsSource = new List<int> { 1, 2, 3 },
            ItemTemplate = new DataTemplate(template),
        };

        private static void Render(SkiaControl control, SKRect destination)
        {
            RenderWithOperationsContext(destination, ctx => control.Render(ctx));
        }

        private static void AssertRect(SKRect rect, float left, float top, float right, float bottom)
        {
            Assert.Equal(left, rect.Left, 0.5f);
            Assert.Equal(top, rect.Top, 0.5f);
            Assert.Equal(right, rect.Right, 0.5f);
            Assert.Equal(bottom, rect.Bottom, 0.5f);
        }

        // ---- Split slots ----

        [Fact]
        public void Column_Split2_NonTemplated_SecondColumnStartsAtSlot()
        {
            var a = Box(50, 20);
            var b = Box(50, 20);
            var c = Box(50, 20);
            var stack = Stack(LayoutType.Column, LayoutOptions.Fill, LayoutOptions.Start, a, b, c);
            stack.Split = 2;
            stack.Spacing = 10;

            stack.CommitInvalidations();
            stack.Measure(410, float.PositiveInfinity, 1);

            AssertRect(a.Destination, 0, 0, 50, 20);
            AssertRect(b.Destination, 210, 0, 260, 20); // was Left = spacing (10), overlapping A
            AssertRect(c.Destination, 0, 30, 50, 50);
        }

        [Fact]
        public void Column_Split2_Templated_SlotIsColumnWidth_NotMeasuredWidth()
        {
            var stack = Templated(2, 0, () => new SkiaLayout { Type = LayoutType.Absolute, WidthRequest = 50, HeightRequest = 20, HorizontalOptions = LayoutOptions.End });
            stack.CommitInvalidations();
            stack.Measure(400, float.PositiveInfinity, 1);
            Render(stack, new SKRect(0, 0, 400, 100));

            var tree = stack.RenderTree;
            Assert.Equal(3, tree.Count);
            AssertRect(tree[0].Control.DrawingRect, 150, 0, 200, 20);
            AssertRect(tree[1].Control.DrawingRect, 350, 0, 400, 20); // slot [200..400]; was [50..250] (advanced by measured 50)
            AssertRect(tree[2].Control.DrawingRect, 150, 20, 200, 40);
        }

        // ---- Templated draw rect = arranged size (no second alignment at draw) ----

        [Fact]
        public void Column_Split2_Templated_CenterCells_NotCenteredTwiceAtDraw()
        {
            var stack = Templated(2, 10, () => new SkiaLayout { Type = LayoutType.Absolute, WidthRequest = 50, HeightRequest = 20, HorizontalOptions = LayoutOptions.Center });
            stack.CommitInvalidations();
            stack.Measure(410, float.PositiveInfinity, 1);
            Render(stack, new SKRect(0, 0, 410, 100));

            var tree = stack.RenderTree;
            Assert.Equal(3, tree.Count);
            AssertRect(tree[0].Control.DrawingRect, 75, 0, 125, 20); // was 150..200
            AssertRect(tree[1].Control.DrawingRect, 285, 0, 335, 20);
            AssertRect(tree[0].Rect, 75, 0, 125, 20);              // draw rect is the arranged size, not the slot
        }

        [Fact]
        public void Templated_FillYCells_InScroll_AreContentSized()
        {
            var stack = Templated(0, 0, () => new SkiaLayout
            {
                Type = LayoutType.Absolute, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill,
                Children = new List<SkiaControl> { Box(80, 30) }
            });
            stack.CommitInvalidations();
            stack.Measure(300, float.PositiveInfinity, 1);
            Render(stack, new SKRect(0, 0, 300, 90));

            Assert.Equal(90, stack.MeasuredSize.Pixels.Height);
            var tree = stack.RenderTree;
            Assert.Equal(3, tree.Count);
            AssertRect(tree[0].Control.DrawingRect, 0, 0, 300, 30);  // was float.MaxValue tall
            AssertRect(tree[2].Control.DrawingRect, 0, 60, 300, 90);
        }

        [Fact]
        public void Templated_VCenterCells_FiniteHeight_StayInTheirSlots()
        {
            var stack = Templated(0, 0, () => new SkiaLayout { Type = LayoutType.Absolute, HeightRequest = 50, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Center });
            stack.CommitInvalidations();
            stack.Measure(300, 1000, 1);
            Render(stack, new SKRect(0, 0, 300, 1000));

            Assert.Equal(150, stack.MeasuredSize.Pixels.Height);
            var tree = stack.RenderTree;
            Assert.Equal(3, tree.Count); // were centered in the remaining 1000 -> outside the 150 stack -> 0 drawn
            AssertRect(tree[1].Control.DrawingRect, 0, 50, 300, 100);
        }

        // ---- Main-axis Center stays in the slot ----

        [Fact]
        public void Column_VerticalCenterChild_StaysInSlot()
        {
            var a = Box(h: 100, ho: LayoutOptions.Fill);
            var b = Box(h: 20, ho: LayoutOptions.Fill, vo: LayoutOptions.Center); // Fill-X -> second pass
            var c = Box(h: 50, ho: LayoutOptions.Fill);
            var stack = Stack(LayoutType.Column, LayoutOptions.Fill, LayoutOptions.Start, a, b, c);

            stack.CommitInvalidations();
            stack.Measure(300, 500, 1);

            Assert.Equal(170, stack.MeasuredSize.Pixels.Height);
            AssertRect(b.Destination, 0, 100, 300, 120); // was 175..195 (centered in the leftover rect)
            AssertRect(c.Destination, 0, 120, 300, 170);
        }

        [Fact]
        public void Column_VerticalCenterChild_FirstPassOnly_StaysInSlot()
        {
            var a = Box(100, 100);
            var b = Box(100, 20, vo: LayoutOptions.Center); // Start-X -> no second pass
            var c = Box(100, 50);
            var stack = Stack(LayoutType.Column, LayoutOptions.Fill, LayoutOptions.Start, a, b, c);

            stack.CommitInvalidations();
            stack.Measure(300, 500, 1);

            AssertRect(b.Destination, 0, 100, 100, 120); // was 290..310
        }

        [Fact]
        public void Row_HorizontalCenterChild_StaysInSlot()
        {
            var a = Box(w: 100, vo: LayoutOptions.Fill);
            var b = Box(w: 20, vo: LayoutOptions.Fill, ho: LayoutOptions.Center);
            var c = Box(w: 50, vo: LayoutOptions.Fill);
            var stack = Stack(LayoutType.Row, LayoutOptions.Start, LayoutOptions.Fill, a, b, c);

            stack.CommitInvalidations();
            stack.Measure(500, 300, 1);

            Assert.Equal(170, stack.MeasuredSize.Pixels.Width);
            AssertRect(b.Destination, 100, 0, 120, 300); // was 175..195
            AssertRect(c.Destination, 120, 0, 170, 300);
        }

        // ---- Fill child on an unbounded main axis = auto ----

        [Fact]
        public void Column_InfiniteHeight_FillYChild_IsContentSized()
        {
            var a = Box(h: 100, ho: LayoutOptions.Fill);
            var b = Panel(80, 30, LayoutOptions.Fill, LayoutOptions.Fill);
            var c = Box(h: 100, ho: LayoutOptions.Fill);
            var stack = Stack(LayoutType.Column, LayoutOptions.Fill, LayoutOptions.Start, a, b, c);

            stack.CommitInvalidations();
            stack.Measure(300, float.PositiveInfinity, 1);

            Assert.Equal(230, stack.MeasuredSize.Pixels.Height); // was 0 (-1 in 1.9.7.4), B float.MaxValue tall, C at Top=inf
            Assert.Equal(30, b.MeasuredSize.Pixels.Height);
            AssertRect(b.Destination, 0, 100, 300, 130);
            AssertRect(c.Destination, 0, 130, 300, 230);
        }

        [Fact]
        public void Row_InfiniteWidth_FillXChild_IsContentSized()
        {
            var a = Box(w: 100, vo: LayoutOptions.Fill);
            var b = Panel(80, 30, LayoutOptions.Fill, LayoutOptions.Fill);
            var c = Box(w: 100, vo: LayoutOptions.Fill);
            var stack = Stack(LayoutType.Row, LayoutOptions.Start, LayoutOptions.Fill, a, b, c);

            stack.CommitInvalidations();
            stack.Measure(float.PositiveInfinity, 300, 1);

            Assert.Equal(280, stack.MeasuredSize.Pixels.Width);
            AssertRect(b.Destination, 100, 0, 180, 300);
            AssertRect(c.Destination, 180, 0, 280, 300);
        }

        [Fact]
        public void Column_FiniteHeight_FillY_StillDistributesSpace()
        {
            var a = Box(h: 100, ho: LayoutOptions.Fill);
            var b = Panel(80, 30, LayoutOptions.Fill, LayoutOptions.Fill);
            var c = Box(h: 100, ho: LayoutOptions.Fill);
            var stack = Stack(LayoutType.Column, LayoutOptions.Fill, LayoutOptions.Fill, a, b, c);
            stack.Spacing = 10;

            stack.CommitInvalidations();
            stack.Measure(300, 500, 1);

            AssertRect(b.Destination, 0, 110, 300, 390);
            AssertRect(c.Destination, 0, 400, 300, 500);
        }

        // ---- Auto stack whose only cross-axis children are Fill ----

        [Fact]
        public void Column_AutoWidth_OnlyFillXChildren_DoesNotCollapse()
        {
            var a = Panel(80, 40, LayoutOptions.Fill);
            var b = Panel(60, 40, LayoutOptions.Fill);
            var stack = Stack(LayoutType.Column, LayoutOptions.Start, LayoutOptions.Start, a, b);

            stack.CommitInvalidations();
            stack.Measure(300, float.PositiveInfinity, 1);

            Assert.Equal(300, stack.MeasuredSize.Pixels.Width); // was 0, children re-measured at width 0
            Assert.Equal(300, a.MeasuredSize.Pixels.Width);
        }

        [Fact]
        public void Row_AutoHeight_OnlyFillYChildren_DoesNotCollapse()
        {
            var a = Panel(40, 80, vo: LayoutOptions.Fill);
            var b = Panel(40, 60, vo: LayoutOptions.Fill);
            var stack = Stack(LayoutType.Row, LayoutOptions.Start, LayoutOptions.Start, a, b);

            stack.CommitInvalidations();
            stack.Measure(float.PositiveInfinity, 300, 1);

            Assert.Equal(300, stack.MeasuredSize.Pixels.Height);
        }

        [Fact]
        public void Column_AutoWidth_FixedPlusFillX_KeepsContentWidth()
        {
            var a = Box(100, 20);
            var b = Panel(10, 20, LayoutOptions.Fill);
            var stack = Stack(LayoutType.Column, LayoutOptions.Start, LayoutOptions.Start, a, b);

            stack.CommitInvalidations();
            stack.Measure(400, float.PositiveInfinity, 1);

            Assert.Equal(100, stack.MeasuredSize.Pixels.Width);
            Assert.Equal(100, b.MeasuredSize.Pixels.Width);
        }

        // ---- Wrap ----

        [Fact]
        public void Wrap_FillXChild_FillsRestOfRow()
        {
            var a = Box(50, 20);
            var b = Panel(10, 20, LayoutOptions.Fill);
            var stack = Stack(LayoutType.Wrap, LayoutOptions.Fill, LayoutOptions.Start, a, b);

            stack.CommitInvalidations();
            stack.Measure(400, float.PositiveInfinity, 1);

            Assert.Equal(20, stack.MeasuredSize.Pixels.Height); // one row (was two: Fill child measured full width, broke row)
            Assert.Equal(b.Destination.Top, a.Destination.Top, 0.5f);
            Assert.Equal(50, b.Destination.Left, 0.5f);
            Assert.True(b.Destination.Width >= 340, $"fill child width {b.Destination.Width}");
        }

        [Fact]
        public void Wrap_CenterChild_KeepsFlowSlot()
        {
            var a = Box(50, 20);
            var b = Box(50, 20, ho: LayoutOptions.Center);
            var c = Box(50, 20);
            var stack = Stack(LayoutType.Wrap, LayoutOptions.Fill, LayoutOptions.Start, a, b, c);

            stack.CommitInvalidations();
            stack.Measure(400, float.PositiveInfinity, 1);

            Assert.Equal(50, b.Destination.Left, 0.5f);  // was 200 (centered in the leftover row strip)
            Assert.Equal(100, c.Destination.Left, 0.5f);
        }

        [Fact]
        public void Wrap_Split2_CenterCells_StillCenteredInChunk()
        {
            var a = Box(50, 20, ho: LayoutOptions.Center);
            var b = Box(50, 20, ho: LayoutOptions.Center);
            var stack = Stack(LayoutType.Wrap, LayoutOptions.Fill, LayoutOptions.Start, a, b);
            stack.Split = 2;
            stack.Spacing = 10;

            stack.CommitInvalidations();
            stack.Measure(412, float.PositiveInfinity, 1);

            Assert.Equal(75, a.Destination.Left, 0.5f);
            Assert.Equal(285, b.Destination.Left, 0.5f);
        }

        // ---- Templated main-axis Fill cells are auto-sized ----

        private static SkiaControl FillCell(float innerW, float innerH) => new SkiaLayout
        {
            Type = LayoutType.Absolute, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill,
            Children = new List<SkiaControl> { Box(innerW, innerH) }
        };

        [Fact]
        public void Templated_Column_FillYCells_FiniteHeight_AreContentSized()
        {
            var stack = Templated(0, 0, () => FillCell(80, 30));
            stack.CommitInvalidations();
            stack.Measure(300, 1000, 1);
            Render(stack, new SKRect(0, 0, 300, 1000));

            Assert.Equal(90, stack.MeasuredSize.Pixels.Height); // was 1000: first cell swallowed the whole column
            var tree = stack.RenderTree;
            Assert.Equal(3, tree.Count);
            AssertRect(tree[0].Control.DrawingRect, 0, 0, 300, 30);
            AssertRect(tree[1].Control.DrawingRect, 0, 30, 300, 60);
            AssertRect(tree[2].Control.DrawingRect, 0, 60, 300, 90);
        }

        [Fact]
        public void Templated_Row_FillXCells_FiniteWidth_AreContentSized()
        {
            var stack = Templated(0, 0, () => FillCell(30, 80));
            stack.Type = LayoutType.Row;
            stack.HorizontalOptions = LayoutOptions.Start;
            stack.VerticalOptions = LayoutOptions.Fill;
            stack.CommitInvalidations();
            stack.Measure(1000, 300, 1);
            Render(stack, new SKRect(0, 0, 1000, 300));

            Assert.Equal(90, stack.MeasuredSize.Pixels.Width);
            var tree = stack.RenderTree;
            Assert.Equal(3, tree.Count);
            AssertRect(tree[1].Control.DrawingRect, 30, 0, 60, 300);
            AssertRect(tree[2].Control.DrawingRect, 60, 0, 90, 300);
        }

        [Fact]
        public void Templated_Column_FillYCells_MeasureVisible_AreContentSized()
        {
            var stack = Templated(0, 0, () => FillCell(80, 30));
            stack.RecyclingTemplate = RecyclingTemplate.Enabled;
            stack.MeasureItemsStrategy = MeasuringStrategy.MeasureVisible;
            stack.CommitInvalidations();
            stack.Measure(300, 1000, 1);
            Render(stack, new SKRect(0, 0, 300, 1000));

            var tree = stack.RenderTree;
            Assert.Equal(3, tree.Count);
            AssertRect(tree[0].Control.DrawingRect, 0, 0, 300, 30);
            AssertRect(tree[1].Control.DrawingRect, 0, 30, 300, 60);
            AssertRect(tree[2].Control.DrawingRect, 0, 60, 300, 90);
        }

        // ---- Absolute: measure never peeks at the previous arrange ----

        [Fact]
        public void Absolute_FillLayer_InfiniteWidth_MeasureDoesNotDependOnPreviousArrange()
        {
            var a = Box(100, 20);
            var b = Panel(80, 20, LayoutOptions.Fill); // Fill-X, auto height
            var layer = new SkiaLayout { Type = LayoutType.Absolute, HorizontalOptions = LayoutOptions.Fill, Children = new List<SkiaControl> { a, b } };

            layer.CommitInvalidations();
            layer.Measure(float.PositiveInfinity, 300, 1);
            var first = b.MeasuredSize.Pixels.Width;
            Assert.Equal(80, first); // unbounded: content width, no previous frame to borrow from

            // arranged into a finite box: the Fill child follows the real box
            layer.Arrange(new SKRect(0, 0, 200, 300), layer.SizeRequest.Width, layer.SizeRequest.Height, 1);
            Assert.Equal(200, b.MeasuredSize.Pixels.Width);

            // same unbounded measure again must give the same numbers as the first time, not the last arrange
            layer.Measure(float.PositiveInfinity, 300, 1);
            Assert.Equal(first, b.MeasuredSize.Pixels.Width);
        }
    }
}
