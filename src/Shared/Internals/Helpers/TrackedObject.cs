namespace DrawnUi.Draw
{
    public class TrackedObject<T> where T : IDisposable
    {
        public T Disposable { get; }

        public TrackedObject(T disposable)
        {
            Disposable = disposable;
        }

        ~TrackedObject()
        {
            Console.WriteLine("[TrackedObject] Finalizer called! Disposed by GC.");
        }

        public void Dispose()
        {
            Debug.WriteLine(
                $"[TrackedObject] Disposed explicitly on thread {Thread.CurrentThread.ManagedThreadId}\n{Environment.StackTrace}");
            Disposable.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
