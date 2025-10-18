using System.Collections.Concurrent;
using System.Reflection;

namespace DrawnUi.Draw;

/// <summary>
/// Unicode block categories for optimized font matching.
/// Used to prioritize appropriate fonts based on character type.
/// </summary>
public enum UnicodeBlock
{
    /// <summary>Basic Latin, extended Latin, common punctuation (U+0000-U+024F)</summary>
    Latin,

    /// <summary>Cyrillic script (U+0400-U+04FF)</summary>
    Cyrillic,

    /// <summary>Arabic script (U+0600-U+06FF)</summary>
    Arabic,

    /// <summary>CJK Unified Ideographs - Chinese, Japanese, Korean (U+4E00-U+9FFF)</summary>
    CJK,

    /// <summary>Hangul - Korean alphabet (U+AC00-U+D7AF)</summary>
    Hangul,

    /// <summary>Hiragana and Katakana - Japanese (U+3040-U+30FF)</summary>
    Japanese,

    /// <summary>Currency symbols (U+20A0-U+20CF, U+00A2-U+00A5)</summary>
    Currency,

    /// <summary>Emoji and emoticons (U+1F300-U+1F9FF, U+2600-U+26FF)</summary>
    Emoji,

    /// <summary>Mathematical symbols (U+2200-U+22FF)</summary>
    Math,

    /// <summary>Unknown or unclassified Unicode block</summary>
    Unknown
}

public partial class SkiaFontManager
{
    static SkiaFontManager()
    {
    }

    public bool Initialized { get; set; }

    public static SKTypeface DefaultTypeface
    {
        get { return SKTypeface.CreateDefault(); }
    }

    private void ThrowIfFontNotFound(string filename)
    {
        if (Debugger.IsAttached)
        {
            throw new ApplicationException($"DrawnUI failed to load font {filename}");
        }
    }

    public void Initialize()
    {
        if (!Initialized)
        {
            if (Super.PreloadRegisteredFonts)
            {
                var instance = FontRegistrar as FontRegistrar;
                var type = instance.GetType();
                var fields = type.GetAllHiddenFields();
                var field = fields.First(x => x.Name == "_nativeFonts");
                var fonts = (Dictionary<string, (string Filename, string? Alias)>)field.GetValue(instance);

                foreach (var data in fonts)
                {
                    var file = data.Value.Filename;
                    try
                    {
                        using (Stream fileStream =
                               FileSystem.Current.OpenAppPackageFileAsync(file).GetAwaiter().GetResult())
                        {
                            var font = SKTypeface.FromStream(fileStream);
                            if (font == null)
                            {
                                ThrowIfFontNotFound(file);
                            }

                            Fonts[data.Value.Alias] = font;
                        }
                    }
                    catch (Exception e)
                    {
                        Super.Log(e);
                        ThrowIfFontNotFound(file);
                    }
                }
            }


            Initialized = true;
        }
    }

    public static bool ThrowIfFailedToCreateFont;
    static SKFontManager _SKFontManager;

    public static SKFontManager Manager
    {
        get
        {
            if (_SKFontManager == null)
            {
                _SKFontManager = SKFontManager.CreateDefault();
            }

            return _SKFontManager;
        }
    }

    /// <summary>
    /// Concurrent cache for character-to-typeface mappings.
    /// Uses ConcurrentDictionary for thread-safe access without locks.
    /// Key: Unicode code point (int), Value: SKTypeface or null if no match found.
    /// </summary>
    private static readonly ConcurrentDictionary<int, SKTypeface> _characterTypefaceCache = new();

    /// <summary>
    /// Detects the Unicode block category for a given code point.
    /// This is cross-platform logic used to optimize font matching.
    /// </summary>
    /// <param name="codePoint">Unicode code point</param>
    /// <returns>Unicode block category</returns>
    public static UnicodeBlock DetectUnicodeBlock(int codePoint)
    {
        // Currency symbols (check first as they're common in e-commerce)
        if ((codePoint >= 0x20A0 && codePoint <= 0x20CF) ||  // Currency Symbols block
            (codePoint >= 0x00A2 && codePoint <= 0x00A5))    // Common currency in Latin-1
            return UnicodeBlock.Currency;

        // Basic Latin and Extended Latin
        if (codePoint <= 0x024F)
            return UnicodeBlock.Latin;

        // Cyrillic (Russian, Ukrainian, Kazakh, etc.)
        if (codePoint >= 0x0400 && codePoint <= 0x04FF)
            return UnicodeBlock.Cyrillic;

        // Arabic
        if (codePoint >= 0x0600 && codePoint <= 0x06FF)
            return UnicodeBlock.Arabic;

        // Japanese Hiragana and Katakana
        if (codePoint >= 0x3040 && codePoint <= 0x30FF)
            return UnicodeBlock.Japanese;

        // CJK Unified Ideographs (Chinese, Japanese Kanji, Korean Hanja)
        if (codePoint >= 0x4E00 && codePoint <= 0x9FFF)
            return UnicodeBlock.CJK;

        // Korean Hangul
        if (codePoint >= 0xAC00 && codePoint <= 0xD7AF)
            return UnicodeBlock.Hangul;

        // Mathematical symbols
        if (codePoint >= 0x2200 && codePoint <= 0x22FF)
            return UnicodeBlock.Math;

        // Emoji - Miscellaneous Symbols and Pictographs
        if (codePoint >= 0x2600 && codePoint <= 0x26FF)
            return UnicodeBlock.Emoji;

        // Emoji - Emoticons and extended emoji
        if (codePoint >= 0x1F300 && codePoint <= 0x1F9FF)
            return UnicodeBlock.Emoji;

        // Unknown/Other
        return UnicodeBlock.Unknown;
    }

