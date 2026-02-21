using Not.Domain;

namespace Not.Application.DomainEvents;

public interface IDomainEventDispatcher
{
    Task Dispatch(Aggregate aggregate, CancellationToken cancellationToken = default);
}
