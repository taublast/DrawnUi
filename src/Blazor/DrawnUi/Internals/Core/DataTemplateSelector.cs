namespace Microsoft.Maui.Controls
{
    public abstract class DataTemplateSelector : DataTemplate
    {
        public virtual DataTemplate SelectTemplate(object item, BindableObject container)
        {
            return null;
        }
    }
}