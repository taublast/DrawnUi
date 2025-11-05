using System.Runtime.InteropServices;

namespace Sandbox
{
    internal sealed class MotionMarkNativeScene : IDisposable
    {
        private const int GridWidth = 80;
        private const int GridHeight = 40;

        private static readonly uint[] s_palette =
        [
            SkiaNativeMethods.PackColor(0xFF, 0x10, 0x10, 0x10),
            SkiaNativeMethods.PackColor(0xFF, 0x80, 0x80, 0x80),
            SkiaNativeMethods.PackColor(0xFF, 0xC0, 0xC0, 0xC0),
            SkiaNativeMethods.PackColor(0xFF, 0x10, 0x10, 0x10),
            SkiaNativeMethods.PackColor(0xFF, 0x80, 0x80, 0x80),
            SkiaNativeMethods.PackColor(0xFF, 0xC0, 0xC0, 0xC0),
            SkiaNativeMethods.PackColor(0xFF, 0xE0, 0x10, 0x40),
        ];

        private static readonly (int X, int Y)[] s_offsets =
        [
            (-4, 0),
            (2, 0),
            (1, -2),
            (1, 2),
        ];

        private readonly List<Element> _elements = new();
        private readonly SkiaPath _path = new();
        private readonly SkiaPaint _strokePaint = new();
        private readonly Random _random = new();
        private GridPoint _lastGridPoint = new(GridWidth / 2, GridHeight / 2);
        private int _complexity = 8;
        private bool _disposed;

        public MotionMarkNativeScene()
        {
            _strokePaint.SetAntialias(true);
            _strokePaint.SetStyle(SKPaintStyle.Stroke);
            _strokePaint.SetStrokeCap(SKStrokeCap.Round);
            _strokePaint.SetStrokeJoin(SKStrokeJoin.Round);

        }

        public int Complexity => _complexity;
        public int ElementCount => _elements.Count;

        public void SetComplexity(int complexity)
        {
            complexity = Math.Clamp(complexity, 0, 24);
            if (_complexity == complexity)
                return;

            _complexity = complexity;
            Resize(ComputeElementCount(_complexity));
        }

