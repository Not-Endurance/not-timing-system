using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using Not.Application.Krud.Abstractions;
using Not.Domain.Aggregates;

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

    public Task<IEnumerable<T>> ReadMany()
    {
        var result = Aggregates.AsEnumerable();
        return Task.FromResult(result);
    }

    public Task<IEnumerable<T>> ReadMany(Expression<Func<T, bool>> filter)
    {
        var predicate = filter.Compile();
        var result = Aggregates.Where(predicate);
        return Task.FromResult(result);
    }

    public async Task Update(T items)
    {
        await _parentNode.Update(items);
    }

    public async Task Delete(T child)
    {
        await _parentNode.Delete(child);
    }

    public async Task Delete(Expression<Func<T, bool>> filter)
    {
        var predicate = filter.Compile();
        var child = Aggregates.FirstOrDefault(predicate);
        if (child == null)
        {
            return;
        }
        await _parentNode.Delete(child);
    }

    public Task Delete(IEnumerable<T> items)
    {
        throw new NotImplementedException();
    }
}
