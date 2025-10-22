namespace DrawnUi.Draw;

public interface IInsideWheelStack
{
	/// <summary>
	/// Called by parent stack inside picker wheel when position changes
	/// </summary>
	/// <param name="offsetRatio">0.0-X.X offset from selection axis, beyond 1.0 is offscreen. Normally you would change opacity accordingly to this.</param>
	/// <param name="isSelected">Whether cell is currently selected, normally you would change text color accordingly.</param>
	public void OnPositionChanged(float offsetRatio, bool isSelected);

}