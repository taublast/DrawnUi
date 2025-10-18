namespace DrawnUi.Draw
{
    public partial class SkiaFontManager
    {
        /// <summary>
        /// Maps Unicode blocks to prioritized Android font lists.
        /// Fonts are tried in order for each block type.
        /// Based on Android AOSP font fallback chain configuration.
        /// </summary>
        private static readonly Dictionary<UnicodeBlock, string[]> AndroidFontsByBlock = new()
        {
            [UnicodeBlock.Latin] = new[]
            {
                "/system/fonts/Roboto-Regular.ttf",
                "/system/fonts/NotoSans-Regular.ttf"
            },

            [UnicodeBlock.Cyrillic] = new[]
            {
                "/system/fonts/Roboto-Regular.ttf",
                "/system/fonts/NotoSans-Regular.ttf"
            },

            [UnicodeBlock.Currency] = new[]
            {
                "/system/fonts/Roboto-Regular.ttf",              // Covers most currency symbols
                "/system/fonts/NotoSans-Regular.ttf",
                "/system/fonts/NotoSansSymbols-Regular-Subsetted2.ttf",
                "/system/fonts/NotoSansSymbols-Regular-Subsetted.ttf"
            },

            [UnicodeBlock.CJK] = new[]
            {
                "/system/fonts/NotoSansCJK-Regular.ttc",         // CJK primary
                "/system/fonts/NotoSans-Regular.ttf"
            },

            [UnicodeBlock.Japanese] = new[]
            {
                "/system/fonts/NotoSansCJK-Regular.ttc",
                "/system/fonts/NotoSans-Regular.ttf"
            },

            [UnicodeBlock.Hangul] = new[]
            {
                "/system/fonts/NotoSansCJK-Regular.ttc",
                "/system/fonts/NotoSans-Regular.ttf"
            },

            [UnicodeBlock.Arabic] = new[]
            {
                "/system/fonts/NotoNaskhArabic-Regular.ttf",
                "/system/fonts/NotoSansArabic-Regular.ttf",
                "/system/fonts/NotoSans-Regular.ttf"
            },

            [UnicodeBlock.Emoji] = new[]
            {
                "/system/fonts/NotoColorEmoji.ttf",
                "/system/fonts/Roboto-Regular.ttf"
            },

            [UnicodeBlock.Math] = new[]
            {
                "/system/fonts/NotoSansSymbols-Regular-Subsetted2.ttf",
                "/system/fonts/NotoSans-Regular.ttf"
            },

            [UnicodeBlock.Unknown] = new[]
            {
                "/system/fonts/Roboto-Regular.ttf",
                "/system/fonts/NotoSans-Regular.ttf",
                "/system/fonts/NotoSerif-Regular.ttf",
                "/system/fonts/NotoSansSymbols-Regular-Subsetted2.ttf",
                "/system/fonts/NotoSansSymbols-Regular-Subsetted.ttf",
                "/system/fonts/NotoColorEmoji.ttf",
                "/system/fonts/NotoSansCJK-Regular.ttc"
            }
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
        public static SKTypeface MatchCharacterWithPlatformFallback(int codePoint, UnicodeBlock unicodeBlock)
        {
            var charString = char.ConvertFromUtf32(codePoint);

            // Get prioritized font list for this Unicode block
            if (!AndroidFontsByBlock.TryGetValue(unicodeBlock, out var prioritizedFonts))
            {
                // Fallback to Unknown block fonts if block not mapped
                prioritizedFonts = AndroidFontsByBlock[UnicodeBlock.Unknown];
            }

            // Try prioritized fonts for this Unicode block
            lock (_androidFontCacheLock)
            {
                foreach (var fontPath in prioritizedFonts)
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
                        var glyphs = systemFont.GetGlyphs(charString);
                        if (glyphs.Length > 0 && glyphs[0] != 0)
                        {
                            return systemFont;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log only errors
                        Trace.WriteLine($"[SKIA] ERROR loading Android font {fontPath}: {ex.Message}");
                    }
                }
            }

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
