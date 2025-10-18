using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Net;
using System.Windows.Input;
using AppoMobi.Maui.Gestures;
using DrawnUi.Draw;
using DrawnUi.Extensions;
using Mapsui;
using Mapsui.Disposing;
using Mapsui.Extensions;
using Mapsui.Fetcher;
using Mapsui.Layers;
using Mapsui.Logging;
using Mapsui.Manipulations;
using Mapsui.Projections;
using Mapsui.Rendering;
using Mapsui.Rendering.Skia;
using Mapsui.Tiling.Layers;
using Mapsui.UI;
using Mapsui.Utilities;
using Mapsui.Widgets;
using SkiaSharp;
using Map = Mapsui.Map;
using Timer = System.Threading.Timer;

namespace DrawnUi.MapsUi;

/// <summary>
/// MapControl for MAUI DrawnUi.
/// Beware whole world mainly uses LAT LON, MapsUi uses mainly LON LAT
/// </summary>
public partial class SkiaMapsUi : SkiaLayout, IMapControl, ISkiaGestureListener
{
    /// <summary>
    /// Lat Lon
    /// </summary>
    public event EventHandler<Point> ClickedPoint;

    public event EventHandler<MapPin> ClickedPin;

    protected MapPin SelectedPin
    {
        get => selectedPin;
        set
        {
            if (value == selectedPin)
                return;
            selectedPin = value;
            OnSelectedPinChanged();
            OnPropertyChanged();
        } 
    }
    private MapPin selectedPin;

    protected virtual void OnSelectedPinChanged()
    {

    }

    protected virtual void SendClickedPin(MapPin pin)
    {
        SelectedPin = pin;
        ClickedPin?.Invoke(this, pin);
        CommandTappedPin?.Execute(pin);
    }

    protected virtual void SendClicked(Point point)
    {
        LastClicked = point;

        ClickedPoint?.Invoke(this, point);

        CommandTapped?.Execute(point);
    }

    public Point LastClicked;

    public static readonly BindableProperty CommandTappedProperty = BindableProperty.Create(nameof(CommandTapped),
        typeof(ICommand),
        typeof(SkiaMapsUi),
        null);

    public ICommand CommandTapped
    {
        get { return (ICommand)GetValue(CommandTappedProperty); }
        set { SetValue(CommandTappedProperty, value); }
    }

    public static readonly BindableProperty CommandTappedPinProperty = BindableProperty.Create(nameof(CommandTappedPin),
        typeof(ICommand),
        typeof(SkiaMapsUi),
        null);

    public ICommand CommandTappedPin
    {
        get { return (ICommand)GetValue(CommandTappedPinProperty); }
        set { SetValue(CommandTappedPinProperty, value); }
    }

    #region PINS

    public void CenterTo(double lat, double lon, double zoom, long duration = -1,
        Mapsui.Animations.Easing? easing = null)
    {
        var point = new MPoint(lat, lon);
        var sphericalMercatorCoordinate = SphericalMercator.FromLonLat(point.Y, point.X).ToMPoint();

        Map.Navigator.CenterOnAndZoomTo(
            sphericalMercatorCoordinate, zoom, duration, easing);
    }

    public int GetCurrentZoomLevel()
    {
        var currentResolution = Map.Navigator.Viewport.Resolution;
        var resolutions = Map.Navigator.Resolutions.ToList();

        for (int i = 0; i < resolutions.Count; i++)
        {
            if (Math.Abs(resolutions[i] - currentResolution) < 0.0001)
            {
                return i;
            }
        }

        return resolutions.FindIndex(r => r <= currentResolution);
    }

    protected SKPaint PaintPins;

    public virtual SkiaControl GetPinIcon(MapPin pin)
    {
        return pin?.Icon;
    }

    /// <summary>
    /// Renders pin on map. Will return null if was not rendered, otherwise returns icon.
    /// Will not render if Icon is not set or is out of viewport bounds.
    /// </summary>
    /// <param name="ctx"></param>
    /// <param name="pin"></param>
    /// <returns></returns>
    protected virtual SkiaControl RenderPin(DrawingContext ctx, MapPin pin)
    {
        if (pin.Icon == null || !pin.IsVisible)
            return null;

        var icon = GetPinIcon(pin);

        var pinSize = icon.MeasuredSize.Pixels;
        if (icon.NeedMeasure || !icon.IsRenderObjectValid(pinSize))
        {
            RasterizePin(ctx, pin, icon);
            pinSize = icon.MeasuredSize.Pixels;
        }

        //will return null if out of bounds, so we can skip rendering
        var screenPos = GetScreenPosition(pin.Longitude, pin.Latitude, pinSize.Width, pinSize.Height);

        if (!screenPos.HasValue)
            return null;

        var x = screenPos.Value.X;
        var y = screenPos.Value.Y;

        var positionX = (float)(x - pinSize.Width / 2.0);
        var positionY = (float)(y - pinSize.Height);

        icon.DrawRenderObject(ctx, positionX, positionY);

        return icon;
    }

    private HitTestChild[] _hitTestCache;
    private int _hitTestCount;