    /// <summary>
    /// Matches a Unicode character to an appropriate typeface with caching.
    /// This method is thread-safe and optimized for high-frequency calls.
    /// </summary>
    /// <param name="character">Unicode code point to match</param>
    /// <returns>SKTypeface that can render the character, or null if no match found</returns>
    public static SKTypeface MatchCharacter(int character)
    {
        // Fast path: check cache first (lock-free read)
        if (_characterTypefaceCache.TryGetValue(character, out var cachedTypeface))
        {
            return cachedTypeface;
        }

        // Slow path: perform actual matching
        SKTypeface match = null;

#if ANDROID || IOS || MACCATALYST
        // Platform fallback is checked FIRST because:
        // 1. iOS bug: Manager.MatchCharacter returns a default typeface (not null) even when glyph doesn't exist
        // 2. Performance: Platform fonts are faster and more reliable for native symbols
        // 3. Accuracy: Platform fonts have better Unicode coverage (e.g., Kazakh Tenge ₸)

        // Detect Unicode block for optimized font matching
        var unicodeBlock = DetectUnicodeBlock(character);

        match = MatchCharacterWithPlatformFallback(character, unicodeBlock);
        if (match != null)
        {
            return _characterTypefaceCache.GetOrAdd(character, match);
        }
#endif

        // Fallback to SkiaSharp's manager if platform fallback didn't find the glyph
        if (Manager != null)
        {
            match = Manager.MatchCharacter(character);
        }

        // Cache the result (even if null) to avoid repeated lookups
        // GetOrAdd ensures thread-safety and prevents duplicate work
        return _characterTypefaceCache.GetOrAdd(character, match);
    }

    /// <summary>
    /// Clears the character-to-typeface cache.
    /// Call this if fonts are dynamically loaded/unloaded at runtime.
    /// </summary>
    public static void ClearCharacterCache()
    {
        _characterTypefaceCache.Clear();
    }

    /// <summary>
    /// Gets the current size of the character cache for diagnostics.
    /// </summary>
    public static int GetCharacterCacheSize()
    {
        return _characterTypefaceCache.Count;
    }


    static Dictionary<SKTypeface, HashSet<int>> typefaceCoverage = new();
    private static object lockCharMatch = new();


    public static (SKTypeface, int) FindBestTypefaceForString(string text)
    {
        lock (lockCharMatch)
        {
            int symbol = 0;
            foreach (char c in text)
            {
                int codePoint = char.ConvertToUtf32(text, char.IsHighSurrogate(c) ? text.IndexOf(c) : 0);
                var typeface = Manager.MatchCharacter(codePoint);
                if (typeface != null)
                {
                    symbol = codePoint;

                    if (!typefaceCoverage.ContainsKey(typeface))
                    {
                        typefaceCoverage[typeface] = new HashSet<int>();
                    }

                    typefaceCoverage[typeface].Add(codePoint);
                }
            }

            var bestTypeface = typefaceCoverage.OrderByDescending(kvp => kvp.Value.Count).FirstOrDefault().Key;
            return (bestTypeface, symbol);
        }
    }

    public bool CanRender(SKTypeface typeface, int character)
    {
        return typeface.GetGlyphs(new string((char)character, 1))[0] != 0;
    }

    public static List<int> StringToUnicodeValues(string text)
    {
        List<int> codePoints = new List<int>();
        for (int i = 0; i < text.Length; i++)
        {
            int codePoint = char.ConvertToUtf32(text, i);
            codePoints.Add(codePoint);

            // If it's a surrogate pair, skip the next char
            if (char.IsSurrogatePair(text, i))
            {
                i++;
            }
        }

        return codePoints;
    }

#if (!ONPLATFORM)
    public SKTypeface GetFont(string alias)
    {
        throw new NotImplementedException();
    }

#endif

    // Change the type of RegisteredWeights
    static Dictionary<string, List<int>> RegisteredWeights = new(16);

    public static void RegisterWeight(string alias, FontWeight weight)
    {
        // Get the list for the alias (or a new list if it doesn't exist yet)
        List<int> list;
        if (!RegisteredWeights.TryGetValue(alias, out list))
        {
            list = new();
            RegisteredWeights[alias] = list;
        }

        list.Add((int)weight);
    }

