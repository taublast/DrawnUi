using System.Collections.Concurrent;
using System.Reflection;

namespace DrawnUi.Draw;

public partial class SkiaFontManager
{
    static SkiaFontManager()
    {
    }

    public bool Initialized { get; set; }

    public static SKTypeface DefaultTypeface
    {
        get
        {
            return SKTypeface.CreateDefault();
        }
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

    static Dictionary<SKTypeface, HashSet<int>>  typefaceCoverage = new();
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
