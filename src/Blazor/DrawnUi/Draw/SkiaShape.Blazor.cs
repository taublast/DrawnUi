using System.Collections.ObjectModel;
using Color = DrawnUi.Color;

namespace DrawnUi.Draw
{
    public partial class SkiaShape : SkiaLayout
    {
        public static readonly BindableProperty StrokePathProperty = BindableProperty.Create(
              nameof(StrokePath),
              typeof(double[]),
              typeof(SkiaShape),
              null);

        /// <summary>
        /// Gets or sets the dash pattern for the shape's stroke.
        /// </summary>
        /// <remarks>
        /// Allows for creating dashed or dotted lines by specifying an array of numbers
        /// that define the pattern of dashes and gaps:
        /// 
        /// - A pattern like [3,1] creates dashes of length 3 followed by gaps of length 1
        /// - [5,2,1,2] creates a dash of 5, gap of 2, dash of 1, gap of 2, then repeats
        /// - Empty or null array means a solid line with no dashes
        /// 
        /// In XAML, this property can be set with a comma-separated list of values:
        /// StrokePath="3,1" or StrokePath="5,2,1,2"
        /// </remarks>
        //[TypeConverter(typeof(StringToDoubleArrayTypeConverter))]
        public double[] StrokePath
        {
            get { return (double[])GetValue(StrokePathProperty); }
            set { SetValue(StrokePathProperty, value); }
        }

        public static readonly BindableProperty PointsProperty = BindableProperty.Create(
            nameof(Points),
            typeof(IList<SkiaPoint>),
            typeof(SkiaShape),
            defaultValueCreator: (instance) =>
            {
                var created = new ObservableCollection<SkiaPoint>();
                created.CollectionChanged += ((SkiaShape)instance).OnPointsCollectionChanged;
                return created;
            },
            validateValue: (bo, v) => v is IList<SkiaPoint>,
            propertyChanged: NeedDraw,
            coerceValue: CoercePoints);

        //[TypeConverter(typeof(SkiaPointCollectionConverter))]
        public IList<SkiaPoint> Points
        {
            get => (IList<SkiaPoint>)GetValue(PointsProperty);
            set => SetValue(PointsProperty, value);
        }
    }
}
