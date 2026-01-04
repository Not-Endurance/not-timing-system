using Not.Domain.Aggregates;

namespace Not.Domain;

public interface IParent<T> : IParent
    where T : AggregateRoot
{
    void Add(T child);
    void Remove(T child);
    void Update(T child);
}

public interface IParent { }
