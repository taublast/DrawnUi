global using System.Collections.Specialized;
global using System.ComponentModel;
global using System.Numerics;
global using System.Runtime.CompilerServices;
global using System.Runtime.InteropServices;
global using System.Text;
global using System.Windows.Input;
global using AppoMobi.Gestures;
global using AppoMobi.Specials;
global using DrawnUi;
global using DrawnUi.Draw;
global using DrawnUi.Extensions;
global using DrawnUi.Infrastructure;
global using DrawnUi.Infrastructure.Models;
global using DrawnUi.Models;
global using DrawnUi.Views;
global using SkiaSharp;
global using PointF = System.Drawing.PointF;
using Microsoft.Extensions.Logging;
using OpenTK.Windowing.GraphicsLibraryFramework;
using DrawnUi.Views;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SkiaSharp;
using OpenTkMouseButton = OpenTK.Windowing.GraphicsLibraryFramework.MouseButton;

namespace DrawnUi.Draw
{
    public partial class Super
    {

        public static DrawnUiBuilder UseDrawnUi() => new();

        private static readonly object FrameLoopLock = new();
        private static CancellationTokenSource _frameLoopCancellation;
        private static bool _loopStarted;

        public static object App { get; set; }

        public static event EventHandler OnFrame;

        public static float RefreshRate { get; protected set; } = 60;

        public static void Init()
        {
            Initialized = true;

            Super.Screen.Density = 1;

            RefreshRate = GetDisplayRefreshRate(60);

            var n = new SkiaControl();
            n.Opacity = 1;
        }

        public static float GetDisplayRefreshRate(float fallback)
        {
            try
            {
                unsafe
                {
                    var monitor = GLFW.GetPrimaryMonitor();
                    if (monitor != null)
                    {
                        var mode = GLFW.GetVideoMode(monitor);
                        // GLFW reports an integer rate — 59 on a 59.94Hz panel; restore the exact fraction
                        if (mode != null && mode->RefreshRate > 0)
                            return AdjustTruncatedRate(mode->RefreshRate);
                    }
                }
            }
            catch (Exception e)
            {
                Log(e);
            }
            return 60;
        }

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

            if (logLevel == LogLevel.Error || logLevel == LogLevel.Critical)
            {
                Trace.TraceError(message);
            }
            else
            {
                Debug.WriteLine(message);
            }
        }

        public static void Log(LogLevel level, string message, [CallerMemberName] string caller = null)
        {
            Log(message, level, caller);
        }

        public static void EnsureFrameLoopStarted()
        {
            lock (FrameLoopLock)
            {
                if (_loopStarted)
                    return;

                _loopStarted = true;
                _frameLoopCancellation = new CancellationTokenSource();
                _ = RunFrameLoopAsync(_frameLoopCancellation.Token);
            }
        }

        private static async Task RunFrameLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var fps = MaxFps > 0 ? MaxFps : RefreshRate;
                    if (fps <= 0) fps = 60;

                    await Task.Delay(TimeSpan.FromSeconds(1.0 / fps), cancellationToken);
                    OnFrame?.Invoke(null, EventArgs.Empty);
                }
            }
            catch (OperationCanceledException) { }
        }

        private static void RestartFrameLoop()
        {
            lock (FrameLoopLock)
            {
                if (!_loopStarted)
                    return;

                _frameLoopCancellation?.Cancel();
                _frameLoopCancellation?.Dispose();
                _frameLoopCancellation = new CancellationTokenSource();
                _ = RunFrameLoopAsync(_frameLoopCancellation.Token);
            }
        }

        static partial void OnMaxFpsChanged(int fps)
        {
            RestartFrameLoop();
        }
    }
}
