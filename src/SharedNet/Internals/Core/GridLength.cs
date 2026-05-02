using System.Collections;
using System.Reflection;

namespace DrawnUi.Draw
{
    public readonly struct GridLength
    {
        public GridLength(double value, GridUnitType gridUnitType = GridUnitType.Absolute)
        {
            Value = value;
            GridUnitType = gridUnitType;
        }

        public double Value { get; }

        public GridUnitType GridUnitType { get; }

        public bool IsAbsolute => GridUnitType == GridUnitType.Absolute;

        public bool IsAuto => GridUnitType == GridUnitType.Auto;

        public bool IsStar => GridUnitType == GridUnitType.Star;

        public static GridLength Auto => new(1, GridUnitType.Auto);

        public static GridLength Star => new(1, GridUnitType.Star);
    }

    public class ColumnDefinitionCollection : DefinitionCollection<ColumnDefinition>
    {
        public ColumnDefinitionCollection() : base()
        {
        }

        public ColumnDefinitionCollection(params ColumnDefinition[] definitions) : base(definitions)
        {
        }

        internal ColumnDefinitionCollection(List<ColumnDefinition> definitions, bool copy) : base(definitions, copy)
        {
        }
    }

    public class RowDefinitionCollection : DefinitionCollection<RowDefinition>
    {
        public RowDefinitionCollection() : base()
        {
        }

        public RowDefinitionCollection(params RowDefinition[] definitions) : base(definitions)
        {
        }

        internal RowDefinitionCollection(List<RowDefinition> definitions, bool copy) : base(definitions, copy)
        {
        }
    }

    public interface IDefinition
    {
        event EventHandler SizeChanged;
    }

    public class DefinitionCollection<T> : BindableObject, IList<T>, ICollection<T> where T : IDefinition
    {
        readonly WeakEventManager _weakEventManager = new WeakEventManager();
        readonly List<T> _internalList;

        internal DefinitionCollection() => _internalList = new List<T>();

        internal DefinitionCollection(params T[] items) => _internalList = new List<T>(items);

        internal DefinitionCollection(List<T> items, bool copy) => _internalList = copy ? new List<T>(items) : items;

        public void Add(T item)
        {
            _internalList.Add(item);
            item.SizeChanged += OnItemSizeChanged;
            OnItemSizeChanged(this, EventArgs.Empty);
        }

        public void Clear()
        {
            foreach (T item in _internalList)
                item.SizeChanged -= OnItemSizeChanged;
            _internalList.Clear();
            OnItemSizeChanged(this, EventArgs.Empty);
        }

