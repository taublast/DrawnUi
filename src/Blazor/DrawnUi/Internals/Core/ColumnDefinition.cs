namespace DrawnUi.Draw
{
    public class ColumnDefinition : BindableObject, IGridColumnDefinition, IDefinition
    {
        public ColumnDefinition(GridLength value)
        {
            Width = value;
        }

        public ColumnDefinition()
        {
        }

 
        private GridLength width = GridLength.Star;
        public GridLength Width
        {
            get => width;
            set
            {
                if (width.GridUnitType != value.GridUnitType || width.Value != value.Value)
                {
                    width = value;
                    SizeChanged?.Invoke(this, EventArgs.Empty);
                    OnPropertyChanged();
                }
            }
        }

        public event EventHandler? SizeChanged;
    }
}
