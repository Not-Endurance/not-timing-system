using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using Not.Application.Krud.Abstractions;
using Not.Domain.Aggregates;
using Not.Exceptions;

namespace Not.Application.Krud;

public abstract class KrudInMemoryRepository<T> : IRepository<T>
    where T : AggregateRoot
{
    readonly IKrudParentNodeOf<T> _parentNode;

    protected KrudInMemoryRepository(IKrudParentNodeOf<T> parentNode)
    {
        _parentNode = parentNode;
    }

    protected abstract IReadOnlyList<T> Aggregates { get; }

    public async Task Create(T item)
    {
        await _parentNode.Create(item);
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
        await _parentNode.Update(items);
    }

    public async Task Delete(int id)
    {
        var official =
            Aggregates.FirstOrDefault(x => x.Id == id)
            ?? throw GuardHelper.Exception($"{typeof(T).Name} with '{id}' not found");
        await _parentNode.Delete(official);
    }

    public async Task Delete(T child)
    {
        await _parentNode.Delete(child);
    }

    public async Task Delete(Expression<Func<T, bool>> filter)
    {
        var predicate = filter.Compile();
        var official =
            Aggregates.FirstOrDefault(predicate) ?? throw GuardHelper.Exception($"{typeof(T).Name} Official not found");
        await _parentNode.Delete(official);
    }
}
