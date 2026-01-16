using Not.Application.Krud.Graph;
using Not.Domain.Aggregates;

namespace Not.Application.Krud.Abstractions;

public interface IKrudRootSetter<T>
    where T : AggregateRoot
{
    void Set(KrudNode node);
}
