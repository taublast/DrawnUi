using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace DrawnUi.Controls;

/// <summary>
/// A wheel-of-names spinner control that displays items in a circular arrangement
/// and allows spinning to select an item through gesture interaction.
/// </summary>
[ContentProperty("ItemTemplate")]
public class SkiaSpinner : SkiaLayout
{
    public SkiaSpinner()
    {
        CreateUi();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        _rangeAnimator?.Dispose();
        _flingAnimator?.Dispose();
    }

    #region UI

    protected SkiaWheelShape Wheel;

    protected virtual SkiaWheelShape CreateWheel()
    {
        return new SkiaWheelShape
        {
            Type = ShapeType.Circle,
            BackgroundColor = Colors.Black,
            StrokeColor = Colors.Grey,
            StrokeWidth = 2,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
    }

    void CreateUi()
    {
        Wheel = CreateWheel();

        Children = new List<SkiaControl>()
        {
            Wheel
        };
    }

    #endregion

    #region BINDABLE PROPERTIES

    // Redirect ItemsSource property to inner wheel shape
    public override void ApplyItemsSource()
    {
        SyncItemsSource();
    }

    // Redirect ItemsSource property to inner wheel shape
    public override void OnItemSourceChanged()
    {
        SyncItemsSource();
    }

    // Redirect ItemsSource property to inner wheel shape
    protected override void ItemsSourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
    {
        SyncItemsSource();
    }

    /// <summary>
    /// Redirect ItemsSource property to inner wheel shape
    /// </summary>
    void SyncItemsSource()
    {
        if (Wheel != null)
        {
            Wheel.ItemsSource = this.ItemsSource;
            Wheel.ItemTemplate = this.ItemTemplate ?? DefaultTemplate;
            Wheel.InverseVisualRotation = this.InverseVisualRotation;
        }

        UpdateSelectedIndexFromRotation();
    }

    public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(
        nameof(SelectedIndex),
        typeof(int),
        typeof(SkiaSpinner),
        -1,
        BindingMode.TwoWay,
        propertyChanged: OnSelectedIndexChanged);

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public static readonly BindableProperty WheelRotationProperty = BindableProperty.Create(
        nameof(WheelRotation),
        typeof(double),
        typeof(SkiaSpinner),
        0.0,
        propertyChanged: NeedApplyRotation);

    public double WheelRotation
    {
        get => (double)GetValue(WheelRotationProperty);
        set => SetValue(WheelRotationProperty, value);
    }

    static Color[] DefaultColors = new[]
    {
        Colors.Cyan, Colors.Blue, Colors.Green, Colors.Lime, Colors.Yellow, Colors.Orange, Colors.Red,
        Colors.Purple, Colors.Magenta, Colors.Pink,
    };

    /// <summary>
    /// To avoid repeating two same in a row
    /// </summary>
    private static Color _lastRandomColor = Colors.Transparent;

    /// <summary>
    /// To avoid repeating two same in start and end
    /// </summary>
    private static Color _firstRandomColor = Colors.Transparent;

    private static DataTemplate DefaultTemplate = new DataTemplate(() =>
    {
        var cell = new SkiaRichLabel()
        {
            UseCache = SkiaCacheType.Operations,
            HorizontalFillRatio = 0.5,
            HorizontalTextAlignment = DrawTextAlignment.Center,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            //VerticalOptions = LayoutOptions.Center,
            //Padding = 10,
            VerticalTextAlignment = TextAlignment.Center
        }.ObserveSelf((me, prop) =>
        {
            if (prop == nameof(BindingContext) && me.BindingContext != null)
            {
                me.Text = me.BindingContext as string;

                //get random color but never let them repeat with the previous one or at the edges
                //checks will work only if all cells are created in a single pass
                var color = Colors.Transparent;
                while (color.Equals(Colors.Transparent) || color.Equals(_lastRandomColor))
                {
                    color = DefaultColors[Random.Next(0, DefaultColors.Length - 1)];

                    //check if we are the last cell, avoid repeating color with the first cell
                    if (me.Parent is SkiaLayout layout && me.ContextIndex == layout.ItemsSource.Count - 1)
                    {
                        if (color.Equals(_firstRandomColor))
                        {
                            color = Colors.Transparent;
                        }
                    }
                }

                _lastRandomColor = color;
                me.BackgroundColor = color;
                if (me.ContextIndex == 0)
                {
                    _firstRandomColor = color;
                }
            }
        });

        return cell;
    });




    public new DataTemplate ItemTemplate
    {
        get => (DataTemplate)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public static readonly BindableProperty SelectionPositionProperty = BindableProperty.Create(
        nameof(SidePosition),
        typeof(SidePosition),
        typeof(SkiaSpinner),
        SidePosition.Right,
        propertyChanged: OnSelectionPositionChanged);

    /// <summary>
    /// Determines where on the wheel the selected item is calculated (Top, Right, Bottom, Left)
    /// </summary>
    public SidePosition SidePosition
    {
        get => (SidePosition)GetValue(SelectionPositionProperty);
        set => SetValue(SelectionPositionProperty, value);
    }

    static void OnSelectionPositionChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SkiaSpinner control)
        {
            control.UpdateSelectedIndexFromRotation();
        }
    }

