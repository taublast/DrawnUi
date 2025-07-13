using System.Collections.Concurrent;

namespace DrawnUi.Views
{
    /// <summary>
    /// Manages delayed disposal of IDisposable objects based on frame count
    /// </summary>
    public class DisposableManager : IDisposable
    {
        /// <summary>
        /// Represents a disposable object with its associated frame number.
        /// </summary>
        internal class FramedDisposable : IDisposable
        {
            public IDisposable Disposable { get; }
            public long FrameNumber { get; }

            public FramedDisposable(IDisposable disposable, long frameNumber)
            {
                Disposable = disposable ?? throw new ArgumentNullException(nameof(disposable));
                FrameNumber = frameNumber;
            }

            public void Dispose()
            {
                Disposable?.Dispose();
            }
        }

        private readonly ConcurrentQueue<FramedDisposable> _toBeDisposed = new ConcurrentQueue<FramedDisposable>();
        private readonly int _framesToHold;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the DisposableManager class.
        /// </summary>
        /// <param name="framesToHold">The number of frames to hold before disposing. Default is 3 frames.</param>
        public DisposableManager(int framesToHold = 3)
        {
            if (framesToHold <= 0)
                throw new ArgumentOutOfRangeException(nameof(framesToHold),
                    "Frames to hold must be positive.");

            _framesToHold = framesToHold;
        }

        /// <summary>
        /// Enqueues an IDisposable object with the specified frame number.
        /// </summary>
        /// <param name="disposable">The IDisposable object to enqueue.</param>
        /// <param name="frameNumber">The frame number when this resource was created/last used.</param>
        public void EnqueueDisposable(IDisposable disposable, long frameNumber)
        {
            if (disposable == null)
                return;

            var framedDisposable = new FramedDisposable(disposable, frameNumber);
            _toBeDisposed.Enqueue(framedDisposable);
        }

        /// <summary>
        /// Disposes of all IDisposable objects that are old enough based on frame count.
        /// Call this before every frame start.
        /// </summary>
        /// <param name="currentFrameNumber">The current frame number.</param>
        public void DisposeDisposables(long currentFrameNumber)
        {
            while (_toBeDisposed.TryPeek(out var framedDisposable))
            {
                var framesPassed = currentFrameNumber - framedDisposable.FrameNumber;
                if (framesPassed >= _framesToHold)
                {
                    if (_toBeDisposed.TryDequeue(out var disposableToDispose))
                    {
                        try
                        {
                            disposableToDispose.Dispose();
                        }
                        catch (Exception ex)
                        {
                            // Log the exception and continue disposing other items
                            LogError(ex);
                        }
                    }
                }
                else
                {
                    // Since the queue is FIFO, no need to check further
                    break;
                }
            }
        }

        private void LogError(Exception ex)
        {
            Super.Log(ex);
        }

        /// <summary>
        /// Disposes all remaining disposables immediately.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            // Dispose remaining disposables
            DisposeAllRemaining();
        }

        /// <summary>
        /// Disposes all remaining IDisposable objects in the queue.
        /// </summary>
        private void DisposeAllRemaining()
        {
            List<FramedDisposable> remainingDisposables = new List<FramedDisposable>();

            while (_toBeDisposed.TryDequeue(out var framedDisposable))
            {
                remainingDisposables.Add(framedDisposable);
            }

            foreach (var disposable in remainingDisposables)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex)
                {
                    // Log the exception and continue disposing other items
                    LogError(ex);
                }
            }
        }

        /// <summary>
        /// Gets the number of items waiting for disposal.
        /// </summary>
        public int PendingDisposalCount => _toBeDisposed.Count;
    }
}
