using DrawnUi.Controls;
using DrawnUi.Features.Images;
using EasyCaching.InMemory;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.LifecycleEvents;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace DrawnUi.Draw;

public static partial class DrawnExtensions
{

    #region STARTUP

    static bool windowAdapted = false;

    public static DrawnUiStartupSettings StartupSettings { get; set; }

    public static MauiAppBuilder UseDrawnUi(this MauiAppBuilder builder, DrawnUiStartupSettings settings = null)
    {
        StartupSettings = settings;

#if ONPLATFORM
        builder.ConfigureMauiHandlers(handlers =>
        {
            ConfigureHandlers(handlers);
        });
#endif

        builder
            .ConfigureAnimations();

        builder.UseSkiaSharp();

        builder.UseGestures();

#if WINDOWS || MACCATALYST
        // on mobile removed IHttpClientFactory for faster app startup
        builder.Services.AddUriImageSourceHttpClient(); 
#endif

        //In-Memory Caching of bitmaps
        builder.Services  //Important step for In-Memory Caching
            .AddEasyCaching(options =>
            {

                // use memory cache with your own configuration
                options.UseInMemory(config =>
                {
                    config.DBConfig = new InMemoryCachingOptions
                    {
                        // scan time, default value is 60s
                        ExpirationScanFrequency = 300,
                        // total count of cache items, default value is 10000
                        SizeLimit = 100,

                        // below two settings are added in v0.8.0
                        // enable deep clone when reading object from cache or not, default value is true.
                        EnableReadDeepClone = false,
                        // enable deep clone when writing object to cache or not, default valuee is false.
                        EnableWriteDeepClone = false,
                    };
                    // the max random second will be added to cache's expiration, default value is 120
                    config.MaxRdSecond = SkiaImageManager.CacheLongevitySecs;
                    // whether enable logging, default is false
                    config.EnableLogging = false;
                    // mutex key's alive time(ms), default is 5000
                    config.LockMs = 5000;
                    // when mutex key alive, it will sleep some time, default is 300
                    config.SleepMs = 300;
                }, "skiaimages");
            });

        Super.OnNativeAppCreated += (s, a) =>
        {
            Super.App = Super.Services.GetRequiredService<IApplication>();

#if  MACCATALYST || WINDOWS
            DeviceDisplay.MainDisplayInfoChanged += (s, e) =>
            {
                Super.Screen.Density = DeviceDisplay.Current.MainDisplayInfo.Density;
            };
#endif

        };

        Super.OnMauiAppCreated += () =>
        {
            Tasks.StartDelayed(TimeSpan.FromSeconds(2), () =>
            {
                //close keyboard if any
                TouchEffect.CloseKeyboard();
            });
        };

        void InvokeLifecycleEvents<TDelegate>(Action<TDelegate> action)
           where TDelegate : Delegate
        {
            if (Super.Services == null)
                return;

            var delegates = GetLifecycleEventDelegates<TDelegate>();

            foreach (var del in delegates)
                action?.Invoke(del);
        }

        IEnumerable<TDelegate> GetLifecycleEventDelegates<TDelegate>(string? eventName = null)
            where TDelegate : Delegate
        {
            var lifecycleService = Super.Services?.GetService<ILifecycleEventService>();
            if (lifecycleService == null)
                yield break;

            if (eventName == null)
                eventName = typeof(TDelegate).Name;

            foreach (var del in lifecycleService.GetEventDelegates<TDelegate>(eventName))
                yield return del;
        }


        builder.ConfigureLifecycleEvents(AppLifecycle =>
        {

            //todo
            // 1 every platform sets density

#if WINDOWS

            AppLifecycle.AddEvent<WindowsLifecycle.OnLaunched>("OnLaunching", (application, args) =>  
                {
                    Super.Init();
                });

            AppLifecycle.AddEvent<WindowsLifecycle.OnWindowCreated>("OnWindowCreated",
                (window) =>
                {
                    Super.OnMauiAppCreated?.Invoke();
                });

            AppLifecycle.AddEvent<WindowsLifecycle.OnLaunched>("OnLaunched",
                (Microsoft.UI.Xaml.Application application, Microsoft.UI.Xaml.LaunchActivatedEventArgs args) =>
                {
                    Super.Init();

                    Super.OnMauiAppCreated?.Invoke();

                    Super.App = Super.Services.GetRequiredService<IApplication>();

                    var appWindow = Super.App.Windows.First() as Microsoft.Maui.Controls.Window;

                    var window = appWindow.Handler?.PlatformView as Microsoft.Maui.MauiWinUIWindow;

                    Super.Screen.Density = DeviceDisplay.Current.MainDisplayInfo.Density;

                    appWindow.DisplayDensityChanged += (sender, args) =>
                    {
                        Super.Screen.Density = args.DisplayDensity; //works fine when changing display
                    };

                    if (StartupSettings != null)
                    {
                        if (StartupSettings.UseDesktopKeyboard)
                        {
                            KeyboardManager.AttachToKeyboard(window.Content);
                        }

                        if (StartupSettings.DesktopWindow != null)
                        {
                            Super.ResizeWindow(appWindow,
                                StartupSettings.DesktopWindow.Value.Width,
                                StartupSettings.DesktopWindow.Value.Height,
                                StartupSettings.DesktopWindow.Value.IsFixedSize);
                        }
                    }

                    //MainPage
                    //var page = appWindow.Page;

                    //App created
                    //var a = window.GetAppWindow();
                    //var win = window.GetWindow();
                    //var handle = window.GetWindowHandle();

                    Super.OnMauiAppCreated?.Invoke();

                    Super.OnCreated();
                });



#elif ANDROID

            AppLifecycle.AddAndroid((android) =>
            {
                bool appCreated = false;

                void AttachActivity(Android.App.Activity activity)
                {

                    if (StartupSettings != null)
                    {
                        if (StartupSettings.MobileIsFullscreen)
                        {
                            Super.SetFullScreen(activity);
                        }
                        if (StartupSettings.UseDesktopKeyboard)
                        {
                            KeyboardManager.AttachToKeyboard(activity);
                        }
                    }
                }

                android.OnCreate((activity, bundle) =>
                {
                    //if (!appCreated)
                    {
                        appCreated = true;

                        Super.Init(activity);

                        AttachActivity(activity);

                        Super.OnMauiAppCreated?.Invoke();
                    }
                });

                android.OnApplicationCreate((app) =>
                {
                    Super.OnCreated();
                });


                ActivityState activityState = ActivityState.Destroyed;

                Platform.ActivityStateChanged += (o, args) =>
                {
                    if (activityState != args.State)
                    {
                        if ((args.State == ActivityState.Resumed || args.State == ActivityState.Started)
                            && activityState != ActivityState.Resumed && activityState != ActivityState.Started)
                        {
                            AttachActivity(args.Activity);

                            //Console.WriteLine("[APP] OnResumed");
                            Super.OnWentForeground();
                        }
                        else
                        if ((args.State == ActivityState.Paused || args.State == ActivityState.Stopped)
                            && activityState != ActivityState.Paused && args.State != ActivityState.Stopped)
                        {
                            //Console.WriteLine("[APP] OnPause");
                            Super.OnWentBackground();
                        }
                        activityState = args.State;
                    }
                };

                //android.OnBackPressed((activity) =>
                //{
                //    //todo use settings?
                //});

                android.OnNewIntent((activity, intent) =>
                {
                    if (StartupSettings != null)
                    {
                        if (StartupSettings.MobileIsFullscreen)
                        {
                            Super.SetFullScreen(activity);
                        }
                    }
                });
            });


#elif IOS || MACCATALYST

            AppLifecycle.AddiOS((apple) =>
            {

                Foundation.NSNotificationCenter.DefaultCenter.AddObserver(
                    UIKit.UIApplication.DidBecomeActiveNotification, (obj) =>
                    {
                        Super.OnWentForeground();
                    });

                Foundation.NSNotificationCenter.DefaultCenter.AddObserver(
                    UIKit.UIApplication.WillResignActiveNotification, (obj) =>
                    {
                        Super.OnWentBackground();
                    });

                bool onceApple = false;
                apple.OnActivated((del) =>
                {
                    if (!onceApple)
                    {
                        onceApple = true;
                        Super.OnMauiAppCreated?.Invoke();
                    }
                });

                apple.FinishedLaunching((application, launchOptions) =>
                {
                    Super.App = Super.Services.GetRequiredService<IApplication>();
                    var appWindow = Super.App.Windows.First() as Microsoft.Maui.Controls.Window;
                    var view = appWindow.Handler?.PlatformView as UIKit.UIView;
                    //var check = UIKit.UIApplication.SharedApplication.KeyWindow;

                    Super.Init();


#if MACCATALYST

                    Foundation.NSNotificationCenter.DefaultCenter.AddObserver("NSWindowDidBecomeMainNotification", null, null,
                              (h) =>
                              {
                                  if (!windowAdapted && StartupSettings.DesktopWindow != null)
                                  {
                                      Super.ResizeWindow(appWindow,
                                          StartupSettings.DesktopWindow.Value.Width,
                                          StartupSettings.DesktopWindow.Value.Height,
                                          StartupSettings.DesktopWindow.Value.IsFixedSize);
                                      windowAdapted=true;
                                  }

                                  if (UIKit.UIApplication.SharedApplication.KeyWindow.RootViewController is Microsoft.Maui.Platform.ContainerViewController container)
                                  {
                                      if (container.CurrentView is DrawnUiBasePage page)
                                      {
                                          if (page.Handler is DrawnUiBasePageHandler handler)
                                          {
                                              Super.RequestMainResponder(handler.PlatformView);
                                          }
                                      }
                                  }
                                  // else if (UIKit.UIApplication.SharedApplication.KeyWindow.RootViewController is
                                  //          Microsoft.Maui.Controls.Platform.Compatibility.ShellFlyoutRenderer shell)
                                  // {
                                  //     //unsupported
                                  // }


                              });

#endif

                    Foundation.NSNotificationCenter.DefaultCenter.AddObserver("NSWindowDidUpdateNotification", null, null,
                        (h) =>
                        {
                            foreach (var scene in UIKit.UIApplication.SharedApplication.ConnectedScenes)
                            {
                                if (scene is UIKit.UIWindowScene windowScene)
                                {
                                    if (Super.Screen.Density != windowScene.Screen.Scale)
                                    {
                                        //not working with several displays never ever
                                        //var test = DeviceDisplay.Current.MainDisplayInfo.Density;
                                        Super.Screen.Density = windowScene.Screen.Scale;
                                        Super.NeedGlobalUpdate();
                                    }
                                }
                            }
                        });

                    if (StartupSettings != null)
                    {
#if MACCATALYST


                        //might be implemented too..
                        //NSWindowDidChangeScreenNotification
                        //NSWindowDidResizeNotification
                        //
                        //NSWindowDidChangeBackingPropertiesNotification

                        if (StartupSettings.UseDesktopKeyboard)
                        {

                            Foundation.NSNotificationCenter.DefaultCenter.AddObserver("NSWindowDidChangeBackingPropertiesNotification", null, null,
                                (h) =>
                                {
                                    foreach (var scene in UIKit.UIApplication.SharedApplication.ConnectedScenes)
                                    {
                                        if (scene is UIKit.UIWindowScene windowScene)
                                        {

                                            if (Super.Screen.Density != windowScene.Screen.Scale)
                                            {
                                                Super.Screen.Density = windowScene.Screen.Scale;
                                                Super.NeedGlobalUpdate();
                                            }
                                        }
                                    }

                                });
                        }


                        if (StartupSettings.DesktopWindow != null)
                        {
                            // Super.ResizeWindow(appWindow,
                            //     StartupSettings.DesktopWindow.Value.Width,
                            //     StartupSettings.DesktopWindow.Value.Height,
                            //     StartupSettings.DesktopWindow.Value.IsFixedSize);
                        }
#elif IOS
                        //see ConfigureHandlers for StartupSettings.MobileIsFullscreen implementation
#endif
                    }

                    Super.OnCreated();



                    //if (StartupSettings != null)
                    //{

                    //}

                    return true;

                });

            });

#endif

        });

        // Microsoft.Maui.Handlers.ViewHandler.ViewMapper.AppendToMapping(nameof(Application.Resources), (handler, view) =>
        // {
        //     if (!mauiCreated)
        //     {
        //         mauiCreated = true;
        //         Super.OnMauiAppCreated?.Invoke();
        //
        //
        //     }
        // });

        return builder;
    }

