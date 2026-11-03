using Not.Domain;
using Not.Krud.Abstractions;
using Not.Krud.Services;

namespace NTS.Tests.Unit.Harness;

internal sealed class Node : IKrudParentNodeOf<Child>
{
    readonly List<Child> _children = [];

    public IReadOnlyList<Child> Children => _children;

    public void Add(Child child)
    {
        _children.Add(child);
    }

    public void Remove(Child child)
    {
        _children.Remove(child);
    }

    public void Update(Child child) { }

    public void SetParent(object aggregate) { }
}
