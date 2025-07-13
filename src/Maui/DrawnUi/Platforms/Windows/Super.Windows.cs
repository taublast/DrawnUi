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

        public static int GetDisplayRefreshRate(int fallback)
        {
            // Method 1: GetDeviceCaps
            IntPtr hdc = GetDC(IntPtr.Zero);
            int refreshRate = GetDeviceCaps(hdc, VREFRESH);
            ReleaseDC(IntPtr.Zero, hdc);

            if (refreshRate > 0)
                return refreshRate;

            // Method 2: EnumDisplaySettings (more precise)
            DEVMODE devMode = new DEVMODE();
            devMode.dmSize = (short)Marshal.SizeOf(devMode);

            if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref devMode))
            {
                return devMode.dmDisplayFrequency;
            }

            return fallback; 
        }

        public static int RefreshRate { get; protected set; }

        public static bool UsingDisplaySync { get; protected set; }

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

            UsingDisplaySync = RefreshRate >= 120;

            if (UsingDisplaySync)
            {
                Tasks.StartDelayed(TimeSpan.FromMilliseconds(250), async () =>
                {
                    while (!_loopStarted)
                    {
                        MainThread.BeginInvokeOnMainThread(async () =>
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
                                        CompositionTarget.Rendering += (s, a) =>
                                        {
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
                        });
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

                Looper.StartOnMainThread(120);
            }
        }


        [DllImport("user32.dll")]
        public static extern bool SetFocus(IntPtr hWnd);

        static Looper Looper { get; set; }

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

        /// <summary>
        /// Lists assets inside the Resources/Raw subfolder
        /// </summary>
        /// <param name="subfolder"></param>
        /// <returns></returns>
        public static List<string> ListAssets(string subfolder)
        {
            StorageFolder installFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;
            StorageFolder sub = installFolder.GetFolderAsync(subfolder).GetAwaiter().GetResult();
            IReadOnlyList<StorageFile> files = sub.GetFilesAsync().GetAwaiter().GetResult();

            return files.Select(f => f.Name).ToList();
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

