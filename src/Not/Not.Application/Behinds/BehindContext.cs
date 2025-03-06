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

    protected IUpdate<T> Updater { get; }
    public T? Entity { get; set; }

    protected async Task Persist()
    {
        GuardHelper.ThrowIfDefault(Entity);
        await Updater.Update(Entity);
    }

    public void SetParent(IParent entity)
    {
        if (entity is T competition)
        {
            Entity = competition;
        }
    }
}
