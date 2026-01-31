using Not.Domain;
using Not.Krud.Graph;

namespace Not.Krud.Abstractions;

public interface IKrudRootSetter<T>
    where T : Aggregate
{
    void Set(KrudNode node);
}
