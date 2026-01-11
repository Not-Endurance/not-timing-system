using Not.Domain.Aggregates;

namespace Not.Application.Krud.Abstractions;

public interface IKrudRootContext<TRoot> where TRoot : AggregateRoot
{
    TRoot? Root { get; }
    Task Set(TRoot root);
}
