global using System.Collections.Specialized;
global using AppoMobi.Gestures;
global using System.ComponentModel;
global using DrawnUi.Draw;
global using System.Numerics;
global using System.Runtime.CompilerServices;
global using System.Runtime.InteropServices;
global using System.Text;
global using System.Windows.Input;
global using AppoMobi.Specials;
global using DrawnUi.Extensions;
global using DrawnUi.Infrastructure;
global using DrawnUi.Infrastructure.Models;
global using DrawnUi.Models;
global using DrawnUi;
global using DrawnUi.Views;
global using SkiaSharp;
global using PointF = System.Drawing.PointF;
global using System.Runtime.InteropServices.JavaScript;
global using Microsoft.Extensions.Logging;
using System.Runtime.Versioning;

namespace DrawnUi.Draw
{
    public partial class Super
    {
        static Super()
        {
            // Forward debounced C# Hot Reload deltas to the public Super.HotReload event.
            DrawnUi.HotReloadService.UpdateApplicationEvent += OnHotReload;
        }

        public static DrawnUiBuilder UseDrawnUi() => new();

        /// <summary>
        /// Raised after a C# Hot Reload metadata update (debounced). Only fires while a debugger
        /// is attached. <c>BrowserHost</c> subscribes to rebuild the drawn scene from the factory.
        /// </summary>
        public static event Action<Type[]> HotReload;

        private static void OnHotReload(Type[] obj) => HotReload?.Invoke(obj);

        public static object App { get; set; }

        public static event EventHandler OnFrame;

        public static float RefreshRate { get; protected set; } = 60;

        /// <summary>
        /// Called by JS requestAnimationFrame to trigger frame updates
        /// </summary>
        [JSExport]
        [SupportedOSPlatform("browser")]
        public static void OnBrowserFrame(double timestamp)
        {
            OnFrame?.Invoke(null, EventArgs.Empty);
        }

        public static void Init()
        {
            Initialized = true;
            Super.Screen.Density = GetDevicePixelRatio();
            RefreshRate = GetDisplayRefreshRate(60);

            var n = new SkiaControl();
            n.Opacity = 1;
        }

        [JSImport("getDevicePixelRatio", "drawnui-web")]
        private static partial double GetDevicePixelRatioJs();

        public static double GetDevicePixelRatio()
        {
            try
            {
                var ratio = GetDevicePixelRatioJs();
                return ratio > 0 ? ratio : 1.0;
            }
            catch
            {
                return 1.0;
            }
        }

        public static float GetDisplayRefreshRate(float fallback) => fallback;

        public static IServiceProvider Services
        {
            get => _services;
            set
            {
                _services = value;
                _servicesFromHandler = value != null;
            }
        }

        public static object AppContext => null;

        public static void DisplayException(View view, Exception e)
        {
            Log(e?.ToString() ?? string.Empty, LogLevel.Error);

            if (view == null)
                throw e;

            view.Update();
        }

        public static void Log(string message, LogLevel logLevel = LogLevel.Warning, [CallerMemberName] string caller = null)
        {
            if (DrawnExtensions.StartupSettings != null)
            {
                DrawnExtensions.StartupSettings.Logger?.Log(logLevel, message);
            }

            Console.WriteLine(message);
        }

        public static void Log(LogLevel level, string message, [CallerMemberName] string caller = null)
        {
            Log(message, level, caller);
        }

        public static void EnsureFrameLoopStarted()
        {
            // Frame loop is driven by JS requestAnimationFrame via OnBrowserFrame
        }

        static partial void OnMaxFpsChanged(int fps)
        {
            // No-op for Web - requestAnimationFrame handles this
        }
    }
}
