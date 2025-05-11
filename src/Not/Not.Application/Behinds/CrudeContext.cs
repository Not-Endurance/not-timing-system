using System.Diagnostics.CodeAnalysis;
using Not.Application.CRUD.Ports;
using Not.Blazor.CRUD.Ports;
using Not.Domain;
using Not.Domain.Base;
using Not.Events;
using Not.Exceptions;

namespace Not.Application.Behinds;

public abstract class CrudeContext<T> : ICrudeParentContext
    where T : AggregateRoot
{
    readonly string _tName = typeof(T).Name;
    readonly IUpdate<T> _parent;

    protected CrudeContext(IUpdate<T> parent)
    {
        _parent = parent;
    }

    public abstract Task Set(IParent parent);
    public Event Changed { get; } = new();

    public async Task Add<TParent, TChild>(TParent? parent, TChild child)
        where TParent : IParent<TChild>, IAggregateRoot
        where TChild : AggregateRoot
    {
        var tParent = ValidateParentType(parent);
        parent.Add(child);
        await _parent.Update(tParent);
        Changed.Emit();
    }

    public async Task Update<TParent, TChild>(TParent? parent, TChild child)
        where TParent : IParent<TChild>, IAggregateRoot
        where TChild : AggregateRoot
    {
        var tParent = ValidateParentType(parent);
        parent.Update(child);
        await _parent.Update(tParent);
        Changed.Emit();
    }

    public async Task Remove<TParent, TChild>(TParent? parent, IEnumerable<TChild> children)
        where TParent : IParent<TChild>, IAggregateRoot
        where TChild : AggregateRoot
    {
        var tParent = ValidateParentType(parent);
        foreach (var child in children)
        {
            parent.Remove(child);
        }
        await _parent.Update(tParent);
        Changed.Emit();
    }

    T ValidateParentType<TParent>([NotNull] TParent? inParent)
    {
        if (inParent is not T tParent)
        {
            throw GuardHelper.Exception(
                $"Invalid parent type '{typeof(TParent).Name}' used. Expected type is '{_tName}'"
            );
        }
        return tParent;
    }
}
