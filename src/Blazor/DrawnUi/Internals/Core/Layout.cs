namespace DrawnUi.Draw
{
    public class Layout : View
    {
        public IList<Microsoft.Maui.IView> Children { get; } = new List<Microsoft.Maui.IView>();
    }
}