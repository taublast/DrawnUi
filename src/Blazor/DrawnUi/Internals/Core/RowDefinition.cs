namespace Microsoft.Maui.Controls
{
    public class RowDefinition : IGridRowDefinition
    {
        public GridLength Height { get; set; } = GridLength.Star;
    }
}