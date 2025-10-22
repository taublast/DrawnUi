namespace DrawnUi.Draw;

public interface ISkiaLayer : ISkiaControl
{

	/// <summary>
	/// Cached layer image
	/// </summary>
	public SkiaDrawingContext LayerPaintArgs { get; set; }

	/// <summary>
	/// Snapshot was taken
	/// </summary>
	public bool HasValidSnapshot { get; set; }

	void DrawLayerImage(SkiaDrawingContext context, SKRect destination, float scale);



}