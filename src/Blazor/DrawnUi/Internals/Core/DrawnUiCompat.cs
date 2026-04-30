using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

namespace Microsoft.Maui
{
    public readonly struct Font
    {
    }

    public interface IText
    {
    }

    public interface IFontRegistrar
    {
    }

    public static class ServiceProviderServiceExtensions
    {
        public static T? GetService<T>(this IServiceProvider services)
        {
            return (T?)services?.GetService(typeof(T));
        }
    }
}

namespace DrawnUi.Draw
{
    [Flags]
    public enum FontAttributes
    {
        None = 0,
        Bold = 1,
        Italic = 2
    }

    public enum TextAlignment
    {
        Start,
        Center,
        End
    }

    public enum LineBreakMode
    {
        NoWrap,
        WordWrap,
        CharacterWrap,
        HeadTruncation,
        MiddleTruncation,
        TailTruncation
    }

    public class FormattedString
    {
    }

    public class PropertyChangingEventArgs : global::System.ComponentModel.PropertyChangingEventArgs
    {
        public PropertyChangingEventArgs(string? propertyName) : base(propertyName)
        {
        }
    }
}

namespace DrawnUi.Draw
{
    public class DrawnFontAttributesConverter : TypeConverter
    {
    }

    public class FontSizeConverter : TypeConverter
    {
    }

    public enum HardwareAccelerationMode
    {
        Disabled,
        Prerender,
        Enabled
    }


    public static class AddGestures
    {
        public static IDictionary<SkiaControl, GestureListener> AttachedListeners { get; } = new Dictionary<SkiaControl, GestureListener>();

        public sealed class GestureListener : ISkiaGestureListener
        {
            public bool InputTransparent => false;

            public bool LockFocus => false;

            public bool BlockGesturesBelow => false;

            public bool CanDraw => true;

            public string Tag => nameof(GestureListener);

            public Guid Uid { get; } = Guid.NewGuid();

            public int ZIndex => 0;

            public DateTime? GestureListenerRegistrationTime { get; set; }

            public ISkiaGestureListener OnSkiaGestureEvent(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
            {
                return null;
            }

            public bool OnFocusChanged(bool focus)
            {
                return false;
            }

            public bool HitIsInside(float x, float y)
            {
                return false;
            }
        }
    }

    public static partial class ColorExtensions
    {
        public static DrawnUi.Color MakeDarker(this DrawnUi.Color color, double percent)
        {
            var factor = Math.Clamp(1.0 - percent / 100.0, 0.0, 1.0);
            return new DrawnUi.Color(
                color.Red * (float)factor,
                color.Green * (float)factor,
                color.Blue * (float)factor,
                color.Alpha);
        }

        public static DrawnUi.Color MakeLighter(this DrawnUi.Color color, double percent)
        {
            float Lerp(float channel) => channel + (1f - channel) * (float)Math.Clamp(percent / 100.0, 0.0, 1.0);

            return new DrawnUi.Color(
                Lerp(color.Red),
                Lerp(color.Green),
                Lerp(color.Blue),
                color.Alpha);
        }
    }

    public static class TouchEffect
    {
        public static float Density { get; set; } = 1f;

        public static float TappedCancelMoveThresholdPoints { get; set; } = 16f;

        public static bool LogEnabled { get; set; }

        public static void CloseKeyboard()
        {
        }
    }

    public static class DrawnExtensions
    {
        public static DrawnUiStartupSettings StartupSettings { get; set; }

        public static async Task<WebAssemblyHost> UseDrawnUiAsync(this WebAssemblyHostBuilder builder,
            DrawnUiStartupSettings settings = null,
            CancellationToken cancellationToken = default)
        {
            StartupSettings = settings;

            var host = builder.Build();

            Super.Services = host.Services;

            await SkiaFontManager.Instance.InitializeAsync(host.Services, cancellationToken);
            await SkiaImageManager.Instance.InitializeAsync(host.Services, cancellationToken);

            Super.Init();

            if (settings?.UseDesktopKeyboard == true)
            {
                var jsRuntime = host.Services.GetService<IJSRuntime>();
                if (jsRuntime != null)
                {
                    await KeyboardManager.AttachToKeyboardAsync(jsRuntime);
                }
            }

            return host;
        }

        public static void RegisterFont(string alias, string sourceUrl)
        {
            SkiaFontManager.Instance.RegisterFont(alias, sourceUrl);
        }

        public static void RegisterFont(string family, FontWeight weight, string sourceUrl)
        {
            SkiaFontManager.Instance.RegisterFont(family, weight, sourceUrl);
        }

        public static void RegisterImage(string sourceUrl)
        {
            SkiaImageManager.Instance.RegisterImage(sourceUrl);
        }

        public static void RegisterImage(string alias, string sourceUrl)
        {
            SkiaImageManager.Instance.RegisterImage(alias, sourceUrl);
        }

        public static bool IsFinite(float value)
        {
            return float.IsFinite(value);
        }

        public static bool IsFinite(double value)
        {
            return double.IsFinite(value);
        }
    }

    public sealed class SkiaFontManager
    {
        public static SkiaFontManager Instance { get; } = new();

        public static SKTypeface DefaultTypeface => SKTypeface.CreateDefault();

        private readonly Dictionary<string, SKTypeface> _fonts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _fontSources = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<int>> _registeredWeights = new(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _loadSemaphore = new(1, 1);
        private static SKFontManager _manager;

        public bool Initialized { get; private set; }

        public static SKFontManager Manager => _manager ??= SKFontManager.CreateDefault();

        public void RegisterFont(string alias, string sourceUrl)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                throw new ArgumentException("Font alias cannot be empty.", nameof(alias));
            }

