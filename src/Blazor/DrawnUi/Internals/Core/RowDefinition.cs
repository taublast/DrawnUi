namespace DrawnUi.Draw
{
    public class RowDefinition : BindableObject, IGridRowDefinition, IDefinition
    {
        private GridLength height = GridLength.Star;

        public RowDefinition(GridLength value)
        {
            Height = value;
        }

        public RowDefinition()
        {
        }

        public GridLength Height
        {
            get => height;
            set
            {
                if (height.GridUnitType != value.GridUnitType || height.Value != value.Value)
                {
                    height = value;
                    SizeChanged?.Invoke(this, EventArgs.Empty);
                    OnPropertyChanged();
                }
            }  
        }

        public event EventHandler? SizeChanged;
    }
}
