using System.Runtime.InteropServices;
using SkiaSharp;

namespace Sandbox
{
    internal sealed class MotionMarkSceneOptimized : IDisposable
    {
        private const int GridWidth = 80;
        private const int GridHeight = 40;

        private static readonly SKColor[] s_palette =
        [
            new SKColor(0x10, 0x10, 0x10),
            new SKColor(0x80, 0x80, 0x80),
            new SKColor(0xC0, 0xC0, 0xC0),
            new SKColor(0x10, 0x10, 0x10),
            new SKColor(0x80, 0x80, 0x80),
            new SKColor(0xC0, 0xC0, 0xC0),
            new SKColor(0xE0, 0x10, 0x40),
        ];

        private static readonly (int X, int Y)[] s_offsets =
        [
            (-4, 0), (2, 0), (1, -2), (1, 2),
        ];

        // CACHED PATHS PER STYLE
        private readonly Dictionary<(SKColor Color, float Width), SKPath> _pathCache = new();

        // PAINTS
        private readonly SKPaint _strokePaint = new()
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
        };

        private readonly SKPaint _backgroundPaint = new()
        {
            Color = new SKColor(12, 16, 24),
            Style = SKPaintStyle.Fill
        };

        // DATA
        private readonly List<Element> _elements = new();
        private readonly Random _random = new();
        private GridPoint _lastGridPoint = new(GridWidth / 2, GridHeight / 2);
        private int _complexity = 8;

        // CACHING
        private SKPicture? _cachedPicture;
        private int _lastComplexity = -1;
        private float _lastScale = -1f;

        private bool _disposed;

        public int Complexity => _complexity;
        public int ElementCount => _elements.Count;

        public void SetComplexity(int complexity)
        {
            complexity = Math.Clamp(complexity, 0, 24);
            if (_complexity == complexity) return;

            _complexity = complexity;
            _lastComplexity = -1; // FORCE RE-RECORD
            Resize(ComputeElementCount(_complexity));
        }

        public void Render(SKCanvas canvas, float viewWidth, float viewHeight)
        {
            Resize(ComputeElementCount(_complexity));
            if (_elements.Count == 0) return;

            float scaleX = viewWidth / (GridWidth + 1);
            float scaleY = viewHeight / (GridHeight + 1);
            float uniformScale = MathF.Min(scaleX, scaleY);
            float offsetX = (viewWidth - uniformScale * (GridWidth + 1)) * 0.5f;
            float offsetY = (viewHeight - uniformScale * (GridHeight + 1)) * 0.5f;

            if (_cachedPicture == null ||
                _complexity != _lastComplexity ||
                MathF.Abs(uniformScale - _lastScale) > 0.001f)
            {
                RecordPicture(uniformScale, offsetX, offsetY);
                _lastComplexity = _complexity;
                _lastScale = uniformScale;
            }

            canvas.DrawRect(0, 0, viewWidth, viewHeight, _backgroundPaint);
            canvas.DrawPicture(_cachedPicture!);
        }