            if (string.IsNullOrWhiteSpace(sourceUrl))
            {
                throw new ArgumentException("Font source URL cannot be empty.", nameof(sourceUrl));
            }

            _fontSources[alias] = sourceUrl;
        }

        public void RegisterFont(string family, FontWeight weight, string sourceUrl)
        {
            RegisterWeight(family, weight);
            RegisterFont(GetAlias(family, weight), sourceUrl);
        }

        public void Initialize()
        {
        }

        public async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
        {
            if (_fontSources.Count == 0)
            {
                Initialized = true;
                return;
            }

            await _loadSemaphore.WaitAsync(cancellationToken);
            try
            {
                var httpClient = services?.GetService(typeof(HttpClient)) as HttpClient;
                if (httpClient == null)
                {
                    Super.Log("[DRAWNUI] Blazor font preload skipped: HttpClient service was not found.", Microsoft.Extensions.Logging.LogLevel.Warning);
                    return;
                }

                foreach (var source in _fontSources)
                {
                    if (_fonts.ContainsKey(source.Key))
                    {
                        continue;
                    }

                    try
                    {
                        var bytes = await httpClient.GetByteArrayAsync(source.Value, cancellationToken);
                        using var data = SKData.CreateCopy(bytes);
                        var typeface = SKTypeface.FromData(data);
                        if (typeface != null)
                        {
                            _fonts[source.Key] = typeface;
                        }
                        else
                        {
                            Super.Log($"[DRAWNUI] Blazor font preload failed for {source.Key} from {source.Value}", Microsoft.Extensions.Logging.LogLevel.Warning);
                        }
                    }
                    catch (Exception e)
                    {
                        Super.Log(e);
                    }
                }

                Initialized = true;
            }
            finally
            {
                _loadSemaphore.Release();
            }
        }

        public SKTypeface GetFont(string alias)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                return DefaultTypeface;
            }

            if (_fonts.TryGetValue(alias, out var registeredFont))
            {
                return registeredFont;
            }

            var systemFont = SKTypeface.FromFamilyName(alias);
            if (systemFont != null)
            {
                return systemFont;
            }

            if (_fontSources.ContainsKey(alias) && !Initialized)
            {
                Super.Log($"[DRAWNUI] Blazor font '{alias}' requested before UseDrawnUiAsync finished preloading registered fonts.", Microsoft.Extensions.Logging.LogLevel.Warning);
            }

            return DefaultTypeface;
        }

        public SKTypeface GetFont(string family, int weight)
        {
            if (string.IsNullOrWhiteSpace(family))
            {
                return DefaultTypeface;
            }

            var weightedAlias = GetRegisteredAlias(family, weight);

            var font = GetFont(weightedAlias);
            if (font != SKTypeface.Default)
            {
                return font;
            }

            if (!string.Equals(weightedAlias, family, StringComparison.OrdinalIgnoreCase))
            {
                font = GetFont(family);
                if (font != SKTypeface.Default)
                {
                    return font;
                }
            }

            return DefaultTypeface;
        }

        public static SKTypeface MatchCharacter(int symbol)
        {
            var text = char.ConvertFromUtf32(symbol);
            foreach (var typeface in Instance._fonts.Values)
            {
                var glyphs = typeface?.GetGlyphs(text);
                if (glyphs != null && glyphs.Any(glyph => glyph != 0))
                {
                    return typeface;
                }
            }

            return Manager.MatchCharacter(symbol) ?? DefaultTypeface;
        }

        public static void RegisterWeight(string alias, FontWeight weight)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                return;
            }

            if (!Instance._registeredWeights.TryGetValue(alias, out var list))
            {
                list = new List<int>();
                Instance._registeredWeights[alias] = list;
            }

            var value = (int)weight;
            if (!list.Contains(value))
            {
                list.Add(value);
            }
        }

        public static string GetRegisteredAlias(string alias, int weight)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                return alias;
            }

            if (Instance._registeredWeights.TryGetValue(alias, out var registeredWeights) && registeredWeights.Count > 0)
            {
                var closestRegisteredWeight = registeredWeights.OrderBy(value => Math.Abs(value - weight)).First();
                return GetAlias(alias, GetWeightEnum(closestRegisteredWeight));
            }

            return alias;
        }

        public static FontWeight GetWeightEnum(int weight)
        {
            var fontWeights = (FontWeight[])Enum.GetValues(typeof(FontWeight));
            return fontWeights
                .Select(value => new { Value = value, Difference = Math.Abs((int)value - weight) })
                .OrderBy(item => item.Difference)
                .First()
                .Value;
        }

        public static string GetAlias(string alias, FontWeight weight)
        {
            return string.IsNullOrEmpty(alias) ? alias : $"{alias}{weight}";
        }
    }
}

namespace DrawnUi.Blazor.Views
{
    public sealed class App
    {
        public static App Current { get; } = new();

        public Task CallJSAsync(string identifier, object arg1, bool arg2)
        {
            return Task.CompletedTask;
        }
    }
}

namespace DrawnUi.Views
{
    public partial class Canvas
    {
        public SkiaControl HasHover { get; set; }
    }
}

 

namespace DrawnUi.Models
{
    public partial class Screen
    {
        public static DisplayInfo DisplayInfo => DeviceDisplay.Current.MainDisplayInfo;
    }
}

namespace DrawnUi.Extensions
{
    public static class SkiaCompatExtensions
    {
        public static DrawnUi.Rect ToMauiRectangle(this SKRect rect)
        {
            return new DrawnUi.Rect(rect.Left, rect.Top, rect.Width, rect.Height);
        }
    }
}
