using NTS.Warp.ACL.Enums;

namespace NTS.Warp.ACL.Entities;

public class EmsStartlist : StartlistBase<EmsStartlistEntry>
{
    public EmsStartlist() { }

    public EmsStartlist(IEnumerable<EmsStartlistEntry> entries)
    {
        AddRange(entries);
    }

    protected override void OnCollectionChanged()
    {
        Sort();
        base.OnCollectionChanged();
    }
}

public class StartlistBase<T> : List<T>
    where T : IEquatable<T>
{
    protected virtual void OnCollectionChanged() { }

    public void Update(T item, EmsCollectionAction action)
    {
        var existing = this.FirstOrDefault(x => x.Equals(item));
        if (existing is not null)
        {
            base.Remove(existing);
        }
        if (action == EmsCollectionAction.AddOrUpdate)
        {
            base.Add(item);
        }
        OnCollectionChanged();
    }

    public new void Add(T item)
    {
        base.Add(item);
        OnCollectionChanged();
    }

    public new void Clear()
    {
        base.Clear();
        OnCollectionChanged();
    }

    public new void Insert(int index, T item)
    {
        base.Insert(index, item);
        OnCollectionChanged();
    }

    public new void RemoveAt(int index)
    {
        base.RemoveAt(index);
        OnCollectionChanged();
    }

    public new void Remove(T item)
    {
        base.Remove(item);
        OnCollectionChanged();
    }

    public void Remove(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            base.Remove(item);
        }
        OnCollectionChanged();
    }

    public new void AddRange(IEnumerable<T> collection)
    {
        base.AddRange(collection);
        OnCollectionChanged();
    }

    public new void RemoveAll(Predicate<T> predicate)
    {
        base.RemoveAll(predicate);
        OnCollectionChanged();
    }
}
