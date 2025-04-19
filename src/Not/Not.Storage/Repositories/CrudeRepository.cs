using System.Linq.Expressions;
using Not.Application.Behinds;
using Not.Application.CRUD.Ports;
using Not.Domain.Base;
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
    
    public async Task Create(T entity)
    {
        await _parentContext.Add(entity);
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

    public async Task Update(T entity)
    {
        await _parentContext.Propagate(entity);
    }

    public async Task Delete(int id)
    {
        var official =  Aggregates.FirstOrDefault(x => x.Id == id) ?? throw GuardHelper.Exception($"{typeof(T).Name} with '{id}' not found");
        await _parentContext.Remove(official);
    }

    public async Task Delete(T entity)
    {
        await _parentContext.Remove(entity);
    }

    public async Task Delete(Expression<Func<T, bool>> filter)
    {
        var predicate = filter.Compile();
        var official =  Aggregates.FirstOrDefault(predicate) ?? throw GuardHelper.Exception($"{typeof(T).Name} Official not found");
        await _parentContext.Remove(official);
    }

    public async Task Delete(IEnumerable<T> entities)
    {
        await _parentContext.Remove(entities);
    }
}

