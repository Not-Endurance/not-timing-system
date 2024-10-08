using Not.Exceptions;
using System.Collections;

namespace Not.Structures;

public class EntitySet<T> : IReadOnlyList<T>
    where T : IIdentifiable
{
    readonly Action? _action;
    Dictionary<int, T> _dictionary = [];

    public EntitySet()
    {
    }

    public EntitySet(Action action)
    {
        _action = action;
    }

    public void AddOrReplace(T item)
    {
        GuardHelper.ThrowIfDefault(item);

        if (_dictionary.ContainsKey(item.Id))
        {
            _dictionary[item.Id] = item;
        }
        else
        {
            _dictionary.Add(item.Id, item);
        }

        _action?.Invoke();
    }

    public bool Remove(T item)
    {
        GuardHelper.ThrowIfDefault(item);
        var result = _dictionary.Remove(item.Id);
        _action?.Invoke();
        return result;
    }

    public bool Remove(int id)
    {
        var result = _dictionary.Remove(id);
        _action?.Invoke();
        return result;
    }

    public void AddRange(IEnumerable<T> items)
    {
        _dictionary = items.ToDictionary(x => x.Id, x => x);
        _action?.Invoke();
    }

    public T this[int index]
    {
        get
        {
            if (index > _dictionary.Count)
            {
                throw new ArgumentOutOfRangeException("index");
            }
            var count = 0;
            foreach (var item in this)
            {
                if (count++ == index)
                {
                    return item;
                }
            }
            // Should never be reached
            throw new ArgumentOutOfRangeException("index");
        }
    }

    public int Count => _dictionary.Count;

    public IEnumerator<T> GetEnumerator()
    {
        return _dictionary.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
