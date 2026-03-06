using Not.Domain;
using Not.Domain.Abstractions;

namespace Not.Application.DomainEvents;

public interface IDomainEventDispatcher
{
    Task Dispatch(IDomainEvent @event, CancellationToken cancellationToken = default);
    Task Dispatch(Aggregate aggregate, CancellationToken cancellationToken = default);
}
