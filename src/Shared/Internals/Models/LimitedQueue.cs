namespace DrawnUi.Infrastructure.Models
{
    public class LimitedQueue<T>
    {
        private Queue<T> queue = new();
        private readonly object _lock = new();
        private readonly int _maxLength = 3;
        private bool _locked;

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return queue.Count;
                }
            }
        }

        public T[] ToArray()
        {
            lock (_lock)
            {
                return queue.ToArray();
            }
        }

        public List<T> ToList()
        {
            lock (_lock)
            {
                return queue.ToArray().ToList();
            }
        }

        public LimitedQueue()
        {
        }

        protected virtual void OnAutoRemovingItem(T item)
        {

        }

        public LimitedQueue(int max)
        {
            _maxLength = max;
        }

        public void Push(T item)
        {
            lock (_lock)
            {
                if (_locked)
                    return;

                queue.Enqueue(item);
                while (queue.Count > _maxLength)
                {
                    queue.TryDequeue(out var removedItem);
                    OnAutoRemovingItem(removedItem);
                }
            }
        }

        public bool IsLocked
        {
            get
            {
                lock (_lock)
                {
                    return _locked;
                }
            }
        }

        public void Lock()
        {
            lock (_lock)
            {
                _locked = true;
            }
        }

        public void Unlock()
        {
            lock (_lock)
            {
                _locked = false;
            }
        }

        public T Pop()
        {
            lock (_lock)
            {
                T latestItem;
                queue.TryDequeue(out latestItem);
                return latestItem;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                while (queue.Count > 0)
                {
                    queue.TryDequeue(out var removedItem);
                    OnAutoRemovingItem(removedItem);
                }
            }
        }
    }
}