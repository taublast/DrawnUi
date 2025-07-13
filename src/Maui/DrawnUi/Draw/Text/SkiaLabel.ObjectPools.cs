using System.Collections.Concurrent;
using System.Text;

namespace DrawnUi.Draw
{
    public partial class SkiaLabel
    {
        /// <summary>
        /// Thread-safe object pools for reducing GC allocations in text measurement
        /// </summary>
        public static class ObjectPools
        {
            private const int MaxPoolSize = 32;
            private const int MaxStringBuilderCapacity = 1024;
            private const int MaxListCapacity = 256;

            // Pools for different collection types
            private static readonly ConcurrentQueue<List<LineGlyph>> _lineGlyphListPool = new();
            private static readonly ConcurrentQueue<List<TextLine>> _textLineListPool = new();
            private static readonly ConcurrentQueue<StringBuilder> _stringBuilderPool = new();

            // Pool size tracking to prevent unlimited growth
            private static int _lineGlyphListPoolSize = 0;
            private static int _textLineListPoolSize = 0;
            private static int _stringBuilderPoolSize = 0;

            #region LineGlyph List Pool

            public static List<LineGlyph> GetLineGlyphList()
            {
                if (_lineGlyphListPool.TryDequeue(out var list))
                {
                    Interlocked.Decrement(ref _lineGlyphListPoolSize);
                    list.Clear(); // Ensure it's clean
                    return list;
                }
                return new List<LineGlyph>();
            }

            public static void ReturnLineGlyphList(List<LineGlyph> list)
            {
                if (list == null) return;

                // Don't pool lists that have grown too large to avoid memory bloat
                if (list.Capacity > MaxListCapacity)
                    return;

                // Don't exceed pool size limit
                if (_lineGlyphListPoolSize >= MaxPoolSize)
                    return;

                list.Clear();
                _lineGlyphListPool.Enqueue(list);
                Interlocked.Increment(ref _lineGlyphListPoolSize);
            }

            #endregion

            #region TextLine List Pool

            public static List<TextLine> GetTextLineList()
            {
                if (_textLineListPool.TryDequeue(out var list))
                {
                    Interlocked.Decrement(ref _textLineListPoolSize);
                    list.Clear(); // Ensure it's clean
                    return list;
                }
                return new List<TextLine>();
            }

            public static void ReturnTextLineList(List<TextLine> list)
            {
                if (list == null) return;

                // Don't pool lists that have grown too large to avoid memory bloat
                if (list.Capacity > MaxListCapacity)
                    return;

                // Don't exceed pool size limit
                if (_textLineListPoolSize >= MaxPoolSize)
                    return;

                list.Clear();
                _textLineListPool.Enqueue(list);
                Interlocked.Increment(ref _textLineListPoolSize);
            }

            #endregion

            #region StringBuilder Pool

            public static StringBuilder GetStringBuilder()
            {
                if (_stringBuilderPool.TryDequeue(out var sb))
                {
                    Interlocked.Decrement(ref _stringBuilderPoolSize);
                    sb.Clear(); // Ensure it's clean
                    return sb;
                }
                return new StringBuilder();
            }

            public static void ReturnStringBuilder(StringBuilder sb)
            {
                if (sb == null) return;

                // Don't pool StringBuilders that have grown too large to avoid memory bloat
                if (sb.Capacity > MaxStringBuilderCapacity)
                    return;

                // Don't exceed pool size limit
                if (_stringBuilderPoolSize >= MaxPoolSize)
                    return;

                sb.Clear();
                _stringBuilderPool.Enqueue(sb);
                Interlocked.Increment(ref _stringBuilderPoolSize);
            }

            #endregion

            #region Pool Statistics (for debugging/monitoring)

            public static (int LineGlyphLists, int TextLineLists, int StringBuilders) GetPoolSizes()
            {
                return (_lineGlyphListPoolSize, _textLineListPoolSize, _stringBuilderPoolSize);
            }

            #endregion
        }

        /// <summary>
        /// Helper struct for managing pooled List<LineGlyph> with automatic return
        /// </summary>
        private readonly struct PooledLineGlyphList : IDisposable
        {
            public List<LineGlyph> List { get; }

            public PooledLineGlyphList(List<LineGlyph> list)
            {
                List = list;
            }

            public static PooledLineGlyphList Get()
            {
                return new PooledLineGlyphList(ObjectPools.GetLineGlyphList());
            }

            public void Dispose()
            {
                ObjectPools.ReturnLineGlyphList(List);
            }
        }

        /// <summary>
        /// Helper struct for managing pooled List<TextLine> with automatic return
        /// </summary>
        private readonly struct PooledTextLineList : IDisposable
        {
            public List<TextLine> List { get; }

            public PooledTextLineList(List<TextLine> list)
            {
                List = list;
            }

            public static PooledTextLineList Get()
            {
                return new PooledTextLineList(ObjectPools.GetTextLineList());
            }

            public void Dispose()
            {
                ObjectPools.ReturnTextLineList(List);
            }
        }

        /// <summary>
        /// Helper struct for managing pooled StringBuilder with automatic return
        /// </summary>
        public readonly struct PooledStringBuilder : IDisposable
        {
            public StringBuilder StringBuilder { get; }

            public PooledStringBuilder(StringBuilder sb)
            {
                StringBuilder = sb;
            }

            public static PooledStringBuilder Get()
            {
                return new PooledStringBuilder(ObjectPools.GetStringBuilder());
            }

            public void Dispose()
            {
                ObjectPools.ReturnStringBuilder(StringBuilder);
            }
        }
    }
}