        public bool Contains(T item)
        {
            return _internalList.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _internalList.CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get { return _internalList.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(T item)
        {
            item.SizeChanged -= OnItemSizeChanged;
            bool success = _internalList.Remove(item);
            if (success)
                OnItemSizeChanged(this, EventArgs.Empty);
            return success;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _internalList.GetEnumerator();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _internalList.GetEnumerator();
        }

        public int IndexOf(T item)
        {
            return _internalList.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            _internalList.Insert(index, item);
            item.SizeChanged += OnItemSizeChanged;
            OnItemSizeChanged(this, EventArgs.Empty);
        }

        public T this[int index]
        {
            get { return _internalList[index]; }
            set
            {
                if (index < _internalList.Count && index >= 0 && _internalList[index] != null)
                    _internalList[index].SizeChanged -= OnItemSizeChanged;

                _internalList[index] = value;
                value.SizeChanged += OnItemSizeChanged;
                OnItemSizeChanged(this, EventArgs.Empty);
            }
        }

        public void RemoveAt(int index)
        {
            T item = _internalList[index];
            _internalList.RemoveAt(index);
            item.SizeChanged -= OnItemSizeChanged;
            OnItemSizeChanged(this, EventArgs.Empty);
        }

        public event EventHandler ItemSizeChanged
        {
            add => _weakEventManager.AddEventHandler(value);
            remove => _weakEventManager.RemoveEventHandler(value);
        }

        void OnItemSizeChanged(object sender, EventArgs e)
        {
            _weakEventManager.HandleEvent(this, e, nameof(ItemSizeChanged));
        }
    }

    /// <summary>
    /// Manages weak event subscriptions, preventing memory leaks by maintaining weak references to handlers.
    /// </summary>
    public class WeakEventManager
    {
        readonly Dictionary<string, List<Subscription>> _eventHandlers = new(StringComparer.Ordinal);

        /// <summary>
        /// Adds an event handler for the specified event, storing a weak reference to the handler's target.
        /// </summary>
        /// <typeparam name="TEventArgs">The type of the event arguments.</typeparam>
        /// <param name="handler">The event handler to add.</param>
        /// <param name="eventName">The name of the event.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="eventName"/> is null or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="handler"/> is null.</exception>
        public void AddEventHandler<TEventArgs>(EventHandler<TEventArgs> handler, [CallerMemberName] string eventName = "")
            where TEventArgs : EventArgs
        {
            if (string.IsNullOrEmpty(eventName))
                throw new ArgumentNullException(nameof(eventName));

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            AddEventHandler(eventName, handler.Target, handler.GetMethodInfo());
        }

        /// <summary>
        /// Adds an event handler for the specified event, storing a weak reference to the handler's target.
        /// </summary>
        /// <param name="handler">The event handler to add.</param>
        /// <param name="eventName">The name of the event.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="eventName"/> is null or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="handler"/> is null.</exception>
        public void AddEventHandler(Delegate? handler, [CallerMemberName] string eventName = "")
        {
            if (string.IsNullOrEmpty(eventName))
                throw new ArgumentNullException(nameof(eventName));

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            AddEventHandler(eventName, handler.Target, handler.GetMethodInfo());
        }

        /// <summary>
        /// Invokes the handlers registered for the specified event. Removes handlers whose targets have been garbage collected.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="args">The event arguments.</param>
        /// <param name="eventName">The name of the event to raise.</param>
        public void HandleEvent(object? sender, object? args, string eventName)
        {
            var toRaise = new List<(object? subscriber, MethodInfo handler)>();
            var toRemove = new List<Subscription>();

            if (_eventHandlers.TryGetValue(eventName, out List<Subscription>? target))
            {
                for (int i = 0; i < target.Count; i++)
                {
                    Subscription subscription = target[i];
                    bool isStatic = subscription.Subscriber == null;
                    if (isStatic)
                    {
                        // For a static method, we'll just pass null as the first parameter of MethodInfo.Invoke
                        toRaise.Add((null, subscription.Handler));
                        continue;
                    }

                    object? subscriber = subscription.Subscriber?.Target;

                    if (subscriber == null)
                        // The subscriber was collected, so there's no need to keep this subscription around
                        toRemove.Add(subscription);
                    else
                        toRaise.Add((subscriber, subscription.Handler));
                }

                for (int i = 0; i < toRemove.Count; i++)
                {
                    Subscription subscription = toRemove[i];
                    target.Remove(subscription);
                }
            }

            for (int i = 0; i < toRaise.Count; i++)
            {
                (var subscriber, var handler) = toRaise[i];
                handler.Invoke(subscriber, new[] { sender, args });
            }
        }

        /// <summary>
        /// Removes a previously added event handler for the specified event.
        /// </summary>
        /// <typeparam name="TEventArgs">The type of the event arguments.</typeparam>
        /// <param name="handler">The event handler to remove.</param>
        /// <param name="eventName">The name of the event.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="eventName"/> is null or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="handler"/> is null.</exception>
        public void RemoveEventHandler<TEventArgs>(EventHandler<TEventArgs> handler, [CallerMemberName] string eventName = "")
            where TEventArgs : EventArgs
        {
            if (string.IsNullOrEmpty(eventName))
                throw new ArgumentNullException(nameof(eventName));

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            RemoveEventHandler(eventName, handler.Target, handler.GetMethodInfo());
        }

        /// <summary>
        /// Removes a previously added event handler for the specified event.
        /// </summary>
        /// <param name="handler">The event handler to remove.</param>
        /// <param name="eventName">The name of the event.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="eventName"/> is null or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="handler"/> is null.</exception>
        public void RemoveEventHandler(Delegate? handler, [CallerMemberName] string eventName = "")
        {
            if (string.IsNullOrEmpty(eventName))
                throw new ArgumentNullException(nameof(eventName));

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            RemoveEventHandler(eventName, handler.Target, handler.GetMethodInfo());
        }

        void AddEventHandler(string eventName, object? handlerTarget, MethodInfo methodInfo)
        {
            if (!_eventHandlers.TryGetValue(eventName, out List<Subscription>? targets))
            {
                targets = new List<Subscription>();
                _eventHandlers.Add(eventName, targets);
            }

            if (handlerTarget == null)
            {
                // This event handler is a static method
                targets.Add(new Subscription(null, methodInfo));
                return;
            }

            targets.Add(new Subscription(new WeakReference(handlerTarget), methodInfo));
        }

        void RemoveEventHandler(string eventName, object? handlerTarget, MemberInfo methodInfo)
        {
            if (!_eventHandlers.TryGetValue(eventName, out List<Subscription>? subscriptions))
                return;

            for (int n = subscriptions.Count - 1; n >= 0; n--)
            {
                Subscription current = subscriptions[n];

                if (current.Subscriber != null && !current.Subscriber.IsAlive)
                {
                    // If not alive, remove and continue
                    subscriptions.RemoveAt(n);
                    continue;
                }

                if (current.Subscriber?.Target == handlerTarget && current.Handler.Name == methodInfo.Name)
                {
                    // Found the match, we can break
                    subscriptions.RemoveAt(n);
                    break;
                }
            }
        }

        readonly struct Subscription : IEquatable<Subscription>
        {
            /// <summary>
            /// Initializes a new <see cref="Subscription"/> with a weak reference to the subscriber and the handler method.
            /// </summary>
            /// <param name="subscriber">A weak reference to the subscriber object.</param>
            /// <param name="handler">The method info of the handler to invoke.</param>
            public Subscription(WeakReference? subscriber, MethodInfo handler)
            {
                Subscriber = subscriber;
                Handler = handler ?? throw new ArgumentNullException(nameof(handler));
            }

            public readonly WeakReference? Subscriber;
            public readonly MethodInfo Handler;

            public bool Equals(Subscription other) => Subscriber == other.Subscriber && Handler == other.Handler;

            public override bool Equals(object? obj) => obj is Subscription other && Equals(other);

            public override int GetHashCode() => Subscriber?.GetHashCode() ?? 0 ^ Handler.GetHashCode();
        }
    }

}
