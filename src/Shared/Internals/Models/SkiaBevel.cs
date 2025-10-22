namespace DrawnUi.Draw
{
    public enum BevelType
    {
        /// <summary>
        /// No bevel or emboss effect
        /// </summary>
        None,
        
        /// <summary>
        /// Raised effect (light on top/left, shadow on bottom/right)
        /// </summary>
        Bevel,
        
        /// <summary>
        /// Pressed in effect (shadow on top/left, light on bottom/right)
        /// </summary>
        Emboss
    }

    /// <summary>
    /// Defines properties for creating bevel or emboss effects on shapes.
    /// </summary>
    public class SkiaBevel : BindableObject
    {
        public ICanBeUpdatedWithContext Parent { get; set; }

        public void Attach(ICanBeUpdatedWithContext parent)
        {
            this.Parent = parent;
            this.BindingContext = parent.BindingContext;
        }

        public void Dettach()
        {
            this.BindingContext = null;
            this.Parent = null;
        }

        private static void RedrawCanvas(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaBevel bevel)
            {
                bevel.Parent?.Update();
            }
        }

        /// <summary>
        /// Gets or sets the depth of the bevel effect in logical pixels.
        /// </summary>
        public static readonly BindableProperty DepthProperty = BindableProperty.Create(
            nameof(Depth), typeof(double), typeof(SkiaBevel), 2.0, propertyChanged: RedrawCanvas);
        public double Depth
        {
            get { return (double)GetValue(DepthProperty); }
            set { SetValue(DepthProperty, value); }
        }

        /// <summary>
        /// Gets or sets the light color for the highlight edges.
        /// </summary>
        public static readonly BindableProperty LightColorProperty = BindableProperty.Create(
            nameof(LightColor), typeof(Color), typeof(SkiaBevel), Microsoft.Maui.Graphics.Colors.White, propertyChanged: RedrawCanvas);
        public Color LightColor
        {
            get { return (Color)GetValue(LightColorProperty); }
            set { SetValue(LightColorProperty, value); }
        }

        /// <summary>
        /// Gets or sets the shadow color for the shadowed edges.
        /// </summary>
        public static readonly BindableProperty ShadowColorProperty = BindableProperty.Create(
            nameof(ShadowColor), typeof(Color), typeof(SkiaBevel), Microsoft.Maui.Graphics.Colors.Black, propertyChanged: RedrawCanvas);
        public Color ShadowColor
        {
            get { return (Color)GetValue(ShadowColorProperty); }
            set { SetValue(ShadowColorProperty, value); }
        }

        /// <summary>
        /// Gets or sets the opacity for the bevel effect (applies to both light and shadow).
        /// </summary>
        public static readonly BindableProperty OpacityProperty = BindableProperty.Create(
            nameof(Opacity), typeof(double), typeof(SkiaBevel), 0.5, propertyChanged: RedrawCanvas);
        public double Opacity
        {
            get { return (double)GetValue(OpacityProperty); }
            set { SetValue(OpacityProperty, value); }
        }
    }
}