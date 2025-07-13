using System.ComponentModel;
using System.Globalization;

namespace DrawnUi.Infrastructure.Xaml
{
    public class SkiaPointCollectionConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            // We can convert from a string to an IList<SkiaPoint>
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            // Convert the string to an IList<SkiaPoint>
            if (value is string strValue)
            {
                return ParseSkiaPointCollection(strValue);
            }

            return base.ConvertFrom(context, culture, value);
        }

        private IList<SkiaPoint> ParseSkiaPointCollection(string str)
        {
            var points = new List<SkiaPoint>();

            if (string.IsNullOrWhiteSpace(str))
                return points;

            // Split the string into individual point representations
            var pointStrings = str.Split(new char[] { ';'}, StringSplitOptions.RemoveEmptyEntries);

            foreach (var pointString in pointStrings)
            {
                var trimmedPointString = pointString.Trim();
                if (string.IsNullOrEmpty(trimmedPointString))
                    continue;

                // Split each point into X and Y coordinates, allowing for ',' or ' ' as separators
                var coordinates = trimmedPointString.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (coordinates.Length != 2)
                    throw new FormatException($"Invalid point format: '{trimmedPointString}'");

                if (double.TryParse(coordinates[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) &&
                    double.TryParse(coordinates[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
                {
                    points.Add(new SkiaPoint(x, y));
                }
                else
                {
                    throw new FormatException($"Invalid coordinate values in point: '{trimmedPointString}'");
                }
            }

            return points;
        }
    }
}
