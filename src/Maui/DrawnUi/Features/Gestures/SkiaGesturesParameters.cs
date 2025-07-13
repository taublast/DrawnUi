namespace DrawnUi.Draw;

public class SkiaGesturesInfo
{
    public SkiaGesturesParameters Args { get; set; }
    public GestureEventProcessingInfo Info { get; set; }
    public bool Consumed { get; set; }

    public static SkiaGesturesInfo Create(SkiaGesturesParameters args, GestureEventProcessingInfo info)
    {
        return new ()
        {
            Args = args,
            Info = info
        };
    }
}

public class SkiaGesturesParameters
{
    //public SKPoint PanningOffset { get; set; }
    public TouchActionResult Type { get; set; }
    public TouchActionEventArgs Event { get; set; }

    public static SkiaGesturesParameters Create(TouchActionResult action, TouchActionEventArgs args)
    {
        return new SkiaGesturesParameters()
        {
            Type = action,
            Event = args//.Clone()
        };
    }

    public static SkiaGesturesParameters Empty
    {
        get
        {
            return new SkiaGesturesParameters();
        }
    }
}

public struct GestureEventProcessingInfo
{
    /// <summary>
    /// Location of the gesture accounting for transforms. Might include all transforms from parents upper th rendering tree.
    /// </summary>
    public SKPoint MappedLocation { get; set; }

    /// <summary>
    /// Coordinate offset used to transform touch coordinates from parent's 
    /// coordinate space to this control's local space.
    /// </summary>
    public SKPoint ChildOffset { get; set; }

    /// <summary>
    /// Direct coordinate offset used for gesture processing without considering 
    /// cached transformations; useful for direct position calculations.
    /// </summary>
    public SKPoint ChildOffsetDirect { get; set; }

    /// <summary>
    /// Reference to a gesture listener that has already consumed this gesture.
    /// Used to track gesture ownership through the control hierarchy.
    /// </summary>
    public ISkiaGestureListener AlreadyConsumed { get; set; }

    
    public GestureEventProcessingInfo(SKPoint mappedLocation, SKPoint childOffset1, SKPoint childOffsetDirect, ISkiaGestureListener wasConsumed)
    {
        MappedLocation = mappedLocation;
        ChildOffset = childOffset1;
        ChildOffsetDirect = childOffsetDirect;
        AlreadyConsumed = wasConsumed;
    }

    //public GestureEventProcessingInfo()
    //{

    //}

    public static GestureEventProcessingInfo Empty
    {
        get
        {
            return new GestureEventProcessingInfo();
        }
    }
}
