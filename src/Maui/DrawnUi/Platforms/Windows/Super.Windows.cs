using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media;
using Windows.Storage;

namespace DrawnUi.Draw
{
    public partial class Super
    {

        [DllImport("gdi32.dll")]
        static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll")]
        static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

        const int VREFRESH = 116;
        const int ENUM_CURRENT_SETTINGS = -1;

        [StructLayout(LayoutKind.Sequential)]
        struct UNSIGNED_RATIO
        {
            public uint uiNumerator;
            public uint uiDenominator;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct DWM_TIMING_INFO
        {
            public uint cbSize;
            public UNSIGNED_RATIO rateRefresh;
            public ulong qpcRefreshPeriod;
            public UNSIGNED_RATIO rateCompose;
            public ulong qpcVBlank;
            public ulong cRefresh;
            public uint cDXRefresh;
            public ulong qpcCompose;
            public ulong cFrame;
            public uint cDXPresent;
            public ulong cRefreshFrame;
            public ulong cFrameSubmitted;
            public uint cDXPresentSubmitted;
            public ulong cFrameConfirmed;
            public uint cDXPresentConfirmed;
            public ulong cRefreshConfirmed;
            public uint cDXRefreshConfirmed;
            public ulong cFramesLate;
            public uint cFramesOutstanding;
            public ulong cFrameDisplayed;
            public ulong qpcFrameDisplayed;
            public ulong cRefreshFrameDisplayed;
            public ulong cFrameComplete;
            public ulong qpcFrameComplete;
            public ulong cFramePending;
            public ulong qpcFramePending;
            public ulong cFramesDisplayed;
            public ulong cFramesComplete;
            public ulong cFramesPending;
            public ulong cFramesAvailable;
            public ulong cFramesDropped;
            public ulong cFramesMissed;
            public ulong cRefreshNextDisplayed;
            public ulong cRefreshNextPresented;
            public ulong cRefreshesDisplayed;
            public ulong cRefreshesPresented;
            public ulong cRefreshStarted;
            public ulong cPixelsReceived;
            public ulong cPixelsDrawn;
            public ulong cBuffersEmpty;
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmGetCompositionTimingInfo(IntPtr hwnd, ref DWM_TIMING_INFO pTimingInfo);

        struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public short dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
        }

        public static float GetDisplayRefreshRate(float fallback)
        {
            // Method 1: DWM composition timing — exact rational (e.g. 59951/1000 = 59.951).
            // hwnd must be IntPtr.Zero (per-window unsupported since Win 8.1) → primary monitor rate.
            var timing = new DWM_TIMING_INFO { cbSize = (uint)Marshal.SizeOf<DWM_TIMING_INFO>() };
            if (DwmGetCompositionTimingInfo(IntPtr.Zero, ref timing) == 0
                && timing.rateRefresh.uiDenominator > 0 && timing.rateRefresh.uiNumerator > 0)
            {
                return (float)timing.rateRefresh.uiNumerator / timing.rateRefresh.uiDenominator;
            }

            // Method 2: GetDeviceCaps (integer)
            IntPtr hdc = GetDC(IntPtr.Zero);
            int refreshRate = GetDeviceCaps(hdc, VREFRESH);
            ReleaseDC(IntPtr.Zero, hdc);

            if (refreshRate > 0)
                return AdjustTruncatedRate(refreshRate);

            // Method 3: EnumDisplaySettings (integer)
            DEVMODE devMode = new DEVMODE();
            devMode.dmSize = (short)Marshal.SizeOf(devMode);

            if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref devMode))
            {
                return AdjustTruncatedRate(devMode.dmDisplayFrequency);
            }

            return fallback;
        }

        public static float RefreshRate { get; protected set; }

        /// <summary>
        /// Default is true. If set to false will use Looper instead of display sync.
        /// </summary>
        public static bool UsingDisplaySync { get; protected set; } = true;

        static bool _loopStarting = false;
        static bool _loopStarted = false;

        public static void Init()
        {
            if (Initialized)
                return;

            Initialized = true;

            if (Super.NavBarHeight < 0)

                Super.NavBarHeight = 50; //manual

            Super.StatusBarHeight = 0;

            //VisualDiagnostics.VisualTreeChanged += OnVisualTreeChanged;
            InitShared();

            RefreshRate = GetDisplayRefreshRate(60);

            if (UsingDisplaySync)
            {
                object lockFrane = new();

                Tasks.StartDelayed(TimeSpan.FromMilliseconds(250), async () =>
                {
                    while (!_loopStarted)
                    {
                        try
                        {
                            MainThread.BeginInvokeOnMainThread(async () =>
                            {
                                lock (lockFrane)
                                {
                                    if (_loopStarting)
                                        return;
                                    _loopStarting = true;

                                    if (MainThread.IsMainThread) //UI thread is available
                                    {
                                        if (!_loopStarted)
                                        {
                                            _loopStarted = true;
                                            try
                                            {
                                                var frameStopwatch = Stopwatch.StartNew();
                                                CompositionTarget.Rendering += (s, a) =>
                                                {
                                                    if (MaxFps > 0)
                                                    {
                                                        var minIntervalMs = 1000.0 / MaxFps;
                                                        if (frameStopwatch.Elapsed.TotalMilliseconds < minIntervalMs)
                                                            return;
                                                        frameStopwatch.Restart();
                                                    }
                                                    OnFrame?.Invoke(null, null);
                                                };
                                            }
                                            catch (Exception e)
                                            {
                                                Console.WriteLine(e);
                                            }
                                        }
                                    }

                                    _loopStarting = false;
                                }
                            });

                            if (_loopStarted)
                                break;
                        }
                        catch
                        {
                            //unable to find mainthread?
                        }
                        await Task.Delay(100);
                    }
                });
            }
            else
            {
                Looper = new(() =>
                {
                    OnFrame?.Invoke(null, null);
                });

                Looper.StartOnMainThread(MaxFps > 0 ? MaxFps : RefreshRate);
            }
        }


        [DllImport("user32.dll")]
        public static extern bool SetFocus(IntPtr hWnd);

        static Looper Looper { get; set; }

        static partial void OnMaxFpsChanged(int fps)
        {
            if (!UsingDisplaySync)
                Looper?.SetTargetFps(fps > 0 ? fps : RefreshRate);
        }

        public static event EventHandler OnFrame;

        /// <summary>
        /// Opens web link in native browser
        /// </summary>
        /// <param name="link"></param>
        public static void OpenLink(string link)
        {
            try
            {
                Windows.System.Launcher.LaunchUriAsync(new Uri(link));
            }
            catch (Exception e)
            {
                Super.Log(e);
            }
        }

        public static async Task<byte[]> CaptureScreenshotAsync()
        {
            var screen = await Screenshot.CaptureAsync();
            using var input = await screen.OpenReadAsync();

            using (MemoryStream ms = new MemoryStream())
            {
                input.CopyTo(ms);
                {
                    var data = ms.ToArray();
                    return data;
                }
            }
        }

        /// <summary>
        /// Prevents display from auto-turning off  Everytime you set this the setting will be applied.
        /// </summary>
        public static bool KeepScreenOn
        {
            get
            {
                return false;
            }
            set
            {
                Console.WriteLine("Not implemented on Windows");
            }
        }
    }


}

