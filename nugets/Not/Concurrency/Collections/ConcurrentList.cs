using System.Collections;
using System.Collections.ObjectModel;

namespace Not.Concurrency.Collections;

public class ConcurrentList<T> : IList<T>
{
    readonly List<T> _items = [];
    readonly object _lock = new();

    public ConcurrentList()
    {
        _items = [];
    }

    public ConcurrentList(IEnumerable<T> enumerable)
    {
        _items = enumerable.ToList();
    }

    public T this[int index]
    {
        get => _items[index];
        set => _items[index] = value;
    }
    public int Count => _items.Count;
    public bool IsReadOnly => false;

    public void Add(T item)
    {
        lock (_lock)
        {
            _items.Add(item);
        }
    }

    public void AddRange(IEnumerable<T> collection)
    {
        lock (_lock)
        {
            _items.AddRange(collection);
        }
    }

    public void Remove(T item)
    {
        lock (_lock)
        {
            _items.Remove(item);
        }
    }

    public void RemoveRange(IEnumerable<T> collection)
    {
        lock (_lock)
        {
            foreach (var item in collection)
            {
                _items.Remove(item);
            }
        }
    }

    public List<T> PopAll()
    {
        lock (_lock)
        {
            var result = _items.ToList();
            _items.Clear();
            return result;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _items.Clear();
        }
    }

    public ReadOnlyCollection<T> AsReadOnly()
    {
        return _items.AsReadOnly();
    }

    public IEnumerator<T> GetEnumerator()
    {
        return _items.GetEnumerator();
    }

    public int IndexOf(T item)
    {
        return _items.IndexOf(item);
    }

    public void Insert(int index, T item)
    {
        _items[index] = item;
    }

    public void RemoveAt(int index)
    {
        _items.RemoveAt(index);
    }

    public bool Contains(T item)
    {
        return _items.Contains(item);
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        _items.CopyTo(array, arrayIndex);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    bool ICollection<T>.Remove(T item)
    {
        return _items.Remove(item);
    }
}
