namespace Microsoft.Maui.Controls
{
    public class GradientBrush : Brush
    {
        public IList<GradientStop> GradientStops { get; } = new List<GradientStop>();
    }
}