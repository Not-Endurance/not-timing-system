using System.Collections;
using Not.Events;
using Not.Exceptions;

namespace Not.Structures;

public class ObservableList<T> : IReadOnlyList<T>
    where T : IIdentifiable
{
    readonly object _lock = new();
    readonly Dictionary<int, T> _dictionary = [];

    public ObservableList() { }

    public ObservableList(IEnumerable<T> items)
    {
        _dictionary = items.ToDictionary(x => x.Id, x => x);
    }

    public T this[int index]
    {
        get
        {
            lock (_lock)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _dictionary.Count);

                var count = 0;
                foreach (var item in this)
                {
                    if (count++ == index)
                    {
                        return item;
                    }
                }
                // Should never be reached
                throw new Exception();
            }
        }
    }
    public Event ChangedEvent { get; } = new();
    public int Count => _dictionary.Count;

    public void AddOrReplace(T item)
    {
        lock (_lock)
        {
            GuardHelper.ThrowIfDefault(item);
            if (!_dictionary.TryAdd(item.Id, item))
            {
                _dictionary[item.Id] = item;
            }
            ChangedEvent.Emit();
        }
    }

    public bool Remove(T item)
    {
        lock (_lock)
        {
            GuardHelper.ThrowIfDefault(item);
            var result = _dictionary.Remove(item.Id);
            ChangedEvent.Emit();
            return result;
        }
    }

    public bool Remove(int id)
    {
        lock (_lock)
        {
            var result = _dictionary.Remove(id);
            ChangedEvent.Emit();
            return result;
        }
    }

    public void RemoveRange(IEnumerable<T> items)
    {
        lock (_lock)
        {
            foreach (var item in items)
            {
                _dictionary.Remove(item.Id);
            }
            ChangedEvent.Emit();
        }
    }

    public void AddRange(IEnumerable<T> items)
    {
        lock (_lock)
        {
            foreach (var (key, value) in items.ToDictionary(x => x.Id, x => x))
            {
                _dictionary.Add(key, value);
            }
            ChangedEvent.Emit();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _dictionary.Clear();
        }
    }

    public bool Contains(T item)
    {
        lock (_lock)
        {
            return _dictionary.ContainsKey(item.Id);
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        return _dictionary.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
