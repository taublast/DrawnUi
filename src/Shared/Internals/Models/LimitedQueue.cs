namespace DrawnUi.Infrastructure.Models
{
    public class LimitedQueue<T>
    {
        private Queue<T> queue = new();
        private readonly int _maxLength = 3;
        private bool _locked;

        public int Count => queue.Count;

        public T[] ToArray()
        {
            return queue.ToArray();
        }

        public List<T> ToList()
        {
            return queue.ToArray().ToList();
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
            if (_locked)
                return;

            queue.Enqueue(item);
            while (queue.Count > _maxLength)
            {
                queue.TryDequeue(out var removedItem);
                OnAutoRemovingItem(removedItem);
            }
        }

        public bool IsLocked
        {
            get
            {
                return _locked;
            }
        }

        public void Lock()
        {
            _locked = true;
        }

        public void Unlock()
        {
            _locked = false;
        }

        public T Pop()
        {
            T latestItem;
            queue.TryDequeue(out latestItem);
            return latestItem;
        }

        public void Clear()
        {
            while (queue.Count > 0)
            {
                queue.TryDequeue(out var removedItem);
                OnAutoRemovingItem(removedItem);
            }
        }
    }
}