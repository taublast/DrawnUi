using DrawnUi.Draw;

namespace DrawnUi.Draw;

public class RangeAnimator : SkiaValueAnimator
{



    protected Action<double> _callback;


    public RangeAnimator(SkiaControl player) : base(player)
    {
        Speed = 250;
        mMinValue = 0;
        mMaxValue = 1;
    }

    object lockUpdate = new object();

    public void Start(Action<double> callback, double start, double end, uint ms = 250, Easing easing = null, int delayMs=0)
    {

        lock (lockUpdate)
        {
            //System.Diagnostics.Debug.WriteLine($"[RangeAnimator] {start}->{end} for {ms}");

            _callback = callback;
            mValue = start;
            LastFrameTimeNanos = 0;
            mMinValue = start;
            mMaxValue = end;
            Speed = ms;
            Distance = end - start;

            if (easing != null)
            {
                Easing = easing;
            }
            base.Start(delayMs);
        }
    }

    public double Distance { get; set; }


    protected override bool UpdateValue(long deltaT, long deltaFromStart)
    {
        lock (lockUpdate)
        {
            if (Distance == 0)
                return true;

            var elapsedMs = deltaFromStart / 1000_000.0;
            var progress = elapsedMs / Speed;

            //System.Diagnostics.Debug.WriteLine($"[RangeAnimator] {elapsedMs}->{Speed} => {progress}");

            ElapsedMs = elapsedMs;
            var eased = Easing.Ease(progress);
            double value = 0.0;

            Progress = eased;

            if (Distance > 0)
            {
                value = mMinValue + eased * Distance;
                if (value < mMinValue)
                {
                    value = mMinValue;
                }
                if (value >= mMaxValue)
                {
                    value = mMaxValue;
                }
            }
            else
            if (Distance < 0)
            {
                value = mMinValue + eased * Distance;
                if (value > mMinValue)
                {
                    value = mMinValue;
                }
                if (value <= mMaxValue)
                {
                    value = mMaxValue;
                }
            }

            mValue = value;

            return progress >= 1;
        }

    }

    protected override double TransformReportedValue(long deltaT)
    {
        _callback?.Invoke(mValue);

        return mValue;
    }
}
