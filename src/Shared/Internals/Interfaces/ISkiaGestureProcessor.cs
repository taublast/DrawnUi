namespace DrawnUi.Draw;

public interface ISkiaGestureProcessor
{
	ISkiaGestureListener ProcessGestures(
		SkiaGesturesParameters args,
		GestureEventProcessingInfo apply);
}