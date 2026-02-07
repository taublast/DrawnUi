namespace DrawnUi.Draw
{
    [DebuggerDisplay("{Start} -> {End} ({Delta})")]
    public struct RangeF
    {
        public float Start { get; set; }
        public float End { get; set; }

        public RangeF(float start, float end)
        {
            Start = start;
            End = end;
        }

        public float Length => Math.Abs(End - Start);

        public float Delta => End - Start;
    }
}
