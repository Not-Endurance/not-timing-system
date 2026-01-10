using Not.Application.CRUD.Ports;
using Not.Blazor.CRUD.Ports;
using Not.Domain;
using Not.Domain.Aggregates;
using Not.Events;
using Not.Exceptions;

namespace Not.Application.Krud.Services;

public abstract class KrudNode<T> : IKrudNodeSetter
    where T : AggregateRoot
{
    readonly IUpdate<T> _proppagator;

    protected KrudNode(IUpdate<T> propagator)
    {
        _proppagator = propagator;
    }

    public abstract Task Set(object value);
    public Event Changed { get; } = new();

    public async Task Add<TAggregate, TChild>(TAggregate? aggregate, TChild child)
        where TAggregate : T, IParent<TChild>, IAggregateRoot
        where TChild : AggregateRoot
    {
        GuardHelper.ThrowIfDefault(aggregate);

        aggregate.Add(child);
        await _proppagator.Update(aggregate);
        Changed.Emit();
    }

    public async Task Update<TAggregate, TChild>(TAggregate? aggregate, TChild child)
        where TAggregate : T, IParent<TChild>, IAggregateRoot
        where TChild : AggregateRoot
    {
        GuardHelper.ThrowIfDefault(aggregate);

        aggregate.Update(child);
        await _proppagator.Update(aggregate);
        Changed.Emit();
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
        Changed.Emit();
    }
}
