namespace DrawnUi.Draw
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