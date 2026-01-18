using Not.Domain.Aggregates;

namespace Not.Domain;

public interface IKrudParent<T>
    where T : Entity
{
    void Add(T child);
    void Remove(T child);
    void Update(T child);
    IReadOnlyList<T> Children { get; }
}