        public void Render(IntPtr canvas, float width, float height, bool resetPath = true)
        {
            Resize(ComputeElementCount(_complexity));

            SkiaNativeMethods.CanvasClear(canvas, SkiaNativeMethods.PackColor(0xFF, 12, 16, 24));

            if (_elements.Count == 0)
                return;

            float scaleX = width / (GridWidth + 1);
            float scaleY = height / (GridHeight + 1);
            float uniformScale = MathF.Min(scaleX, scaleY);
            float offsetX = (width - uniformScale * (GridWidth + 1)) * 0.5f;
            float offsetY = (height - uniformScale * (GridHeight + 1)) * 0.5f;

            Span<Element> elements = CollectionsMarshal.AsSpan(_elements);
            if (resetPath)
                _path.Reset();
            bool pathStarted = false;

            for (int i = 0; i < elements.Length; i++)
            {
                ref Element element = ref elements[i];
                if (!pathStarted)
                {
                    Point start = element.Start.ToPoint(uniformScale, offsetX, offsetY);
                    _path.MoveTo(start.X, start.Y);
                    pathStarted = true;
                }

                switch (element.Kind)
                {
                    case SegmentKind.Line:
                    {
                        Point end = element.End.ToPoint(uniformScale, offsetX, offsetY);
                        _path.LineTo(end.X, end.Y);
                        break;
                    }

                    case SegmentKind.Quad:
                    {
                        Point c1 = element.Control1.ToPoint(uniformScale, offsetX, offsetY);
                        Point end = element.End.ToPoint(uniformScale, offsetX, offsetY);
                        _path.QuadTo(c1.X, c1.Y, end.X, end.Y);
                        break;
                    }

                    case SegmentKind.Cubic:
                    {
                        Point c1 = element.Control1.ToPoint(uniformScale, offsetX, offsetY);
                        Point c2 = element.Control2.ToPoint(uniformScale, offsetX, offsetY);
                        Point end = element.End.ToPoint(uniformScale, offsetX, offsetY);
                        _path.CubicTo(c1.X, c1.Y, c2.X, c2.Y, end.X, end.Y);
                        break;
                    }
                }

                bool finalize = element.Split || i == elements.Length - 1;
                if (finalize)
                {
                    _strokePaint.SetColor(element.Color);
                    _strokePaint.SetStrokeWidth(element.Width);
                    SkiaNativeMethods.CanvasDrawPath(canvas, _path.Handle, _strokePaint.Handle);
                    _path.Reset();
                    pathStarted = false;
                }

                if (_random.NextDouble() > 0.995)
                {
                    element.Split = !element.Split;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _path.Dispose();
            _strokePaint.Dispose();
            _disposed = true;
        }

        private void Resize(int count)
        {
            int current = _elements.Count;
            if (count == current)
                return;

            if (count < current)
            {
                _elements.RemoveRange(count, current - count);
                _lastGridPoint = count > 0
                    ? _elements[^1].End
                    : new GridPoint(GridWidth / 2, GridHeight / 2);
                return;
            }

            _elements.Capacity = Math.Max(_elements.Capacity, count);
            if (current == 0)
            {
                _lastGridPoint = new GridPoint(GridWidth / 2, GridHeight / 2);
            }
            else
            {
                _lastGridPoint = _elements[^1].End;
            }

            for (int i = current; i < count; i++)
            {
                Element element = CreateRandomElement(_lastGridPoint);
                _elements.Add(element);
                _lastGridPoint = element.End;
            }
        }

        private Element CreateRandomElement(GridPoint last)
        {
            int segType = _random.Next(4);
            GridPoint next = RandomPoint(last);

            Element element = default;
            element.Start = last;

            if (segType < 2)
            {
                element.Kind = SegmentKind.Line;
                element.End = next;
            }
            else if (segType == 2)
            {
                GridPoint p2 = RandomPoint(next);
                element.Kind = SegmentKind.Quad;
                element.Control1 = next;
                element.End = p2;
            }
            else
            {
                GridPoint p2 = RandomPoint(next);
                GridPoint p3 = RandomPoint(next);
                element.Kind = SegmentKind.Cubic;
                element.Control1 = next;
                element.Control2 = p2;
                element.End = p3;
            }

            element.Color = s_palette[_random.Next(s_palette.Length)];
            element.Width = (float)(Math.Pow(_random.NextDouble(), 5) * 20.0 + 1.0);
            element.Split = _random.Next(2) == 0;
            return element;
        }

        private static int ComputeElementCount(int complexity)
        {
            if (complexity < 10)
            {
                return (complexity + 1) * 1_000;
            }

            int extended = (complexity - 8) * 10_000;
            return Math.Min(extended, 120_000);
        }

        private GridPoint RandomPoint(GridPoint last)
        {
            var offset = s_offsets[_random.Next(s_offsets.Length)];

            int x = last.X + offset.X;
            if (x < 0 || x > GridWidth)
            {
                x -= offset.X * 2;
            }

            int y = last.Y + offset.Y;
            if (y < 0 || y > GridHeight)
            {
                y -= offset.Y * 2;
            }

            return new GridPoint(x, y);
        }

        private enum SegmentKind : byte
        {
            Line,
            Quad,
            Cubic
        }

        private struct Element
        {
            public SegmentKind Kind;
            public GridPoint Start;
            public GridPoint Control1;
            public GridPoint Control2;
            public GridPoint End;
            public uint Color;
            public float Width;
            public bool Split;
        }

        private readonly struct GridPoint
        {
            public GridPoint(int x, int y)
            {
                X = x;
                Y = y;
            }

            public int X { get; }
            public int Y { get; }

            public Point ToPoint(float scale, float offsetX, float offsetY)
            {
                float px = offsetX + (X + 0.5f) * scale;
                float py = offsetY + (Y + 0.5f) * scale;
                return new Point(px, py);
            }
        }

        private readonly struct Point
        {
            public Point(float x, float y)
            {
                X = x;
                Y = y;
            }

            public float X { get; }
            public float Y { get; }
        }
    }
}
