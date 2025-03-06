using Not.Application.CRUD.Ports;
using Not.Blazor.CRUD.Ports;
using Not.Domain;
using Not.Domain.Base;
using Not.Exceptions;

namespace Not.Application.Behinds;

public abstract class BehindContext<T> : ICrudParentContext
    where T : AggregateRoot
{
    protected BehindContext(IRepository<T> repository)
    {
        Repository = repository;
    }

    protected IRepository<T> Repository { get; }
    public T? Entity { get; set; }

    protected async Task Persist()
    {
        GuardHelper.ThrowIfDefault(Entity);
        await Repository.Update(Entity);
    }

    public void SetParent(IParent entity)
    {
        if (entity is T competition)
        {
            Entity = competition;
        }
    }
}
