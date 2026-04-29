namespace Microsoft.Maui.Controls
{
    public class ColumnDefinition : IGridColumnDefinition
    {
        public GridLength Width { get; set; } = GridLength.Star;
    }
}