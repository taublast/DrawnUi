namespace Microsoft.Maui.Controls
{
    public class DataTemplate
    {
        public DataTemplate()
        {
        }

        public DataTemplate(Func<object> createContent)
        {
            CreateContent = createContent;
        }

        public Func<object> CreateContent { get; set; }
    }
}