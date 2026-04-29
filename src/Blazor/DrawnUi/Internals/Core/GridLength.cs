namespace Microsoft.Maui.Controls
{
    public readonly struct GridLength
    {
        public GridLength(double value, GridUnitType gridUnitType = GridUnitType.Absolute)
        {
            Value = value;
            GridUnitType = gridUnitType;
        }

        public double Value { get; }

        public GridUnitType GridUnitType { get; }

        public bool IsAbsolute => GridUnitType == GridUnitType.Absolute;

        public bool IsAuto => GridUnitType == GridUnitType.Auto;

        public bool IsStar => GridUnitType == GridUnitType.Star;

        public static GridLength Auto => new(1, GridUnitType.Auto);

        public static GridLength Star => new(1, GridUnitType.Star);
    }
}