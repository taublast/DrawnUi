#if BROWSER
using System;
using Microsoft.AspNetCore.Components;
using DrawnUi.Views;

namespace DrawnUi.Views
{
    public class ViewCell<T> : VisualElement
    {
        [Parameter]
        public T BindingContext { get; set; }

        [Parameter]
        public Action<T> OnSelected { get; set; }

        [Parameter]
        public EventCallback<T> BindingContextChanged { get; set; }
    }
}
#else
namespace DrawnUi.Views
{
    public class ViewCell<T> : DrawnUi.Draw.View
    {
        public new T BindingContext { get; set; }
        public Action<T> OnSelected { get; set; }
    }
}
#endif