    static void OnNeedUpdateWheel(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SkiaSpinner control)
        {
            control.SyncItemsSource();
        }
    }

    public static readonly BindableProperty SnapProperty = BindableProperty.Create(
        nameof(Snap),
        typeof(bool),
        typeof(SkiaSpinner),
        true);

    /// <summary>
    /// Whether it should snap after scrolling stopped, default is True.
    /// </summary>
    public bool Snap
    {
        get => (bool)GetValue(SnapProperty);
        set => SetValue(SnapProperty, value);
    }

    public static readonly BindableProperty VelocityProperty = BindableProperty.Create(
        nameof(Velocity),
        typeof(double),
        typeof(SkiaSpinner),
        2.0);

    /// <summary>
    /// How much the velocity in increased when rotating wheel using gestures. Default is 2.0.
    /// </summary>
    public double Velocity
    {
        get => (double)GetValue(VelocityProperty);
        set => SetValue(VelocityProperty, value);
    }

    public static readonly BindableProperty DecelerationProperty = BindableProperty.Create(
        nameof(Deceleration),
        typeof(double),
        typeof(SkiaSpinner),
        0.0003);

    /// <summary>
    /// How much friction will be applied when rotating. Default is 0.0003. 
    /// /// </summary>
    public double Deceleration
    {
        get => (double)GetValue(DecelerationProperty);
        set => SetValue(DecelerationProperty, value);
    }

    public static readonly BindableProperty InverseVisualRotationProperty = BindableProperty.Create(
        nameof(InverseVisualRotation),
        typeof(bool),
        typeof(SkiaSpinner),
        false,
        propertyChanged: OnNeedUpdateWheel);

    /// <summary>
    /// Controls the visual orientation direction. False = normal (readable at right), True = inverted (readable at left)
    /// </summary>
    public bool InverseVisualRotation
    {
        get => (bool)GetValue(InverseVisualRotationProperty);
        set => SetValue(InverseVisualRotationProperty, value);
    }

    public static readonly BindableProperty RespondsToGesturesProperty = BindableProperty.Create(
        nameof(RespondsToGestures),
        typeof(bool),
        typeof(SkiaSpinner),
        true);

    /// <summary>
    /// If disabled will not scroll using gestures. Scrolling will still be possible by code.
    /// </summary>
    public bool RespondsToGestures
    {
        get { return (bool)GetValue(RespondsToGesturesProperty); }
        set { SetValue(RespondsToGesturesProperty, value); }
    }

    #endregion

    #region EVENTS

    public event EventHandler<int> SelectedIndexChanged;

    #endregion

    #region PRIVATE FIELDS

    //readonly List<object> _itemsList = new();
    bool _isUpdatingFromRotation;

    #endregion

    #region PROPERTY CHANGED HANDLERS

    static void OnSelectedIndexChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SkiaSpinner control && !control._isUpdatingFromRotation)
        {
            control.UpdateWheelRotationFromIndex();
            control.SelectedIndexChanged?.Invoke(control, (int)newValue);
        }
    }

    static void NeedApplyRotation(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SkiaSpinner spinner)
        {
            spinner.UpdateSelectedIndexFromRotation();
            spinner.Wheel.Rotation = spinner.WheelRotation;
        }
    }

    #endregion

    protected int ItemsCount
    {
        get
        {
            if (ItemsSource == null)
            {
                return 0;
            }

            return ItemsSource.Count;
        }
    }

    #region PRIVATE METHODS



    void UpdateSelectedIndexFromRotation()
    {
        if (ItemsCount == 0)
        {
            return;
        }

        _isUpdatingFromRotation = true;

        var anglePerItem = 360.0 / ItemsCount;
        var positionOffset = GetSelectionPositionOffset();

        var adjustedRotation = (-WheelRotation + positionOffset) % 360;
        if (adjustedRotation < 0) adjustedRotation += 360;

        // When visual rotation is inverted, we need to flip the rotation calculation
        if (InverseVisualRotation)
        {
            adjustedRotation = (-adjustedRotation + positionOffset * 2) % 360;
            if (adjustedRotation < 0) adjustedRotation += 360;
        }

        var index = (int)Math.Round(adjustedRotation / anglePerItem) % ItemsCount;

        if (SelectedIndex != index)
        {
            SelectedIndex = index;
        }

        _isUpdatingFromRotation = false;
    }

    void UpdateWheelRotationFromIndex()
    {
        if (ItemsCount == 0) return;

        var anglePerItem = 360.0 / ItemsCount;
        var positionOffset = GetSelectionPositionOffset();

        var targetRotation = -(SelectedIndex * anglePerItem + positionOffset);

        // When visual rotation is inverted, flip the rotation calculation
        if (InverseVisualRotation)
        {
            targetRotation = -targetRotation - positionOffset * 2;
        }

        if (Math.Abs(WheelRotation - targetRotation) > 0.1)
        {
            WheelRotation = targetRotation;
        }
    }


    /// <summary>
    /// Gets the angle offset for the current selection position
    /// </summary>
    public double GetSelectionPositionOffset()
    {
        return SidePosition switch
        {
            SidePosition.Top => 0,
            SidePosition.Right => 90,
            SidePosition.Bottom => 180,
            SidePosition.Left => 270,
            _ => 90
        };
    }

    #endregion

    #region PUBLIC METHODS

    /// <summary>
    /// Spins the wheel to a specific index with animation
    /// </summary>
    /// <param name="index">Target index to spin to</param>
    /// <param name="spins">Number of extra full rotations (0 = direct spin)</param>
    /// <param name="speed">Animation duration in milliseconds</param>
    public void SpinToIndex(int index, int spins = 0, uint speed = 350)
    {
        if (ItemsCount == 0 || index < 0 || index >= ItemsCount) return;

        var targetRotation = GetRotationForIndex(index);

        // Add extra spins for dramatic effect
        targetRotation += (spins * 360);

        Rotate(targetRotation, speed);
    }

    /// <summary>
    /// Spins the wheel to a specific index with shortest path with animation
    /// </summary>
    /// <param name="index">Target index to spin to</param>
    /// <param name="spins">Number of extra full rotations (0 = direct spin)</param>
    /// <param name="speed">Animation duration in milliseconds</param>
    public void SpinToIndexShortest(int index, uint speed = 350)
    {
        if (ItemsCount == 0 || index < 0 || index >= ItemsCount) return;

        var baseTargetRotation = GetRotationForIndex(index);
        double finalTargetRotation;

        var shortestDistance = GetShortestRotationDistance(WheelRotation, baseTargetRotation);
        finalTargetRotation = WheelRotation + shortestDistance;

        Rotate(finalTargetRotation, speed);
    }

    /// <summary>
    /// Gets the shortest rotation distance from current rotation to target rotation
    /// </summary>
    /// <param name="currentRotation">Current wheel rotation</param>
    /// <param name="targetRotation">Target rotation</param>
    /// <returns>Shortest rotation distance (can be negative for counterclockwise)</returns>
    public double GetShortestRotationDistance(double currentRotation, double targetRotation)
    {
        var difference = targetRotation - currentRotation;

        // Normalize the difference to [-180, 180] range for shortest path
        while (difference > 180) difference -= 360;
        while (difference < -180) difference += 360;

        return difference;
    }

    /// <summary>
    /// Gets the rotation value needed to position a specific index at the selection position
    /// </summary>
    /// <param name="index">Target index</param>
    /// <returns>Rotation value in degrees</returns>
    public double GetRotationForIndex(int index)
    {
        if (ItemsCount == 0 || index < 0 || index >= ItemsCount)
            return WheelRotation;

        var anglePerItem = 360.0 / ItemsCount;
        var positionOffset = GetSelectionPositionOffset();

        // Calculate target rotation using the same logic as UpdateWheelRotationFromIndex
        var targetRotation = -(index * anglePerItem + positionOffset);

        // When visual rotation is inverted, flip the rotation calculation
        if (InverseVisualRotation)
        {
            targetRotation = -targetRotation - positionOffset * 2;
        }

        return targetRotation;
    }

    /// <summary>
    /// Spins the wheel to a random position with animation
    /// </summary>
    public void SpinToRandom()
    {
        if (ItemsCount == 0) return;

        StopScrolling();

        var random = new Random();
        var randomIndex = random.Next(ItemsCount);
        var extraSpins = random.Next(3, 8); // 3-7 full rotations for dramatic effect
        var animationDuration = (uint)(2000 + random.Next(1000)); // 2-3 seconds

        Debug.WriteLine($"SpinToRandom {randomIndex}");
        SpinToIndex(randomIndex, extraSpins, animationDuration);
    }

    /// <summary>
    /// Animates the wheel to a specific rotation
    /// </summary>
    public void Rotate(double targetRotation, uint durationMs = 500)
    {
        if (_rangeAnimator == null)
        {
            _rangeAnimator = new RangeAnimator(this);
        }
        else if (_rangeAnimator.IsRunning)
        {
            _rangeAnimator.Stop();
        }

        _rangeAnimator.Start(value => { WheelRotation = value; }, WheelRotation, targetRotation, durationMs,
            Easing.CubicOut);
    }

    #endregion

    #region SCROLLING

    protected RangeAnimator _rangeAnimator;
    protected ScrollFlingAnimator _flingAnimator;

    public void StopScrolling()
    {
        if (_flingAnimator != null && _flingAnimator.IsRunning)
        {
            _flingAnimator.Stop();
        }

        if (_rangeAnimator != null && _rangeAnimator.IsRunning)
        {
            _rangeAnimator.Stop();
        }

        //VelocityTrackerPan.Clear();
        //VelocityTrackerScale.Clear();
    }

    void StartFlingAnimation(double angularVelocity)
    {
        if (_flingAnimator == null)
        {
            _flingAnimator = new ScrollFlingAnimator(this);
            _flingAnimator.OnStop = () =>
            {
                SnapToNearestItem();
            };
            _flingAnimator.OnUpdated = (value) =>
            {
                WheelRotation = value;
            };
        }
        else
        if (_flingAnimator.IsRunning)
        {
            _flingAnimator.Stop();
        }

        Debug.WriteLine($"Start FLEEING with velocity {angularVelocity}");

        _flingAnimator.InitializeWithVelocity((float)WheelRotation, (float)angularVelocity, 1 - (float)Deceleration);
        _flingAnimator.Start();
    }

    void SnapToNearestItem()
    {
        if (Children.Count == 0 || !Snap)
        {
            return;
        }

        var targetRotation = GetRotationForIndex(SelectedIndex);
        if (!CompareDoubles(targetRotation, WheelRotation, 0.1))
        {
            Debug.WriteLine($"Snapping from {WheelRotation} to {targetRotation} at {SelectedIndex}");
            SpinToIndexShortest(SelectedIndex);
        }
    }

    #endregion

    #region GESTURES

    protected bool IsUserPanning;
    double _lastPanAngle;

    double GetAngleFromPoint(PointF point)
    {
        var center = new SKPoint(DrawingRect.MidX, DrawingRect.MidY);
        var dx = point.X - center.X;
        var dy = point.Y - center.Y;
        return Math.Atan2(dy, dx) * 180.0 / Math.PI;
    }

    double GetAngularVelocity(PointF velocity, PointF touchPoint)
    {
        var center = new SKPoint(DrawingRect.MidX, DrawingRect.MidY);
        var radius = Math.Min(DrawingRect.Width, DrawingRect.Height) / 2.0;
        var linearVelocity = Math.Sqrt(velocity.X * velocity.X + velocity.Y * velocity.Y);

        // need sign based on the direction of the swipe
        var dx = touchPoint.X - center.X;
        var dy = touchPoint.Y - center.Y;

        var crossProduct = dx * velocity.Y - dy * velocity.X;
        var sign = crossProduct >= 0 ? 1 : -1;

        return sign * linearVelocity / radius * 180.0 / Math.PI; // degrees per second
    }

    protected bool InContact;
    public bool HadDown { get; protected set; }

    protected virtual void ResetPan()
    {
        IsUserPanning = false;
        StopScrolling();
    }

    /// <summary>
    /// Checks if a point is inside the wheel's circular area
    /// </summary>
    bool IsPointInsideWheel(PointF point)
    {
        var center = new SKPoint(DrawingRect.MidX, DrawingRect.MidY);
        var radius = Math.Min(DrawingRect.Width, DrawingRect.Height) / 2.0;

        var dx = point.X - center.X;
        var dy = point.Y - center.Y;
        var distanceSquared = dx * dx + dy * dy;

        return distanceSquared <= radius * radius; // Using squared values avoids expensive sqrt
    }

    public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
    {
        var consumedDefault = BlockGesturesBelow ? this : null;

        if (!RespondsToGestures || Wheel == null || ItemsCount < 1)
        {
            return consumedDefault;
        }

        if (args.Type == TouchActionResult.Down)
        {
            InContact = true;

            if (!IsPointInsideWheel(args.Event.Location))
            {
                return this;
            }

            HadDown = true;
            ResetPan();
            _lastPanAngle = GetAngleFromPoint(args.Event.Location);
            return this;
        }

        if (args.Type == TouchActionResult.Up)
        {
            HadDown = false;
            InContact = false;
            if (IsUserPanning)
            {
                IsUserPanning = false;

                // Start fling animation if there's sufficient velocity
                var velocity = args.Event.Distance.Velocity;
                var angularVelocity = GetAngularVelocity(velocity, args.Event.Location);

                if (Math.Abs(angularVelocity) > 50)
                {
                    StartFlingAnimation(angularVelocity * Velocity);
                }
                else
                {
                    SnapToNearestItem();
                }

                return null;
            }
        }

        if (!IsPointInsideWheel(args.Event.Location))
        {
            Debug.WriteLine("GESTURES outside");
            return consumedDefault;
        }

        if (!HadDown)
        {
            HadDown = true;
            ResetPan();
            _lastPanAngle = GetAngleFromPoint(args.Event.Location);
        }

        Debug.WriteLine("GESTURES IN-side");

        if (args.Type == TouchActionResult.Panning)
        {
            IsUserPanning = true;

            var currentAngle = GetAngleFromPoint(args.Event.Location);
            var deltaAngle = currentAngle - _lastPanAngle;

            // Handle angle wrapping
            if (deltaAngle > 180) deltaAngle -= 360;
            if (deltaAngle < -180) deltaAngle += 360;

            WheelRotation += deltaAngle;
            _lastPanAngle = currentAngle;

            return this;
        }


        return base.ProcessGestures(args, apply);
    }

    #endregion
}
