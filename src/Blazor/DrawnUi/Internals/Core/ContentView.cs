using Microsoft.AspNetCore.Components;
using DrawnUi.Views;

namespace Microsoft.Maui.Controls
{
    public class ContentView : VisualElement, IContentView
    {
        [Parameter]
        public object Content { get; set; }

        public Microsoft.Maui.IView PresentedContent => Content as Microsoft.Maui.IView;

        protected virtual SizeRequest OnMeasure(double widthConstraint, double heightConstraint)
        {
            return new SizeRequest(new Size(widthConstraint, heightConstraint), new Size(0, 0));
        }

        protected virtual void OnParentSet()
        {
        }

        private object _bindingContext;
        public new object BindingContext
        {
            get => _bindingContext;
            set
            {
                if (ReferenceEquals(_bindingContext, value))
                    return;

                _bindingContext = value;
                OnPropertyChanged();
                OnBindingContextChanged();
            }
        }

        protected new virtual void OnBindingContextChanged()
        {
        }
    }
}
