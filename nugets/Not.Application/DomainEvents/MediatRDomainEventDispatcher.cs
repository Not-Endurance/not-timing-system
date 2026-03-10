using MediatR;
using Not.Domain;
using Not.Domain.Abstractions;
using Not.Exceptions;
using Not.Logging;

namespace Not.Application.DomainEvents;

public class MediatRDomainEventDispatcher : IDomainEventDispatcher
{
    readonly IPublisher _publisher;

    public MediatRDomainEventDispatcher(IPublisher publisher)
    {
        _publisher = publisher;
    }

    public Task Dispatch(IDomainEvent @event, CancellationToken ct = default)
    {
        return PublishIgnoringValidation(@event, ct);
    }

    public async Task Dispatch(Aggregate aggregate, CancellationToken ct = default)
    {
        foreach (var @event in aggregate.DequeueDomainEvents())
        {
            await Dispatch(@event, ct);
        }
    }

    async Task PublishIgnoringValidation(INotification @event, CancellationToken ct)
    {
#if DEBUG
        await _publisher.Publish(@event, ct);
#else
        try
        {
            await _publisher.Publish(@event, ct);
        }
        catch (Not.Exceptions.ValidationException validation)
        {
            var message = $"""
                Validation was suppressed while dispatching '{@event.GetType().Name}' in RELEASE.
                Message: {validation.Message}
                """;
            Not.Logging.LoggingHelper.Debug(message);
        }
#endif
    }
}
