using Not.Domain.Aggregates;

namespace Not.Domain;

public interface IParent<T>
    where T : Aggregate
{
    void Add(T child);
    void Remove(T child);
    void Update(T child);
    IReadOnlyList<T> Children { get; }
}
