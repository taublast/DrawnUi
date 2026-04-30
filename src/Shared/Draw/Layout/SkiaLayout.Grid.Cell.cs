//Adapted code from the Xamarin.Forms Grid implementation

namespace DrawnUi.Draw;

public partial class SkiaLayout
{
    public class Cell
    {
        public int ViewIndex { get; }
        public int Row { get; }
        public int Column { get; }
        public int RowSpan { get; }
        public int ColumnSpan { get; }

        /// <summary>
        /// A combination of all the measurement types in the columns this cell spans
        /// </summary>
        public GridLengthType ColumnGridLengthType { get; }

        /// <summary>
        /// A combination of all the measurement types in the rows this cell spans
        /// </summary>
        public GridLengthType RowGridLengthType { get; }

        public Cell(int viewIndex, int row, int column, int rowSpan, int columnSpan,
            GridLengthType columnGridLengthType, GridLengthType rowGridLengthType)
        {
            ViewIndex = viewIndex;
            Row = row;
            Column = column;
            RowSpan = rowSpan;
            ColumnSpan = columnSpan;
            ColumnGridLengthType = columnGridLengthType;
            RowGridLengthType = rowGridLengthType;
        }

        public bool IsColumnSpanAuto => HasFlag(ColumnGridLengthType, GridLengthType.Auto);
        public bool IsRowSpanAuto => HasFlag(RowGridLengthType, GridLengthType.Auto);
        public bool IsColumnSpanStar => HasFlag(ColumnGridLengthType, GridLengthType.Star);
        public bool IsRowSpanStar => HasFlag(RowGridLengthType, GridLengthType.Star);
        public bool IsAbsolute => ColumnGridLengthType == GridLengthType.Absolute
                                  && RowGridLengthType == GridLengthType.Absolute;

        // If any part of the Cell's spans are Absolute or Star, then the Cell will need a measure pass at the known
        // size once that's been determined (i.e., when the Auto stuff has been worked out)
        public bool NeedsKnownMeasurePass => ((ColumnGridLengthType | RowGridLengthType) ^ GridLengthType.Auto) > 0;

        static bool HasFlag(GridLengthType a, GridLengthType b)
        {
            // Avoiding Enum.HasFlag here for performance reasons; we don't need the type check
            return (a & b) == b;
        }
    }
}