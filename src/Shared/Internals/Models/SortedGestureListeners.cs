namespace DrawnUi.Draw;

public class SortedGestureListeners
{
    protected readonly Dictionary<Guid, ISkiaGestureListener> _dic = new();
    protected List<ISkiaGestureListener> Sorted { get; set; }
    protected bool _isDirty = true;

    public List<ISkiaGestureListener> GetListeners()
    {
        lock (_dic)
        {
            if (_isDirty)
            {
                Sorted = _dic.Values
                    .OrderByDescending(listener => listener.ZIndex)
                    .ThenByDescending(listener => listener.GestureListenerRegistrationTime.GetValueOrDefault())
                    .ToList();
                _isDirty = false;
            }
            return Sorted;
        }
    }

    public void Add(ISkiaGestureListener item)
    {
        lock (_dic)
        {
            _dic[item.Uid] = item;
            _isDirty = true;
        }
    }

    public void Remove(ISkiaGestureListener item)
    {
        lock (_dic)
        {
            if (_dic.Remove(item.Uid))
            {
                _isDirty = true;
            }
        }
    }

    public void Clear()
    {
        lock (_dic)
        {
            _dic.Clear();
            _isDirty = true;
        }
    }

    public void Invalidate()
    {
        lock (_dic)
        {
            _isDirty = true;
        }
    }
}
