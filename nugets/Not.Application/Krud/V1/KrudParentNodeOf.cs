using Not.Domain;
using Not.Domain.Aggregates;

namespace Not.Application.Krud.V1;

public class KrudParentNodeOf<TChild> : IKrudParentSetter
    where TChild : Aggregate
{
    public IParent<TChild>? Aggregate { get; private set; }

    public void Set(object aggregate)
    {
        if (aggregate is not IParent<TChild> parent)
        {
            return;
        }
        Aggregate = parent;
    }
}
