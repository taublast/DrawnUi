using System.Diagnostics;

namespace DrawnUi.Infrastructure;

public class RenderOnTimer
{
	private Stopwatch _stopwatch;

	public bool IsActive
	{
		get
		{
			if (_stopwatch == null)
				return false;
			return _stopwatch.IsRunning;
		}
	}
	public void Start(Action tickAction, int fps)
	{
		Task.Run(() =>
		{

			_stopwatch?.Stop();
			_stopwatch = Stopwatch.StartNew();

			int _interval = 1000 / fps; // Interval in milliseconds
			long _nextTickTime = _stopwatch.ElapsedMilliseconds + _interval;

			while (true)
			{
				long currentTime = _stopwatch.ElapsedMilliseconds;

				if (currentTime >= _nextTickTime)
				{
					// Perform the tick action
					tickAction();

					_nextTickTime += _interval;
				}
				else
				{
					// Wait for the remaining time until the next tick
					int remainingTime = (int)(_nextTickTime - currentTime);
					Thread.Sleep(remainingTime);
				}
			}

		}).ConfigureAwait(false);

	}
}