        private void RecordPicture(float uniformScale, float offsetX, float offsetY)
        {
            _cachedPicture?.Dispose();

            using var recorder = new SKPictureRecorder();
            var recordingCanvas = recorder.BeginRecording(new SKRect(0, 0, 10000, 10000));

            Span<Element> elements = CollectionsMarshal.AsSpan(_elements);
            SKPath? currentPath = null;
            (SKColor Color, float Width)? currentStyle = null;

            for (int i = 0; i < elements.Length; i++)
            {
                ref Element el = ref elements[i];
                var style = (el.Color, el.Width);

                if (currentPath == null || currentStyle != style)
                {
                    if (currentPath != null)
                    {
                        _strokePaint.Color = currentStyle.Value.Color;
                        _strokePaint.StrokeWidth = currentStyle.Value.Width;
                        recordingCanvas.DrawPath(currentPath, _strokePaint);
                        currentPath.Reset();
                    }

                    currentPath = GetOrCreatePath(style.Color, style.Width);
                    currentStyle = style;

                    SKPoint start = el.Start.ToPoint(uniformScale, offsetX, offsetY);
                    currentPath.MoveTo(start);
                }

                SKPoint end = el.End.ToPoint(uniformScale, offsetX, offsetY);

                switch (el.Kind)
                {
                    case SegmentKind.Line:
                        currentPath.LineTo(end);
                        break;

                    case SegmentKind.Quad:
                    {
                        SKPoint ctrl1 = el.Control1.ToPoint(uniformScale, offsetX, offsetY);
                        currentPath.QuadTo(ctrl1, end);
                        break;
                    }

                    case SegmentKind.Cubic:
                    {
                        SKPoint ctrl1 = el.Control1.ToPoint(uniformScale, offsetX, offsetY);
                        SKPoint ctrl2 = el.Control2.ToPoint(uniformScale, offsetX, offsetY);
                        currentPath.CubicTo(ctrl1, ctrl2, end);
                        break;
                    }
                }

 
            }

            if (currentPath != null && currentStyle != null)
            {
                _strokePaint.Color = currentStyle.Value.Color;
                _strokePaint.StrokeWidth = currentStyle.Value.Width;
                recordingCanvas.DrawPath(currentPath, _strokePaint);
            }

            _cachedPicture = recorder.EndRecording();
        }


        private SKPath GetOrCreatePath(SKColor color, float width)
        {
            var key = (color, width);
            if (_pathCache.TryGetValue(key, out var path))
                return path;

            path = new SKPath();
            _pathCache[key] = path;
            return path;
        }

        public void Dispose()
        {
            if (_disposed) return;

            _cachedPicture?.Dispose();
            _strokePaint.Dispose();
            _backgroundPaint.Dispose();

            foreach (var path in _pathCache.Values)
                path.Dispose();
            _pathCache.Clear();

            _disposed = true;
        }

        private void Resize(int count)
        {
            int current = _elements.Count;
            if (count == current) return;

            if (count < current)
            {
                _elements.RemoveRange(count, current - count);
                _lastGridPoint = count > 0 ? _elements[^1].End : new GridPoint(GridWidth / 2, GridHeight / 2);
                return;
            }

            _elements.Capacity = Math.Max(_elements.Capacity, count);
            _lastGridPoint = current == 0 ? new(GridWidth / 2, GridHeight / 2) : _elements[^1].End;

            for (int i = current; i < count; i++)
            {
                var el = CreateRandomElement(_lastGridPoint);
                _elements.Add(el);
                _lastGridPoint = el.End;

                // TOGGLE SPLIT EVERY 100 ELEMENTS (rare)
                if (i % 100 == 0 && _elements.Count > 10)
                {
                    int idx = _random.Next(_elements.Count);
                    _elements[idx] = _elements[idx] with { Split = !_elements[idx].Split };
                }
            }

            _lastComplexity = -1; // Force re-record
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
                return (complexity + 1) * 1_000;
            int extended = (complexity - 8) * 10_000;
            return Math.Min(extended, 120_000);
        }

        private GridPoint RandomPoint(GridPoint last)
        {
            var offset = s_offsets[_random.Next(s_offsets.Length)];
            int x = last.X + offset.X;
            if (x < 0 || x > GridWidth) x -= offset.X * 2;
            int y = last.Y + offset.Y;
            if (y < 0 || y > GridHeight) y -= offset.Y * 2;
            return new GridPoint(x, y);
        }

        private enum SegmentKind : byte { Line, Quad, Cubic }

        private struct Element
        {
            public SegmentKind Kind;
            public GridPoint Start, Control1, Control2, End;
            public SKColor Color;
            public float Width;
            public bool Split;
        }

        private readonly struct GridPoint
        {
            public GridPoint(int x, int y) { X = x; Y = y; }
            public int X { get; }
            public int Y { get; }
            public SKPoint ToPoint(float scale, float offsetX, float offsetY) =>
                new(offsetX + (X + 0.5f) * scale, offsetY + (Y + 0.5f) * scale);
        }
    }
}
