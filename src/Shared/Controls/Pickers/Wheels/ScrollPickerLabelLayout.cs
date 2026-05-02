namespace DrawnUi.Controls
{
    public class ScrollPickerLabelLayout : SkiaLayout, IWheelPickerCell
    {
        public ScrollPickerLabelLayout()
        {
            UpdateControl();
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
        }

        public static readonly BindableProperty ColorTextSelectedProperty = BindableProperty.Create(nameof(ColorTextSelected),
            typeof(Color),
            typeof(ScrollPickerLabelContainer),
            Colors.Orange, propertyChanged: OnNeedUpdate);

        public Color ColorTextSelected
        {
            get { return (Color)GetValue(ColorTextSelectedProperty); }
            set { SetValue(ColorTextSelectedProperty, value); }
        }

        public static readonly BindableProperty ColorTextProperty = BindableProperty.Create(nameof(ColorText),
            typeof(Color),
            typeof(ScrollPickerLabelContainer),
            Colors.White, propertyChanged: OnNeedUpdate);
        public Color ColorText
        {
            get { return (Color)GetValue(ColorTextProperty); }
            set { SetValue(ColorTextProperty, value); }
        }

        private static void OnNeedUpdate(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is ScrollPickerLabelContainer control)
            {
                control.UpdateControl();
            }
        }

        public void UpdateControl()
        {
            if (MainLabel == null)
            {
                MainLabel = Views.FirstOrDefault(x => x is SkiaLabel) as SkiaLabel;
            }
            if (MainLabel!=null)
            {
                var color = IsSelected ? ColorTextSelected : ColorText;
                MainLabel.TextColor = color;
            }
        }

        private SkiaLabel MainLabel;

        protected bool IsSelected { get; set; }
 
        public void UpdateContext(WheelCellInfo ctx)
        {
            IsSelected = ctx.IsSelected;
            UpdateControl();
        }
    }
}
