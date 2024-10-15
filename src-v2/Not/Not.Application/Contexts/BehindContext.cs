using AngleSharp.Dom;
using Not.Application.Ports.CRUD;
using Not.Domain;
using Not.Exceptions;

namespace Not.Application.Contexts;

public abstract class BehindContext<T>(IRepository<T> entities)
    where T : DomainEntity
{
    protected IRepository<T> Repository { get; } = entities;

    public T? Entity { get; set; }

    public bool HasLoaded()
    {
        return Entity != null;
    }

    public async Task Persist()
    {
        GuardHelper.ThrowIfDefault(Entity);
        await Repository.Update(Entity);
    }
}
