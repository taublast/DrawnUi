namespace DrawnUi.Draw;

public class CachedShadow : IDisposable
{
	public SkiaShadow Shadow { get; set; }
	public SKImageFilter Filter { get; set; }
	public float Scale { get; set; }

	public void Dispose()
	{
		Filter?.Dispose();
	}
}