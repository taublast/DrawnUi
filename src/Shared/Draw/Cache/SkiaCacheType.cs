namespace DrawnUi.Draw;


public enum SkiaCacheType
{
    /// <summary>
    /// True and old school
    /// </summary>
    None,

    /// <summary>
    /// Create and reuse SKPicture. Try this first for labels, svg etc. 
    /// Do not use this when dropping shadows or with other effects, better use Bitmap. 
    /// </summary>
    Operations,

    /// <summary>
    /// EXPERIMENTAL! May be bugged. Create and reuse SKPicture all over the canvas ignoring clipping.
    /// Try this first for labels, svg etc. 
    /// Do not use this when dropping shadows or with other effects, better use Bitmap. 
    /// </summary>
    OperationsFull,

    /// <summary>
    /// Will use simple SKBitmap cache type, will not use hardware acceleration.
    /// Slower but will work for sizes bigger than graphics memory if needed.
    /// </summary>
    Image,

    /// <summary>
    /// Using `Image` cache type with double buffering. Will display a previous cache while rendering the new one in background, thus not slowing scrolling etc.
    /// </summary>
    ImageDoubleBuffered,


    /// <summary>
    /// Would receive the invalidated area rectangle, then redraw the previous cache
    /// but clipped to exclude the dirty area, then would re-create the dirty area and
    /// draw it clipped inside the dirty rectangle. This is useful for layouts with many children,
    /// like scroll content etc, but useless for non-containers.
    /// But will suffer from children that love to invalidate parent much!
    /// </summary>
    ImageComposite,

    /// <summary>
    /// The cached surface will use the same graphic context as your hardware-accelerated canvas.
    /// This kind of cache will not apply Opacity as not all platforms support transparency for hardware accelerated layer.
    /// Will fallback to simple Image cache type if hardware acceleration is not available.
    /// </summary>
    GPU,

}
