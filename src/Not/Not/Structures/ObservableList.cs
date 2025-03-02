using System.Collections;
using Not.Events;
using Not.Exceptions;

namespace Not.Structures;

public class ObservableList<T> : IReadOnlyList<T>
    where T : IIdentifiable
{
    Dictionary<int, T> _dictionary = [];

    public T this[int index]
    {
        get
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
    public Event ChangedEvent { get; } = new();
    public int Count => _dictionary.Count;

    public void AddOrReplace(T item)
    {
        GuardHelper.ThrowIfDefault(item);

        if (!_dictionary.TryAdd(item.Id, item))
        {
            _dictionary[item.Id] = item;
        }

        ChangedEvent.Emit();
    }

    public bool Remove(T item)
    {
        GuardHelper.ThrowIfDefault(item);
        var result = _dictionary.Remove(item.Id);
        ChangedEvent.Emit();
        return result;
    }

    public bool Remove(int id)
    {
        var result = _dictionary.Remove(id);
        ChangedEvent.Emit();
        return result;
    }

    public void RemoveRange(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            _dictionary.Remove(item.Id);
        }
        ChangedEvent.Emit();
    }

    public void AddRange(IEnumerable<T> items)
    {
        foreach (var (key, value) in items.ToDictionary(x => x.Id, x => x))
        {
            _dictionary.Add(key, value);
        }
        ChangedEvent.Emit();
    }

    public void Clear()
    {
        _dictionary.Clear();
    }

    public bool Contains(T item)
    {
        return _dictionary.ContainsKey(item.Id);
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