    public readonly struct HitTestChild
    {
        public readonly MapPin Child;
        public readonly SKRect Rect;

        public HitTestChild(MapPin control, SKRect rect)
        {
            Child = control;
            Rect = rect;
        }
    }

    protected virtual void RasterizePin(DrawingContext ctx, MapPin pin, SkiaControl icon)
    {
        RasterizeIcon(ctx, icon);
    }

    protected virtual void RasterizeIcon(DrawingContext ctx, SkiaControl icon)
    {
        if (icon == null)
        {
            return;
        }

        var destination = new SKRect(0, 0, float.PositiveInfinity, float.PositiveInfinity);

        var measuredSize = icon.Measure(destination.Width, destination.Height, ctx.Scale);
        var size = measuredSize.Pixels;

        icon.Arrange(
            new SKRect(0, 0, size.Width, size.Height),
            size.Width, size.Height, ctx.Scale);

        icon.RenderObject = icon.CreateRenderedObject(ctx, icon.DrawingRect, false);
    }

    #endregion

    #region PINS PROP

    private static void PinsPropertyChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaMapsUi control)
        {
            var enumerablePins = (IEnumerable<MapPin>)newvalue;

            if (oldvalue != null)
            {
                if (oldvalue is INotifyCollectionChanged oldCollection)
                {
                    oldCollection.CollectionChanged -= control.OnPinCollectionChanged;
                }

                if (oldvalue is IEnumerable<MapPin> oldList)
                {
                    foreach (var position in oldList)
                    {
                        //position.Dettach();
                    }
                }
            }

            foreach (var position in enumerablePins)
            {
                //position.Attach(control);
            }

            if (newvalue is INotifyCollectionChanged newCollection)
            {
                newCollection.CollectionChanged -= control.OnPinCollectionChanged;
                newCollection.CollectionChanged += control.OnPinCollectionChanged;
            }

            control.Update();
        }
    }

    private void OnPinCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (MapPin newSkiaPropertyPin in e.NewItems)
                {
                    //newSkiaPropertyPin.Attach(this);
                }

                break;

            case NotifyCollectionChangedAction.Reset:
            case NotifyCollectionChangedAction.Remove:
                foreach (MapPin oldSkiaPropertyPin in e.OldItems ?? new MapPin[0])
                {
                    //oldSkiaPropertyPin.Dettach();
                }

                break;
        }

        Update();
    }

    public new static readonly BindableProperty PinsProperty = BindableProperty.Create(
        nameof(Pins),
        typeof(IList<MapPin>),
        typeof(SkiaMapsUi),
        defaultValueCreator: (instance) =>
        {
            var created = new AppoMobi.Specials.ObservableRangeCollection<MapPin>();
            created.CollectionChanged += ((SkiaMapsUi)instance).OnPinCollectionChanged;
            return created;
        },
        validateValue: (bo, v) => v is IList<MapPin>,
        propertyChanged: PinsPropertyChanged,
        coerceValue: CoercePins);

    private static int instanceCount = 0;

    /// <summary>
    /// Please use UI thread to change this observable collection
    /// </summary>
    public new IList<MapPin> Pins
    {
        get => (IList<MapPin>)GetValue(PinsProperty);
        set => SetValue(PinsProperty, value);
    }

    private static object CoercePins(BindableObject bindable, object value)
    {
        if (!(value is ReadOnlyCollection<MapPin> readonlyCollection))
        {
            return value;
        }

        return new ReadOnlyCollection<MapPin>(
            readonlyCollection.ToList());
    }

    #endregion

    public class SkiaMapLayer : SkiaControl
    {
        public SkiaMapLayer()
        {
            HorizontalOptions = LayoutOptions.Fill;
            VerticalOptions = LayoutOptions.Fill;
        }
    }

    public SkiaMapsUi()
    {
        SharedConstructor();

        Map = new()
        {
            BackColor = Mapsui.Styles.Color.FromArgb(0, 0, 0, 0), //feel the freedom..
            CRS = "EPSG:3857"
        };
    }

    public List<SkiaMapLayer> Layers;

    protected override void Paint(DrawingContext ctx)
    {
        //paint background
        if (ctx.Destination.Width == 0 || ctx.Destination.Height == 0 || IsDisposing || IsDisposed)
            return;
        PaintTintBackground(ctx.Context.Canvas, ctx.Destination);
        WasDrawn = true;

        //map
        ctx.Context.Canvas.Save();
        ctx.Context.Canvas.ClipRect(ctx.Destination);
        ctx.Context.Canvas.Translate(ctx.Destination.Left, ctx.Destination.Top);
        ctx.Context.Canvas.Scale(PixelDensity, PixelDensity);

        //paint map into the layout area
        CommonDrawControl(ctx.Context.Canvas);

        ctx.Context.Canvas.Restore();

        //children overlay
        ClearDirtyChildren();

        var rectForChildren = ContractPixelsRect(ctx.Destination, ctx.Scale, UsePadding);

        //absolute layout
        DrawViews(ctx.WithDestination(rectForChildren));
        if (LayoutReady)
        {
            OnAppeared();
        }
    }

    protected override void OnLayoutChanged()
    {
        base.OnLayoutChanged();

        ClearTouchState();
        SetViewportSize();
    }

    protected override void OnLayoutReady()
    {
        base.OnLayoutReady();

        // Start the invalidation timer
        StartUpdates(false);
    }

    private static bool GetShiftPressed() => KeyboardManager.IsShiftPressed;
    private readonly ConcurrentDictionary<long, ScreenPosition> _positions = new();
    private Size _oldSize;
    private static List<WeakReference<SkiaMapsUi>>? _listeners;
    private readonly ManipulationTracker _manipulationTracker = new();

    /// <summary>
    /// Pixels
    /// </summary>
    private double ViewportWidth => Width;

    /// <summary>
    /// Pixels
    /// </summary>
    private double ViewportHeight => Height;

    #region GESTURES

    /// <summary>
    /// Clears the Touch State
    /// </summary>
    public void ClearTouchState()
    {
        _positions.Clear();
    }

    public new bool OnFocusChanged(bool focus)
    {
        return false;
    }

    protected override int DrawViews(DrawingContext ctx)
    {
        int countRendered = 1; //map counts

        if (Pins != null)
        {
            if (_hitTestCache != null)
                ArrayPool<HitTestChild>.Shared.Return(_hitTestCache, clearArray: true);

            _hitTestCache = ArrayPool<HitTestChild>.Shared.Rent(Pins.Count);
            _hitTestCount = 0;

            foreach (var pin in Pins)
            {
                var icon = RenderPin(ctx, pin);
                if (icon != null)
                {
                    countRendered++;
                    var rect = icon.LastDrawnAt;
                    if (icon.RenderObject != null)
                    {
                        rect = icon.RenderObject.LastDrawnAt;
                    }
                    _hitTestCache[_hitTestCount++] = new HitTestChild(pin, rect);
                }
            }
        }

        return countRendered;
    }

 
    public virtual MapPin HitTestPins(SKPoint point)
    {
        for (int i = _hitTestCount - 1; i >= 0; i--) // Reverse for top-to-bottom
        {
            if (_hitTestCache[i].Rect.ContainsInclusive(point))
            {
                return _hitTestCache[i].Child;
            }
        }

        return null;
    }

    public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
    {
        var consumedDefault = BlockGesturesBelow ? this : null;

        var consumed = consumedDefault;

        var point = TranslateInputOffsetToPixels(args.Event.Location, apply.ChildOffset);

        var position = new ScreenPosition((point.X - DrawingRect.Left) / RenderingScale,
            (point.Y - DrawingRect.Top) / RenderingScale);

        //TAP
        if (args.Type == TouchActionResult.Tapped)
        {
            var thisOffset = TranslateInputCoords(apply.ChildOffset);
            var x = args.Event.Location.X + thisOffset.X;
            var y = args.Event.Location.Y + thisOffset.Y;
            var relativeX = x - LastDrawnAt.Left; //inside this control
            var relativeY = y - LastDrawnAt.Top; //inside this control

            var clickedPin = HitTestPins(new (x, y));
            if (clickedPin!=null)
            {
                SendClickedPin(clickedPin);
                return this;
            }

            var coords = GetGeographicCoordinate(relativeX, relativeY);

            if (coords.HasValue)
            {
                var consumedThis = ProcessClick(coords.Value.Latitude, coords.Value.Longitude);
                if (consumedThis != null)
                {
                    return consumedThis;
                }

                return consumedDefault;
            }
        }
        else if (args.Type == TouchActionResult.Down)
        {
            _positions[args.Event.Id] = position;
            if (_positions.Count == 1) // Not sure if this check is necessary.
                _manipulationTracker.Restart(_positions.Values.ToArray());

            OnMapPointerPressed(_positions.Values.ToArray());

            return base.ProcessGestures(args, apply);
        }
        else if (args.Type == TouchActionResult.Panning)
        {
            _positions[args.Event.Id] = position;

            if (OnMapPointerMoved(_positions.Values.ToArray(), false))
                return consumed;

            //if (args.Event.NumberOfTouches > 1)
            //{
            //    //maybe zooming, so apply limits
            //    var currentZoomLevel = GetCurrentZoomLevel();
            //    Debug.WriteLine($"ZOOM {currentZoomLevel}");

            //    // Check if the zoom change would exceed limits
            //    if ((args.Event.Manipulation.Scale>0 && currentZoomLevel >= MaxZoomLevel) ||
            //        (args.Event.Manipulation.Scale < 0 && currentZoomLevel <= MinZoomLevel))
            //    {
            //        // Don't allow zoom if it would exceed limits
            //    }
            //    else
            //    {
            //        _manipulationTracker.Manipulate(_positions.Values.ToArray(), Map.Navigator.Manipulate);
            //        RefreshGraphics();
            //    }
            //}
            //else
            {
                _manipulationTracker.Manipulate(_positions.Values.ToArray(), Map.Navigator.Manipulate);
                RefreshGraphics();
            }
        }
        else if (args.Type == TouchActionResult.Up)
        {
            //            _positions.Remove(args.Id, out var releasedTouch);
            _positions.Clear();
            OnMapPointerReleased([position]);

            Refresh();
        }
        //todo add event
        //else if (e.ActionType == SKTouchAction.WheelChanged)
        //{
        //    OnZoomInOrOut(e.WheelDelta, position);
        //}
        else if (args.Type == TouchActionResult.Wheel)
        {
            _wasPinching = true;

            if (_lastPinch != 0 || args.Event.Wheel.Delta != 0)
            {
                double delta = 0;
                if (args.Event.Wheel.Delta != 0)
                {
                    delta = args.Event.Wheel.Delta * PinchMultiplier * ZoomSpeed;
                }
                else
                {
                    delta = (args.Event.Wheel.Scale - _lastPinch) * PinchMultiplier * ZoomSpeed;

                    if (Math.Abs(delta) < 10)
                    {
                        _pinchAccumulated += delta;
                        if (Math.Abs(_pinchAccumulated) < 10)
                        {
                            delta = 0;
                        }
                        else
                        {
                            delta = _pinchAccumulated;
                            _pinchAccumulated = 0;
                        }
                    }
                    else
                    {
                        _pinchAccumulated = 0;
                    }
                }

                _lastPinch = args.Event.Wheel.Scale;

                if (delta != 0)
                {
                    point = TranslateInputOffsetToPixels(args.Event.Wheel.Center, apply.ChildOffset);

                    position = new ScreenPosition((point.X - DrawingRect.Left) / RenderingScale,
                        (point.Y - DrawingRect.Top) / RenderingScale);

                    var intZoom = (int)Math.Round(delta);

                    // Add zoom limit check
                    var currentZoom = GetCurrentZoomLevel();
                    if ((intZoom > 0 && currentZoom < MaxZoomLevel) ||
                        (intZoom < 0 && currentZoom > MinZoomLevel))
                    {
                        if (Math.Abs(intZoom) >= 0.1)
                            OnZoomInOrOut(intZoom, position);
                    }
                }
            }
            else
            {
                //attach
                _lastPinch = args.Event.Wheel.Scale;
            }
        }

        return consumed;
    }


    private Point _coordsSelected;
    public bool CanClick { get; set; } = true;

    protected virtual ISkiaGestureListener ProcessClick(double lat, double lon)
    {
        if (!CanClick)
        {
            return null;
        }

        _coordsSelected = new(lon, lat);

        SendClicked(new(_coordsSelected.X, _coordsSelected.Y));

        Update();

        return this;
    }

    double _lastPinch = 0;
    bool _wasPinching = false;

    #endregion

    public static readonly BindableProperty ZoomSpeedProperty = BindableProperty.Create(nameof(ZoomSpeed),
        typeof(double), typeof(SkiaMapsUi),
        0.9);

    /// <summary>
    /// How much of finger movement will afect zoom change
    /// </summary>
    public double ZoomSpeed
    {
        get { return (double)GetValue(ZoomSpeedProperty); }
        set { SetValue(ZoomSpeedProperty, value); }
    }

    /// <summary>
    /// Magic ergonomic number, you can change it
    /// </summary>
    public static float PinchMultiplier = 55.0f;

    private ScreenPosition GetScreenPosition(SKPoint point) =>
        new ScreenPosition(point.X / PixelDensity, point.Y / PixelDensity);

    private void OnZoomInOrOut(int mouseWheelDelta, ScreenPosition centerOfZoom)
    {
        var currentResolution = Map.Navigator.Viewport.Resolution;
        var currentZoomLevel = GetCurrentZoomLevel();

        // Check if the zoom change would exceed limits
        if ((mouseWheelDelta > 0 && currentZoomLevel >= MaxZoomLevel) ||
            (mouseWheelDelta < 0 && currentZoomLevel <= MinZoomLevel))
        {
            return; // Don't allow zoom if it would exceed limits
        }

        Map.Navigator.MouseWheelZoom(mouseWheelDelta, centerOfZoom);
    }

    public static readonly BindableProperty MinZoomLevelProperty = BindableProperty.Create(
        nameof(MinZoomLevel),
        typeof(double),
        typeof(SkiaMapsUi),
        1.0);

    public static readonly BindableProperty MaxZoomLevelProperty = BindableProperty.Create(
        nameof(MaxZoomLevel),
        typeof(double),
        typeof(SkiaMapsUi),
        19.0);

    /// <summary>
    /// Gets or sets the minimum allowed zoom level (1-19)
    /// </summary>
    public double MinZoomLevel
    {
        get => (double)GetValue(MinZoomLevelProperty);
        set => SetValue(MinZoomLevelProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum allowed zoom level (1-19)
    /// </summary>
    public double MaxZoomLevel
    {
        get => (double)GetValue(MaxZoomLevelProperty);
        set => SetValue(MaxZoomLevelProperty, value);
    }

    /// <summary>
    /// Public functions
    /// </summary>
    public void OpenInBrowser(string url)
    {
        Catch.TaskRun(() => _ = Launcher.OpenAsync(new Uri(url)));
    }

    public override void OnDisposing()
    {
        base.OnDisposing();

        var weakReference = _listeners?.FirstOrDefault(f => f.TryGetTarget(out var control) && control == this);
        if (weakReference != null)
        {
            _listeners?.Remove(weakReference);
        }

        Map?.Dispose();

        CommonDispose(true);
    }

    //---------------------------------------------------
    //from shared but already changed a bit:
    //---------------------------------------------------

    // Flag indicating if a drawing process is running
    private bool _drawing;

    // Flag indicating if the control has to be redrawn
    private bool _invalidated;

    // Flag indicating if a new drawing process should start
    private bool _refresh;
    private IRenderer _renderer = new MapRenderer();
    //private IRenderer _renderer = new DrawnUiSkiaMapRenderer();

    // Timer for loop to invalidating the control
    private Timer? _invalidateTimer;

    // Interval between two calls of the invalidate function in ms
    private int _updateInterval = 16;

    // Stopwatch for measuring drawing times
    private readonly System.Diagnostics.Stopwatch _stopwatch = new();
    private readonly TapGestureTracker _tapGestureTracker = new();
    private readonly FlingTracker _flingTracker = new();

    /// <summary>
    /// The movement allowed between a touch down and touch up in a touch gestures in device independent pixels.
    /// </summary>
    public int MaxTapGestureMovement { get; set; } = 8;

    /// <summary>
    /// Use fling gesture to move the map. Default is true. Fling means that the map will continue to move for a 
    /// short time after the user has lifted the finger.
    /// </summary>
    public bool UseFling { get; set; } = true;

    private void SharedConstructor()
    {
        PlatformUtilities.SetOpenInBrowserFunc(OpenInBrowser);
        // Create timer for invalidating the control
        _invalidateTimer?.Dispose();
        _invalidateTimer = new Timer(InvalidateTimerCallback, null,
            Timeout.Infinite, 16);

        // Mapsui.Rendering.Skia use Mapsui.Nts where GetDbaseLanguageDriver need encoding providers
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
    }

    private protected void CommonDrawControl(object canvas)
    {
        if (_drawing) return;
        if (Renderer is null) return;
        if (Map is null) return;
        if (!Map.Navigator.Viewport.HasSize()) return;

        // Start drawing
        _drawing = true;

        // Start stopwatch before updating animations and drawing control
        _stopwatch.Restart();

        // All requested updates up to this point will be handled by this redraw
        _refresh = false;
        Renderer.Render(canvas, Map.Navigator.Viewport, Map.Layers, Map.Widgets, Map.BackColor);

        // Stop stopwatch after drawing control
        _stopwatch.Stop();

        // If we are interested in performance measurements, we save the new drawing time
        _performance?.Add(_stopwatch.Elapsed.TotalMilliseconds);

        // End drawing
        _drawing = false;
        _invalidated = false;
    }

    public event EventHandler<bool> LoadingChanged;

    public ILayer LayerMap { get; protected set; }

    public bool IsLoading { get; protected set; }

    object lockTimer = new();

    private void InvalidateTimerCallback(object? state)
    {
        lock (lockTimer)
        {
            try
            {
                // In MAUI if you use binding there is an event where the new value is null even though
                // the current value en the value you are binding to are not null. Perhaps this should be
                // considered a bug.
                if (Map is null) return;

                // Access the tile layer
                var tileLayer = LayerMap as TileLayer;
                if (tileLayer != null)
                {
                    // Check if tiles are still loading
                    if (tileLayer.Busy)
                    {
                        if (!IsLoading)
                        {
                            IsLoading = true;
                            // Tiles are still loading
                            LoadingChanged?.Invoke(this, true);
                        }
                    }
                    else
                    {
                        if (IsLoading)
                        {
                            IsLoading = false;
                            // All tiles in view are loaded - safe to capture PDF
                            LoadingChanged?.Invoke(this, false);
                        }
                    }
                }

                // Check, if we have to redraw the screen

                if (Map?.UpdateAnimations() == true)
                    _refresh = true;

                // seems that this could be null sometimes
                if (Map?.Navigator?.UpdateAnimations() ?? false)
                    _refresh = true;

                // Check if widgets need refresh
                if (!_refresh && (Map?.Widgets?.Any(w => w.NeedsRedraw) ?? false))
                    _refresh = true;

                if (!_refresh)
                    return;

                if (_drawing)
                {
                    if (_performance != null)
                        _performance.Dropped++;

                    return;
                }

                if (_invalidated)
                {
                    return;
                }

                _invalidated = true;
                Update();
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, ex.Message, ex);
            }
        }
    }

    /// <summary>
    /// Start updates for control
    /// </summary>
    /// <remarks>
    /// When this function is called, the control is redrawn if needed
    /// </remarks>
    public void StartUpdates(bool refresh = true)
    {
        _refresh = refresh;
        _invalidateTimer?.Change(0, _updateInterval);
    }


    /// <summary>
    /// Stop updates for control
    /// </summary>
    /// <remarks>
    /// When this function is called, the control stops to redraw itself, 
    /// even if it is needed
    /// </remarks>
    public void StopUpdates()
    {
        _invalidateTimer?.Change(Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// Force a update of control
    /// </summary>
    /// <remarks>
    /// When this function is called, the control draws itself once 
    /// </remarks>
    public void ForceUpdate()
    {
        _invalidated = true;
        Update();
    }

    /// <summary>
    /// Interval between two redraws of the SkiaMapControl in ms
    /// </summary>
    public int UpdateInterval
    {
        get => _updateInterval;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(UpdateInterval), value,
                    "Parameter must be greater than zero");

            if (_updateInterval != value)
            {
                _updateInterval = value;
                StartUpdates();
            }
        }
    }

    private Performance? _performance;

    /// <summary>
    /// Object to save performance information about the drawing of the map
    /// </summary>
    /// <remarks>
    /// If this is null, no performance information is saved.
    /// </remarks>
    public Performance? Performance
    {
        get => _performance;
        set
        {
            if (_performance != value)
            {
                _performance = value;
                OnPropertyChanged();
            }
        }
    }

    public float PixelDensity => this.RenderingScale;

    /// <summary>
    /// Renderer that is used from this SkiaMapControl
    /// </summary>
    public IRenderer Renderer => _renderer;

    /// <summary>
    /// Called whenever the map is clicked. The MapInfoEventArgs contain the features that were hit in
    /// the layers that have IsMapInfoLayer set to true. 
    /// </summary>
    public event EventHandler<MapInfoEventArgs> Info;

    /// <summary>
    /// Unsubscribe from map events 
    /// </summary>
    public void Unsubscribe()
    {
        UnsubscribeFromMapEvents(Map);
    }

    /// <summary>
    /// Subscribe to map events
    /// </summary>
    /// <param name="map">Map, to which events to subscribe</param>
    private void SubscribeToMapEvents(Map map)
    {
        map.DataChanged -= Map_DataChanged;
        map.PropertyChanged -= Map_PropertyChanged;
        map.RefreshGraphicsRequest -= Map_RefreshGraphicsRequest;

        map.DataChanged += Map_DataChanged;
        map.PropertyChanged += Map_PropertyChanged;
        map.RefreshGraphicsRequest += Map_RefreshGraphicsRequest;
    }

    private void Map_RefreshGraphicsRequest(object? sender, EventArgs e)
    {
        RefreshGraphics();
    }

    /// <summary>
    /// Unsubscribe from map events
    /// </summary>
    /// <param name="map">Map, to which events to unsubscribe</param>
    private void UnsubscribeFromMapEvents(Map map)
    {
        var localMap = map;
        localMap.DataChanged -= Map_DataChanged;
        localMap.PropertyChanged -= Map_PropertyChanged;
        localMap.RefreshGraphicsRequest -= Map_RefreshGraphicsRequest;
        localMap.AbortFetch();
    }

    public void Refresh(ChangeType changeType = ChangeType.Discrete)
    {
        Map.Refresh(changeType);

        Update();
    }

    public void RefreshGraphics()
    {
        _refresh = true;

        Update();
    }

    private void Map_DataChanged(object? sender, DataChangedEventArgs? e)
    {
        try
        {
            if (e == null)
            {
                Logger.Log(LogLevel.Warning, "Unexpected error: DataChangedEventArgs can not be null");
            }
            //else if (e.Cancelled)
            //{
            //    Logger.Log(LogLevel.Warning, "Fetching data was cancelled.");
            //}
            else if (e.Error is WebException)
            {
                Logger.Log(LogLevel.Warning,
                    $"A WebException occurred. Do you have internet? Exception: {e.Error?.Message}", e.Error);
            }
            else if (e.Error != null)
            {
                Logger.Log(LogLevel.Warning, $"An error occurred while fetching data. Exception: {e.Error?.Message}",
                    e.Error);
            }
            else // no problems
            {
                RefreshGraphics();
            }
        }
        catch (Exception exception)
        {
            Logger.Log(LogLevel.Warning, $"Unexpected exception in {nameof(Map_DataChanged)}", exception);
        }
    }

    // ReSharper disable RedundantNameQualifier - needed for iOS for disambiguation
    private void Map_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Mapsui.Layers.Layer.Enabled))
        {
            RefreshGraphics();
        }
        else if (e.PropertyName == nameof(Mapsui.Layers.Layer.Opacity))
        {
            RefreshGraphics();
        }
        else if (e.PropertyName == nameof(Map.BackColor))
        {
            RefreshGraphics();
        }
        else if (e.PropertyName == nameof(Mapsui.Layers.Layer.DataSource))
        {
            Refresh(); // There is a new DataSource so let's fetch the new data.
        }
        else if (e.PropertyName == nameof(Map.Extent))
        {
            Refresh();
        }
        else if (e.PropertyName == nameof(Map.Layers))
        {
            Refresh();
        }
    }

    // ReSharper restore RedundantNameQualifier
    private DisposableWrapper<Map>? _map;

    public static readonly BindableProperty MapProperty = BindableProperty.Create(nameof(Map),
        typeof(Map), typeof(SkiaMapsUi), default(Map), defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: MapPropertyChanged, propertyChanging: MapPropertyChanging);

    private double _pinchAccumulated;

    private static void MapPropertyChanging(BindableObject bindable,
        object oldValue, object newValue)
    {
        var mapControl = (SkiaMapsUi)bindable;
        mapControl.BeforeSetMap();
    }

    private static void MapPropertyChanged(BindableObject bindable,
        object oldValue, object newValue)
    {
        var mapControl = (SkiaMapsUi)bindable;
        mapControl.AfterSetMap((Map)newValue);
    }

    public Map Map
    {
        get => (Map)GetValue(MapProperty);
        set => SetValue(MapProperty, value);
    }

    private void BeforeSetMap()
    {
        if (Map is null)
            return; // Although the Map property can not null the map argument can null during initializing and binding.

        UnsubscribeFromMapEvents(Map);
    }

    private void AfterSetMap(Map? map)
    {
        if (map is null)
            return; // Although the Map property can not null the map argument can null during initializing and binding.

        map.Navigator.SetSize(ViewportWidth, ViewportHeight);
        SubscribeToMapEvents(map);
        Refresh();
    }

    /// <inheritdoc />
    public MPoint ToPixels(MPoint coordinateInDeviceIndependentUnits)
    {
        return new MPoint(
            coordinateInDeviceIndependentUnits.X * PixelDensity,
            coordinateInDeviceIndependentUnits.Y * PixelDensity);
    }

    /// <inheritdoc />
    public MPoint ToDeviceIndependentUnits(MPoint coordinateInPixels)
    {
        return new MPoint(coordinateInPixels.X / PixelDensity, coordinateInPixels.Y / PixelDensity);
    }

    /// <summary>
    /// Refresh data of Map, but don't paint it
    /// </summary>
    public void RefreshData(ChangeType changeType = ChangeType.Discrete)
    {
        Map.RefreshData(changeType);
    }

    protected void OnMapInfo(MapInfoEventArgs mapInfoEventArgs)
    {
        Map?.OnMapInfo(mapInfoEventArgs); // Also propagate to Map
        Info?.Invoke(this, mapInfoEventArgs);
    }

    /// <inheritdoc />
    public MapInfo GetMapInfo(ScreenPosition screenPosition, int margin = 0)
    {
        return Renderer.GetMapInfo(screenPosition.X, screenPosition.Y, Map.Navigator.Viewport, Map?.Layers ?? [],
            margin);
    }

    /// <inheritdoc />
    public byte[] GetSnapshot(IEnumerable<ILayer>? layers = null)
    {
        using var stream = Renderer.RenderToBitmapStream(Map.Navigator.Viewport, layers ?? Map?.Layers ?? [],
            pixelDensity: PixelDensity);
        return stream.ToArray();
    }

    /// <summary>
    /// Check if a widget or feature at a given screen position is clicked/tapped
    /// </summary>
    /// <param name="screenPosition">Screen position to check for widgets and features</param>
    /// <param name="tapType">single or double tap</param>
    /// <returns>True, if something done </returns>
    private MapInfoEventArgs CreateMapInfoEventArgs(ScreenPosition screenPosition, TapType tapType)
    {
        var mapInfo =
            Renderer.GetMapInfo(screenPosition.X, screenPosition.Y, Map.Navigator.Viewport, Map?.Layers ?? []);

        return new MapInfoEventArgs(mapInfo, tapType, false);
    }

    private void SetViewportSize()
    {
        var hadSize = Map.Navigator.Viewport.HasSize();
        Map.Navigator.SetSize(ViewportWidth, ViewportHeight);
        if (!hadSize && Map.Navigator.Viewport.HasSize()) Map.OnViewportSizeInitialized();
        Refresh();
    }

    private void CommonDispose(bool disposing)
    {
        if (disposing)
        {
            Unsubscribe();
            StopUpdates();
            _invalidateTimer?.Dispose();
            _invalidateTimer = null;
            _renderer.Dispose();
            _map?.Dispose();
            _map = null;
        }

        _invalidateTimer = null;
    }

    private bool OnWidgetPointerPressed(ScreenPosition position, bool shiftPressed)
    {
        foreach (var widget in WidgetInput.GetWidgetsAtPosition(position, Map))
        {
            Logger.Log(LogLevel.Information, $"Widget.PointerPressed: {widget.GetType().Name}");
            if (widget.OnPointerPressed(Map.Navigator,
                    new WidgetEventArgs(position, 0, true, shiftPressed, () => GetMapInfo(position))))
                return true;
        }

        return false;
    }

    private bool OnWidgetPointerMoved(ScreenPosition position, bool leftButton, bool shiftPressed)
    {
        foreach (var widget in WidgetInput.GetWidgetsAtPosition(position, Map))
            if (widget.OnPointerMoved(Map.Navigator,
                    new WidgetEventArgs(position, 0, leftButton, shiftPressed, () => GetMapInfo(position))))
                return true;
        return false;
    }

    private bool OnWidgetPointerReleased(ScreenPosition position, bool shiftPressed)
    {
        foreach (var widget in WidgetInput.GetWidgetsAtPosition(position, Map))
        {
            Logger.Log(LogLevel.Information, $"Widget.Released: {widget.GetType().Name}");
            if (widget.OnPointerReleased(Map.Navigator,
                    new WidgetEventArgs(position, 0, true, shiftPressed, () => GetMapInfo(position))))
                return true;
        }

        return false;
    }

    private bool OnWidgetTapped(ScreenPosition position, TapType tapType, bool shiftPressed)
    {
        var touchedWidgets = WidgetInput.GetWidgetsAtPosition(position, Map);
        foreach (var widget in touchedWidgets)
        {
            Logger.Log(LogLevel.Information,
                $"Widget.Tapped: {widget.GetType().Name} TapType: {tapType} KeyState: {shiftPressed}");
            var e = new WidgetEventArgs(position, tapType, true, shiftPressed, () => GetMapInfo(position));
            if (widget.OnTapped(Map.Navigator, e))
                return true;
        }

        return false;
    }

    private bool OnMapPointerPressed(ReadOnlySpan<ScreenPosition> positions)
    {
        if (positions.Length != 1)
            return false;

        _flingTracker.Restart();
        _tapGestureTracker.Restart(positions[0]);
        return OnWidgetPointerPressed(positions[0], GetShiftPressed());
    }

    private bool OnMapPointerMoved(ReadOnlySpan<ScreenPosition> positions, bool isHovering = false)
    {
        if (positions.Length != 1)
            return false;

        if (OnWidgetPointerMoved(positions[0], !isHovering, GetShiftPressed()))
            return true;
        if (!isHovering)
            _flingTracker.AddEvent(positions[0], DateTime.Now.Ticks);
        return false;
    }

    private bool OnMapPointerReleased(ReadOnlySpan<ScreenPosition> positions)
    {
        if (positions.Length != 1)
            return false;

        var handled = false;
        if (OnWidgetPointerReleased(positions[0], GetShiftPressed()))
            handled = true; // Set to handled but still handle tap in the next line
        if (_tapGestureTracker.TapIfNeeded(positions[0], MaxTapGestureMovement * PixelDensity, OnMapTapped))
            handled = true;
        if (UseFling)
            _flingTracker.FlingIfNeeded((vX, vY) => Map.Navigator.Fling(vX, vY, 1000));
        Refresh();
        return handled;
    }

    private bool OnMapTapped(ScreenPosition position, TapType tapType)
    {
        if (OnWidgetTapped(position, tapType, GetShiftPressed()))
            return true;
        OnMapInfo(CreateMapInfoEventArgs(position, TapType.Single));
        return false;
    }

    #region HELPERS

    /// <summary>
    /// Converts geographic coordinates to screen coordinates taking into account current map state.
    /// Returns null if the point is outside the visible area.
    /// The result is in PIXELS
    /// </summary>
    protected SKPoint? GetScreenPosition(double longitude, double latitude, float inflateBoundsWidthPixels = 0, float inflateBoundsHeightPixels = 0)
    {
        try
        {
            // Convert to spherical mercator
            var worldPos = SphericalMercator.FromLonLat(longitude, latitude);
            // Convert to screen coordinates
            var screen = Map.Navigator.Viewport.WorldToScreen(worldPos.x, worldPos.y);

            // Check if point is within visible bounds
            if (screen.X < -inflateBoundsWidthPixels || screen.Y < -inflateBoundsHeightPixels ||
                screen.X > (DrawingRect.Width + inflateBoundsWidthPixels) / RenderingScale ||
                screen.Y > (DrawingRect.Height + inflateBoundsHeightPixels) / RenderingScale)
            {
                return null;
            }

            // Convert to actual pixel coordinates on our canvas
            return new SKPoint(
                (float)Math.Round(screen.X * RenderingScale + DrawingRect.Left),
                (float)Math.Round(screen.Y * RenderingScale + DrawingRect.Top)
            );
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Converts screen pixel coordinates to geographic coordinates (latitude, longitude)
    /// taking into account current map state and pixel density.
    /// </summary>
    /// <param name="pixelX">Screen X coordinate in pixels</param>
    /// <param name="pixelY">Screen Y coordinate in pixels</param>
    /// <returns>Tuple containing (latitude, longitude) or null if conversion fails</returns>
    protected (double Latitude, double Longitude)? GetGeographicCoordinate(double pixelX, double pixelY)
    {
        try
        {
            // Convert from canvas pixel coordinates to screen coordinates (remove offset and pixel density)
            var screenX = pixelX / RenderingScale;
            ; //(pixelX - DrawingRect.Left) / RenderingScale;
            var screenY = pixelY / RenderingScale;
            ; //(pixelY - DrawingRect.Top) / RenderingScale;

            // Convert screen coordinates to world coordinates
            var worldCoords = Map.Navigator.Viewport.ScreenToWorld(screenX, screenY);

            // Convert world coordinates (spherical mercator) to lat/lon
            var geographic = SphericalMercator.ToLonLat(worldCoords.X, worldCoords.Y);

            return (Latitude: geographic.lat, Longitude: geographic.lon);
        }
        catch
        {
            return null;
        }
    }

    #endregion
}
