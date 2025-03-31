using Not.Application.CRUD.Ports;
using Not.Blazor.CRUD.Ports;
using Not.Domain;
using Not.Domain.Base;
using Not.Exceptions;

namespace Not.Application.Behinds;

public abstract class BehindContext<T> : ICrudParentContext
    where T : AggregateRoot
{
    protected BehindContext(IUpdate<T> updater)
    {
        Updater = updater;
    }

    public abstract void SetParent(IParent parent);

    protected IUpdate<T> Updater { get; }
    public T? Entity { get; protected set; }

    public async Task Persist()
    {
        GuardHelper.ThrowIfDefault(Entity);
        await Updater.Update(Entity);
    }
}
