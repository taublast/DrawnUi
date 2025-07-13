namespace DrawnUi.Controls
{
    public class SkiaWheelPickerCell : SkiaLayout, IWheelPickerCell
    {
        private readonly SkiaLabel _label;

        public SkiaWheelPickerCell(Color textColor)
        {
            HorizontalOptions = LayoutOptions.Fill;
            Padding = 0;
            UseCache = SkiaCacheType.Operations;

            Children = new List<SkiaControl>()
            {
                new SkiaLayer() //will be "filled" by picker
                {
                    Children =
                    {
                        new SkiaLabel()
                        {
                            HeightRequest = 200, //whatever, just as long it's not -1 !!!
                            FontSize = 17,
                            HorizontalOptions = LayoutOptions.Center,
                            TextColor = textColor,
                            VerticalOptions = LayoutOptions.Center,
                            VerticalTextAlignment = TextAlignment.Center,
                            HorizontalTextAlignment = DrawTextAlignment.Center
                        }.Assign(out _label)
                    }
                }
            };

            ApplyContext();
        }

        void ApplyContext()
        {
            if (BindingContext is string text)
            {
                _label.Text = text;
            }
            else
            if (BindingContext is IHasStringTitle value)
            {
                _label.Text = value.Title;
            }
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();

            ApplyContext();
        }

        public void UpdateContext(WheelCellInfo ctx)
        {
            Opacity = ctx.Opacity;
            if (ctx.IsSelected)
            {
                _label.FontAttributes =  FontAttributes.Bold;
            }
            else
            {
                _label.FontAttributes = FontAttributes.None;
            }
        }
    }
}
