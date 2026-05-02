using DrawnUi.Views;

namespace DrawnUi.Draw
{
    public class Element : DrawnUi.Draw.BindableObject
    {
        public virtual Element Parent { get; set; }

        protected virtual void OnParentSet()
        {
        }

        protected virtual void OnChildRemoved(Element child, int oldLogicalIndex)
        {
        }

        protected virtual void OnChildAdded(Element child)
        {
        }
    }

    public class View : Element, DrawnUi.Draw.IView
    {
        public object Handler { get; set; }

        public event EventHandler SizeChanged;

        /// <summary>
        /// ToDo
        /// </summary>
        public bool IsEnabled { get; set; }

        public IList<object> Effects { get; } = new List<object>();

        public IList<object> Behaviors { get; } = new List<object>();

        public new virtual bool IsVisible { get; set; } = true;

        public virtual bool InputTransparent { get; set; }

        public virtual bool IsClippedToBounds { get; set; }

        public virtual double WidthRequest { get; set; } = -1;

        public virtual double HeightRequest { get; set; } = -1;

        public virtual double Width { get; set; } = -1;

        public virtual double Height { get; set; } = -1;

        public virtual double TranslationX { get; set; }

        public virtual double TranslationY { get; set; }

        public virtual double X { get; set; }

        public virtual double Y { get; set; }

        public virtual int ZIndex { get; set; }

        public virtual double Opacity { get; set; } = 1.0;

        public virtual double Rotation { get; set; }

        public virtual double RotationX { get; set; }

        public virtual double RotationY { get; set; }

        public virtual double AnchorX { get; set; } = 0.5;

        public virtual double AnchorY { get; set; } = 0.5;

        public virtual double ScaleX { get; set; } = 1.0;

        public virtual double ScaleY { get; set; } = 1.0;

        public virtual double MaximumWidthRequest { get; set; } = double.PositiveInfinity;

        public virtual double MinimumWidthRequest { get; set; } = -1;

        public virtual double MaximumHeightRequest { get; set; } = double.PositiveInfinity;

        public virtual double MinimumHeightRequest { get; set; } = -1;

        public virtual Shadow Shadow { get; set; }

        public virtual Geometry Clip { get; set; }

        public virtual Brush Background { get; set; }

        public virtual Style Style { get; set; }

        public virtual Thickness Padding { get; set; }

        public virtual Thickness Margin { get; set; }

        public virtual LayoutOptions HorizontalOptions { get; set; } = LayoutOptions.Start;

        public virtual LayoutOptions VerticalOptions { get; set; } = LayoutOptions.Start;

        public virtual Color BackgroundColor { get; set; }

        public virtual void Update()
        {
        }

        protected void RaiseSizeChanged()
        {
            SizeChanged?.Invoke(this, EventArgs.Empty);
        }

        protected virtual SizeRequest OnMeasure(double widthConstraint, double heightConstraint)
        {
            return new SizeRequest(new Size(widthConstraint, heightConstraint), new Size(0, 0));
        }

        public virtual void DisconnectHandlers()
        {
        }
    }
}