    public static string GetRegisteredAlias(string alias, int weight)
    {
        // Check if any weights have been registered for this alias
        if (RegisteredWeights.TryGetValue(alias, out var registeredWeights))
        {
            // Find the closest registered weight
            var closestRegisteredWeight = registeredWeights.OrderBy(w => Math.Abs(w - weight)).First();

            // Get the enum equivalent of the closest registered weight
            var closestWeight = GetWeightEnum(closestRegisteredWeight);

            return GetAlias(alias, closestWeight);
        }

        // If no weights have been registered under this alias, return the alias itself
        return alias;
    }

    public SKTypeface GetFont(string fontFamily, int fontWeight)
    {
        if (string.IsNullOrEmpty(fontFamily))
        {
            return SkiaFontManager.DefaultTypeface;
        }

        var alias = GetRegisteredAlias(fontFamily, fontWeight);
        var font = GetFont(alias);

        //safety check to avoid any chance of crash split_config.arm64_v8a.apk!libSkiaSharp.so (sk_font_set_typeface+60)
        if (font == null)
        {
            return SkiaFontManager.DefaultTypeface;
        }

        return font;
    }

    /// <summary>
    /// Gets the closest enum value to the given weight. Like 590 would return Semibold.
    /// </summary>
    /// <param name="weight"></param>
    /// <returns></returns>
    public static FontWeight GetWeightEnum(int weight)
    {
        FontWeight[] fontWeights = (FontWeight[])Enum.GetValues(typeof(FontWeight));
        var closest = fontWeights.Select(f => new { Value = f, Difference = Math.Abs((int)f - weight) })
            .OrderBy(item => item.Difference)
            .First().Value;

        return closest;
    }

    public static string GetAlias(string alias, FontWeight weight)
    {
        if (!string.IsNullOrEmpty(alias))
            return $"{alias}{weight}";

        return alias;
    }

    public static string GetAlias(string alias, int weight)
    {
        var e = GetWeightEnum(weight);
        return GetAlias(alias, e);
    }

    /// <summary>
    /// Takes the full name of a resource and loads it in to a stream.
    /// </summary>
    /// <param name="resourceName">Assuming an embedded resource is a file
    /// called info.png and is located in a folder called Resources, it
    /// will be compiled in to the assembly with this fully qualified
    /// name: Full.Assembly.Name.Resources.info.png. That is the string
    /// that you should pass to this method.</param>
    /// <returns></returns>
    public static Stream GetEmbeddedResourceStream(string resourceName)
    {
        return Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
    }

    public static Stream GetEmbeddedResourceStream(Assembly assembly, string resourceFileName)
    {
        var resourceNames = assembly.GetManifestResourceNames();

        var resourcePaths = resourceNames
            .Where(x => x.EndsWith(resourceFileName, StringComparison.CurrentCultureIgnoreCase))
            .ToArray();

        if (!resourcePaths.Any())
        {
            throw new Exception(string.Format("Resource ending with {0} not found.", resourceFileName));
        }

        if (resourcePaths.Length > 1)
        {
            resourcePaths = resourcePaths.Where(x => IsFile(x, resourceFileName)).ToArray();
        }

        return assembly.GetManifestResourceStream(resourcePaths.FirstOrDefault());
    }

    static bool IsFile(string path, string file)
    {
        if (!path.EndsWith(file, StringComparison.Ordinal))
            return false;
        return path.Replace(file, "").EndsWith(".", StringComparison.Ordinal);
    }

    /// <summary>
    /// Get the list of all emdedded resources in the assembly.
    /// </summary>
    /// <returns>An array of fully qualified resource names</returns>
    public static string[] GetEmbeddedResourceNames()
    {
        return Assembly.GetExecutingAssembly().GetManifestResourceNames();
    }

    private static SkiaFontManager _instance;

    public static SkiaFontManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new SkiaFontManager();
            }

            return _instance;
        }
    }

    public Dictionary<string, SKTypeface> Fonts { get; set; } = new(128);
    private static IFontRegistrar _registrar;

    public static IFontRegistrar FontRegistrar
    {
        get
        {
            if (_registrar == null)
            {
                _registrar = Super.Services.GetService<IFontRegistrar>();
            }

            return _registrar;
        }
    }

    public SKTypeface GetEmbeededFont(string filename, Assembly assembly, string alias = null)
    {
        if (string.IsNullOrEmpty(alias))
            alias = filename;

        SKTypeface font = null;
        try
        {
            font = Fonts[alias];
        }
        catch (Exception e)
        {
        }

        if (font == null)
        {
            using (var stream = GetEmbeddedResourceStream(assembly, filename))
            {
                if (stream == null)
                    return SkiaFontManager.DefaultTypeface;

                font = SKTypeface.FromStream(stream);
                if (font != null)
                {
                    if (!string.IsNullOrEmpty(alias))
                        Fonts[alias] = font;
                }
            }
        }

        if (font == null)
            font = SkiaFontManager.DefaultTypeface;

        return font;
    }
}
