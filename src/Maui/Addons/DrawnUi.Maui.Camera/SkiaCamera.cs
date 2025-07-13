global using DrawnUi.Draw;
global using SkiaSharp;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AppoMobi.Specials;
using DrawnUi.Views;
using Microsoft.Maui.Controls;
using static Microsoft.Maui.ApplicationModel.Permissions;
using Color = Microsoft.Maui.Graphics.Color;

namespace DrawnUi.Camera;

/// <summary>
/// SkiaCamera control with support for manual camera selection.
///
/// Basic usage:
/// - Set Facing to Default/Selfie for automatic camera selection
/// - Set Facing to Manual and CameraIndex for manual camera selection
///
/// Example:
/// var camera = new SkiaCamera { Facing = CameraPosition.Manual, CameraIndex = 2 };
/// var cameras = await camera.GetAvailableCamerasAsync();
/// </summary>
public partial class SkiaCamera : SkiaControl
{
    public override bool CanUseCacheDoubleBuffering => false;
    public override bool WillClipBounds => true;


    public SkiaCamera()
    {
        Instances.Add(this);
        Super.OnNativeAppResumed += Super_OnNativeAppResumed;
        Super.OnNativeAppPaused += Super_OnNativeAppPaused;
    }

    public override void LockUpdate(bool value)
    {
    }

    public override void OnWillDisposeWithChildren()
    {
        base.OnWillDisposeWithChildren();

        Super.OnNativeAppResumed -= Super_OnNativeAppResumed;
        Super.OnNativeAppPaused -= Super_OnNativeAppPaused;

        if (Superview != null)
        {
            Superview.DeviceRotationChanged -= DeviceRotationChanged;
        }

        if (NativeControl != null)
        {
            StopInternal(true);

            NativeControl?.Dispose();
        }

        NativeControl = null;

        Instances.Remove(this);
    }

    /// <summary>
    /// Command to start the camera
    /// </summary>
    public ICommand CommandStart
    {
        get { return new Command((object context) => { Start(); }); }
    }

#if (!ANDROID && !IOS && !MACCATALYST && !WINDOWS && !TIZEN)
    public virtual void SetZoom(double value)
    {
        throw new NotImplementedException();
    }

#endif


    #region EVENTS

    public event EventHandler<CapturedImage> CaptureSuccess;

    public event EventHandler<Exception> CaptureFailed;

    public event EventHandler<LoadedImageSource> NewPreviewSet;

    public event EventHandler<string> OnError;

    public event EventHandler<double> Zoomed;

    internal void RaiseError(string error)
    {
        OnError?.Invoke(this, error);
    }

    #endregion


    #region Display

    public SkiaImage Display { get; protected set; }

    protected virtual SkiaImage CreatePreview()
    {
        return new SkiaImage()
        {
            LoadSourceOnFirstDraw = true,
#if WINDOWS || MACCATALYST
            RescalingQuality = SKFilterQuality.Low,
#else
            RescalingQuality = SKFilterQuality.None,
#endif
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Aspect = this.Aspect,
        };
    }


    public override ScaledSize OnMeasuring(float widthConstraint, float heightConstraint, float scale)
    {
        if (Display == null)
        {
            Display = CreatePreview();
            Display.AddEffect = Effect;
            Display.SetParent(this);
            OnDisplayReady();
        }

        return base.OnMeasuring(widthConstraint, heightConstraint, scale);
    }

