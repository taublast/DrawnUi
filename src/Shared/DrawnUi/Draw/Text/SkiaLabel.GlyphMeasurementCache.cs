namespace DrawnUi.Draw
{
    public partial class SkiaLabel
    {
        private static class GlyphMeasurementCache
        {
            private const int MaxCacheSize = 2000;

            /// <summary>
            /// A measured width is only valid for the exact font geometry it was measured with:
            /// pixel size, skew, horizontal scale and the label's character spacing all change
            /// it. Keying on typeface + text alone handed a 13px width to a 12px label of the
            /// same family and clipped its last glyph (found 2026-09-07).
            /// </summary>
            private struct CacheKey : IEquatable<CacheKey>
            {
                public string TypefaceFamilyName;
                public SKFontStyle TypefaceStyle;
                public bool NeedsShaping;
                public string Text;
                public float Size;
                public float SkewX;
                public float ScaleX;
                public float Spacing;

                public bool Equals(CacheKey other) =>
                    TypefaceFamilyName == other.TypefaceFamilyName &&
                    TypefaceStyle.Equals(other.TypefaceStyle) &&
                    NeedsShaping == other.NeedsShaping &&
                    Size == other.Size &&
                    SkewX == other.SkewX &&
                    ScaleX == other.ScaleX &&
                    Spacing == other.Spacing &&
                    Text == other.Text;

                public override bool Equals(object obj) => obj is CacheKey ck && Equals(ck);

                public override int GetHashCode()
                {
                    unchecked
                    {
                        int hash = 17;
                        hash = hash * 23 + (TypefaceFamilyName?.GetHashCode() ?? 0);
                        hash = hash * 23 + TypefaceStyle.GetHashCode();
                        hash = hash * 23 + NeedsShaping.GetHashCode();
                        hash = hash * 23 + Size.GetHashCode();
                        hash = hash * 23 + SkewX.GetHashCode();
                        hash = hash * 23 + ScaleX.GetHashCode();
                        hash = hash * 23 + Spacing.GetHashCode();
                        hash = hash * 23 + (Text?.GetHashCode() ?? 0);
                        return hash;
                    }
                }
            }

            private static readonly Dictionary<CacheKey, LinkedListNode<(CacheKey Key, float Width, LineGlyph[] Glyphs)>> _cache
                = new Dictionary<CacheKey, LinkedListNode<(CacheKey, float, LineGlyph[])>>();

            private static readonly LinkedList<(CacheKey Key, float Width, LineGlyph[] Glyphs)> _lruList
                = new LinkedList<(CacheKey, float, LineGlyph[])>();

            // Optional: Add a lock object for thread safety
            private static readonly object _lock = new object();

            private static CacheKey MakeKey(SKTypeface typeface, SKFont font, bool needsShaping, float spacing, string text) => new()
            {
                TypefaceFamilyName = typeface.FamilyName,
                TypefaceStyle = typeface.FontStyle,
                NeedsShaping = needsShaping,
                Text = text,
                Size = font.Size,
                SkewX = font.SkewX,
                ScaleX = font.ScaleX,
                Spacing = spacing,
            };

            /// <param name="spacing">Extra pixels the label adds between glyphs (character spacing at the current scale).</param>
            public static bool TryGetValue(SKTypeface typeface, SKFont font, bool needsShaping, float spacing, string text, out (float Width, LineGlyph[] Glyphs) result)
            {
                var key = MakeKey(typeface, font, needsShaping, spacing, text);

                lock (_lock)
                {
                    if (_cache.TryGetValue(key, out var node))
                    {
                        // Move to end for LRU
                        _lruList.Remove(node);
                        _lruList.AddLast(node);
                        result = (node.Value.Width, node.Value.Glyphs);
                        return true;
                    }
                }

                result = default;
                return false;
            }

            public static void Add(SKTypeface typeface, SKFont font, bool needsShaping, float spacing, string text, float width, LineGlyph[] glyphs)
            {
                var key = MakeKey(typeface, font, needsShaping, spacing, text);

                lock (_lock)
                {
                    if (_cache.TryGetValue(key, out var existingNode))
                    {
                        // Update existing and move to end
                        _lruList.Remove(existingNode);
                    }
                    else if (_cache.Count >= MaxCacheSize)
                    {
                        // Evict oldest
                        var oldest = _lruList.First;
                        if (oldest != null)
                        {
                            _cache.Remove(oldest.Value.Key);
                            _lruList.RemoveFirst();
                        }
                    }

                    var newNode = new LinkedListNode<(CacheKey, float, LineGlyph[])>((key, width, glyphs));
                    _lruList.AddLast(newNode);
                    _cache[key] = newNode;
                }
            }
        }
    }


}
