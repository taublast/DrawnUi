namespace DrawnUi.Draw;

public enum RecycleTemplateType
{
	/// <summary>
	/// One cell per item will be created, while a SkiaControl has little memory consumption for some controls
	/// like SkiaLayer it might take more, so you might consider recycling for large number o items
	/// </summary>
	None,

	/// <summary>
	/// Create cells instances until viewport is filled, then recycle while scrolling
	/// </summary>
	FillViewport,

	/// <summary>
	/// Try using one cell per template at all times, binding context will change just before drawing.
	/// ToDo investigate case of async data changes like images loading from web.
	/// </summary>
	Single
}