namespace DrawnUi.Infrastructure;

public struct ClosedRange<T> where T : IComparable<T>
{
    public T LowerBound { get; }
    public T UpperBound { get; }

    public ClosedRange(T lowerBound, T upperBound)
    {
        LowerBound = lowerBound;
        UpperBound = upperBound;
    }

    public bool Contains(T value)
    {
        return value.CompareTo(LowerBound) >= 0 && value.CompareTo(UpperBound) <= 0;
    }
}