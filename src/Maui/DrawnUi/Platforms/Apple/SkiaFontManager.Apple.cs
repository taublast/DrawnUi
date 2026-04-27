using System.Diagnostics;

namespace DrawnUi.Draw
{
    public partial class SkiaFontManager
    {
        /// <summary>
        /// Maps Unicode blocks to prioritized iOS font lists.
        /// Fonts are tried in order for each block type.
        /// </summary>
        private static readonly Dictionary<UnicodeBlock, string[]> iOSFontsByBlock = new()
        {
            [UnicodeBlock.Latin] = new[]
            {
                ".SF UI Text",
                "Helvetica Neue",
                "Helvetica"
            },

            [UnicodeBlock.Cyrillic] = new[]
            {
                ".SF UI Text",
                "Helvetica Neue",
                "Helvetica"
            },

            [UnicodeBlock.Currency] = new[]
            {
                "Helvetica Neue",      // Best Unicode coverage for currency
                "Arial Unicode MS",    // Comprehensive fallback
                "Helvetica",
                ".SF UI Text"
            },

            [UnicodeBlock.CJK] = new[]
            {
                "PingFang SC",         // Simplified Chinese (primary)
                "PingFang TC",         // Traditional Chinese
                "Hiragino Sans",       // Japanese Kanji
                "Helvetica Neue"
            },

            [UnicodeBlock.Japanese] = new[]
            {
                "Hiragino Sans",       // Japanese primary
                "PingFang SC",         // CJK fallback
                "Helvetica Neue"
            },

            [UnicodeBlock.Hangul] = new[]
            {
                "Apple SD Gothic Neo", // Korean primary
                "PingFang SC",         // CJK fallback
                "Helvetica Neue"
            },

            [UnicodeBlock.Arabic] = new[]
            {
                "Geeza Pro",           // Arabic primary
                "Helvetica Neue"
            },

            [UnicodeBlock.Emoji] = new[]
            {
                "Apple Color Emoji",   // Emoji primary
                "Helvetica Neue"
            },

            [UnicodeBlock.Math] = new[]
            {
                "Helvetica Neue",
                "Arial Unicode MS"
            },

            [UnicodeBlock.Unknown] = new[]
            {
                "Helvetica Neue",      // Best general Unicode coverage
                "Arial Unicode MS",
                ".SF UI Text",
                "PingFang SC",
                "Hiragino Sans",
                "Apple SD Gothic Neo",
                "Geeza Pro",
                "Apple Color Emoji"
            }
        };

        private static readonly Dictionary<string, SKTypeface> _iOSSystemFontCache = new Dictionary<string, SKTypeface>();
        private static readonly object _iOSFontCacheLock = new object();

        public static SKTypeface MatchCharacterWithPlatformFallback(int codePoint, UnicodeBlock unicodeBlock)
        {
            var charString = char.ConvertFromUtf32(codePoint);

            // NOTE: This is called BEFORE SkiaSharp's Manager.MatchCharacter because of iOS bug:
            // Manager.MatchCharacter returns a default typeface (not null) even when the glyph doesn't exist,
            // making it impossible to detect missing glyphs. Platform fonts are checked first to avoid this.

            // Get prioritized font list for this Unicode block
            if (!iOSFontsByBlock.TryGetValue(unicodeBlock, out var prioritizedFonts))
            {
                // Fallback to Unknown block fonts if block not mapped
                prioritizedFonts = iOSFontsByBlock[UnicodeBlock.Unknown];
            }

            // Try prioritized fonts for this Unicode block
            lock (_iOSFontCacheLock)
            {
                foreach (var fontName in prioritizedFonts)
                {
                    try
                    {
                        SKTypeface systemFont = null;

                        if (!_iOSSystemFontCache.TryGetValue(fontName, out systemFont))
                        {
                            // Try loading with the exact name first (iOS system fonts often start with ".")
                            systemFont = SKTypeface.FromFamilyName(fontName);

                            // If that fails and name starts with ".", try without the dot
                            if (systemFont == null && fontName.StartsWith("."))
                            {
                                var nameWithoutDot = fontName.Substring(1);
                                systemFont = SKTypeface.FromFamilyName(nameWithoutDot);
                            }

                            if (systemFont == null)
                                continue;

                            _iOSSystemFontCache[fontName] = systemFont;
                        }

                        if (systemFont != null)
                        {
                            var glyphs = systemFont.GetGlyphs(charString);
                            if (glyphs.Length > 0 && glyphs[0] != 0)
                            {
                                return systemFont;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log only errors
                        Debug.WriteLine($"[SKIA] ERROR loading iOS font {fontName}: {ex.Message}");
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
            if (font == null || font.FamilyName != alias)
            {
                try
                {
                    var instance = FontRegistrar as FontRegistrar;
                    var type = instance.GetType();
                    var fields = type.GetAllHiddenFields();
                    var field = fields.First(x => x.Name == "_nativeFonts");
                    var fonts = (Dictionary<string, (string Filename, string? Alias)>)field.GetValue(instance);

                    var registered = fonts.Values.FirstOrDefault(x =>
                        x.Filename == alias
                        || x.Alias == alias);

                    using (Stream fileStream = FileSystem.Current.OpenAppPackageFileAsync(registered.Filename).GetAwaiter().GetResult())
                    {
                        font = SKTypeface.FromStream(fileStream);
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                }
            }


            if (font == null)
            {
                if (ThrowIfFailedToCreateFont)
                {
                    throw new Exception($"[SKIA] Couldn't create font {alias}");
                }

                font = DefaultTypeface;
                Debug.WriteLine($"[SKIA] Couldn't create font {alias}");
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
