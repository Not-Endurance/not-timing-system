using Not.Domain.Abstractions;
using Not.Exceptions;

namespace Not.Domain;

public abstract class Aggregate : Entity, IAggregate
{
    readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>
    /// Provide <paramref name="id"/> when updating state null to generate it
    /// </summary>
    /// <param name="id">Id, generated when null</param>
    protected Aggregate(int? id)
        : base(id) { }

    protected void Raise(IDomainEvent @event)
    {
        GuardHelper.ThrowIfDefault(@event);
        _domainEvents.Add(@event);
    }

    public IReadOnlyList<IDomainEvent> DequeueDomainEvents()
    {
        var events = _domainEvents.ToList().AsReadOnly();
        _domainEvents.Clear();
        return events;
    }
}