    protected virtual void OnDisplayReady()
    {
        DisplayReady?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler DisplayReady;

    /// <summary>
    /// Apply effects on preview
    /// </summary>
    public virtual void ApplyPreviewProperties()
    {
        if (Display != null)
        {
            Display.AddEffect = Effect;
        }
    }

    #endregion

    #region Capture Photo / Take Picture

    /// <summary>
    /// Take camera picture. Run this in background thread!
    /// </summary>
    /// <returns></returns>
    public async Task TakePicture()
    {
        if (IsBusy)
            return;

        Debug.WriteLine($"[TakePicture] IsMainThread {MainThread.IsMainThread}");

        IsBusy = true;

        IsTakingPhoto = true;

        NativeControl.StillImageCaptureFailed = ex =>
        {
            OnCaptureFailed(ex);

            IsTakingPhoto = false;
        };

        NativeControl.StillImageCaptureSuccess = captured =>
        {
            CapturedStillImage = captured;

            OnCaptureSuccess(captured);

            IsTakingPhoto = false;
        };

        NativeControl.TakePicture();

        while (IsTakingPhoto)
        {
            await Task.Delay(150);
        }

        IsBusy = false;
    }

    /// <summary>
    /// Flash screen with color
    /// </summary>
    /// <param name="color"></param>
    public virtual void FlashScreen(Color color)
    {
        var layer = new SkiaControl()
        {
            Tag = "Flash",
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            BackgroundColor = color,
            ZIndex = int.MaxValue,
        };

        layer.SetParent(this);

        layer.FadeToAsync(0).ContinueWith(_ => { layer.Parent = null; });
    }

    /// <summary>
    /// Play shutter sound
    /// </summary>
    public virtual void PlaySound()
    {
        //todo
    }

    private static int filenamesCounter = 0;

    /// <summary>
    /// Generate Jpg filename
    /// </summary>
    /// <returns></returns>
    public virtual string GenerateJpgFileName()
    {
        var add = $"{DateTime.Now:MM/dd/yyyy HH:mm:ss}{++filenamesCounter}";
        var filename =
            $"skiacamera-{add.Replace("/", "").Replace(":", "").Replace(" ", "").Replace(",", "").Replace(".", "").Replace("-", "")}.jpg";

        return filename;
    }

    /// <summary>
    /// Save captured bitmap to native gallery
    /// </summary>
    /// <param name="captured"></param>
    /// <param name="reorient"></param>
    /// <param name="album"></param>
    /// <returns></returns>
    public async Task<string> SaveToGallery(CapturedImage captured, bool reorient, string album = null)
    {
        var filename = GenerateJpgFileName();

        await using var stream = CreateOutputStream(captured, reorient);
        if (stream != null)
        {
            using var exifStream = await JpegExifInjector.InjectExifMetadata(stream, captured.Meta);

            var filenameOutput = GenerateJpgFileName();

            //tofo apply gps etc

            var path = await NativeControl.SaveJpgStreamToGallery(exifStream, filename, captured.Rotation,
                captured.Meta, album);

            if (!string.IsNullOrEmpty(path))
            {
                captured.Path = path;
                Debug.WriteLine($"[SkiaCamera] saved photo: {filenameOutput}");
                return path;
            }
        }

        Debug.WriteLine($"[SkiaCamera] failed to save photo");
        return null;
    }

    /// <summary>
    /// Gets the list of available cameras on the device
    /// </summary>
    /// <returns>List of available cameras</returns>
    public virtual async Task<List<CameraInfo>> GetAvailableCamerasAsync()
    {
        return await GetAvailableCamerasInternal();
    }

    /// <summary>
    /// Refreshes and returns the list of available cameras
    /// </summary>
    /// <returns>List of available cameras</returns>
    public virtual async Task<List<CameraInfo>> RefreshAvailableCamerasAsync()
    {
        _availableCameras = null; // Clear cache
        return await GetAvailableCamerasInternal();
    }

    private List<CameraInfo> _availableCameras;

    /// <summary>
    /// Internal method to get available cameras with caching
    /// </summary>
    protected virtual async Task<List<CameraInfo>> GetAvailableCamerasInternal()
    {
        if (_availableCameras != null)
            return _availableCameras;

#if ONPLATFORM
        _availableCameras = await GetAvailableCamerasPlatform();
#else
                _availableCameras = new List<CameraInfo>();
#endif

        return _availableCameras;
    }

    #endregion

    #region METHODS

    public virtual void Stop()
    {
        IsOn = false;
    }

    /// <summary>
    /// Stops the camera
    /// </summary>
    public virtual void StopInternal(bool force = false)
    {
        if (IsDisposing || IsDisposed)
            return;

        System.Diagnostics.Debug.WriteLine($"[CAMERA] Stopped {Uid} {Tag}");

        NativeControl?.Stop(force);
        State = CameraState.Off;
        //DisplayImage.IsVisible = false;
    }


    /// <summary>
    /// Override this method to customize DisplayInfo content
    /// </summary>
    public virtual void UpdateInfo()
    {
        var info = $"Position: {Facing}" +
                   $"\nState: {State}" +
                   //$"\nSize: {Width}x{Height} pts" +
                   $"\nPreview: {PreviewSize} px" +
                   $"\nPhoto: {CapturePhotoSize} px" +
                   $"\nRotation: {this.DeviceRotation}";

        if (Display != null)
        {
            info += $"\nAspect: {Display.Aspect}";
        }

        DisplayInfo = info;
    }


    public Stream CreateOutputStream(CapturedImage captured,
        bool reorient,
        SKEncodedImageFormat format = SKEncodedImageFormat.Jpeg,
        int quality = 90)
    {
        try
        {
            var rotated = Reorient();
            var data = rotated.Encode(format, quality);
            return data.AsStream();

            SKBitmap Reorient()
            {
                var bitmap = SKBitmap.FromImage(captured.Image);

                if (!reorient)
                    return bitmap;

                SKBitmap rotated;

                switch (captured.Rotation)
                {
                    case 180:
                        using (var surface = new SKCanvas(bitmap))
                        {
                            surface.RotateDegrees(180, bitmap.Width / 2.0f, bitmap.Height / 2.0f);
                            surface.DrawBitmap(bitmap.Copy(), 0, 0);
                        }

                        return bitmap;
                    case 270:
                        rotated = new SKBitmap(bitmap.Height, bitmap.Width);
                        using (var surface = new SKCanvas(rotated))
                        {
                            surface.Translate(rotated.Width, 0);
                            surface.RotateDegrees(90);
                            surface.DrawBitmap(bitmap, 0, 0);
                        }

                        return rotated;
                    case 90:
                        rotated = new SKBitmap(bitmap.Height, bitmap.Width);
                        using (var surface = new SKCanvas(rotated))
                        {
                            surface.Translate(0, rotated.Height);
                            surface.RotateDegrees(270);
                            surface.DrawBitmap(bitmap, 0, 0);
                        }

                        return rotated;
                    default:
                        return bitmap;
                }
            }
        }
        catch (Exception e)
        {
            Trace.WriteLine(e);
            return null;
        }
    }

    #endregion

    #region ENGINE

    protected virtual void OnCaptureSuccess(CapturedImage captured)
    {
        CaptureSuccess?.Invoke(this, captured);
    }

    protected virtual void OnCaptureFailed(Exception ex)
    {
        CaptureFailed?.Invoke(this, ex);
    }


    public INativeCamera NativeControl;


    protected override void OnLayoutReady()
    {
        base.OnLayoutReady();

        if (State == CameraState.Error)
            StartInternal();
    }

    bool subscribed;

    public override void SuperViewChanged()
    {
        if (Superview != null && !subscribed)
        {
            subscribed = true;
            Superview.DeviceRotationChanged += DeviceRotationChanged;
        }

        base.SuperViewChanged();
    }


    private void DeviceRotationChanged(object sender, int orientation)
    {
        var rotation = ((orientation + 45) / 90) * 90 % 360;

        DeviceRotation = rotation;
    }

    private int _DeviceRotation;

    public int DeviceRotation
    {
        get { return _DeviceRotation; }
        set
        {
            if (_DeviceRotation != value)
            {
                _DeviceRotation = value;
                OnPropertyChanged();
                NativeControl?.ApplyDeviceOrientation(value);
                UpdateInfo();
            }
        }
    }

    object lockFrame = new();

    public bool FrameAquired { get; set; }

    public virtual void UpdatePreview()
    {
        FrameAquired = false;
        Update();
    }

    public SKSurface FrameSurface { get; protected set; }
    public SKImageInfo FrameSurfaceInfo { get; protected set; }

    //public bool AllocatedFrameSurface(int width, int height)
    //{
    //    if (Superview == null || width == 0 || height == 0)
    //    {
    //        return false;
    //    }

    //    var kill = FrameSurface;
    //    FrameSurfaceInfo = new SKImageInfo(width, height);
    //    if (Superview.CanvasView is SkiaViewAccelerated accelerated)
    //    {
    //        FrameSurface = SKSurface.Create(accelerated.GRContext, true, FrameSurfaceInfo);
    //    }
    //    else
    //    {
    //        //normal one
    //        FrameSurface = SKSurface.Create(FrameSurfaceInfo);
    //    }

    //    kill?.Dispose();

    //    return true;
    //}

    protected virtual void OnNewFrameSet(LoadedImageSource source)
    {
        NewPreviewSet?.Invoke(this, source);
    }

    protected virtual SKImage AquireFrameFromNative()
    {
        return NativeControl.GetPreviewImage();
    }

    protected virtual void SetFrameFromNative()
    {
        if (NativeControl != null && !FrameAquired)
        {
            //acquire latest image from camera
            var image = AquireFrameFromNative();
            if (image != null)
            {
                FrameAquired = true;
                OnNewFrameSet(Display.SetImageInternal(image, false));
            }
        }
    }

    protected override void Paint(DrawingContext ctx)
    {
        base.Paint(ctx);

        if (State == CameraState.On)
        {
            SetFrameFromNative();
        }

        DrawViews(ctx);

        if (ConstantUpdate && State == CameraState.On)
        {
            Update();
        }
    }

    #endregion

#if (!ANDROID && !IOS && !MACCATALYST && !WINDOWS && !TIZEN)
    public SKBitmap GetPreviewBitmap()
    {
        throw new NotImplementedException();
    }


#endif


    bool lockStartup;

    public virtual void Start()
    {
        IsOn = true;
    }

    private static void PowerChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaCamera control)
        {
            control.StopInternal(true);
            if (control.IsOn)
            {
                control.StartWithPermissionsInternal();
            }
        }
    }

    /// <summary>
    /// Request permissions and start camera without setting IsOn true. Will set IsOn to false if permissions denied.
    /// </summary>
    public virtual void StartWithPermissionsInternal()
    {
        if (lockStartup)
        {
            Debug.WriteLine("[SkiaCamera] Startup locked.");
            return;
        }

        lockStartup = true;

        try
        {
            Debug.WriteLine("[SkiaCamera] Requesting permissions...");

            SkiaCamera.CheckPermissions((presented) =>
                {
                    Debug.WriteLine("[SkiaCamera] Starting..");
                    PermissionsWarning = false;
                    StartInternal();

                    //if (Geotag)
                    //	CommandGetLocation.Execute(null);
                    //else
                    //{
                    //	CanDetectLocation = false;
                    //}
                },
                (presented) =>
                {
                    Super.Log("[SkiaCamera] Permissions denied");
                    IsOn = false;
                    PermissionsWarning = true;
                });
        }
        catch (Exception e)
        {
            Trace.WriteLine(e);
        }
        finally
        {
            Tasks.StartDelayed(TimeSpan.FromSeconds(1), () =>
            {
                Debug.WriteLine("[SkiaCamera] Startup UNlocked.");
                lockStartup = false;
            });
        }
    }

    /// <summary>
    /// Starts the camera after permissions where acquired
    /// </summary>
    protected virtual void StartInternal()
    {
        if (IsDisposing || IsDisposed)
            return;

        if (NativeControl == null)
        {
#if ONPLATFORM
            CreateNative();
#endif
        }

        //var rotation = ((Superview.DeviceRotation + 45) / 90) % 4;
        //NativeControl?.ApplyDeviceOrientation(rotation);

        if (Display != null)
        {
            //DestroyRenderingObject();
            Display.IsVisible = true;
        }

        //IsOn = true;

        NativeControl?.Start();
    }

    #region SkiaCamera xam control

    private bool _PermissionsWarning;

    public bool PermissionsWarning
    {
        get { return _PermissionsWarning; }
        set
        {
            if (_PermissionsWarning != value)
            {
                _PermissionsWarning = value;
                OnPropertyChanged();
            }
        }
    }


    public class CameraQueuedPictured
    {
        public string Filename { get; set; }

        public double SensorRotation { get; set; }

        /// <summary>
        /// Set by renderer after work
        /// </summary>
        public bool Processed { get; set; }
    }

    public class CameraPicturesQueue : Queue<CameraQueuedPictured>
    {
    }

    private bool _IsBusy;

    public bool IsBusy
    {
        get { return _IsBusy; }
        set
        {
            if (_IsBusy != value)
            {
                _IsBusy = value;
                OnPropertyChanged();
            }
        }
    }


    private bool _IsTakingPhoto;

    public bool IsTakingPhoto
    {
        get { return _IsTakingPhoto; }
        set
        {
            if (_IsTakingPhoto != value)
            {
                _IsTakingPhoto = value;
                OnPropertyChanged();
            }
        }
    }


    public CameraPicturesQueue PicturesQueue { get; } = new CameraPicturesQueue();


    #region PERMISSIONS

    protected static bool ChecksBusy = false;

    private static DateTime lastTimeChecked = DateTime.MinValue;

    public static bool PermissionsGranted { get; protected set; }


    public static void CheckGalleryPermissions(Action granted, Action notGranted)
    {
        if (lastTimeChecked + TimeSpan.FromSeconds(5) < DateTime.Now) //avoid spam
        {
            lastTimeChecked = DateTime.Now;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (ChecksBusy)
                    return;

                bool okay1 = false;


                ChecksBusy = true;
                // Update the UI
                try
                {
                    var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
                    if (status != PermissionStatus.Granted)
                    {
                        status = await Permissions.RequestAsync<Permissions.Camera>();


                        if (status == PermissionStatus.Granted)
                        {
                            okay1 = true;
                        }
                    }
                    else
                    {
                        okay1 = true;
                    }


                    // Could prompt to enable in settings if needed
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex);
                }
                finally
                {
                    if (okay1)
                    {
                        PermissionsGranted = true;
                        granted?.Invoke();
                    }
                    else
                    {
                        PermissionsGranted = false;
                        notGranted?.Invoke();
                    }

                    ChecksBusy = false;
                }
            });
        }
    }

    private bool _GpsBusy;

    public bool GpsBusy
    {
        get { return _GpsBusy; }
        set
        {
            if (_GpsBusy != value)
            {
                _GpsBusy = value;
                OnPropertyChanged();
            }
        }
    }

    private double _LocationLat;

    public double LocationLat
    {
        get { return _LocationLat; }
        set
        {
            if (_LocationLat != value)
            {
                _LocationLat = value;
                OnPropertyChanged();
            }
        }
    }

    private double _LocationLon;

    public double LocationLon
    {
        get { return _LocationLon; }
        set
        {
            if (_LocationLon != value)
            {
                _LocationLon = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _CanDetectLocation;

    public bool CanDetectLocation
    {
        get { return _CanDetectLocation; }
        set
        {
            if (_CanDetectLocation != value)
            {
                _CanDetectLocation = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Safe and if CanDetectLocation
    /// </summary>
    /// <returns></returns>
    public async Task RefreshLocation(int msTimeout)
    {
        if (CanDetectLocation)
        {
            //my ACTUAL location
            try
            {
                GpsBusy = true;

                var request = new GeolocationRequest(GeolocationAccuracy.Medium);
                var cancel = new CancellationTokenSource();
                cancel.CancelAfter(msTimeout);
                var location = await Geolocation.GetLocationAsync(request, cancel.Token);

                if (location != null)
                {
                    Debug.WriteLine(
                        $"ACTUAL Latitude: {location.Latitude}, Longitude: {location.Longitude}, Altitude: {location.Altitude}");

                    this.LocationLat = location.Latitude;
                    this.LocationLon = location.Longitude;
                }
            }
            catch (FeatureNotSupportedException fnsEx)
            {
                // Handle not supported on device exception
                //Toast.ShortMessage("GPS не поддерживается на устройстве");
            }
            catch (FeatureNotEnabledException fneEx)
            {
                // Handle not enabled on device exception
                //Toast.ShortMessage("GPS отключен на устройстве");
            }
            catch (PermissionException pEx)
            {
                // Handle permission exception
                //Toast.ShortMessage("Вы не дали разрешение на использование GPS");
            }
            catch (Exception ex)
            {
                // Unable to get location
            }
            finally
            {
                GpsBusy = false;
            }
        }
    }

    //public ICommand CommandGetLocation
    //{
    //	get
    //	{
    //		return new Command((object context) =>
    //		{
    //			if (GpsBusy || !App.Native.CheckGpsEnabled())
    //				return;

    //			MainThread.BeginInvokeOnMainThread(async () =>
    //			{
    //				// Update the UI
    //				try
    //				{
    //					GpsBusy = true;

    //					var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
    //					if (status != PermissionStatus.Granted)
    //					{
    //						CanDetectLocation = false;

    //						await App.Current.MainPage.DisplayAlert(Core.Current.MyCompany.Name, ResStrings.X_NeedMoreForGeo, ResStrings.ButtonOk);

    //						status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
    //						if (status != PermissionStatus.Granted)
    //						{
    //							// Additionally could prompt the user to turn on in settings
    //							return;
    //						}
    //						else
    //						{
    //							CanDetectLocation = true;
    //						}
    //					}
    //					else
    //					{
    //						CanDetectLocation = true;
    //					}

    //					if (CanDetectLocation)
    //					{
    //						//my LAST location:
    //						try
    //						{
    //							if (App.Native.CheckGpsEnabled())
    //							{
    //								var location = await Geolocation.GetLastKnownLocationAsync();

    //								if (location != null)
    //								{
    //									Debug.WriteLine(
    //										$"LAST Latitude: {location.Latitude}, Longitude: {location.Longitude}, Altitude: {location.Altitude}");

    //									LocationLat = location.Latitude;
    //									LocationLon = location.Longitude;
    //								}
    //							}
    //						}
    //						catch (FeatureNotSupportedException fnsEx)
    //						{
    //							// Handle not supported on device exception
    //							//Toast.ShortMessage("GPS не поддерживается на устройстве");
    //						}
    //						catch (FeatureNotEnabledException fneEx)
    //						{
    //							// Handle not enabled on device exception
    //							//Toast.ShortMessage("GPS отключен на устройстве");
    //						}
    //						catch (PermissionException pEx)
    //						{
    //							// Handle permission exception
    //							//Toast.ShortMessage("Вы не дали разрешение на использование GPS");
    //						}
    //						catch (Exception ex)
    //						{
    //							// Unable to get location
    //						}

    //						await Task.Run(async () =>
    //						{
    //							await RefreshLocation(1200);

    //						}).ConfigureAwait(false);

    //					}
    //					else
    //					{
    //						GpsBusy = false;
    //					}


    //				}
    //				catch (Exception ex)
    //				{
    //					//Something went wrong
    //					Trace.WriteLine(ex);
    //					CanDetectLocation = false;
    //					GpsBusy = false;
    //				}
    //				finally
    //				{

    //				}

    //			});


    //		});
    //	}
    //}

    /// <summary>
    /// Will pass the fact if permissions dialog was diplayed as bool
    /// </summary>
    /// <param name="granted"></param>
    /// <param name="notGranted"></param>
    public static void CheckPermissions(Action<bool> granted, Action<bool> notGranted)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (ChecksBusy)
                return;

            bool grantedCam = false;
            bool grantedStorage = false;
            bool presented = false;

            ChecksBusy = true;
            // Update the UI
            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
                if (status != PermissionStatus.Granted)
                {
                    presented = true;

                    status = await Permissions.RequestAsync<Permissions.Camera>();


                    if (status == PermissionStatus.Granted)
                    {
                        grantedCam = true;
                    }
                }
                else
                {
                    grantedCam = true;
                }

                var needStorage = true;
                if (Device.RuntimePlatform == Device.Android && DeviceInfo.Version.Major > 9)
                {
                    needStorage = false;
                }

                if (needStorage)
                {
                    status = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
                    if (status != PermissionStatus.Granted)
                    {
                        presented = true;

                        status = await Permissions.RequestAsync<Permissions.StorageWrite>();

                        if (status == PermissionStatus.Granted)
                        {
                            grantedStorage = true;
                        }
                    }
                    else
                    {
                        grantedStorage = true;
                    }
                }
                else
                {
                    grantedStorage = true;
                }


                // Could prompt to enable in settings if needed
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
            finally
            {
                if (grantedCam && grantedStorage)
                {
                    PermissionsGranted = true;
                    granted?.Invoke(presented);
                }
                else
                {
                    PermissionsGranted = false;
                    notGranted?.Invoke(presented);
                }

                ChecksBusy = false;
            }
        });
    }

    #endregion


    /// <summary>
    /// This is filled by renderer  
    /// </summary>
    public string SavedFilename
    {
        get { return _SavedFilename; }
        set
        {
            if (_SavedFilename != value)
            {
                _SavedFilename = value;
                OnPropertyChanged("SavedFilename");
            }
        }
    }

    private string _SavedFilename;

    public static readonly BindableProperty CaptureLocationProperty = BindableProperty.Create(
        nameof(CaptureLocation),
        typeof(CaptureLocationType),
        typeof(SkiaCamera),
        CaptureLocationType.Gallery);

    public CaptureLocationType CaptureLocation
    {
        get { return (CaptureLocationType)GetValue(CaptureLocationProperty); }
        set { SetValue(CaptureLocationProperty, value); }
    }

    public static readonly BindableProperty CaptureCustomFolderProperty = BindableProperty.Create(
        nameof(CaptureCustomFolder),
        typeof(string),
        typeof(SkiaCamera),
        string.Empty);

    public string CaptureCustomFolder
    {
        get { return (string)GetValue(CaptureCustomFolderProperty); }
        set { SetValue(CaptureCustomFolderProperty, value); }
    }


    public static readonly BindableProperty FacingProperty = BindableProperty.Create(
        nameof(Facing),
        typeof(CameraPosition),
        typeof(SkiaCamera),
        CameraPosition.Default, propertyChanged: NeedRestart);

    private static void NeedRestart(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaCamera control)
        {
            if (control.State == CameraState.On)
            {
                control.StopInternal();
            }

            if (control.IsOn)
            {
                control.StartInternal();
            }
            else
            {
                control.Start();
            }
        }
    }

    public CameraPosition Facing
    {
        get { return (CameraPosition)GetValue(FacingProperty); }
        set { SetValue(FacingProperty, value); }
    }

    public static readonly BindableProperty CameraIndexProperty = BindableProperty.Create(
        nameof(CameraIndex),
        typeof(int),
        typeof(SkiaCamera),
        -1, propertyChanged: NeedRestart);

    /// <summary>
    /// Camera index for manual camera selection.
    /// When set to -1 (default), uses automatic selection based on Facing property.
    /// When Facing is set to Manual, this property determines which camera to use.
    /// </summary>
    public int CameraIndex
    {
        get { return (int)GetValue(CameraIndexProperty); }
        set { SetValue(CameraIndexProperty, value); }
    }

    public static readonly BindableProperty CapturePhotoQualityProperty = BindableProperty.Create(
        nameof(CapturePhotoQuality),
        typeof(CaptureQuality),
        typeof(SkiaCamera),
        CaptureQuality.Max);

    /// <summary>
    /// Photo capture quality
    /// </summary>
    public CaptureQuality CapturePhotoQuality
    {
        get { return (CaptureQuality)GetValue(CapturePhotoQualityProperty); }
        set { SetValue(CapturePhotoQualityProperty, value); }
    }


    public static readonly BindableProperty TypeProperty = BindableProperty.Create(
        nameof(Type),
        typeof(CameraType),
        typeof(SkiaCamera),
        CameraType.Default);

    /// <summary>
    /// To be implemented
    /// </summary>
    public CameraType Type
    {
        get { return (CameraType)GetValue(TypeProperty); }
        set { SetValue(TypeProperty, value); }
    }


    /// <summary>
    /// Will be applied to viewport for focal length etc
    /// </summary>
    public CameraUnit CameraDevice
    {
        get { return _virtualCameraUnit; }
        set
        {
            if (_virtualCameraUnit != value)
            {
                if (_virtualCameraUnit != value)
                {
                    _virtualCameraUnit = value;
                    AssignFocalLengthInternal(value);
                }
            }
        }
    }

    private CameraUnit _virtualCameraUnit;

    public void AssignFocalLengthInternal(CameraUnit value)
    {
        if (value != null)
        {
            FocalLength = (float)(value.FocalLength * value.SensorCropFactor);
        }

        OnPropertyChanged(nameof(CameraDevice));
    }

    private int _PreviewWidth;

    public int PreviewWidth
    {
        get { return _PreviewWidth; }
        set
        {
            if (_PreviewWidth != value)
            {
                _PreviewWidth = value;
                OnPropertyChanged("PreviewWidth");
            }
        }
    }

    private int _PreviewHeight;

    public int PreviewHeight
    {
        get { return _PreviewHeight; }
        set
        {
            if (_PreviewHeight != value)
            {
                _PreviewHeight = value;
                OnPropertyChanged("PreviewHeight");
            }
        }
    }

    private int _CaptureWidth;

    public int CaptureWidth
    {
        get { return _CaptureWidth; }
        set
        {
            if (_CaptureWidth != value)
            {
                _CaptureWidth = value;
                OnPropertyChanged("CaptureWidth");
            }
        }
    }

    private int _CaptureHeight;

    public int CaptureHeight
    {
        get { return _CaptureHeight; }
        set
        {
            if (_CaptureHeight != value)
            {
                _CaptureHeight = value;
                OnPropertyChanged("CaptureHeight");
            }
        }
    }


    protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName == nameof(FocalLength) || propertyName == nameof(FocalLengthAdjustment))
        {
            FocalLengthAdjusted = FocalLength + FocalLengthAdjustment;
        }
    }

    public static double GetSensorRotation(DeviceOrientation orientation)
    {
        if (orientation == DeviceOrientation.PortraitUpsideDown)
            return 180.0;

        if (orientation == DeviceOrientation.LandscapeLeft)
            return 90.0;

        if (orientation == DeviceOrientation.LandscapeRight)
            return 270.0;

        return 0.0;
    }


    public static readonly BindableProperty CapturedStillImageProperty = BindableProperty.Create(
        nameof(CapturedStillImage),
        typeof(CapturedImage),
        typeof(SkiaCamera),
        null);

    public CapturedImage CapturedStillImage
    {
        get { return (CapturedImage)GetValue(CapturedStillImageProperty); }
        set { SetValue(CapturedStillImageProperty, value); }
    }


    public static readonly BindableProperty CustomAlbumProperty = BindableProperty.Create(nameof(CustomAlbum),
        typeof(string),
        typeof(SkiaCamera),
        string.Empty);

    /// <summary>
    /// If not null will use this instead of Camera Roll folder for photos output
    /// </summary>
    public string CustomAlbum
    {
        get { return (string)GetValue(CustomAlbumProperty); }
        set { SetValue(CustomAlbumProperty, value); }
    }


    public static readonly BindableProperty GeotagProperty = BindableProperty.Create(nameof(Geotag),
        typeof(bool),
        typeof(SkiaCamera),
        false);

    /// <summary>
    /// try to inject location metadata if to photos if GPS succeeds
    /// </summary>
    public bool Geotag
    {
        get { return (bool)GetValue(GeotagProperty); }
        set { SetValue(GeotagProperty, value); }
    }


    public static readonly BindableProperty FocalLengthProperty = BindableProperty.Create(
        nameof(FocalLength),
        typeof(double),
        typeof(SkiaCamera),
        0.0);

    public double FocalLength
    {
        get { return (double)GetValue(FocalLengthProperty); }
        set { SetValue(FocalLengthProperty, value); }
    }

    public static readonly BindableProperty FocalLengthAdjustedProperty = BindableProperty.Create(
        nameof(FocalLengthAdjusted),
        typeof(double),
        typeof(SkiaCamera),
        0.0);

    public double FocalLengthAdjusted
    {
        get { return (double)GetValue(FocalLengthAdjustedProperty); }
        set { SetValue(FocalLengthAdjustedProperty, value); }
    }

    public static readonly BindableProperty FocalLengthAdjustmentProperty = BindableProperty.Create(
        nameof(FocalLengthAdjustment),
        typeof(double),
        typeof(SkiaCamera),
        0.0);

    public double FocalLengthAdjustment
    {
        get { return (double)GetValue(FocalLengthAdjustmentProperty); }
        set { SetValue(FocalLengthAdjustmentProperty, value); }
    }

    public static readonly BindableProperty ManualZoomProperty = BindableProperty.Create(
        nameof(ManualZoom),
        typeof(bool),
        typeof(SkiaCamera),
        false);

    public bool ManualZoom
    {
        get { return (bool)GetValue(ManualZoomProperty); }
        set { SetValue(ManualZoomProperty, value); }
    }

    public static readonly BindableProperty ZoomProperty = BindableProperty.Create(
        nameof(Zoom),
        typeof(double),
        typeof(SkiaCamera),
        1.0,
        propertyChanged: NeedSetZoom);

    /// <summary>
    /// Zoom camera
    /// </summary>
    public double Zoom
    {
        get { return (double)GetValue(ZoomProperty); }
        set { SetValue(ZoomProperty, value); }
    }

    public static readonly BindableProperty ConstantUpdateProperty = BindableProperty.Create(
        nameof(ConstantUpdate),
        typeof(bool),
        typeof(SkiaCamera),
        true);

    /// <summary>
    /// Default is true.
    /// Whether it should update non-stop or only when a new frame is acquired.
    /// For example if camera gives frames at 30 fps, screen might update around 40fps without this set to true.
    /// If enabled will force max redraws at 60 fps.
    /// </summary>
    public bool ConstantUpdate
    {
        get { return (bool)GetValue(ConstantUpdateProperty); }
        set { SetValue(ConstantUpdateProperty, value); }
    }

    public static readonly BindableProperty ViewportScaleProperty = BindableProperty.Create(
        nameof(ViewportScale),
        typeof(double),
        typeof(SkiaCamera),
        1.0);

    /// <summary>
    /// Zoom viewport value, NOT a camera zoom,
    /// </summary>
    public double ViewportScale
    {
        get { return (double)GetValue(ViewportScaleProperty); }
        set { SetValue(ViewportScaleProperty, value); }
    }

    public static readonly BindableProperty TextureScaleProperty = BindableProperty.Create(
        nameof(TextureScale),
        typeof(double),
        typeof(SkiaCamera),
        1.0, defaultBindingMode: BindingMode.OneWayToSource);

    public double TextureScale
    {
        get { return (double)GetValue(TextureScaleProperty); }
        set { SetValue(TextureScaleProperty, value); }
    }

    public static readonly BindableProperty ZoomLimitMinProperty = BindableProperty.Create(
        nameof(ZoomLimitMin),
        typeof(double),
        typeof(SkiaCamera),
        1.0);

    public double ZoomLimitMin
    {
        get { return (double)GetValue(ZoomLimitMinProperty); }
        set { SetValue(ZoomLimitMinProperty, value); }
    }

    public static readonly BindableProperty ZoomLimitMaxProperty = BindableProperty.Create(
        nameof(ZoomLimitMax),
        typeof(double),
        typeof(SkiaCamera),
        10.0);

    public double ZoomLimitMax
    {
        get { return (double)GetValue(ZoomLimitMaxProperty); }
        set { SetValue(ZoomLimitMaxProperty, value); }
    }


    private static void NeedSetZoom(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaCamera control)
        {
            var zoom = (double)newvalue;
            if (zoom < control.ZoomLimitMin)
            {
                zoom = control.ZoomLimitMin;
            }
            else if (zoom > control.ZoomLimitMax)
            {
                zoom = control.ZoomLimitMax;
            }

            control.SetZoom(zoom);
        }
    }

    protected override void OnLayoutChanged()
    {
        base.OnLayoutChanged();

        Display.Aspect = this.Aspect;
    }

    //public static readonly BindableProperty DisplayModeProperty = BindableProperty.Create(
    //    nameof(DisplayMode),
    //    typeof(StretchModes),
    //    typeof(SkiaCamera),
    //    StretchModes.Fill);

    //public StretchModes DisplayMode
    //{
    //    get { return (StretchModes)GetValue(DisplayModeProperty); }
    //    set { SetValue(DisplayModeProperty, value); }
    //}

    public static readonly BindableProperty AspectProperty = BindableProperty.Create(
        nameof(Aspect),
        typeof(TransformAspect),
        typeof(SkiaImage),
        TransformAspect.AspectCover,
        propertyChanged: NeedInvalidateMeasure);

    /// <summary>
    /// Apspect to render image with, default is AspectCover. 
    /// </summary>
    public TransformAspect Aspect
    {
        get { return (TransformAspect)GetValue(AspectProperty); }
        set { SetValue(AspectProperty, value); }
    }


    public static readonly BindableProperty StateProperty = BindableProperty.Create(
        nameof(State),
        typeof(CameraState),
        typeof(SkiaCamera),
        CameraState.Off,
        BindingMode.OneWayToSource, propertyChanged: ControlStateChanged);

    private static void ControlStateChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaCamera control)
        {
            control.StateChanged?.Invoke(control, control.State);
            control.UpdateInfo();
        }
    }

    public CameraState State
    {
        get { return (CameraState)GetValue(StateProperty); }
        set { SetValue(StateProperty, value); }
    }

    public event EventHandler<CameraState> StateChanged;

    public static readonly BindableProperty IsOnProperty = BindableProperty.Create(
        nameof(IsOn),
        typeof(bool),
        typeof(SkiaCamera),
        false,
        propertyChanged: PowerChanged);

    public bool IsOn
    {
        get { return (bool)GetValue(IsOnProperty); }
        set { SetValue(IsOnProperty, value); }
    }


    public static readonly BindableProperty PickerModeProperty = BindableProperty.Create(
        nameof(PickerMode),
        typeof(CameraPickerMode),
        typeof(SkiaCamera),
        CameraPickerMode.None);

    public CameraPickerMode PickerMode
    {
        get { return (CameraPickerMode)GetValue(PickerModeProperty); }
        set { SetValue(PickerModeProperty, value); }
    }

    public static readonly BindableProperty FilterProperty = BindableProperty.Create(
        nameof(Filter),
        typeof(CameraEffect),
        typeof(SkiaCamera),
        CameraEffect.None);

    public CameraEffect Filter
    {
        get { return (CameraEffect)GetValue(FilterProperty); }
        set { SetValue(FilterProperty, value); }
    }


    public double SavedRotation { get; set; }


    //public bool
    //ShowSettings
    //{
    //    get { return (bool)GetValue(PageCamera.ShowSettingsProperty); }
    //    set { SetValue(PageCamera.ShowSettingsProperty, value); }
    //}

    #endregion

    /// <summary>
    /// The size of the camera preview in pixels 
    /// </summary>

    public SKSize PreviewSize
    {
        get { return _previewSize; }
        set
        {
            if (_previewSize != value)
            {
                _previewSize = value;
                OnPropertyChanged();
            }
        }
    }

    SKSize _previewSize;


    public SKSize CapturePhotoSize
    {
        get { return _capturePhotoSize; }

        set
        {
            if (_capturePhotoSize != value)
            {
                _capturePhotoSize = value;
                OnPropertyChanged();
                //UpdateInfo();
            }
        }
    }

    SKSize _capturePhotoSize;

    public void SetRotatedContentSize(SKSize size, int cameraRotation)
    {
        if (size.Width < 0 || size.Height < 0)
        {
            throw new Exception("Camera preview size cannot be negative.");
        }

        PreviewSize = size;

        Invalidate();
    }

    private string _DisplayInfo;
    private bool _hasPermissions;

    public string DisplayInfo
    {
        get { return _DisplayInfo; }
        set
        {
            if (_DisplayInfo != value)
            {
                _DisplayInfo = value;
                OnPropertyChanged();
            }
        }
    }

    #region PROPERTIES

    public static readonly BindableProperty EffectProperty = BindableProperty.Create(
        nameof(Effect),
        typeof(SkiaImageEffect),
        typeof(SkiaCamera),
        SkiaImageEffect.None,
        propertyChanged: NeedSetupPreview);

    private static void NeedSetupPreview(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaCamera control)
        {
            control.ApplyPreviewProperties();
        }
    }

    public SkiaImageEffect Effect
    {
        get { return (SkiaImageEffect)GetValue(EffectProperty); }
        set { SetValue(EffectProperty, value); }
    }

    #endregion

    private void Super_OnNativeAppPaused(object sender, EventArgs e)
    {
        StopAll();
    }

    private void Super_OnNativeAppResumed(object sender, EventArgs e)
    {
        ResumeIfNeeded();
    }

    public void ResumeIfNeeded()
    {
        if (IsOn)
            StartInternal();
    }

    public static List<SkiaCamera> Instances = new();

    /// <summary>
    /// Stops all instances
    /// </summary>
    public static void StopAll()
    {
        foreach (var renderer in Instances)
        {
            renderer.StopInternal(true);
        }
    }
}
