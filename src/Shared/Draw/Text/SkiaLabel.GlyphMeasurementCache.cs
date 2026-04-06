namespace DrawnUi.Draw
{
    public partial class SkiaLabel
    {
        private static class GlyphMeasurementCache
        {
            private const int MaxCacheSize = 2000;

            private struct CacheKey : IEquatable<CacheKey>
            {
                public string TypefaceFamilyName;
                public SKFontStyle TypefaceStyle;
                public bool NeedsShaping;
                public string Text;

                public bool Equals(CacheKey other) =>
                    TypefaceFamilyName == other.TypefaceFamilyName &&
                    TypefaceStyle.Equals(other.TypefaceStyle) &&
                    NeedsShaping == other.NeedsShaping &&
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

            public static bool TryGetValue(SKTypeface typeface, bool needsShaping, string text, out (float Width, LineGlyph[] Glyphs) result)
            {
                var key = new CacheKey
                {
                    TypefaceFamilyName = typeface.FamilyName,
                    TypefaceStyle = typeface.FontStyle,
                    NeedsShaping = needsShaping,
                    Text = text
                };

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

            public static void Add(SKTypeface typeface, bool needsShaping, string text, float width, LineGlyph[] glyphs)
            {
                var key = new CacheKey
                {
                    TypefaceFamilyName = typeface.FamilyName,
                    TypefaceStyle = typeface.FontStyle,
                    NeedsShaping = needsShaping,
                    Text = text
                };

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
