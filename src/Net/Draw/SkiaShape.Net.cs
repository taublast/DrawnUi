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

        public IList<SkiaPoint> Points
        {
            get => (IList<SkiaPoint>)GetValue(PointsProperty);
            set => SetValue(PointsProperty, value);
        }
    }
}
