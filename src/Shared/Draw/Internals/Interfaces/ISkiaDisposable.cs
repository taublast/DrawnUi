namespace DrawnUi.Draw
{
    public interface ISkiaDisposable : IDisposable
    {
        ObjectAliveType IsAlive { get; set; }
    }
}
