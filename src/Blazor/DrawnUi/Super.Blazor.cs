global using System.Collections.Specialized;
global using AppoMobi.Gestures;
global using System.ComponentModel;
global using Microsoft.Maui;
global using System.Numerics;
global using System.Runtime.CompilerServices;
global using System.Runtime.InteropServices;
global using System.Text;
global using System.Windows.Input;
global using AppoMobi.Specials;
global using DrawnUi.Draw;
global using DrawnUi.Extensions;
global using DrawnUi.Infrastructure;
global using DrawnUi.Infrastructure.Models;
global using DrawnUi.Models;
global using Microsoft.Maui.ApplicationModel;
global using DrawnUi;
global using DrawnUi.Views;
global using SkiaSharp;
//global using SkiaSharp.Views.Maui;
//global using SkiaSharp.Views.Maui.Controls;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DrawnUi.Draw
{
    public partial class Super
    {
        private static readonly object FrameLoopLock = new();
        private static CancellationTokenSource _frameLoopCancellation;
        private static bool _loopStarted;

        public static object App { get; set; }

        public static event EventHandler OnFrame;

        public static int RefreshRate { get; protected set; } = 60;
        public static void Init()
        {
            Initialized = true;

            Super.Screen.Density = 1;

            RefreshRate = GetDisplayRefreshRate(60);
        }

        public static int GetDisplayRefreshRate(int fallback)
        {
            var ret = fallback;

            //TODO: Implement browser-specific logic to get the actual display refresh rate

            return ret;
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

        public static void DisplayException(VisualElement view, Exception e)
        {
            Log(e?.ToString() ?? string.Empty, LogLevel.Error);

            if (view == null)
                throw e;

            view.Update();
        }

        public static void Log(string message, LogLevel logLevel = LogLevel.Warning, [System.Runtime.CompilerServices.CallerMemberName] string caller = null)
        {
            if (DrawnExtensions.StartupSettings != null)
            {
                DrawnExtensions.StartupSettings.Logger?.Log(logLevel, message);
            }

            Console.WriteLine(message);
        }

        public static void Log(LogLevel level, string message, [System.Runtime.CompilerServices.CallerMemberName] string caller = null)
        {
            Log(message, level, caller);
        }

        public static void EnsureFrameLoopStarted()
        {
            lock (FrameLoopLock)
            {
                if (_loopStarted)
                {
                    return;
                }

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
                    if (fps <= 0)
                    {
                        fps = 60;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1.0 / fps), cancellationToken);
                    OnFrame?.Invoke(null, EventArgs.Empty);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private static void RestartFrameLoop()
        {
            lock (FrameLoopLock)
            {
                if (!_loopStarted)
                {
                    return;
                }

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
