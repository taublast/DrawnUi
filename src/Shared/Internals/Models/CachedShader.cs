namespace DrawnUi.Draw;

public class CachedShader : IDisposable
{

	public SKRect Destination { get; set; }
	public SKShader Shader { get; set; }

	public void Dispose()
	{
		Shader?.Dispose();
		Shader = null;
		OnDisposing();
	}

	protected virtual void OnDisposing()
	{

	}
}