namespace DrawnUi.Camera;

public enum CaptureQuality
{
	/// <summary>
	/// Max possible photo size
	/// </summary>
	Max,

    /// <summary>
    /// 20% reduction from Max
    /// </summary>
    High,

	/// <summary>
	/// Medium photo size
	/// </summary>
	Medium,

	/// <summary>
	/// Small photo size
	/// </summary>
	Low,

	/// <summary>
	/// Will be sending image from preview as still capture without taking a real still picture, make sure you have preview enabled
	/// </summary>
	Preview,

	/// <summary>
	/// Manual capture size selection - use ManualCaptureSize property to specify exact dimensions
	/// </summary>
	Manual
}
