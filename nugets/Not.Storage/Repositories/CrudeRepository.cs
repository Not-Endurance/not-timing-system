using System.Linq.Expressions;
using Not.Application.Behinds;
using Not.Application.CRUD.Ports;
using Not.Domain.Aggregates;
using Not.Exceptions;

namespace Not.Storage.Repositories;

public abstract class CrudeRepository<T> : IRepository<T>
    where T : AggregateRoot
{
    readonly ICrudeParent<T> _parentContext;

    protected CrudeRepository(ICrudeParent<T> parentContext)
    {
        _parentContext = parentContext;
    }

    protected abstract IReadOnlyList<T> Aggregates { get; }

    public async Task Create(T item)
    {
        await _parentContext.Create(item);
    }

    public Task<T?> Read(Expression<Func<T, bool>> filter)
    {
        var predicate = filter.Compile();
        var result = Aggregates.FirstOrDefault(predicate);
        return Task.FromResult(result);
    }

    public Task<T?> Read(int id)
    {
        var result = Aggregates.FirstOrDefault(x => x.Id == id);
        return Task.FromResult(result);
    }

    public Task<IEnumerable<T>> ReadAll()
    {
        var result = Aggregates.AsEnumerable();
        return Task.FromResult(result);
    }

    public Task<IEnumerable<T>> ReadAll(Expression<Func<T, bool>> filter)
    {
        var predicate = filter.Compile();
        var result = Aggregates.Where(predicate);
        return Task.FromResult(result);
    }

    public async Task Update(T items)
    {
        await _parentContext.Update(items);
    }

    public async Task Delete(int id)
    {
        var official =
            Aggregates.FirstOrDefault(x => x.Id == id)
            ?? throw GuardHelper.Exception($"{typeof(T).Name} with '{id}' not found");
        await _parentContext.Delete(official);
    }

    public async Task Delete(T item)
    {
        await _parentContext.Delete(item);
    }

    public async Task Delete(Expression<Func<T, bool>> filter)
    {
        var predicate = filter.Compile();
        var official =
            Aggregates.FirstOrDefault(predicate) ?? throw GuardHelper.Exception($"{typeof(T).Name} Official not found");
        await _parentContext.Delete(official);
    }

    public async Task Delete(IEnumerable<T> items)
    {
        await _parentContext.Delete(items);
    }
}
