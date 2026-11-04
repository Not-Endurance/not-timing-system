namespace Not.Domain.Krud;

// TODO: create Not.Krud.Abstractions project and put interfaces there
public interface IKrudParent<T>
    where T : Entity
{
    void Add(T child);
    void Remove(T child);
    void Update(T child);
    IReadOnlyList<T> Children { get; }
}
