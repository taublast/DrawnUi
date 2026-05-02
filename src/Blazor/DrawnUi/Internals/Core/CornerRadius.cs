namespace DrawnUi.Draw
{
    public readonly struct CornerRadius : IEquatable<CornerRadius>
    {
        public CornerRadius(double uniformRadius)
            : this(uniformRadius, uniformRadius, uniformRadius, uniformRadius)
        {
        }

        public CornerRadius(double topLeft, double topRight, double bottomLeft, double bottomRight)
        {
            TopLeft = topLeft;
            TopRight = topRight;
            BottomLeft = bottomLeft;
            BottomRight = bottomRight;
        }

        public double TopLeft { get; }

        public double TopRight { get; }

        public double BottomLeft { get; }

        public double BottomRight { get; }

        public bool Equals(CornerRadius other)
        {
            return TopLeft.Equals(other.TopLeft)
                   && TopRight.Equals(other.TopRight)
                   && BottomLeft.Equals(other.BottomLeft)
                   && BottomRight.Equals(other.BottomRight);
        }

        public override bool Equals(object obj)
        {
            return obj is CornerRadius other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(TopLeft, TopRight, BottomLeft, BottomRight);
        }

        public static implicit operator CornerRadius(double uniformRadius)
        {
            return new CornerRadius(uniformRadius);
        }

        public static bool operator ==(CornerRadius left, CornerRadius right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CornerRadius left, CornerRadius right)
        {
            return !left.Equals(right);
        }
    }
}