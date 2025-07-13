using System.Diagnostics;
using System.Runtime.InteropServices;
using CoreAnimation;
using Foundation;
using Microsoft.Maui.Controls.Compatibility.Platform.iOS;
using SkiaSharp.Views.iOS;
using UIKit;
using Platform = Microsoft.Maui.ApplicationModel.Platform;

namespace DrawnUi.Draw
{

    public partial class Super
    {

        public static int GetDisplayRefreshRate(int fallback)
        {
            return (int)UIScreen.MainScreen.MaximumFramesPerSecond;
        }

        public static int RefreshRate { get; protected set; }

        public static void Init()
        {
            if (Initialized)
                return;

            Initialized = true;

            RefreshRate = GetDisplayRefreshRate(60);

            Super.Screen.Density = UIScreen.MainScreen.Scale;
            Super.Screen.WidthDip = UIScreen.MainScreen.Bounds.Width;
            Super.Screen.HeightDip = UIScreen.MainScreen.Bounds.Height;

            if (UIDevice.CurrentDevice.CheckSystemVersion(11, 0))
            {
                var window = new UIWindow(frame: UIScreen.MainScreen.Bounds)
                { BackgroundColor = Colors.Transparent.ToUIColor() };

                Super.Screen.TopInset = (int)(window.SafeAreaInsets.Top);
                Super.Screen.BottomInset = (int)(window.SafeAreaInsets.Bottom);
                Super.Screen.LeftInset = (int)(window.SafeAreaInsets.Left);
                Super.Screen.RightInset = (int)(window.SafeAreaInsets.Right);
            }

            Super.StatusBarHeight = Super.Screen.TopInset;
            if (Super.StatusBarHeight <= 0)
                Super.StatusBarHeight = 20;

            if (Super.NavBarHeight < 0)

                Super.NavBarHeight = 47; //manual

            InitShared();

            InsetsChanged?.Invoke(null, null);

            if (UseDisplaySync)
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

                            if (MainThread.IsMainThread) //CADisplayLink is available
                            {
                                if (!_loopStarted)
                                {
                                    _loopStarted = true;
                                    try
                                    {
                                        _displayLink = CADisplayLink.Create(() => OnFrame?.Invoke(null, null));
                                        _displayLink.AddToRunLoop(NSRunLoop.Main, NSRunLoopMode.Default);
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

        static Looper Looper { get; set; }

        /// <summary>
        /// When set to true will run loop upon CADisplayLink hits instead of a timer looper that targets 120 fps
        /// </summary>
        public static bool UseDisplaySync = true;

        static bool _loopStarting = false;
        static bool _loopStarted = false;


        //static void OnFrame()
        //{
        //    DisplayLinkCallback?.Invoke(null, null);
        //}

        public static event EventHandler OnFrame;

        static CADisplayLink _displayLink;

        public static UINavigationController NavigationController { get; set; } = null;

        public static UIStatusBarStyle? OrderedStyle { get; set; }

        public static void SetBlackTextStatusBar()
        {
            Debug.WriteLine("[StatusBar] BLACK");

            var controller = Platform.GetCurrentUIViewController();

            if (controller == null || controller.NavigationController == null)
            {
                OrderedStyle = UIStatusBarStyle.DarkContent;

                UIApplication.SharedApplication.SetStatusBarStyle(UIStatusBarStyle.DarkContent, false);
                controller.SetNeedsStatusBarAppearanceUpdate();

            }
            else
            {
                OrderedStyle = null;
                controller.NavigationController.NavigationBar.BarStyle = UIBarStyle.Default;
            }
        }

        public static void SetWhiteTextStatusBar()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Update the UI
                Debug.WriteLine("[StatusBar] WHITE");

                var controller = Platform.GetCurrentUIViewController();
                if (controller == null || controller.NavigationController == null)
                {
                    OrderedStyle = UIStatusBarStyle.LightContent;

                    UIApplication.SharedApplication.SetStatusBarStyle(UIStatusBarStyle.LightContent, false);
                    controller.SetNeedsStatusBarAppearanceUpdate();
                }
                else
                {
                    OrderedStyle = null;
                    controller.NavigationController.NavigationBar.BarStyle = UIBarStyle.Black;
                }
            });



        }

        /// <summary>
        /// Completely hides the status bar
        /// </summary>
        public static void HideStatusBar()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Debug.WriteLine("[StatusBar] HIDDEN");
                var controller = Platform.GetCurrentUIViewController();

                UIApplication.SharedApplication.SetStatusBarHidden(true, UIStatusBarAnimation.Fade);
                controller?.SetNeedsStatusBarAppearanceUpdate();
            });
        }

        /// <summary>
        /// Shows the status bar
        /// </summary>
        public static void ShowStatusBar()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Debug.WriteLine("[StatusBar] VISIBLE");
                var controller = Platform.GetCurrentUIViewController();

                UIApplication.SharedApplication.SetStatusBarHidden(false, UIStatusBarAnimation.Fade);
                controller?.SetNeedsStatusBarAppearanceUpdate();
            });
        }

        /// <summary>
        /// Makes status bar text invisible by using black text on black background
        /// </summary>
        public static void MakeStatusBarInvisible()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Debug.WriteLine("[StatusBar] INVISIBLE (black on black)");
                var controller = Platform.GetCurrentUIViewController();

                if (controller == null || controller.NavigationController == null)
                {
                    OrderedStyle = UIStatusBarStyle.DarkContent; // Black text
                    UIApplication.SharedApplication.SetStatusBarStyle(UIStatusBarStyle.DarkContent, false);
                    controller?.SetNeedsStatusBarAppearanceUpdate();
                }
                else
                {
                    OrderedStyle = null;
                    controller.NavigationController.NavigationBar.BarStyle = UIBarStyle.Default;
                    // Set navigation bar background to black to hide black text
                    controller.NavigationController.NavigationBar.BackgroundColor = UIColor.Black;
                    controller.NavigationController.NavigationBar.BarTintColor = UIColor.Black;
                }

                // Ensure your page background is black where status bar area is
                if (controller?.View != null)
                {
                    controller.View.BackgroundColor = UIColor.Black;
                }
            });
        }

        /// <summary>
        /// Opens web link in native browser
        /// </summary>
        /// <param name="link"></param>

        public static void OpenLink(string link)
        {
            try
            {
                var url = new NSUrl(link);
                var res = UIApplication.SharedApplication.OpenUrl(url);
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
        public static IEnumerable<string> ListResources(string subfolder)
        {
            NSBundle mainBundle = NSBundle.MainBundle;
            string resourcesPath = mainBundle.ResourcePath;
            string subfolderPath = Path.Combine(resourcesPath, subfolder);

            if (Directory.Exists(subfolderPath))
            {
                string[] files = Directory.GetFiles(subfolderPath);
                return files.Select(Path.GetFileName).ToList();
            }
            else
            {
                return new List<string>();
            }
        }

        public static async Task<byte[]> CaptureScreenshotAsync()
        {
            UIWindow window;
            if (UIDevice.CurrentDevice.CheckSystemVersion(13, 0))
            {
                window = UIApplication.SharedApplication.Windows.First();
                if (window.GetType().Name.Contains("Popup"))
                {
                    var maybe = UIApplication.SharedApplication.Windows.FirstOrDefault(x => x != window);
                    if (maybe != null)
                    {
                        window = maybe;
                    }
                }
            }
            else
            {
                window = UIApplication.SharedApplication.KeyWindow;
            }

            UIGraphics.BeginImageContextWithOptions(window.Bounds.Size, false, UIScreen.MainScreen.Scale);
            window.Layer.RenderInContext(UIGraphics.GetCurrentContext());
            UIImage image = UIGraphics.GetImageFromCurrentImageContext();
            UIGraphics.EndImageContext();
            using (NSData data = image.AsPNG())
            {
                var bytes = new byte[data.Length];
                Marshal.Copy(data.Bytes, bytes, 0, Convert.ToInt32(data.Length));
                return bytes;
            }

        }

        private static bool _keepScreenOn;
        /// <summary>
        /// Prevents display from auto-turning off  Everytime you set this the setting will be applied.
        /// </summary>
        public static bool KeepScreenOn
        {
            get
            {
                return _keepScreenOn;
            }
            set
            {
                UIApplication.SharedApplication.IdleTimerDisabled = value;
                _keepScreenOn = value;
            }
        }

    }
}
