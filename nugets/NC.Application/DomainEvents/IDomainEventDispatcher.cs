using NC.Domain;
using NC.Domain.Abstractions;

namespace NC.Application.DomainEvents;

public interface IDomainEventDispatcher
{
    Task Dispatch(IDomainEvent @event, CancellationToken cancellationToken = default);
    Task Dispatch(Aggregate aggregate, CancellationToken cancellationToken = default);
}
