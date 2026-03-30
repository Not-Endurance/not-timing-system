using Not.Domain;
using Not.Domain.Krud;
using Not.Exceptions;
using Not.Krud.Abstractions;

namespace Not.Krud.Graph;

public class KrudParentNodeOf<T> : KrudNode, IKrudParentNodeOf<T>
    where T : Entity
{
    IKrudParent<T>? Parent => (IKrudParent<T>?)Value;

    IReadOnlyList<T> IKrudParent<T>.Children => Parent?.Children ?? [];

    public void SetParent(object aggregate)
    {
        if (aggregate is not IKrudParent<T> parent)
        {
            return;
        }
        Set(parent);
    }

    public void Add(T child)
    {
        GuardHelper.ThrowIfDefault(Parent);
        Parent.Add(child);
        StateChanged.Emit();
    }

    public void Remove(T child)
    {
        GuardHelper.ThrowIfDefault(Parent);
        Parent.Remove(child);
        StateChanged.Emit();
    }

    public void Update(T child)
    {
        GuardHelper.ThrowIfDefault(Parent);
        Parent.Update(child);
        StateChanged.Emit();
    }
}
