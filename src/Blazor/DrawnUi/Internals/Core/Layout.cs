namespace DrawnUi.Draw
{
    public class Layout : View
    {
        public IList<DrawnUi.Draw.IView> Children { get; } = new List<DrawnUi.Draw.IView>();
    }
}