using System.Collections;
using Not.Collections;
using Not.Events;
using Not.Exceptions;
using Not.Structures;

namespace Not.Observables.Structures;

public class ObservableList<T> : IReadOnlyList<T>, IObservable
    where T : IIdentifiable
{
    readonly object _lock = new();
    readonly Dictionary<int, T> _dictionary = [];
    Event _changed = new();

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
    public IEventSubscriber Event => _changed;
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
            _changed.Emit();
        }
    }

    public bool Remove(T item)
    {
        lock (_lock)
        {
            GuardHelper.ThrowIfDefault(item);
            var result = _dictionary.Remove(item.Id);
            _changed.Emit();
            return result;
        }
    }

    public bool Remove(int id)
    {
        lock (_lock)
        {
            var result = _dictionary.Remove(id);
            _changed.Emit();
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
            _changed.Emit();
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
            _changed.Emit();
        }
    }

    public void Update(T item, NCollectionAction action)
    {
        switch (action)
        {
            case NCollectionAction.AddOrUpdate:
                AddOrReplace(item);
                break;
            case NCollectionAction.Remove:
                Remove(item);
                break;
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
