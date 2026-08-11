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
global using DrawnUi.Draw;
global using DrawnUi.Extensions;
global using DrawnUi.Infrastructure;
global using DrawnUi.Infrastructure.Models;
global using DrawnUi.Models;
global using DrawnUi;
global using DrawnUi.Views;
global using SkiaSharp;
global using PointF = System.Drawing.PointF;
//global using SkiaSharp.Views.Maui;
//global using SkiaSharp.Views.Maui.Controls;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
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
        private static readonly object NativeAppLifecycleLock = new();
        private static CancellationTokenSource _frameLoopCancellation;
        private static IJSObjectReference? _nativeAppLifecycleModule;
        private static DotNetObjectReference<AppLifecycleProxy>? _lifecycleProxyRef;
        private static bool _loopStarted;
        private static bool _nativeAppCreated;
        private static bool _nativeAppDestroyed;
        private static bool _nativeAppLifecycleAttached;
        private static bool? _nativeAppVisible;

        private static readonly string NativeAppLifecycleModulePath =
            $"./_content/DrawnUi.Blazor.Core/drawnui-app-lifecycle.js?v={System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

        public static object App { get; set; }

        public static event EventHandler OnFrame;

        private const int DEFAULT_TARGET_FPS = 120;

        public static float RefreshRate { get; protected set; } = DEFAULT_TARGET_FPS;
        public static void Init()
        {
            Initialized = true;

            Super.Screen.Density = 1;

            RefreshRate = GetDisplayRefreshRate(DEFAULT_TARGET_FPS);
        }

        public static float GetDisplayRefreshRate(float fallback)
        {
            return DEFAULT_TARGET_FPS;
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

        public static async Task AttachNativeAppLifecycleAsync(IJSRuntime jsRuntime)
        {
            lock (NativeAppLifecycleLock)
            {
                if (_nativeAppLifecycleAttached)
                {
                    return;
                }

                _nativeAppLifecycleAttached = true;
            }

            try
            {
                _nativeAppLifecycleModule ??= await jsRuntime.InvokeAsync<IJSObjectReference>(
                    "import",
                    NativeAppLifecycleModulePath);

                _lifecycleProxyRef ??= DotNetObjectReference.Create(new AppLifecycleProxy());
                await _nativeAppLifecycleModule.InvokeVoidAsync("attachNativeAppLifecycle", _lifecycleProxyRef);
            }
            catch
            {
                lock (NativeAppLifecycleLock)
                {
                    _nativeAppLifecycleAttached = false;
                }

                throw;
            }
        }

        public static object AppContext => null;

        public static void DisplayException(SkiaControl view, Exception e)
        {
            Log(e?.ToString() ?? string.Empty, LogLevel.Error);

            if (view is SkiaControl skia)
            {
                var scroll = new SkiaScroll()
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Content = new SkiaLabel
                    {
                        Margin = new Thickness(32),
                        TextColor = Colors.Red,
                        Text = $"{e}"
                    }
                };

                if (skia is ContentLayout content)
                {
                    content.Content = scroll;
                }
                else
                {
                    skia.AddSubView(scroll);
                }
            }
        }

        public static void DisplayException(VisualElement view, Exception e)
        {
            Log(e?.ToString() ?? string.Empty, LogLevel.Error);

            if (view == null)
                return;


            //todo

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

        private static bool IsClose(double left, double right, double tolerance)
        {
            return Math.Abs(left - right) <= tolerance;
        }

        [JSInvokable]
        public static void HandleScreenMetricsChanged(
            double density,
            double widthDip,
            double heightDip,
            double topInset,
            double rightInset,
            double bottomInset,
            double leftInset)
        {
            EnsureNativeAppCreated();

            if (!double.IsFinite(density) || density <= 0)
            {
                density = 1;
            }

            if (!double.IsFinite(widthDip) || widthDip < 0)
            {
                widthDip = 0;
            }

            if (!double.IsFinite(heightDip) || heightDip < 0)
            {
                heightDip = 0;
            }

            if (!double.IsFinite(topInset) || topInset < 0)
            {
                topInset = 0;
            }

            if (!double.IsFinite(rightInset) || rightInset < 0)
            {
                rightInset = 0;
            }

            if (!double.IsFinite(bottomInset) || bottomInset < 0)
            {
                bottomInset = 0;
            }

            if (!double.IsFinite(leftInset) || leftInset < 0)
            {
                leftInset = 0;
            }

            var densityChanged = !IsClose(Screen.Density, density, 0.01);
            var widthChanged = !IsClose(Screen.WidthDip, widthDip, 0.5);
            var heightChanged = !IsClose(Screen.HeightDip, heightDip, 0.5);
            var topInsetChanged = !IsClose(Screen.TopInset, topInset, 0.5);
            var rightInsetChanged = !IsClose(Screen.RightInset, rightInset, 0.5);
            var bottomInsetChanged = !IsClose(Screen.BottomInset, bottomInset, 0.5);
            var leftInsetChanged = !IsClose(Screen.LeftInset, leftInset, 0.5);
            var insetChanged = topInsetChanged || rightInsetChanged || bottomInsetChanged || leftInsetChanged;

            if (densityChanged)
            {
                Screen.Density = density;
            }

            if (widthChanged)
            {
                Screen.WidthDip = widthDip;
            }

            if (heightChanged)
            {
                Screen.HeightDip = heightDip;
            }

            if (topInsetChanged)
            {
                Screen.TopInset = topInset;
            }

            if (rightInsetChanged)
            {
                Screen.RightInset = rightInset;
            }

            if (bottomInsetChanged)
            {
                Screen.BottomInset = bottomInset;
            }

            if (leftInsetChanged)
            {
                Screen.LeftInset = leftInset;
            }

            if (!IsClose(StatusBarHeight, topInset, 0.5))
            {
                StatusBarHeight = topInset;
            }

            if (!IsClose(NavigationBarHeight, bottomInset, 0.5))
            {
                NavigationBarHeight = bottomInset;
            }

            if (insetChanged)
            {
                InsetsChanged?.Invoke(null, EventArgs.Empty);
            }

            if (!densityChanged && (widthChanged || heightChanged || insetChanged))
            {
                NeedGlobalUpdate();
            }
        }

        [JSInvokable]
        public static void HandleNativeAppCreated()
        {
            bool raiseCreated = false;

            lock (NativeAppLifecycleLock)
            {
                if (!_nativeAppCreated)
                {
                    _nativeAppCreated = true;
                    _nativeAppDestroyed = false;
                    raiseCreated = true;
                }
            }

            if (raiseCreated)
            {
                OnCreated();
            }
        }

        [JSInvokable]
        public static void HandleNativeAppHidden()
        {
            if (!TrySetNativeAppVisibility(false))
            {
                return;
            }

            EnsureNativeAppCreated();
            OnWentBackground();
        }

        [JSInvokable]
        public static void HandleNativeAppVisible()
        {
            if (!TrySetNativeAppVisibility(true))
            {
                return;
            }

            EnsureNativeAppCreated();
            OnWentForeground();
        }

        [JSInvokable]
        public static void HandleNativeAppDestroyed()
        {
            bool raiseDestroyed = false;

            lock (NativeAppLifecycleLock)
            {
                if (!_nativeAppDestroyed)
                {
                    _nativeAppDestroyed = true;
                    _nativeAppVisible = false;
                    raiseDestroyed = true;
                }
            }

            if (!raiseDestroyed)
            {
                return;
            }

            InBackground = true;
            OnNativeAppDestroyed?.Invoke(null, EventArgs.Empty);
        }

        private static void EnsureNativeAppCreated()
        {
            if (_nativeAppCreated)
            {
                return;
            }

            HandleNativeAppCreated();
        }

        private static bool TrySetNativeAppVisibility(bool isVisible)
        {
            lock (NativeAppLifecycleLock)
            {
                if (_nativeAppDestroyed)
                {
                    return false;
                }

                if (_nativeAppVisible == isVisible)
                {
                    return false;
                }

                _nativeAppVisible = isVisible;
                return true;
            }
        }

        static partial void OnMaxFpsChanged(int fps)
        {
            RestartFrameLoop();
        }

        public class AppLifecycleProxy
        {
            [JSInvokable] public void HandleNativeAppCreated() => Super.HandleNativeAppCreated();
            [JSInvokable] public void HandleNativeAppHidden() => Super.HandleNativeAppHidden();
            [JSInvokable] public void HandleNativeAppVisible() => Super.HandleNativeAppVisible();
            [JSInvokable] public void HandleNativeAppDestroyed() => Super.HandleNativeAppDestroyed();
            [JSInvokable]
            public void HandleScreenMetricsChanged(
                double density,
                double widthDip,
                double heightDip,
                double topInset,
                double rightInset,
                double bottomInset,
                double leftInset)
                => Super.HandleScreenMetricsChanged(density, widthDip, heightDip, topInset, rightInset, bottomInset, leftInset);
        }
    }
}
