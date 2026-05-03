namespace DrawnUi.Views
{
    public class VisualElement : DrawnUi.Draw.View
    {
        public virtual void Update() { }
        public virtual bool Focus() => true;
        public virtual SKRect Frame { get; protected set; }
    }
}
