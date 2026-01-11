using Not.Application.CRUD.Ports;
using Not.Application.Krud.Abstractions;
using Not.Domain;
using Not.Domain.Aggregates;
using Not.Events;
using Not.Exceptions;
using Not.Observables;

namespace Not.Application.Krud.Services;

public abstract class ManualKrudNode<T> : Observer, IKrudNodeSetter, IObservable
    where T : AggregateRoot
{
    readonly IUpdate<T> _proppagator;
    readonly Event _changed = new();

    protected ManualKrudNode(IUpdate<T> propagator)
    {
        _proppagator = propagator;
    }

    public abstract Task Set(object value);

    public IEventSubscriber Event => _changed;

    public async Task Add<TAggregate, TChild>(TAggregate? aggregate, TChild child)
        where TAggregate : T, IParent<TChild>, IAggregateRoot
        where TChild : AggregateRoot
    {
        GuardHelper.ThrowIfDefault(aggregate);

        aggregate.Add(child);
        await _proppagator.Update(aggregate);
        _changed.Emit();
    }

    public async Task Update<TAggregate, TChild>(TAggregate? aggregate, TChild child)
        where TAggregate : T, IParent<TChild>, IAggregateRoot
        where TChild : AggregateRoot
    {
        GuardHelper.ThrowIfDefault(aggregate);

        aggregate.Update(child);
        await _proppagator.Update(aggregate);
        _changed.Emit();
    }

    public async Task Remove<TAggregate, TChild>(TAggregate? aggregate, IEnumerable<TChild> children)
        where TAggregate : T, IParent<TChild>, IAggregateRoot
        where TChild : AggregateRoot
    {
        GuardHelper.ThrowIfDefault(aggregate);

        foreach (var child in children)
        {
            aggregate.Remove(child);
        }
        await _proppagator.Update(aggregate);
        _changed.Emit();
    }
}
