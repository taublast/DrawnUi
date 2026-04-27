using DrawnUi.Draw;
using DrawnUi.Infrastructure.Xaml;
using Microsoft.Maui.Controls.Shapes;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Color = Microsoft.Maui.Graphics.Color;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace DrawnUi.Draw
{
    public partial class SkiaShape
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
		[TypeConverter(typeof(StringToDoubleArrayTypeConverter))]
		public double[] StrokePath
		{
			get { return (double[])GetValue(StrokePathProperty); }
			set { SetValue(StrokePathProperty, value); }
		}

	}
}
