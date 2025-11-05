namespace DrawnUi.Draw;

public class CachedGradient : CachedShader
{
	public SkiaGradient Gradient { get; set; }

	protected override void OnDisposing()
	{
		Gradient = null;
	}
}