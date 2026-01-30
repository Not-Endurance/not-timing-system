namespace Not.Domain.Krud;

public interface IParent<T>
    where T : Entity
{
    void Add(T child);
    void Remove(T child);
    void Update(T child);
    IReadOnlyList<T> Children { get; }
}