    private static bool mauiCreated;

    #endregion

    public static IFontCollection AddFont(
        this IFontCollection fontCollection,
        string filename,
        string alias, int weight)
    {
        var weightEnum = SkiaFontManager.GetWeightEnum(weight);
        return fontCollection.AddFont(filename, alias, weightEnum);
    }


    public static IFontCollection AddFont(
        this IFontCollection fontCollection,
        string filename,
        string alias, FontWeight weight)
    {
        //FontText alias for weight Regular will be registered as FontTextRegular
        //After that when we look for FontText with weight Regular we will ask FontManager for FontTextRegular
        var newAlias = SkiaFontManager.GetAlias(alias, weight);
        SkiaFontManager.RegisterWeight(alias, weight);
        fontCollection.AddFont(filename, newAlias);

        return fontCollection;
    }

    public static Task AnimateRangeAsync(this SkiaControl owner, Action<double> callback, double start, double end, uint length = 250, Easing easing = null, CancellationTokenSource _cancelTranslate = default)
    {
        RangeAnimator animator = null;
        var tcs = new TaskCompletionSource<bool>(_cancelTranslate.Token);
        tcs.Task.ContinueWith(task =>
        {
            animator?.Dispose();
        });

        animator = new RangeAnimator(owner)
        {
            OnStop = () =>
            {
                if (animator.WasStarted && !_cancelTranslate.IsCancellationRequested)
                {
                    tcs.SetResult(true);
                }
            }
        };
        animator.Start(
            (value) =>
            {
                if (!_cancelTranslate.IsCancellationRequested)
                {
                    callback?.Invoke(value);
                }
                else
                {
                    animator.Stop();
                }
            },
            start, end, length, easing);

        return tcs.Task;
    }


    public static (float RatioX, float RatioY) GetVelocityRatioForChild(this IDrawnBase container,
        ISkiaControl control)
    {
        //return (1, 1);

        var containerWidth = container.Width;
        if (containerWidth <= 0)
            containerWidth = container.MeasuredSize.Units.Width;

        var containerHeight = container.Height;
        if (containerHeight <= 0)
            containerHeight = container.MeasuredSize.Units.Height;

        var velocityRatoX = (float)(containerWidth / control.MeasuredSize.Units.Width);
        var velocityRatoY = (float)(containerHeight / control.MeasuredSize.Units.Height);

        if (float.IsNaN(velocityRatoY) || velocityRatoY == 0)
        {
            velocityRatoY = 1;
        }
        if (float.IsNaN(velocityRatoX) || velocityRatoX == 0)
        {
            velocityRatoX = 1;
        }

        return (velocityRatoX, velocityRatoY);
    }
}
