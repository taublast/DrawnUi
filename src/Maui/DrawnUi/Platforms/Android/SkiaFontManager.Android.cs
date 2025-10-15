namespace DrawnUi.Draw
{
    public partial class SkiaFontManager
    {
        /// <summary>
        /// Android system fonts optimized for Unicode coverage and symbol fallback.
        /// Ordered by priority: Roboto covers most common symbols, then NotoSans for extended Unicode,
        /// then NotoSansSymbols for specific symbol ranges.
        /// These fonts are loaded lazily only when character matching fails with user fonts.
        /// Based on Android AOSP font fallback chain configuration.
        /// </summary>
        private static readonly string[] AndroidSystemFontFallbacks = new[]
        {
            "/system/fonts/Roboto-Regular.ttf",              // Main system font, covers most currency symbols including ₽
            "/system/fonts/NotoSans-Regular.ttf",            // Primary fallback for extended Unicode
            "/system/fonts/NotoSerif-Regular.ttf",           // Serif fallback
            "/system/fonts/NotoSansSymbols-Regular-Subsetted2.ttf", // Symbol-specific font (und-Zsym)
            "/system/fonts/NotoSansSymbols-Regular-Subsetted.ttf",  // Older Android versions
            "/system/fonts/NotoColorEmoji.ttf",              // Emoji support
            "/system/fonts/NotoSansCJK-Regular.ttc",         // CJK support
        };

        /// <summary>
        /// Cache for Android system fonts, loaded lazily to avoid startup performance impact.
        /// Only fonts that are actually needed for character matching are loaded.
        /// </summary>
        private static readonly Dictionary<string, SKTypeface> _androidSystemFontCache = new Dictionary<string, SKTypeface>();
        private static readonly object _androidFontCacheLock = new object();

        /// <summary>
        /// Android-specific implementation of platform font fallback.
        /// This addresses the issue where native Android TextView can display glyphs
        /// that SkiaSharp's font manager cannot find by directly accessing Android system fonts.
        /// </summary>
        public static SKTypeface MatchCharacterWithPlatformFallback(int codePoint)
        {
            // First try SkiaSharp's built-in matching
            var typeface = Manager.MatchCharacter(codePoint);
            if (typeface != null)
            {
                // Verify the matched font actually contains the glyph
                var glyphs = typeface.GetGlyphs(char.ConvertFromUtf32(codePoint));
                if (glyphs.Length > 0 && glyphs[0] != 0)
                {
                    return typeface;
                }
            }

            // If built-in matching failed, try Android system fonts
            lock (_androidFontCacheLock)
            {
                foreach (var fontPath in AndroidSystemFontFallbacks)
                {
                    try
                    {
                        // Check cache first
                        if (!_androidSystemFontCache.TryGetValue(fontPath, out var systemFont))
                        {
                            // Verify file exists before attempting to load
                            if (!File.Exists(fontPath))
                                continue;

                            // Lazy load: only load when needed
                            systemFont = SKTypeface.FromFile(fontPath);
                            if (systemFont == null)
                                continue;

                            // Cache the loaded font for future use
                            _androidSystemFontCache[fontPath] = systemFont;
                        }

                        // Verify this font contains the glyph
                        var glyphs = systemFont.GetGlyphs(char.ConvertFromUtf32(codePoint));
                        if (glyphs.Length > 0 && glyphs[0] != 0)
                        {
                            return systemFont;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log but continue trying other fonts
                        Trace.WriteLine($"[SKIA] Failed to load Android system font {fontPath}: {ex.Message}");
                    }
                }
            }

            // If no system font worked, return null (caller will handle fallback)
            return null;
        }

        public SKTypeface GetFont(string alias)
        {

            if (Fonts.TryGetValue(alias, out var existing))
            {
                return existing;
            }

            var font = SKTypeface.FromFamilyName(alias);
            if (font == null)
            {
                try
                {
                    var registrar = FontRegistrar;
                    var realName = registrar.GetFont(alias);
                    if (!string.IsNullOrEmpty(realName))
                    {
                        using (Stream fileStream = FileSystem.Current.OpenAppPackageFileAsync(realName).GetAwaiter().GetResult())
                        {
                            font = SKTypeface.FromStream(fileStream);
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

            }

            if (font == null)
            {
                if (ThrowIfFailedToCreateFont)
                {
                    throw new Exception($"[SKIA] Couldn't create font {alias}");
                }

                font = SkiaFontManager.DefaultTypeface;

                Trace.WriteLine($"[SKIA] Couldn't create font {alias}");
            }
            else
            {
                if (!string.IsNullOrEmpty(alias))
                    Fonts[alias] = font;
            }

            return font;
        }

    }
}
