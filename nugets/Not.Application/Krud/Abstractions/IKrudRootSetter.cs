using Not.Application.Krud.Nodes;
using Not.Domain.Aggregates;

namespace Not.Application.Krud.Abstractions;

public interface IKrudRootSetter<T>
    where T : AggregateRoot
{
    void Set(KrudNode node);
}
