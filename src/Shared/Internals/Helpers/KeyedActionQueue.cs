namespace DrawnUi.Draw
{
    /// <summary>
    /// Ultra-fast key generator for queues using long keys
    /// </summary>
    public static class LongKeyGenerator
    {
        private static long _counter = 0;

        /// <summary>
        /// Generates next unique key (thread-safe, fastest option)
        /// </summary>
        public static long Next() => Interlocked.Increment(ref _counter);

        /// <summary>
        /// Generates key with thread distribution for better performance under high contention
        /// </summary>
        public static long NextThreadDistributed()
        {
            var threadId = (uint)Environment.CurrentManagedThreadId;
            var counter = (uint)Interlocked.Increment(ref _counter);
            return ((long)threadId << 32) | counter;
        }

        /// <summary>
        /// Encodes semantic string directly to long (up to 8 chars, case-sensitive)
        /// </summary>
        public static unsafe long EncodeSemantic(string semantic)
        {
            if (semantic.Length == 0) return 0;

            long result = 0;
            int len = Math.Min(semantic.Length, 8);

            fixed (char* ptr = semantic)
            {
                for (int i = 0; i < len; i++)
                {
                    result |= ((long)(ptr[i] & 0xFF)) << (i * 8);
                }
            }

            return result;
        }
    }

    public class KeyedActionQueue<TKey> where TKey : notnull
    {
        private class ActionNode
        {
            public TKey Key;
            public Action Action;
            public ActionNode? Next;
            public ActionNode? Previous;

            public ActionNode(TKey key, Action action)
            {
                Key = key;
                Action = action;
            }
        }

        private readonly Dictionary<TKey, ActionNode> _lookup;
        private readonly object _lock = new();
        private ActionNode? _head;
        private ActionNode? _tail;
        private int _count;

        /// <summary>
        /// Initializes a new instance of the FastKeyedActionQueue with specified initial capacity
        /// </summary>
        public KeyedActionQueue(int capacity = 1024)
        {
            _lookup = new Dictionary<TKey, ActionNode>(capacity);
        }

        /// <summary>
        /// Enqueues an action with a key, removing any existing action with the same key
        /// </summary>
        public void Enqueue(TKey key, Action action)
        {
            var newNode = new ActionNode(key, action);

            lock (_lock)
            {
                // Remove existing node if present
                if (_lookup.TryGetValue(key, out var existingNode))
                {
                    RemoveNodeUnsafe(existingNode);
                    _count--;
                }

                // Add new node to tail
                if (_tail == null)
                {
                    _head = _tail = newNode;
                }
                else
                {
                    _tail.Next = newNode;
                    newNode.Previous = _tail;
                    _tail = newNode;
                }

                _lookup[key] = newNode;
                _count++;
            }
        }

        /// <summary>
        /// Dequeues the next action in FIFO order
        /// </summary>
        public Action? Dequeue()
        {
            lock (_lock)
            {
                if (_head == null) return null;

                var node = _head;
                RemoveNodeUnsafe(node);
                _lookup.Remove(node.Key);
                _count--;

                return node.Action;
            }
        }

        /// <summary>
        /// Gets the current count of queued actions
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _count;
                }
            }
        }

        /// <summary>
        /// Removes all queued actions
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _head = _tail = null;
                _lookup.Clear();
                _count = 0;
            }
        }

        /// <summary>
        /// Checks if a key exists in the queue
        /// </summary>
        public bool ContainsKey(TKey key)
        {
            lock (_lock)
            {
                return _lookup.ContainsKey(key);
            }
        }

        /// <summary>
        /// Tries to remove an action by key
        /// </summary>
        public bool TryRemove(TKey key)
        {
            lock (_lock)
            {
                if (_lookup.TryGetValue(key, out var node))
                {
                    RemoveNodeUnsafe(node);
                    _lookup.Remove(key);
                    _count--;
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Dequeues and executes all actions in FIFO order
        /// </summary>
        public void ExecuteAll()
        {
            ActionNode? current;
            lock (_lock)
            {
                current = _head;
                _head = _tail = null;
                _lookup.Clear();
                _count = 0;
            }

            // Execute outside the lock
            while (current != null)
            {
                var next = current.Next;
                try
                {
                    current.Action();
                }
                catch (Exception e)
                {
                    Super.Log(e);
                }
                current = next;
            }
        }

        private void RemoveNodeUnsafe(ActionNode node)
        {
            if (node.Previous != null)
                node.Previous.Next = node.Next;
            else
                _head = node.Next;

            if (node.Next != null)
                node.Next.Previous = node.Previous;
            else
                _tail = node.Previous;
        }
    }
}
